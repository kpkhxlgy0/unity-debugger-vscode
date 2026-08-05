using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class ValueProperty : DebugProperty
    {
        private readonly Func<
            IRuntimeValue,
            CancellationToken,
            Task>? setValue;
        private IRuntimeValue value;

        public ValueProperty(
            string name,
            IRuntimeValue value,
            Func<IRuntimeValue, CancellationToken, Task>? setValue = null)
        {
            Name = name;
            this.value = value;
            this.setValue = setValue;
        }

        public override string Name { get; }
        public override string TypeName => value.Type.FullName;

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(value);
        }

        public override async Task SetValueAsync(
            IRuntimeValue newValue,
            CancellationToken cancellationToken)
        {
            if (setValue == null)
                throw ReadOnly(Name);
            await setValue(newValue, cancellationToken).ConfigureAwait(false);
            value = newValue;
        }

        public override Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var children = new List<DebugProperty>();
            foreach (var field in value.Type.Fields)
                children.Add(new FieldProperty(value, field));
            foreach (var property in value.Type.Properties)
            {
                if (property.Getter != null)
                    children.Add(new AccessorProperty(value, property));
            }

            return Task.FromResult<IReadOnlyList<DebugProperty>>(children);
        }
    }
}
