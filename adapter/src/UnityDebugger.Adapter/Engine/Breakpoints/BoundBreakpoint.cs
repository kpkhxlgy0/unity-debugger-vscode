using System;
using UnityDebugger.Adapter.Engine.Source;

namespace UnityDebugger.Adapter.Engine.Breakpoints
{
    internal interface IEngineBreakpointRequest
    {
        object Identity { get; }
        void Enable();
        void Disable();
    }

    internal interface IEngineBreakpointRuntime
    {
        IEngineBreakpointRequest CreateBreakpoint(
            EngineSourceLocation location);
    }

    internal sealed class BoundBreakpoint
    {
        public BoundBreakpoint(
            long id,
            PendingBreakpoint pending,
            EngineSourceLocation location,
            IEngineBreakpointRequest request)
        {
            Id = id;
            Pending = pending ??
                throw new ArgumentNullException(nameof(pending));
            Location = location ??
                throw new ArgumentNullException(nameof(location));
            Request = request ??
                throw new ArgumentNullException(nameof(request));
        }

        public long Id { get; }
        public PendingBreakpoint Pending { get; }
        public EngineSourceLocation Location { get; }
        public IEngineBreakpointRequest Request { get; }
        public int HitCount { get; internal set; }
        public bool? PreviousConditionResult { get; internal set; }
    }
}
