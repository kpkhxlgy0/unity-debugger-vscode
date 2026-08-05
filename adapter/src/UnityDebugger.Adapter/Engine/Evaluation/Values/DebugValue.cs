using System;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Values
{
    internal enum DebugValueKind
    {
        Value,
        Error,
        NotEvaluated,
    }

    internal sealed class DebugValue
    {
        private DebugValue(
            DebugValueKind kind,
            IRuntimeValue? value,
            string display)
        {
            Kind = kind;
            Value = value;
            Display = display;
        }

        public DebugValueKind Kind { get; }
        public IRuntimeValue? Value { get; }
        public string Display { get; }

        public static DebugValue FromValue(IRuntimeValue value) =>
            new DebugValue(
                DebugValueKind.Value,
                value ?? throw new ArgumentNullException(nameof(value)),
                string.Empty);

        public static DebugValue Error(string display) =>
            new DebugValue(DebugValueKind.Error, null, display);

        public static DebugValue NotEvaluated(string display) =>
            new DebugValue(DebugValueKind.NotEvaluated, null, display);
    }

    internal sealed class EvaluationPolicy
    {
        public EvaluationPolicy(
            bool allowTargetInvoke,
            bool allowGetters,
            bool allowToString)
        {
            AllowTargetInvoke = allowTargetInvoke;
            AllowGetters = allowGetters;
            AllowToString = allowToString;
        }

        public static EvaluationPolicy Safe { get; } =
            new EvaluationPolicy(false, false, false);

        public static EvaluationPolicy Explicit { get; } =
            new EvaluationPolicy(true, true, true);

        public bool AllowTargetInvoke { get; }
        public bool AllowGetters { get; }
        public bool AllowToString { get; }
    }
}
