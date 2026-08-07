using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        private const int DefaultInspectionTimeoutMilliseconds = 10000;
        private const string SupportedVersion = "2022.3.62t11";
        private const string SupportPolicyUrl =
            "https://marketplace.visualstudio.com/items?itemName=" +
            "kpk.unity-debugger-pure" +
            "#support-policy";
        private readonly Func<IDebuggerBackend> backendFactory;
        private readonly ThreadIdMap threadIds = new ThreadIdMap();
        private readonly Dictionary<long, BackendExceptionInfo> exceptions =
            new Dictionary<long, BackendExceptionInfo>();
        private IDebuggerBackend? backend;
        private BreakpointManager? breakpointManager;
        private FunctionBreakpointManager? functionBreakpointManager;
        private SourceMapper? sourceMapper;
        private bool terminatedSent;

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
                        "User-Unhandled Exceptions",
                        false),
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
                var createdBackend = backendFactory();
                backend = createdBackend;
                Subscribe(createdBackend);
                var breakpointIds = new BreakpointIdAllocator();
                breakpointManager = new BreakpointManager(
                    createdBackend,
                    breakpointIds);
                breakpointManager.Changed += OnManagedBreakpointChanged;
                functionBreakpointManager = new FunctionBreakpointManager(
                    createdBackend,
                    breakpointIds);
                functionBreakpointManager.Changed +=
                    OnManagedFunctionBreakpointChanged;
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
            var request = arguments as JObject;
            var requested = new List<RequestedFunctionBreakpoint>();
            if (request?["breakpoints"] is JArray breakpointTokens)
            {
                foreach (var token in breakpointTokens.OfType<JObject>())
                {
                    var name = token["name"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        SendErrorResponse(
                            response,
                            2014,
                            "Function breakpoint name is required.");
                        return;
                    }
                    requested.Add(
                        new RequestedFunctionBreakpoint(
                            name!,
                            token["condition"]?.Value<string>(),
                            token["hitCondition"]?.Value<string>()));
                }
            }

            if (functionBreakpointManager == null)
            {
                if (requested.Count == 0)
                {
                    SendResponse(
                        response,
                        new DapSetFunctionBreakpointsResponseBody(
                            Array.Empty<DapFunctionBreakpoint>()));
                    return;
                }
                SendErrorResponse(
                    response,
                    2013,
                    "Attach to an Editor before setting function breakpoints.");
                return;
            }

            var managed = functionBreakpointManager.Replace(requested);
            SendResponse(
                response,
                new DapSetFunctionBreakpointsResponseBody(
                    managed.Select(ToDapFunctionBreakpoint)));
        }

        public override void StepInTargets(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = arguments as JObject;
            var frameId = request?["frameId"]?.Value<int>() ?? 0;
            try
            {
                var targets = value.GetStepInTargets(frameId)
                    .Select(
                        target => new StepInTarget(
                            ToDapHandle(target.Id),
                            target.Label))
                    .Where(target => target.id > 0)
                    .ToArray();
                SendResponse(
                    response,
                    new StepInTargetsResponseBody(targets));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendResponse(
                    response,
                    new StepInTargetsResponseBody(
                        Array.Empty<StepInTarget>()));
            }
        }

        public override void GotoTargets(
            Response response,
            dynamic arguments)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = arguments as JObject;
            var source = request?["source"] as JObject;
            var clientPath = source?["path"]?.Value<string>();
            var sourcePath = ConvertClientPathToDebugger(clientPath);
            if (string.IsNullOrEmpty(sourcePath))
            {
                SendResponse(
                    response,
                    new GotoTargetsResponseBody(
                        Array.Empty<GotoTarget>()));
                return;
            }
            var line = ConvertClientLineToDebugger(
                request?["line"]?.Value<int>() ?? 0);
            var column = Math.Max(
                1,
                request?["column"]?.Value<int>() ?? 1);
            try
            {
                var targets = value.GetGotoTargets(
                        sourcePath,
                        line,
                        column)
                    .Select(
                        target => new GotoTarget(
                            ToDapHandle(target.Id),
                            target.Label,
                            ConvertDebuggerLineToClient(target.Line),
                            Math.Max(1, target.Column),
                            ConvertDebuggerLineToClient(target.EndLine),
                            Math.Max(1, target.EndColumn)))
                    .Where(target => target.id > 0)
                    .ToArray();
                SendResponse(
                    response,
                    new GotoTargetsResponseBody(targets));
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendResponse(
                    response,
                    new GotoTargetsResponseBody(
                        Array.Empty<GotoTarget>()));
            }
        }

        public override void Goto(
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
            var request = arguments as JObject;
            var targetId = request?["targetId"]?.Value<int>() ?? 0;
            try
            {
                value.Goto(threadId, targetId);
                SendResponse(response);
            }
            catch (NotSupportedException)
            {
                SendResponse(response);
            }
            catch (Exception exception)
                when (IsControlFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2034,
                    "Goto request failed.");
            }
        }

        public override void ExceptionInfo(
            Response response,
            dynamic arguments)
        {
            var request = arguments as JObject;
            var dapThreadId = request?["threadId"]?.Value<int>() ?? 0;
            if (
                !threadIds.TryGetBackendId(
                    dapThreadId,
                    out var backendThreadId) ||
                !exceptions.TryGetValue(
                    backendThreadId,
                    out var exception))
            {
                SendResponse(response);
                return;
            }

            SendResponse(
                response,
                new DapExceptionInfoResponseBody(exception));
        }

        protected override void SetVariable(
            Response response,
            object args)
        {
            if (!TryGetInspectionBackend(response, out var value))
                return;
            var request = args as JObject;
            var reference =
                request?["variablesReference"]?.Value<int>() ?? 0;
            var name = request?["name"]?.Value<string>();
            var expression = request?["value"]?.Value<string>();
            if (
                reference <= 0 ||
                string.IsNullOrEmpty(name) ||
                string.IsNullOrWhiteSpace(expression))
            {
                SendErrorResponse(
                    response,
                    2027,
                    "A variable reference, name, and value are required.");
                return;
            }

            try
            {
                var result = value.SetVariable(
                    reference,
                    name!,
                    expression!,
                    GetTimeoutMilliseconds(request),
                    CancellationToken.None);
                if (result == null)
                {
                    SendResponse(response);
                    return;
                }
                SendResponse(
                    response,
                    new SetVariablesResponseBody(
                        result.DisplayValue,
                        result.TypeName,
                        ToDapHandle(result.VariablesReference)));
            }
            catch (OperationCanceledException)
            {
                SendResponse(response);
            }
            catch (BackendEvaluationException exception)
            {
                SendErrorResponse(
                    response,
                    2028,
                    exception.DisplayMessage);
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2028,
                    "Set Variable failed.");
            }
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
                        token["condition"]?.Value<string>(),
                        token["hitCondition"]?.Value<string>(),
                        token["logMessage"]?.Value<string>()));
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
            dynamic arguments)
        {
            var request = arguments as JObject;
            var targetId = request?["targetId"]?.Value<long?>();
            Resume(
                response,
                (object)arguments,
                "Step in",
                (value, threadId) => value.StepIn(threadId, targetId));
        }

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
                            ToDapHandle(frame.Id),
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
            var request = arguments as JObject;
            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;

            try
            {
                var backendScopes = value.GetScopes(
                    frameHandle,
                    GetTimeoutMilliseconds(request),
                    CancellationToken.None).ToArray();
                var scopes = new List<Scope>();
                foreach (var scope in backendScopes)
                {
                    scopes.Add(
                        new Scope(
                            scope.Name,
                            ToDapHandle(scope.VariablesReference),
                            scope.Expensive));
                }
                SendResponse(response, new ScopesResponseBody(scopes));
            }
            catch (OperationCanceledException)
            {
                SendEmptyScopes(response);
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendEmptyScopes(response);
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

            try
            {
                var dapVariables = new List<Variable>();
                var variables = value.GetVariables(
                    reference,
                    GetTimeoutMilliseconds(request),
                    CancellationToken.None);
                foreach (var variable in variables)
                {
                    dapVariables.Add(
                        new Variable(
                            variable.Name,
                            variable.DisplayValue,
                            variable.TypeName,
                            ToDapHandle(
                                variable.VariablesReference)));
                }
                SendResponse(
                    response,
                    new VariablesResponseBody(dapVariables));
            }
            catch (OperationCanceledException)
            {
                SendEmptyVariables(response);
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
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
            var request = arguments as JObject;
            var context = request?["context"]?.Value<string>();
            if (
                !string.Equals(
                    context,
                    "hover",
                    StringComparison.Ordinal) &&
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
                    "The requested evaluation context is not supported.");
                return;
            }

            var frameHandle = request?["frameId"]?.Value<int>() ?? 0;
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
                    frameHandle,
                    expression!,
                    GetTimeoutMilliseconds(request),
                    CancellationToken.None);
                if (result == null)
                {
                    SendEmptyEvaluation(response);
                    return;
                }
                SendResponse(
                    response,
                    new EvaluateResponseBody(
                        result.DisplayValue,
                        ToDapHandle(result.VariablesReference)));
            }
            catch (OperationCanceledException)
            {
                SendEmptyEvaluation(response);
            }
            catch (BackendEvaluationException exception)
            {
                SendErrorResponse(
                    response,
                    2026,
                    exception.DisplayMessage,
                    user: false);
            }
            catch (Exception exception)
                when (IsInspectionFailure(exception))
            {
                SendErrorResponse(
                    response,
                    2026,
                    "Expression evaluation failed.",
                    user: false);
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
            threadIds.Reset();
            exceptions.Clear();
            if (breakpointManager != null)
            {
                breakpointManager.Changed -=
                    OnManagedBreakpointChanged;
                breakpointManager.Dispose();
                breakpointManager = null;
                sourceMapper = null;
            }
            if (functionBreakpointManager != null)
            {
                functionBreakpointManager.Changed -=
                    OnManagedFunctionBreakpointChanged;
                functionBreakpointManager.Dispose();
                functionBreakpointManager = null;
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
            exceptions.Clear();
            if (arguments.ExceptionInfo != null)
            {
                exceptions[arguments.ThreadId] =
                    arguments.ExceptionInfo;
            }
            var dapThreadId =
                threadIds.GetOrCreate(arguments.ThreadId);
            long[]? hitBreakpointIds = null;
            if (
                arguments.Reason == BackendStopReason.Breakpoint &&
                arguments.BreakpointIds.Count > 0)
            {
                var logicalIds = new List<long>();
                foreach (var breakpointId in arguments.BreakpointIds)
                {
                    if (
                        breakpointManager != null &&
                        breakpointManager.TryGetLogicalId(
                            breakpointId,
                            out var logicalId))
                    {
                        logicalIds.Add(logicalId);
                        continue;
                    }
                    if (
                        functionBreakpointManager != null &&
                        functionBreakpointManager.TryGetLogicalId(
                            breakpointId,
                            out logicalId))
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

        private void OnManagedFunctionBreakpointChanged(
            object? sender,
            ManagedFunctionBreakpointChangedEventArgs arguments)
        {
            SendEvent(new Event(
                "breakpoint",
                new
                {
                    reason = "changed",
                    breakpoint = ToDapFunctionBreakpoint(
                        arguments.Breakpoint),
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

        private static DapFunctionBreakpoint ToDapFunctionBreakpoint(
            ManagedFunctionBreakpoint item) =>
            new DapFunctionBreakpoint(
                item.Id,
                item.Verified,
                item.Message);

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
            exception is DebuggerBackendException ||
            exception is BackendEvaluationException;

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

        private void SendEmptyScopes(Response response) =>
            SendResponse(
                response,
                new ScopesResponseBody(new List<Scope>()));

        private void SendEmptyVariables(Response response) =>
            SendResponse(
                response,
                new VariablesResponseBody(new List<Variable>()));

        private void SendEmptyEvaluation(Response response) =>
            SendResponse(response);

        private static int GetTimeoutMilliseconds(JObject? request)
        {
            var timeout = request?["timeout"]?.Value<int>() ??
                DefaultInspectionTimeoutMilliseconds;
            return timeout > 0
                ? timeout
                : DefaultInspectionTimeoutMilliseconds;
        }

        private static int ToDapHandle(long handle) =>
            handle > 0 && handle <= int.MaxValue
                ? (int)handle
                : 0;

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
                case BackendStopReason.Goto:
                    return "goto";
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
