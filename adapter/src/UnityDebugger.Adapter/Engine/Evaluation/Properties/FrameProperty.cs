using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class FrameProperty : DebugProperty
    {
        private readonly IFrameEvaluationEnvironment environment;

        public FrameProperty(IFrameEvaluationEnvironment environment)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
        }

        public override string Name => "Locals";
        public override string TypeName => string.Empty;

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The Locals root does not represent a target value.");

        public override Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken) =>
            throw ReadOnly(Name);

        public override Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = environment.GetFrameValues();
            var children = new List<DebugProperty>();
            Add(children, frame.ThisValue);
            Add(children, frame.Locals);
            Add(children, frame.Arguments);
            Add(children, frame.Constants);
            Add(children, frame.ClosureCaptures);
            Add(children, frame.HoistedValues);
            Add(children, frame.CurrentException);
            return Task.FromResult<IReadOnlyList<DebugProperty>>(children);
        }

        private static void Add(
            ICollection<DebugProperty> children,
            FrameVariable? variable)
        {
            if (variable != null)
                children.Add(CreateProperty(variable));
        }

        private static void Add(
            ICollection<DebugProperty> children,
            IReadOnlyList<FrameVariable> variables)
        {
            foreach (var variable in variables)
                children.Add(CreateProperty(variable));
        }

        private static ValueProperty CreateProperty(FrameVariable variable) =>
            new ValueProperty(
                variable.Name,
                variable.Value,
                variable.CanSet ? variable.SetValueAsync : null);
    }
}
