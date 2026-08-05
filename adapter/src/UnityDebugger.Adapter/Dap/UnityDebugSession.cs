using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Breakpoints;
using UnityDebugger.Adapter.Diagnostics;
using UnityDebugger.Adapter.Source;
using UnityDebugger.Adapter.State;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class UnityDebugSession : DebugSession
    {
        private const string SupportedVersion = "2022.3.62t11";
        private const string SupportPolicyUrl =
            "https://marketplace.visualstudio.com/items?itemName=" +
            "kpk.unity-debugger-pure" +
            "#support-policy";
        private readonly Func<IDebuggerBackend> backendFactory;
        private readonly ThreadIdMap threadIds = new ThreadIdMap();
        private readonly HandleTable<BackendStackFrame> frameHandles =
            new HandleTable<BackendStackFrame>();
        private readonly HandleTable<long> variableHandles =
            new HandleTable<long>();
        private IDebuggerBackend? backend;
        private BreakpointManager? breakpointManager;
        private SourceMapper? sourceMapper;
        private bool terminatedSent;
        private BackendEvaluationMode automaticEvaluationMode =
            BackendEvaluationMode.Explicit;
        private int inspectionGeneration;

        public UnityDebugSession(Func<IDebuggerBackend> backendFactory)
        {
            this.backendFactory = backendFactory ??
                throw new ArgumentNullException(nameof(backendFactory));
        }

        protected override void DispatchRequest(
            string command,
            dynamic arguments,
            Response response)
        {
            if (
                string.Equals(command, "scopes", StringComparison.Ordinal) ||
                string.Equals(command, "variables", StringComparison.Ordinal) ||
                string.Equals(command, "evaluate", StringComparison.Ordinal))
            {
                Task.Run(
                    () => base.DispatchRequest(
                        command,
                        (object)arguments,
                        response));
                return;
            }

            base.DispatchRequest(command, (object)arguments, response);
        }

        public override void Initialize(Response response, dynamic args)
        {
            SendResponse(response, new Capabilities
            {
                supportsConfigurationDoneRequest = false,
                supportsFunctionBreakpoints = true,
                supportsConditionalBreakpoints = true,
                supportsEvaluateForHovers = true,
                supportsExceptionOptions = true,
                supportsLogPoints = true,
                supportsSetVariable = true,
                supportsStepInTargetsRequest = true,
                supportsGotoTargetsRequest = true,
                supportsTerminateRequest = true,
                exceptionBreakpointFilters = new[]
                {
                    new ExceptionBreakpointsFilter(
                        "all",
                        "All Exceptions",
                        false),
                    new ExceptionBreakpointsFilter(
                        "uncaught",
                        "Uncaught Exceptions",
                        true),
                },
            });
            SendEvent(new InitializedEvent());
        }

        public override void Launch(
            Response response,
            dynamic arguments)
        {
            SendErrorResponse(
                response,
                2001,
                "This debugger supports attach only.");
        }

        public override void Attach(
            Response response,
            dynamic arguments)
        {
            if (backend != null)
            {
                SendErrorResponse(
                    response,
                    2002,
                    "A debugger backend is already active.");
                return;
            }

            try
            {
                var target = AttachArguments.Parse((JObject)arguments);
                automaticEvaluationMode = target.EnableImplicitEvaluation
                    ? BackendEvaluationMode.Explicit
                    : BackendEvaluationMode.Safe;
                var createdBackend = backendFactory();
                backend = createdBackend;
                Subscribe(createdBackend);
                breakpointManager = new BreakpointManager(createdBackend);
                breakpointManager.Changed += OnManagedBreakpointChanged;
                sourceMapper = new SourceMapper(
                    target.WorkspaceRoot,
                    File.Exists);
                createdBackend.Attach(target);
                SendResponse(response);

                if (!string.Equals(
                    target.ProjectVersion,
                    SupportedVersion,
                    StringComparison.Ordinal))
                {
                    SendEvent(new OutputEvent(
                        "console",
                        $"Editor {target.ProjectVersion} is unverified; " +
                        $"the supported baseline is {SupportedVersion}. " +
                        $"Support policy: {SupportPolicyUrl}" +
                        Environment.NewLine));
                }
            }
            catch (AttachArgumentException exception)
            {
                ReleaseBackend();
                SendErrorResponse(response, 2003, exception.Message);
            }
            catch (DebuggerBackendException exception)
            {
                ReleaseBackend();
                SendErrorResponse(response, 2004, exception.Message);
            }
        }

        public override void Disconnect(
            Response response,
            dynamic arguments)
        {
            ReleaseBackend();
            SendResponse(response);
            SendTerminatedOnce();
        }

        public override void SetFunctionBreakpoints(
            Response response,
            dynamic arguments)
        {
            SendResponse(
                response,
                new SetFunctionBreakpointsBody(
                    new VSCodeDebug.Breakpoint[0]));
        }

        public override void StepInTargets(
            Response response,
            dynamic arguments)
        {
            SendResponse(
                response,
                new StepInTargetsResponseBody(
                    Array.Empty<StepInTarget>()));
        }

        public override void GotoTargets(
            Response response,
            dynamic arguments)
        {
            SendResponse(
                response,
                new GotoTargetsResponseBody(
                    Array.Empty<GotoTarget>()));
        }

        public override void Goto(
            Response response,
            dynamic arguments)
        {
            SendResponse(response);
        }

        public override void ExceptionInfo(
            Response response,
            dynamic arguments)
        {
            SendResponse(response, new ResponseBody());
        }

        protected override void SetVariable(
            Response response,
            object args)
        {
            SendResponse(
                response,
                new SetVariablesResponseBody("", "", 0));
        }

        public override void Source(
            Response response,
            dynamic arguments)
        {
            SendResponse(response, new ResponseBody());
        }

        public override void SetExceptionBreakpoints(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = arguments as JObject;
            var filters = (request?["filters"] as JArray)?
                .Values<string>()
                .Where(item => item != null)
                .ToArray() ?? Array.Empty<string>();
            var unknown = filters.FirstOrDefault(
                filter =>
                    !string.Equals(
                        filter,
                        "all",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        filter,
                        "uncaught",
                        StringComparison.Ordinal));
            if (unknown != null)
            {
                SendErrorResponse(
                    response,
                    2030,
                    "Unknown exception breakpoint filter.");
                return;
            }

            var mode = filters.Contains("all")
                ? ExceptionBreakMode.All
                : filters.Contains("uncaught")
                    ? ExceptionBreakMode.Uncaught
                    : ExceptionBreakMode.None;
            try
            {
                value.ConfigureExceptions(mode);
                SendResponse(response);
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2031,
                    "Could not configure exception breakpoints.");
            }
        }

        public override void SetBreakpoints(
            Response response,
            dynamic arguments)
        {
            if (breakpointManager == null || sourceMapper == null)
            {
                SendErrorResponse(
                    response,
                    2010,
                    "Attach to an Editor before setting breakpoints.");
                return;
            }

            var request = arguments as JObject;
            var source = request?["source"] as JObject;
            var clientPath = source?["path"]?.Value<string>();
            var sourcePath = ConvertClientPathToDebugger(clientPath);
            if (
                string.IsNullOrWhiteSpace(sourcePath) ||
                !string.Equals(
                    Path.GetExtension(sourcePath),
                    ".cs",
                    StringComparison.OrdinalIgnoreCase))
            {
                SendErrorResponse(
                    response,
                    2011,
                    "Managed source breakpoints require a .cs file.");
                return;
            }

            var requested = new List<RequestedBreakpoint>();
            var sourceBreakpoints = request?["breakpoints"] as JArray;
            if (sourceBreakpoints != null)
            {
                foreach (var token in sourceBreakpoints.OfType<JObject>())
                {
                    var line = token["line"]?.Value<int>() ?? 0;
                    if (line <= 0)
                    {
                        SendErrorResponse(
                            response,
                            2012,
                            "Breakpoint line must be a positive integer.");
                        return;
                    }
                    requested.Add(new RequestedBreakpoint(
                        line,
                        token["condition"]?.Value<string>()));
                }
            }

            var managed = breakpointManager.ReplaceForSource(
                sourcePath,
                requested);
            var dapSource = new DapSource(
                Path.GetFileName(sourcePath),
                ConvertDebuggerPathToClient(sourcePath) ?? sourcePath,
                0);
            SendResponse(
                response,
                new DapSetBreakpointsResponseBody(
                    managed.Select(
                        item => ToDapBreakpoint(item, dapSource))));
        }

        public override void Continue(
            Response response,
            dynamic arguments) =>
            Resume(
                response,
                (object)arguments,
                "Continue",
                (value, threadId) => value.Continue(threadId),
                new ContinueResponseBody());

        public override void Next(
            Response response,
            dynamic arguments) =>
            Resume(
                response,
                (object)arguments,
                "Step over",
                (value, threadId) => value.StepOver(threadId));

        public override void StepIn(
            Response response,
            dynamic arguments) =>
            Resume(
                response,
                (object)arguments,
                "Step in",
                (value, threadId) => value.StepIn(threadId));

        public override void StepOut(
            Response response,
            dynamic arguments) =>
            Resume(
                response,
                (object)arguments,
                "Step out",
                (value, threadId) => value.StepOut(threadId));

        public override void Pause(
            Response response,
            dynamic arguments)
        {
            if (!TryResolveControlTarget(
                response,
                (object)arguments,
                out IDebuggerBackend value,
                out long threadId))
            {
                return;
            }
            try
            {
                value.Pause(threadId);
                SendResponse(response);
            }
            catch (Exception exception)
                when (IsControlFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2032,
                    "Pause request failed.");
            }
        }

        public override void StackTrace(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = arguments as JObject;
            var dapThreadId = request?["threadId"]?.Value<int>() ?? 0;
            if (!threadIds.TryGetBackendId(
                dapThreadId,
                out var backendThreadId))
            {
                SendUnavailable(response, "Thread");
                return;
            }

            var startFrame = Math.Max(
                0,
                request?["startFrame"]?.Value<int>() ?? 0);
            var levels = request?["levels"]?.Value<int>() ?? 0;
            if (levels <= 0)
                levels = int.MaxValue;
            try
            {
                var frames = value.GetStackTrace(
                    backendThreadId,
                    startFrame,
                    levels);
                var dapFrames = new List<DapStackFrame>();
                foreach (var frame in frames)
                {
                    var mapped = sourceMapper?.ToClientPath(
                        frame.SourcePath);
                    var source = ToDapSource(mapped);
                    dapFrames.Add(
                        new DapStackFrame(
                            frameHandles.Create(frame),
                            frame.Name,
                            source,
                            frame.Line,
                            Math.Max(1, frame.Column),
                            source == null ? "subtle" : "normal"));
                }
                SendResponse(
                    response,
                    new DapStackTraceResponseBody(
                        dapFrames,
                        dapFrames.Count));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendInspectionFailure(response);
            }
        }

        public override void Scopes(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var generation = Volatile.Read(ref inspectionGeneration);
            var request = arguments as JObject;
            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;
            if (!frameHandles.TryGet(frameHandle, out var frame))
            {
                SendEmptyScopes(response);
                return;
            }

            try
            {
                var backendScopes = value.GetScopes(
                    frame.Id,
                    automaticEvaluationMode).ToArray();
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyScopes(response);
                    return;
                }
                var scopes = new List<Scope>();
                foreach (var scope in backendScopes)
                {
                    scopes.Add(
                        new Scope(
                            scope.Name,
                            variableHandles.Create(
                                scope.VariablesReference),
                            scope.Expensive));
                }
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyScopes(response);
                    return;
                }
                SendResponse(response, new ScopesResponseBody(scopes));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                if (IsCurrentInspectionGeneration(generation))
                    SendInspectionFailure(response);
                else
                    SendEmptyScopes(response);
            }
        }

        public override void Variables(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var generation = Volatile.Read(ref inspectionGeneration);
            var request = arguments as JObject;
            var reference =
                request?["variablesReference"]?.Value<int>() ?? 0;
            if (!variableHandles.TryGet(
                reference,
                out var backendReference))
            {
                SendEmptyVariables(response);
                return;
            }

            try
            {
                const int MaximumVariables = 100;
                var dapVariables = new List<Variable>();
                var variables = value.GetVariables(
                    backendReference,
                    automaticEvaluationMode);
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyVariables(response);
                    return;
                }
                foreach (var variable in variables.Take(MaximumVariables))
                {
                    var childReference = variable.VariablesReference > 0
                        ? variableHandles.Create(
                            variable.VariablesReference)
                        : 0;
                    dapVariables.Add(
                        new Variable(
                            variable.Name,
                            variable.DisplayValue,
                            variable.TypeName,
                            childReference));
                }
                if (variables.Count > MaximumVariables)
                {
                    dapVariables.Add(
                        new Variable(
                            "...",
                            "More variables are not shown.",
                            "",
                            0));
                }
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyVariables(response);
                    return;
                }
                SendResponse(
                    response,
                    new VariablesResponseBody(dapVariables));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                if (IsCurrentInspectionGeneration(generation))
                    SendInspectionFailure(response);
                else
                    SendEmptyVariables(response);
            }
        }

        public override void Threads(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            try
            {
                var threads = value.GetThreads()
                    .Select(
                        item => new VSCodeDebug.Thread(
                            threadIds.GetOrCreate(item.Id),
                            item.Name))
                    .ToList();
                SendResponse(
                    response,
                    new ThreadsResponseBody(threads));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendInspectionFailure(response);
            }
        }

        public override void Evaluate(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var generation = Volatile.Read(ref inspectionGeneration);
            var request = arguments as JObject;
            var context = request?["context"]?.Value<string>();
            BackendEvaluationMode mode;
            if (string.Equals(
                context,
                "hover",
                StringComparison.Ordinal))
            {
                mode = automaticEvaluationMode;
            }
            else if (
                string.Equals(
                    context,
                    "watch",
                    StringComparison.Ordinal) ||
                string.Equals(
                    context,
                    "repl",
                    StringComparison.Ordinal))
            {
                mode = BackendEvaluationMode.Explicit;
            }
            else
            {
                SendErrorResponse(
                    response,
                    2024,
                    "The requested evaluation context is not supported.");
                return;
            }

            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;
            if (!frameHandles.TryGet(frameHandle, out var frame))
            {
                SendEmptyEvaluation(response);
                return;
            }
            var expression = request?["expression"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(expression))
            {
                SendErrorResponse(
                    response,
                    2025,
                    "An evaluation expression is required.");
                return;
            }

            try
            {
                var result = value.Evaluate(
                    frame.Id,
                    expression!,
                    mode);
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyEvaluation(response);
                    return;
                }
                var childReference = 0;
                if (result.VariablesReference > 0)
                {
                    childReference = variableHandles.Create(
                        result.VariablesReference);
                }
                if (!IsCurrentInspectionGeneration(generation))
                {
                    SendEmptyEvaluation(response);
                    return;
                }
                SendResponse(
                    response,
                    new EvaluateResponseBody(
                        result.DisplayValue,
                        childReference));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                if (IsCurrentInspectionGeneration(generation))
                {
                    SendErrorResponse(
                        response,
                        2026,
                        "Expression evaluation failed.");
                }
                else
                {
                    SendEmptyEvaluation(response);
                }
            }
        }

        private void NotImplemented(Response response)
        {
            SendErrorResponse(response, 2005, "Not implemented yet.");
        }

        private void Subscribe(IDebuggerBackend value)
        {
            value.Stopped += OnStopped;
            value.ThreadChanged += OnThreadChanged;
            value.ModuleChanged += OnModuleChanged;
            value.Output += OnOutput;
            value.Terminated += OnTerminated;
        }

        private void Unsubscribe(IDebuggerBackend value)
        {
            value.Stopped -= OnStopped;
            value.ThreadChanged -= OnThreadChanged;
            value.ModuleChanged -= OnModuleChanged;
            value.Output -= OnOutput;
            value.Terminated -= OnTerminated;
        }

        private void ReleaseBackend()
        {
            ResetInspectionState(resetThreads: true);
            if (breakpointManager != null)
            {
                breakpointManager.Changed -=
                    OnManagedBreakpointChanged;
                breakpointManager.Dispose();
                breakpointManager = null;
                sourceMapper = null;
            }

            var value = backend;
            if (value == null)
                return;

            backend = null;
            Unsubscribe(value);
            try
            {
                if (value.IsAttached)
                    value.Disconnect();
            }
            finally
            {
                value.Dispose();
            }
        }

        private void SendTerminatedOnce()
        {
            if (terminatedSent)
                return;
            terminatedSent = true;
            SendEvent(new TerminatedEvent());
        }

        private void OnStopped(
            object sender,
            BackendStoppedEventArgs arguments)
        {
            ResetInspectionState(resetThreads: false);
            var dapThreadId =
                threadIds.GetOrCreate(arguments.ThreadId);
            long[]? hitBreakpointIds = null;
            if (
                arguments.Reason == BackendStopReason.Breakpoint &&
                arguments.BreakpointIds.Count > 0 &&
                breakpointManager != null)
            {
                var logicalIds = new List<long>();
                foreach (var breakpointId in arguments.BreakpointIds)
                {
                    if (
                        breakpointManager.TryGetLogicalId(
                            breakpointId,
                            out var logicalId))
                    {
                        logicalIds.Add(logicalId);
                    }
                }
                if (logicalIds.Count > 0)
                    hitBreakpointIds = logicalIds.Distinct().ToArray();
            }
            SendEvent(
                new Event(
                    "stopped",
                    new DapStoppedEventBody(
                        dapThreadId,
                        ToDapStopReason(arguments.Reason),
                        arguments.Description,
                        hitBreakpointIds)));
        }

        private void OnThreadChanged(
            object sender,
            BackendThreadEventArgs arguments)
        {
            if (arguments.Started)
            {
                SendEvent(
                    new ThreadEvent(
                        "started",
                        threadIds.GetOrCreate(arguments.ThreadId)));
                return;
            }

            if (TryGetDapThreadId(arguments.ThreadId, out var dapId))
                SendEvent(new ThreadEvent("exited", dapId));
            threadIds.Remove(arguments.ThreadId);
        }

        private void OnModuleChanged(
            object sender,
            BackendModuleChangedEventArgs arguments)
        {
            var module = arguments.Module;
            SendEvent(
                new Event(
                    "module",
                    new
                    {
                        reason = arguments.Loaded ? "new" : "removed",
                        module = new
                        {
                            id = module.Id,
                            name = module.Name,
                            path = module.Path,
                            symbolStatus = module.HasSymbols
                                ? "Symbols loaded."
                                : "Symbols not loaded.",
                        },
                    }));
        }

        private void OnOutput(
            object sender,
            BackendOutputEventArgs arguments)
        {
            SendEvent(
                new OutputEvent(
                    arguments.Category,
                    arguments.Output));
        }

        private void OnManagedBreakpointChanged(
            object? sender,
            ManagedBreakpointChangedEventArgs arguments)
        {
            var item = arguments.Breakpoint;
            InternalDebuggerLog.Write(
                item.Verified
                    ? "unity-debugger.breakpoint.dap.status.bound"
                    : "unity-debugger.breakpoint.dap.status.pending");
            var mapped = sourceMapper?.ToClientPath(item.SourcePath);
            var source = mapped != null
                ? new DapSource(
                    mapped.Name,
                    mapped.Path,
                    mapped.SourceReference)
                : new DapSource("Unavailable source", null, 0);
            SendEvent(new Event(
                "breakpoint",
                new
                {
                    reason = "changed",
                    breakpoint = ToDapBreakpoint(item, source),
                }));
        }

        private static DapBreakpoint ToDapBreakpoint(
            ManagedBreakpoint item,
            DapSource source) =>
            new DapBreakpoint(
                item.Id,
                item.Verified,
                item.Message,
                source,
                item.Line,
                1);

        private void OnTerminated(object sender, EventArgs arguments)
        {
            ReleaseBackend();
            SendTerminatedOnce();
        }

        private bool TryGetBackend(
            Response response,
            out IDebuggerBackend value)
        {
            value = backend!;
            if (value != null)
                return true;
            SendErrorResponse(
                response,
                2020,
                "Attach to an Editor before inspecting execution state.");
            return false;
        }

        private bool TryGetInspectionBackend(
            Response response,
            out IDebuggerBackend value) =>
            TryGetBackend(response, out value);

        private static bool IsInspectionFailure(Exception exception) =>
            exception is InvalidOperationException ||
            exception is DebuggerBackendException;

        private void SendUnavailable(Response response, string kind)
        {
            SendErrorResponse(
                response,
                2021,
                $"{kind} is no longer available. Refresh the debug view.");
        }

        private void SendInspectionFailure(Response response)
        {
            SendErrorResponse(
                response,
                2022,
                "Managed inspection failed. Pause again and retry.");
        }

        private DapSource? ToDapSource(
            MappedSource? mapped)
        {
            if (mapped == null || !mapped.Available)
                return null;
            return new DapSource(
                mapped.Name,
                ConvertDebuggerPathToClient(mapped.Path) ?? mapped.Path,
                mapped.SourceReference);
        }

        private void ResetInspectionState(bool resetThreads)
        {
            Interlocked.Increment(ref inspectionGeneration);
            frameHandles.Reset();
            variableHandles.Reset();
            if (resetThreads)
                threadIds.Reset();
        }

        private bool IsCurrentInspectionGeneration(int generation) =>
            Volatile.Read(ref inspectionGeneration) == generation;

        private void SendEmptyScopes(Response response) =>
            SendResponse(
                response,
                new ScopesResponseBody(new List<Scope>()));

        private void SendEmptyVariables(Response response) =>
            SendResponse(
                response,
                new VariablesResponseBody(new List<Variable>()));

        private void SendEmptyEvaluation(Response response) =>
            SendResponse(
                response,
                new EvaluateResponseBody(string.Empty, 0));

        private bool TryGetDapThreadId(
            long backendThreadId,
            out int dapThreadId) =>
            threadIds.TryGetDapId(backendThreadId, out dapThreadId);

        private static string ToDapStopReason(
            BackendStopReason reason)
        {
            switch (reason)
            {
                case BackendStopReason.Breakpoint:
                    return "breakpoint";
                case BackendStopReason.Step:
                    return "step";
                case BackendStopReason.Exception:
                    return "exception";
                case BackendStopReason.Entry:
                    return "entry";
                default:
                    return "pause";
            }
        }

        private void Resume(
            Response response,
            object arguments,
            string operation,
            Action<IDebuggerBackend, long> action,
            ResponseBody? body = null)
        {
            if (!TryResolveControlTarget(
                response,
                arguments,
                out IDebuggerBackend value,
                out long threadId))
            {
                return;
            }
            try
            {
                action(value, threadId);
                SendResponse(response, body);
            }
            catch (Exception exception)
                when (IsControlFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2033,
                    "Execution control request failed.");
            }
        }

        private bool TryResolveControlTarget(
            Response response,
            object arguments,
            out IDebuggerBackend value,
            out long threadId)
        {
            threadId = 0;
            if (!TryGetBackend(response, out value))
                return false;
            var request = arguments as JObject;
            var dapThreadId = request?["threadId"]?.Value<int>() ?? 0;
            if (!threadIds.TryGetBackendId(dapThreadId, out threadId))
            {
                SendUnavailable(response, "Thread");
                return false;
            }
            return true;
        }

        private static bool IsControlFailure(Exception exception) =>
            exception is InvalidOperationException ||
            exception is DebuggerBackendException;
    }
}
