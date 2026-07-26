using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            "unity-debugger-community.unity-debugger-vscode" +
            "#support-policy";
        private readonly Func<IDebuggerBackend> backendFactory;
        private readonly ThreadIdMap threadIds = new ThreadIdMap();
        private readonly HandleTable<BackendStackFrame> frameHandles =
            new HandleTable<BackendStackFrame>();
        private readonly HandleTable<BackendVariable[]> variableHandles =
            new HandleTable<BackendVariable[]>();
        private readonly ExecutionState executionState =
            new ExecutionState();
        private IDebuggerBackend? backend;
        private BreakpointManager? breakpointManager;
        private SourceMapper? sourceMapper;
        private bool terminatedSent;
        private bool controlResponsePending;
        private bool awaitingContinuedEvent;
        private bool reloadRequiresRebind;
        private int activeDapThreadId;
        private Event? bufferedControlEvent;

        public UnityDebugSession(Func<IDebuggerBackend> backendFactory)
        {
            this.backendFactory = backendFactory ??
                throw new ArgumentNullException(nameof(backendFactory));
        }

        public override void Initialize(Response response, dynamic args)
        {
            SendResponse(response, new Capabilities
            {
                supportsConfigurationDoneRequest = false,
                supportsFunctionBreakpoints = false,
                supportsConditionalBreakpoints = true,
                supportsEvaluateForHovers = false,
                supportsExceptionOptions = true,
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
                supportsSetVariable = false,
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
                var createdBackend = backendFactory();
                backend = createdBackend;
                Subscribe(createdBackend);
                createdBackend.Attach(target);
                executionState.Attached();
                breakpointManager = new BreakpointManager(createdBackend);
                breakpointManager.Changed += OnManagedBreakpointChanged;
                sourceMapper = new SourceMapper(
                    target.WorkspaceRoot,
                    File.Exists);
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
                executionState.RequireRunning("Pause");
            }
            catch (InvalidOperationException exception)
            {
                SendErrorResponse(response, 2032, exception.Message);
                return;
            }
            try
            {
                BeginControlResponse();
                value.Pause(threadId);
                SendResponse(response);
                EndControlResponse();
            }
            catch (Exception exception)
                when (IsControlFailure(exception))
            {
                CancelControlResponse();
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
                            source == null ? "deemphasize" : "normal"));
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
            var request = arguments as JObject;
            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;
            if (!frameHandles.TryGet(frameHandle, out var frame))
            {
                SendUnavailable(response, "Stack frame");
                return;
            }

            try
            {
                var scopes = new List<Scope>();
                foreach (var scope in value.GetScopes(frame.Id))
                {
                    var variables = value.GetVariables(
                        scope.VariablesReference);
                    scopes.Add(
                        new Scope(
                            scope.Name,
                            variableHandles.Create(variables.ToArray()),
                            scope.Expensive));
                }
                SendResponse(response, new ScopesResponseBody(scopes));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendInspectionFailure(response);
            }
        }

        public override void Variables(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = arguments as JObject;
            var reference =
                request?["variablesReference"]?.Value<int>() ?? 0;
            if (!variableHandles.TryGet(reference, out var variables))
            {
                SendUnavailable(response, "Variable collection");
                return;
            }

            try
            {
                const int MaximumVariables = 100;
                var dapVariables = new List<Variable>();
                foreach (var variable in variables.Take(MaximumVariables))
                {
                    var childReference = 0;
                    if (variable.VariablesReference > 0)
                    {
                        var children = value.GetVariables(
                            variable.VariablesReference);
                        childReference = variableHandles.Create(
                            children.ToArray());
                    }
                    dapVariables.Add(
                        new Variable(
                            variable.Name,
                            variable.DisplayValue,
                            variable.TypeName,
                            childReference));
                }
                if (variables.Length > MaximumVariables)
                {
                    dapVariables.Add(
                        new Variable(
                            "...",
                            "More variables are not shown.",
                            "",
                            0));
                }
                SendResponse(
                    response,
                    new VariablesResponseBody(dapVariables));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendInspectionFailure(response);
            }
        }

        public override void Threads(
            Response response,
            dynamic arguments)
        {
            if (executionState.Status == ExecutionStatus.Reloading)
            {
                SendResponse(
                    response,
                    new ThreadsResponseBody(
                        new List<VSCodeDebug.Thread>()));
                return;
            }
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
            var request = arguments as JObject;
            var context = request?["context"]?.Value<string>();
            if (
                !string.Equals(
                    context,
                    "watch",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    context,
                    "repl",
                    StringComparison.Ordinal))
            {
                SendErrorResponse(
                    response,
                    2024,
                    "Evaluation is available only for watch or repl.");
                return;
            }

            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;
            if (!frameHandles.TryGet(frameHandle, out var frame))
            {
                SendUnavailable(response, "Stack frame");
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
                var result = value.Evaluate(frame.Id, expression!);
                var childReference = 0;
                if (result.VariablesReference > 0)
                {
                    childReference = variableHandles.Create(
                        value.GetVariables(
                            result.VariablesReference).ToArray());
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
                SendErrorResponse(
                    response,
                    2026,
                    "Expression evaluation failed.");
            }
        }

        private void NotImplemented(Response response)
        {
            SendErrorResponse(response, 2005, "Not implemented yet.");
        }

        private void Subscribe(IDebuggerBackend value)
        {
            value.Stopped += OnStopped;
            value.Continued += OnContinued;
            value.ThreadChanged += OnThreadChanged;
            value.BreakpointChanged += OnBreakpointChanged;
            value.ReloadStarted += OnReloadStarted;
            value.ReloadProgress += OnReloadProgress;
            value.ReloadCompleted += OnReloadCompleted;
            value.ReconnectFailed += OnReconnectFailed;
            value.Terminated += OnTerminated;
        }

        private void Unsubscribe(IDebuggerBackend value)
        {
            value.Stopped -= OnStopped;
            value.Continued -= OnContinued;
            value.ThreadChanged -= OnThreadChanged;
            value.BreakpointChanged -= OnBreakpointChanged;
            value.ReloadStarted -= OnReloadStarted;
            value.ReloadProgress -= OnReloadProgress;
            value.ReloadCompleted -= OnReloadCompleted;
            value.ReconnectFailed -= OnReconnectFailed;
            value.Terminated -= OnTerminated;
        }

        private void ReleaseBackend()
        {
            ResetInspectionState(resetThreads: true);
            executionState.Disconnected();
            CancelControlResponse();
            activeDapThreadId = 0;
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
            if (executionState.Status != ExecutionStatus.Running)
                return;
            executionState.Stopped();
            awaitingContinuedEvent = false;
            ResetInspectionState(resetThreads: false);
            activeDapThreadId =
                threadIds.GetOrCreate(arguments.ThreadId);
            SendOrBufferControlEvent(
                new StoppedEvent(
                    activeDapThreadId,
                    ToDapStopReason(arguments.Reason),
                    arguments.Description));
        }

        private void OnContinued(object sender, EventArgs arguments)
        {
            ResetInspectionState(resetThreads: false);
            if (executionState.Status == ExecutionStatus.Stopped)
            {
                executionState.Continued();
            }
            else if (
                executionState.Status != ExecutionStatus.Running ||
                !awaitingContinuedEvent)
            {
                return;
            }
            awaitingContinuedEvent = false;
            SendOrBufferControlEvent(
                new Event(
                    "continued",
                    new
                    {
                        threadId = activeDapThreadId,
                        allThreadsContinued = true,
                    }));
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

        private void OnBreakpointChanged(
            object sender,
            BackendBreakpointChangedEventArgs arguments)
        {
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

        private void OnReloadStarted(object sender, EventArgs arguments)
        {
            bool wasStopped;
            try
            {
                wasStopped = executionState.ReloadStarted();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            reloadRequiresRebind =
                sender is IDebuggerBackend value &&
                !value.IsAttached;
            awaitingContinuedEvent = false;
            ResetInspectionState(resetThreads: true);
            if (wasStopped)
            {
                SendEvent(
                    new Event(
                        "continued",
                        new
                        {
                            threadId = activeDapThreadId,
                            allThreadsContinued = true,
                        }));
            }
            breakpointManager?.BeginReload(
                preserveVerified: !reloadRequiresRebind,
                "Waiting for assemblies after Domain Reload.");
            SendEvent(
                new OutputEvent(
                    "console",
                    "Domain Reload detected; waiting for assemblies." +
                    Environment.NewLine));
        }

        private void OnReloadProgress(
            object sender,
            EventArgs arguments)
        {
            if (executionState.Status != ExecutionStatus.Reloading)
                return;
        }

        private void OnReloadCompleted(object sender, EventArgs arguments)
        {
            if (!executionState.ReloadCompleted())
                return;
            var reboundBindings = reloadRequiresRebind;
            if (reboundBindings)
                breakpointManager?.RebindAll();
            breakpointManager?.CompleteReload();
            reloadRequiresRebind = false;
            var verified = breakpointManager?.VerifiedCount ?? 0;
            var pending = breakpointManager?.PendingCount ?? 0;
            InternalDebuggerLog.Write(
                pending == 0 && verified > 0
                    ? "unity-debugger.reload.complete.bound"
                    : "unity-debugger.reload.complete.pending");
            SendEvent(
                new OutputEvent(
                    "console",
                    $"Domain Reload complete; {verified} " +
                    (reboundBindings ? "rebound" : "verified") +
                    ", " +
                    $"{pending} pending." +
                    Environment.NewLine));
        }

        private void OnTerminated(object sender, EventArgs arguments)
        {
            ReleaseBackend();
            SendTerminatedOnce();
        }

        private void OnReconnectFailed(
            object sender,
            EventArgs arguments)
        {
            SendEvent(
                new OutputEvent(
                    "console",
                    "Lost connection to the local Editor and could not " +
                    "reconnect within 10 seconds. Check Code " +
                    "Optimization and the local firewall." +
                    Environment.NewLine));
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
            out IDebuggerBackend value)
        {
            if (!TryGetBackend(response, out value))
                return false;
            if (executionState.Status != ExecutionStatus.Reloading)
                return true;
            SendErrorResponse(
                response,
                2027,
                "Managed inspection is unavailable during Domain Reload.");
            return false;
        }

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
            frameHandles.Reset();
            variableHandles.Reset();
            if (resetThreads)
                threadIds.Reset();
        }

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
                executionState.RequireStopped(operation);
            }
            catch (InvalidOperationException exception)
            {
                SendErrorResponse(response, 2033, exception.Message);
                return;
            }
            try
            {
                threadIds.TryGetDapId(
                    threadId,
                    out activeDapThreadId);
                executionState.Continued();
                awaitingContinuedEvent = true;
                BeginControlResponse();
                action(value, threadId);
                SendResponse(response, body);
                EndControlResponse();
            }
            catch (Exception exception)
                when (IsControlFailure(exception))
            {
                CancelControlResponse();
                awaitingContinuedEvent = false;
                if (executionState.Status == ExecutionStatus.Running)
                    executionState.Stopped();
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

        private void BeginControlResponse()
        {
            controlResponsePending = true;
            bufferedControlEvent = null;
        }

        private void EndControlResponse()
        {
            controlResponsePending = false;
            var value = bufferedControlEvent;
            bufferedControlEvent = null;
            if (value != null)
                SendEvent(value);
        }

        private void CancelControlResponse()
        {
            controlResponsePending = false;
            bufferedControlEvent = null;
        }

        private void SendOrBufferControlEvent(Event value)
        {
            if (controlResponsePending)
            {
                bufferedControlEvent = value;
                return;
            }
            SendEvent(value);
        }
    }
}
