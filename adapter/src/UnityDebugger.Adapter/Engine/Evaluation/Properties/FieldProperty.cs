using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class FieldProperty : DebugProperty
    {
        private readonly IRuntimeValue target;
        private readonly RuntimeField runtimeField;

        public FieldProperty(IRuntimeValue target, RuntimeField field)
        {
            this.target = target;
            runtimeField = field;
        }

        public override string Name => runtimeField.Name;
        public override string TypeName => runtimeField.Type.FullName;

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(target.GetField(runtimeField));
        }

        public override Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (runtimeField.IsLiteral)
                throw ReadOnly(Name);
            target.SetField(runtimeField, value);
            return Task.CompletedTask;
        }

        public override async Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken)
        {
            var value = await GetValueAsync(cancellationToken)
                .ConfigureAwait(false);
            return await new ValueProperty(Name, value)
                .GetChildrenAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
