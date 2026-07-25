using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Breakpoints;
using UnityDebugger.Adapter.Source;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class UnityDebugSession : DebugSession
    {
        private const string SupportedVersion = "2022.3.62t11";
        private readonly Func<IDebuggerBackend> backendFactory;
        private IDebuggerBackend? backend;
        private BreakpointManager? breakpointManager;
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
                supportsFunctionBreakpoints = false,
                supportsConditionalBreakpoints = true,
                supportsEvaluateForHovers = false,
                supportsExceptionOptions = false,
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
                        "Support policy: https://marketplace.visualstudio.com/" +
                        "items?itemName=unity-debugger-community." +
                        "unity-debugger-vscode#support-policy" +
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
            dynamic arguments) => NotImplemented(response);

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
            dynamic arguments) => NotImplemented(response);

        public override void Next(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void StepIn(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void StepOut(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void Pause(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void StackTrace(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void Scopes(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void Variables(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void Threads(
            Response response,
            dynamic arguments) => NotImplemented(response);

        public override void Evaluate(
            Response response,
            dynamic arguments) => NotImplemented(response);

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
            value.ReloadCompleted += OnReloadCompleted;
            value.Terminated += OnTerminated;
        }

        private void Unsubscribe(IDebuggerBackend value)
        {
            value.Stopped -= OnStopped;
            value.Continued -= OnContinued;
            value.ThreadChanged -= OnThreadChanged;
            value.BreakpointChanged -= OnBreakpointChanged;
            value.ReloadStarted -= OnReloadStarted;
            value.ReloadCompleted -= OnReloadCompleted;
            value.Terminated -= OnTerminated;
        }

        private void ReleaseBackend()
        {
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
        }

        private void OnContinued(object sender, EventArgs arguments)
        {
        }

        private void OnThreadChanged(
            object sender,
            BackendThreadEventArgs arguments)
        {
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
        }

        private void OnReloadCompleted(object sender, EventArgs arguments)
        {
        }

        private void OnTerminated(object sender, EventArgs arguments)
        {
            ReleaseBackend();
            SendTerminatedOnce();
        }
    }
}
