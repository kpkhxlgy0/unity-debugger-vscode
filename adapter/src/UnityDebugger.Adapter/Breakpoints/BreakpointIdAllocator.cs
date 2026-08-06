using System.Threading;

namespace UnityDebugger.Adapter.Breakpoints
{
    internal sealed class BreakpointIdAllocator
    {
        private long nextId;

        public long Next() => Interlocked.Increment(ref nextId);
    }
}
