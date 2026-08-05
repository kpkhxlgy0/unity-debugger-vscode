using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;

namespace UnityDebugger.Adapter.Engine.Evaluation.Runtime
{
    internal enum RuntimeValueKind
    {
        Null,
        Primitive,
        String,
        Enum,
        Array,
        Struct,
        Object,
        Pointer,
    }

    internal interface IRuntimeValue
    {
        RuntimeValueKind Kind { get; }
        IRuntimeType Type { get; }
        object? Primitive { get; }
        string? String { get; }
        long? Address { get; }
        IRuntimeValue GetField(RuntimeField field);
        void SetField(RuntimeField field, IRuntimeValue value);
        IRuntimeValue GetElement(int index);
        int Length { get; }
        Task<IRuntimeValue> InvokeAsync(
            RuntimeMethod method,
            IReadOnlyList<IRuntimeValue> arguments,
            InvokeOptions options,
            CancellationToken cancellationToken);
    }
}
