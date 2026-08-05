using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal interface IFrameEvaluationEnvironment
    {
        FrameValues GetFrameValues();
        bool TryGetValue(string name, out IRuntimeValue value);
        bool TryGetType(string fullOrSimpleName, out IRuntimeType type);
    }

    internal sealed class FrameVariable
    {
        private readonly Func<
            IRuntimeValue,
            CancellationToken,
            Task>? setValue;

        public FrameVariable(
            string name,
            IRuntimeValue value,
            Func<IRuntimeValue, CancellationToken, Task>? setValue = null)
        {
            Name = name;
            Value = value;
            this.setValue = setValue;
        }

        public string Name { get; }
        public IRuntimeValue Value { get; }
        public bool CanSet => setValue != null;

        public Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken)
        {
            if (setValue == null)
            {
                throw new InvalidOperationException(
                    $"The frame value '{Name}' cannot be changed.");
            }

            return setValue(value, cancellationToken);
        }
    }

    internal sealed class FrameValues
    {
        public FrameValues(
            FrameVariable? thisValue,
            IReadOnlyList<FrameVariable> locals,
            IReadOnlyList<FrameVariable> arguments,
            IReadOnlyList<FrameVariable> constants,
            IReadOnlyList<FrameVariable> closureCaptures,
            IReadOnlyList<FrameVariable> hoistedValues,
            FrameVariable? currentException)
        {
            ThisValue = thisValue;
            Locals = locals;
            Arguments = arguments;
            Constants = constants;
            ClosureCaptures = closureCaptures;
            HoistedValues = hoistedValues;
            CurrentException = currentException;
        }

        public static FrameValues Empty { get; } = new FrameValues(
            null,
            Array.Empty<FrameVariable>(),
            Array.Empty<FrameVariable>(),
            Array.Empty<FrameVariable>(),
            Array.Empty<FrameVariable>(),
            Array.Empty<FrameVariable>(),
            null);

        public FrameVariable? ThisValue { get; }
        public IReadOnlyList<FrameVariable> Locals { get; }
        public IReadOnlyList<FrameVariable> Arguments { get; }
        public IReadOnlyList<FrameVariable> Constants { get; }
        public IReadOnlyList<FrameVariable> ClosureCaptures { get; }
        public IReadOnlyList<FrameVariable> HoistedValues { get; }
        public FrameVariable? CurrentException { get; }
    }
}
