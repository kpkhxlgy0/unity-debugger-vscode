using System;

namespace UnityDebugger.Adapter.Engine.Control
{
    internal sealed class StepManager
    {
        private const EngineStepFilter StandardFilters =
            EngineStepFilter.DebuggerHidden |
            EngineStepFilter.DebuggerStepThrough |
            EngineStepFilter.DebuggerNonUserCode;

        private readonly IStepRuntime runtime;
        private readonly Action<long> onStepCompleted;
        private readonly object sync = new object();
        private IStepRequest? activeRequest;

        public StepManager(
            IStepRuntime runtime,
            Action<long>? onStepCompleted = null)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            this.onStepCompleted = onStepCompleted ?? (_ => { });
        }

        public void RequestStep(long threadId, EngineStepDepth depth)
        {
            lock (sync)
            {
                CancelStepCore();
                var filters = StandardFilters;
                if (!runtime.IsStaticConstructorFrame(threadId))
                    filters |= EngineStepFilter.StaticConstructor;
                var options = new StepRequestOptions(
                    depth,
                    EngineStepSize.Line,
                    filters);
                var request = runtime.CreateStepRequest(threadId, options);
                activeRequest = request;
                try
                {
                    request.Enable();
                }
                catch
                {
                    runtime.Resume();
                    return;
                }
                runtime.Resume();
            }
        }

        public void CancelStep()
        {
            lock (sync)
            {
                CancelStepCore();
            }
        }

        public void ProcessStepEvent(long threadId)
        {
            lock (sync)
            {
                CancelStepCore();
            }
            onStepCompleted(threadId);
        }

        private void CancelStepCore()
        {
            if (activeRequest == null)
                return;
            activeRequest.Disable();
            activeRequest = null;
        }
    }
}
