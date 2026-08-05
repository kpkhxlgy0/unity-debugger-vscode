using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal interface IFrameEvaluationEnvironment
    {
        bool TryGetValue(string name, out IRuntimeValue value);
        bool TryGetType(string fullOrSimpleName, out IRuntimeType type);
    }
}
