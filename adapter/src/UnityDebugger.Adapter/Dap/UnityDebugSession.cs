using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class UnityDebugSession : DebugSession
    {
        private const string SupportedVersion = "2022.3.62t11";
        private readonly Func<IDebuggerBackend> backendFactory;
        private IDebuggerBackend? backend;
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
                supportsConditionalBreakpoints = false,
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
            dynamic arguments) => NotImplemented(response);

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
