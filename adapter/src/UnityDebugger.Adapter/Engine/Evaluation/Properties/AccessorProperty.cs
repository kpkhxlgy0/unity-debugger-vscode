using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class AccessorProperty : DebugProperty
    {
        private readonly IRuntimeValue target;
        private readonly RuntimeProperty property;
        private readonly RuntimeInvoker invoker;

        public AccessorProperty(
            IRuntimeValue target,
            RuntimeProperty property,
            RuntimeInvoker? invoker = null)
        {
            this.target = target;
            this.property = property;
            this.invoker = invoker ?? new RuntimeInvoker();
        }

        public override string Name => property.Name;
        public override string TypeName => property.Type.FullName;

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken)
        {
            if (property.Getter == null)
                throw new InvalidOperationException(
                    $"The debugger property '{Name}' does not have a getter.");
            return invoker.InvokeAsync(
                target,
                property.Getter,
                Array.Empty<IRuntimeValue>(),
                cancellationToken);
        }

        public override async Task<DebugValue> GetDebugValueAsync(
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            if (!policy.AllowTargetInvoke || !policy.AllowGetters)
                return DebugValue.NotEvaluated("{get;}");

            try
            {
                return DebugValue.FromValue(
                    await GetValueAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (RuntimeInvocationException exception)
            {
                return DebugValue.Error(
                    $"{exception.TargetExceptionType}: " +
                    exception.TargetMessage);
            }
        }

        public override Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken) =>
            throw ReadOnly(Name);

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
