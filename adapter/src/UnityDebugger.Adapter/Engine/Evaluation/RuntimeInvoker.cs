using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal sealed class RuntimeInvoker
    {
        public const InvokeOptions EvaluationOptions =
            InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded;

        public Task<IRuntimeValue> InvokeAsync(
            IRuntimeValue value,
            RuntimeMethod method,
            IReadOnlyList<IRuntimeValue> arguments,
            CancellationToken cancellationToken) =>
            value.InvokeAsync(
                method,
                arguments,
                EvaluationOptions,
                cancellationToken);
    }
}
