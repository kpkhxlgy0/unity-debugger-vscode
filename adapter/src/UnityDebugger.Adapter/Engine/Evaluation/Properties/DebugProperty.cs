using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal abstract class DebugProperty
    {
        public abstract string Name { get; }
        public abstract string TypeName { get; }
        public abstract Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken);
        public abstract Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken);

        public virtual Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DebugProperty>>(
                Array.Empty<DebugProperty>());

        protected static InvalidOperationException ReadOnly(string name) =>
            new InvalidOperationException(
                $"The debugger property '{name}' cannot be changed.");
    }
}
