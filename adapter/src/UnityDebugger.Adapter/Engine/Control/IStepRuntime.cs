namespace UnityDebugger.Adapter.Engine.Control
{
    internal interface IStepRequest
    {
        void Enable();
        void Disable();
    }

    internal interface IStepRuntime
    {
        bool IsStaticConstructorFrame(long threadId);
        IStepRequest CreateStepRequest(
            long threadId,
            StepRequestOptions options);
        void Resume();
    }
}
