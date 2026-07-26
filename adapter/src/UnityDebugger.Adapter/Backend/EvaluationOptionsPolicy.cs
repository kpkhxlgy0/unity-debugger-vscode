using System;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Backend
{
    internal static class EvaluationOptionsPolicy
    {
        public static EvaluationOptions Create(
            EvaluationOptions baseline,
            BackendEvaluationMode mode)
        {
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));

            var options = baseline.Clone();
            switch (mode)
            {
                case BackendEvaluationMode.Safe:
                    options.AllowTargetInvoke = false;
                    options.AllowMethodEvaluation = false;
                    options.AllowToStringCalls = false;
                    return options;
                case BackendEvaluationMode.Explicit:
                    options.AllowTargetInvoke = true;
                    options.AllowMethodEvaluation = true;
                    options.AllowToStringCalls = true;
                    return options;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unknown backend evaluation mode.");
            }
        }
    }
}
