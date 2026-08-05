using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class ComputedProperty : DebugProperty
    {
        private readonly Func<CancellationToken, Task<IRuntimeValue>> getValue;
        private readonly Func<
            CancellationToken,
            Task<IReadOnlyList<DebugProperty>>> getChildren;

        public ComputedProperty(
            string name,
            string typeName,
            Func<CancellationToken, Task<IRuntimeValue>> getValue,
            Func<
                CancellationToken,
                Task<IReadOnlyList<DebugProperty>>>? getChildren = null)
        {
            Name = name;
            TypeName = typeName;
            this.getValue = getValue;
            this.getChildren = getChildren ?? EmptyChildren;
        }

        public override string Name { get; }
        public override string TypeName { get; }

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken) =>
            getValue(cancellationToken);

        public override Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken) =>
            throw ReadOnly(Name);

        public override Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken) =>
            getChildren(cancellationToken);

        private static Task<IReadOnlyList<DebugProperty>> EmptyChildren(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DebugProperty>>(
                Array.Empty<DebugProperty>());
        }
    }
}
