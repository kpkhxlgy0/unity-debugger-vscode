using System;

namespace UnityDebugger.Adapter.Engine.Control
{
    internal enum EngineStepDepth
    {
        Into,
        Over,
        Out,
    }

    internal enum EngineStepSize
    {
        Line,
    }

    [Flags]
    internal enum EngineStepFilter
    {
        None = 0,
        DebuggerHidden = 1,
        DebuggerStepThrough = 2,
        DebuggerNonUserCode = 4,
        StaticConstructor = 8,
    }

    internal sealed class StepRequestOptions
    {
        public StepRequestOptions(
            EngineStepDepth depth,
            EngineStepSize size,
            EngineStepFilter filters)
        {
            Depth = depth;
            Size = size;
            Filters = filters;
        }

        public EngineStepDepth Depth { get; }
        public EngineStepSize Size { get; }
        public EngineStepFilter Filters { get; }
    }
}
