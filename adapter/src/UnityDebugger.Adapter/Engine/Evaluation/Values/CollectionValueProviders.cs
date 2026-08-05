using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Values
{
    internal sealed class ReferenceValueProviders
    {
        private static readonly HashSet<string> ListTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "System.Collections.ArrayList",
                "System.Collections.Queue",
                "System.Collections.Stack",
                "System.Collections.Generic.Queue`1",
                "System.Collections.Generic.Stack`1",
                "System.Collections.Generic.List`1",
            };

        private static readonly HashSet<string> DictionaryTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "System.Collections.Hashtable",
                "System.Collections.Generic.Dictionary`2",
            };

        private readonly RuntimeInvoker invoker = new RuntimeInvoker();
        private readonly UnityValueProviders? unityProviders;
        private readonly Func<IRuntimeType, bool> isUserType;

        public ReferenceValueProviders(
            IUnityEvaluationContext? unityContext = null,
            Func<IRuntimeType, bool>? isUserType = null)
        {
            if (unityContext != null)
                unityProviders = new UnityValueProviders(unityContext);
            this.isUserType = isUserType ?? IsLikelyUserType;
        }

        public async Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            IRuntimeValue value,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                policy.AllowTargetInvoke &&
                value.Type.DebuggerProxyType != null)
            {
                var proxy = await TryGetProxyChildrenAsync(
                        value,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (proxy != null)
                    return proxy;
            }

            if (unityProviders != null)
            {
                var unity = await unityProviders.TryGetChildrenAsync(
                        value,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (unity != null)
                {
                    var result = new List<DebugProperty>(unity);
                    result.AddRange(StandardChildren(value, policy, raw: false));
                    return result;
                }
            }

            var collection = await TryGetCollectionChildrenAsync(
                    value,
                    policy,
                    cancellationToken)
                .ConfigureAwait(false);
            return collection ?? StandardChildren(value, policy, raw: false);
        }

        private async Task<IReadOnlyList<DebugProperty>?>
            TryGetCollectionChildrenAsync(
                IRuntimeValue value,
                EvaluationPolicy policy,
                CancellationToken cancellationToken)
        {
            if (value.Kind == RuntimeValueKind.Array)
                return ArrayChildren(value, 0, value.Length, policy);
            if (value.Type.FullName == "System.Collections.DictionaryEntry")
                return DictionaryEntryChildren(value, policy);
            if (!policy.AllowTargetInvoke)
                return null;
            if (ListTypes.Contains(value.Type.FullName))
            {
                return await ListChildrenAsync(
                        value,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            if (DictionaryTypes.Contains(value.Type.FullName))
            {
                return await DictionaryChildrenAsync(
                        value,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return null;
        }

        private IReadOnlyList<DebugProperty> ArrayChildren(
            IRuntimeValue array,
            int index,
            int count,
            EvaluationPolicy policy)
        {
            var bucketSize = 1;
            while (count / bucketSize > 150)
                bucketSize *= 10;
            if (bucketSize == 10)
                bucketSize = 100;

            var children = new List<DebugProperty>();
            if (bucketSize == 1)
            {
                for (var current = index; current < index + count; current++)
                {
                    var captured = current;
                    children.Add(CreateValueProperty(
                        $"[{captured}]",
                        array.Type.ElementType?.FullName ?? string.Empty,
                        cancellationToken => Task.FromResult(
                            array.GetElement(captured)),
                        policy));
                }

                return children;
            }

            while (count > 0)
            {
                var bucketCount = Math.Min(bucketSize, count);
                var bucketIndex = index;
                children.Add(new ComputedProperty(
                    $"[{bucketIndex}..{bucketIndex + bucketCount - 1}]",
                    array.Type.FullName,
                    cancellationToken => Task.FromResult(array),
                    cancellationToken =>
                        Task.FromResult<IReadOnlyList<DebugProperty>>(
                            ArrayChildren(
                                array,
                                bucketIndex,
                                bucketCount,
                                policy))));
                index += bucketCount;
                count -= bucketCount;
            }

            return children;
        }

        private async Task<IReadOnlyList<DebugProperty>> ListChildrenAsync(
            IRuntimeValue list,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            try
            {
                var toArray = FindMethods(list.Type)
                    .First(value =>
                        value.Name == "ToArray" &&
                        value.Parameters.Count == 0);
                var array = await invoker.InvokeAsync(
                        list,
                        toArray,
                        Array.Empty<IRuntimeValue>(),
                        cancellationToken)
                    .ConfigureAwait(false);
                var children = new List<DebugProperty>(
                    ArrayChildren(array, 0, array.Length, policy));
                children.Add(RawView(list, policy));
                return children;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return StandardChildren(list, policy, raw: false);
            }
        }

        private async Task<IReadOnlyList<DebugProperty>> DictionaryChildrenAsync(
            IRuntimeValue dictionary,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            try
            {
                var children = await EnumerateAsync(
                        dictionary,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
                var result = new List<DebugProperty>(children)
                {
                    RawView(dictionary, policy),
                };
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return StandardChildren(dictionary, policy, raw: false);
            }
        }

        private IReadOnlyList<DebugProperty> DictionaryEntryChildren(
            IRuntimeValue entry,
            EvaluationPolicy policy)
        {
            var fields = entry.Type.Fields
                .Where(value => !value.IsStatic)
                .Take(2)
                .ToArray();
            if (fields.Length < 2)
                return StandardChildren(entry, policy, raw: false);
            return new[]
            {
                CreateValueProperty(
                    "Key",
                    fields[0].Type.FullName,
                    cancellationToken => Task.FromResult(
                        entry.GetField(fields[0])),
                    policy),
                CreateValueProperty(
                    "Value",
                    fields[1].Type.FullName,
                    cancellationToken => Task.FromResult(
                        entry.GetField(fields[1])),
                    policy),
            };
        }

        private async Task<IReadOnlyList<DebugProperty>?>
            TryGetProxyChildrenAsync(
                IRuntimeValue value,
                EvaluationPolicy policy,
                CancellationToken cancellationToken)
        {
            try
            {
                var proxyType = value.Type.DebuggerProxyType!;
                var constructor = proxyType.Methods.First(method =>
                    method.Name == ".ctor" &&
                    method.Parameters.Count == 1);
                var proxy = await value.CreateInstanceAsync(
                        proxyType,
                        constructor,
                        new[] { value },
                        RuntimeInvoker.EvaluationOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                var children = PublicInstanceChildren(proxy);
                children.Add(RawView(value, policy));
                return children;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private IReadOnlyList<DebugProperty> StandardChildren(
            IRuntimeValue value,
            EvaluationPolicy policy,
            bool raw)
        {
            var children = new List<DebugProperty>();
            if (
                value.Kind == RuntimeValueKind.Object &&
                value.Type.BaseType != null &&
                value.Type.BaseType.FullName != "System.Object")
            {
                var baseValue = new RuntimeValueView(value, value.Type.BaseType);
                children.Add(CreateValueProperty(
                    "base",
                    baseValue.Type.FullName,
                    cancellationToken => Task.FromResult<IRuntimeValue>(
                        baseValue),
                    policy));
            }

            var instance = InstanceMembers(value).ToArray();
            var userType = isUserType(value.Type);
            children.AddRange(instance
                .Where(member => userType || member.IsPublic)
                .OrderBy(member => member.Property.Name, StringComparer.Ordinal)
                .Select(member => member.Property));

            var staticMembers = StaticMembers(value).ToArray();
            var visibleStatic = staticMembers
                .Where(member => userType || member.IsPublic)
                .OrderBy(member => member.Property.Name, StringComparer.Ordinal)
                .Select(member => member.Property)
                .ToArray();
            if (!userType)
            {
                var nonPublicStatic = staticMembers
                    .Where(member => !member.IsPublic)
                    .OrderBy(
                        member => member.Property.Name,
                        StringComparer.Ordinal)
                    .Select(member => member.Property)
                    .ToArray();
                if (nonPublicStatic.Length > 0)
                {
                    var groupedStatic = new List<DebugProperty>(visibleStatic)
                    {
                        Group(
                            "Non-Public members",
                            value,
                            nonPublicStatic),
                    };
                    visibleStatic = groupedStatic.ToArray();
                }
            }
            if (visibleStatic.Length > 0)
            {
                children.Add(Group(
                    "Static members",
                    value,
                    visibleStatic));
            }

            var nonPublic = instance
                .Where(member => !member.IsPublic)
                .OrderBy(member => member.Property.Name, StringComparer.Ordinal)
                .Select(member => member.Property)
                .ToArray();
            if (!userType && nonPublic.Length > 0)
                children.Add(Group("Non-Public members", value, nonPublic));

            if (!raw && Implements(value.Type, "System.Collections.IEnumerable"))
            {
                children.Add(new ComputedProperty(
                    "Results View",
                    value.Type.FullName,
                    cancellationToken => Task.FromResult(value),
                    cancellationToken => EnumerateAsync(
                        value,
                        policy,
                        cancellationToken)));
            }

            return children;
        }

        private List<DebugProperty> PublicInstanceChildren(IRuntimeValue value) =>
            InstanceMembers(value)
                .Where(member => member.IsPublic)
                .OrderBy(member => member.Property.Name, StringComparer.Ordinal)
                .Select(member => member.Property)
                .ToList();

        private IEnumerable<(DebugProperty Property, bool IsPublic)>
            InstanceMembers(IRuntimeValue value)
        {
            foreach (var field in value.Type.Fields)
            {
                if (field.IsStatic || field.Name.StartsWith("<", StringComparison.Ordinal))
                    continue;
                yield return (new FieldProperty(value, field), field.IsPublic);
            }
            foreach (var property in value.Type.Properties)
            {
                if (
                    property.Getter == null ||
                    property.Getter.IsStatic ||
                    property.Getter.Parameters.Count != 0)
                {
                    continue;
                }
                yield return (
                    new AccessorProperty(value, property),
                    property.Getter.IsPublic);
            }
        }

        private IEnumerable<(DebugProperty Property, bool IsPublic)>
            StaticMembers(IRuntimeValue value)
        {
            foreach (var field in value.Type.Fields)
            {
                if (field.IsStatic)
                {
                    yield return (
                        new FieldProperty(value, field),
                        field.IsPublic);
                }
            }
            foreach (var property in value.Type.Properties)
            {
                if (property.Getter?.IsStatic == true)
                {
                    yield return (
                        new AccessorProperty(value, property),
                        property.Getter.IsPublic);
                }
            }
        }

        private async Task<IReadOnlyList<DebugProperty>> EnumerateAsync(
            IRuntimeValue value,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            var getEnumerator = FindMethods(value.Type).FirstOrDefault(method =>
                method.Name.EndsWith("GetEnumerator", StringComparison.Ordinal) &&
                method.Parameters.Count == 0);
            if (getEnumerator == null)
                return Array.Empty<DebugProperty>();
            var enumerator = await invoker.InvokeAsync(
                    value,
                    getEnumerator,
                    Array.Empty<IRuntimeValue>(),
                    cancellationToken)
                .ConfigureAwait(false);
            var moveNext = FindMethods(enumerator.Type).FirstOrDefault(method =>
                method.Name == "MoveNext" && method.Parameters.Count == 0);
            var current = FindProperties(enumerator.Type).FirstOrDefault(property =>
                property.Name.EndsWith("Current", StringComparison.Ordinal) ||
                property.Name.EndsWith("Entry", StringComparison.Ordinal));
            if (moveNext == null || current?.Getter == null)
                return Array.Empty<DebugProperty>();

            var children = new List<DebugProperty>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hasNext = await invoker.InvokeAsync(
                        enumerator,
                        moveNext,
                        Array.Empty<IRuntimeValue>(),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!(hasNext.Primitive is bool boolean) || !boolean)
                    break;
                var item = await invoker.InvokeAsync(
                        enumerator,
                        current.Getter,
                        Array.Empty<IRuntimeValue>(),
                        cancellationToken)
                    .ConfigureAwait(false);
                children.Add(CreateValueProperty(
                    $"[{children.Count}]",
                    item.Type.FullName,
                    token => Task.FromResult(item),
                    policy));
            }

            return children;
        }

        private ProviderValueProperty CreateValueProperty(
            string name,
            string typeName,
            Func<CancellationToken, Task<IRuntimeValue>> getValue,
            EvaluationPolicy policy) =>
            new ProviderValueProperty(
                name,
                typeName,
                getValue,
                (value, cancellationToken) => GetChildrenAsync(
                    value,
                    policy,
                    cancellationToken));

        private DebugProperty RawView(
            IRuntimeValue value,
            EvaluationPolicy policy) =>
            new ComputedProperty(
                "Raw View",
                value.Type.FullName,
                cancellationToken => Task.FromResult(value),
                cancellationToken =>
                    Task.FromResult(StandardChildren(value, policy, raw: true)));

        private static DebugProperty Group(
            string name,
            IRuntimeValue value,
            IReadOnlyList<DebugProperty> children) =>
            new ComputedProperty(
                name,
                value.Type.FullName,
                cancellationToken => Task.FromResult(value),
                cancellationToken =>
                    Task.FromResult(children));

        private static IEnumerable<RuntimeMethod> FindMethods(IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var method in current.Methods)
                    yield return method;
            }
        }

        private static IEnumerable<RuntimeProperty> FindProperties(
            IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.Properties)
                    yield return property;
            }
        }

        private static bool Implements(IRuntimeType type, string fullName)
        {
            if (type.FullName == fullName)
                return true;
            foreach (var implemented in type.Interfaces)
            {
                if (Implements(implemented, fullName))
                    return true;
            }
            return type.BaseType != null && Implements(type.BaseType, fullName);
        }

        private static bool IsLikelyUserType(IRuntimeType type)
        {
            var name = type.FullName;
            return !name.StartsWith("System.", StringComparison.Ordinal) &&
                !name.StartsWith("Microsoft.", StringComparison.Ordinal) &&
                !name.StartsWith("UnityEngine.", StringComparison.Ordinal) &&
                !name.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        private sealed class ProviderValueProperty : DebugProperty
        {
            private readonly Func<
                CancellationToken,
                Task<IRuntimeValue>> getValue;
            private readonly Func<
                IRuntimeValue,
                CancellationToken,
                Task<IReadOnlyList<DebugProperty>>> getChildren;

            public ProviderValueProperty(
                string name,
                string typeName,
                Func<CancellationToken, Task<IRuntimeValue>> getValue,
                Func<
                    IRuntimeValue,
                    CancellationToken,
                    Task<IReadOnlyList<DebugProperty>>> getChildren)
            {
                Name = name;
                TypeName = typeName;
                this.getValue = getValue;
                this.getChildren = getChildren;
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

            public override async Task<IReadOnlyList<DebugProperty>>
                GetChildrenAsync(CancellationToken cancellationToken)
            {
                var value = await getValue(cancellationToken)
                    .ConfigureAwait(false);
                return await getChildren(value, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private sealed class RuntimeValueView : IRuntimeValue
        {
            private readonly IRuntimeValue value;

            public RuntimeValueView(IRuntimeValue value, IRuntimeType type)
            {
                this.value = value;
                Type = type;
            }

            public RuntimeValueKind Kind => value.Kind;
            public IRuntimeType Type { get; }
            public object? Primitive => value.Primitive;
            public string? String => value.String;
            public long? Address => value.Address;
            public int Length => value.Length;

            public IRuntimeValue GetField(RuntimeField field) =>
                value.GetField(field);

            public void SetField(RuntimeField field, IRuntimeValue newValue) =>
                value.SetField(field, newValue);

            public IRuntimeValue GetElement(int index) =>
                value.GetElement(index);

            public Task<IRuntimeValue> InvokeAsync(
                RuntimeMethod method,
                IReadOnlyList<IRuntimeValue> arguments,
                InvokeOptions options,
                CancellationToken cancellationToken) =>
                value.InvokeAsync(
                    method,
                    arguments,
                    options,
                    cancellationToken);

            public Task<IRuntimeValue> CreateInstanceAsync(
                IRuntimeType type,
                RuntimeMethod constructor,
                IReadOnlyList<IRuntimeValue> arguments,
                InvokeOptions options,
                CancellationToken cancellationToken) =>
                value.CreateInstanceAsync(
                    type,
                    constructor,
                    arguments,
                    options,
                    cancellationToken);
        }
    }
}
