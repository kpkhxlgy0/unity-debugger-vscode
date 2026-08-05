using System;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Engine.Breakpoints
{
    internal interface IEngineExceptionRequest
    {
        object Identity { get; }
        void Enable();
        void Disable();
    }

    internal interface IEngineExceptionRuntime
    {
        IEngineExceptionRequest CreateExceptionRequest(
            bool caught,
            bool uncaught);
    }

    internal sealed class RuntimeExceptionHit
    {
        public RuntimeExceptionHit(
            object requestIdentity,
            string typeName,
            string description,
            string? stackTrace)
        {
            RequestIdentity = requestIdentity ??
                throw new ArgumentNullException(nameof(requestIdentity));
            TypeName = typeName;
            Description = description;
            StackTrace = stackTrace;
        }

        public object RequestIdentity { get; }
        public string TypeName { get; }
        public string Description { get; }
        public string? StackTrace { get; }
    }

    internal sealed class ExceptionHitResult
    {
        public ExceptionHitResult(
            bool shouldStop,
            BackendExceptionInfo? exceptionInfo)
        {
            ShouldStop = shouldStop;
            ExceptionInfo = exceptionInfo;
        }

        public bool ShouldStop { get; }
        public BackendExceptionInfo? ExceptionInfo { get; }
    }

    internal sealed class EngineExceptionManager
    {
        private readonly IEngineExceptionRuntime runtime;
        private IEngineExceptionRequest? request;
        private ExceptionBreakMode mode;

        public EngineExceptionManager(IEngineExceptionRuntime runtime)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public void Configure(ExceptionBreakMode value)
        {
            request?.Disable();
            request = null;
            mode = value;
            if (value == ExceptionBreakMode.None)
                return;
            request = runtime.CreateExceptionRequest(
                value == ExceptionBreakMode.All,
                uncaught: true);
            request.Enable();
        }

        public ExceptionHitResult Process(RuntimeExceptionHit value)
        {
            if (
                request == null ||
                !ReferenceEquals(
                    request.Identity,
                    value.RequestIdentity))
            {
                return new ExceptionHitResult(false, null);
            }

            return new ExceptionHitResult(
                true,
                new BackendExceptionInfo(
                    value.TypeName,
                    value.Description,
                    mode == ExceptionBreakMode.All
                        ? "always"
                        : "unhandled",
                    value.StackTrace));
        }
    }
}
