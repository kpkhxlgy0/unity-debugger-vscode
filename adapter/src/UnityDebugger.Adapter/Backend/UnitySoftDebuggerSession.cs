using Mono.Debugging.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class UnitySoftDebuggerSession : SoftDebuggerSession
    {
        protected override void OnExit()
        {
            Detach();
        }
    }
}
