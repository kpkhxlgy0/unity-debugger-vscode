using Mono.Debugging.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class UnitySoftDebuggerSession :
        SoftDebuggerSession,
        ISoftExceptionRequestSession
    {
        protected override void OnExit()
        {
            Detach();
        }
    }
}
