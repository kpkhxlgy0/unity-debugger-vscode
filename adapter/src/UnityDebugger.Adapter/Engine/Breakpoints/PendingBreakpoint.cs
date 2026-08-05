using System;
using System.Collections.Generic;
using System.Linq;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Engine.Breakpoints
{
    internal sealed class PendingBreakpoint
    {
        private readonly List<BoundBreakpoint> bound =
            new List<BoundBreakpoint>();

        public PendingBreakpoint(LogicalBreakpoint value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public LogicalBreakpoint Value { get; }
        public long Id => Value.Id;
        public string SourcePath => Value.SourcePath;
        public int Line => Value.Line;
        public string? Condition => Value.Condition;
        public string? HitCondition => Value.HitCondition;
        public string? LogMessage => Value.LogMessage;
        public IReadOnlyList<BoundBreakpoint> Bound => bound.ToArray();

        internal void Add(BoundBreakpoint value)
        {
            bound.Add(value);
        }

        internal bool Remove(BoundBreakpoint value) => bound.Remove(value);
    }
}
