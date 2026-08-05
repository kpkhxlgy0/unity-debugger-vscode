using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class CollectionValueTests
    {
        [Fact]
        public async Task SmallArrayExposesLiteralIndexNames()
        {
            var array = ArrayValue(3);

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    array,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "[0]", "[1]", "[2]" },
                children.Select(value => value.Name));
        }

        [Fact]
        public async Task LargeArrayUsesReferenceHundredItemBuckets()
        {
            var array = ArrayValue(151);

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    array,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "[0..99]", "[100..150]" },
                children.Select(value => value.Name));
        }

        [Fact]
        public async Task ListExposesArrayElementsThenRawView()
        {
            var array = ArrayValue(2);
            var toArray = Method("ToArray", array.Type);
            var listType = new FakeRuntimeType(
                "List`1",
                "System.Collections.Generic.List`1")
            {
                Methods = new[] { toArray },
            };
            var list = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(listType);
            list.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                Assert.Same(toArray, method);
                Assert.Equal(RuntimeInvoker.EvaluationOptions, options);
                return Task.FromResult<IRuntimeValue>(array);
            };

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    list,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "[0]", "[1]", "Raw View" },
                children.Select(value => value.Name));
        }

        [Fact]
        public async Task DictionaryEntryExposesKeyThenValue()
        {
            var objectType = new FakeRuntimeType("Object", "System.Object");
            var keyField = Field("key", objectType);
            var valueField = Field("value", objectType);
            var entryType = new FakeRuntimeType(
                "DictionaryEntry",
                "System.Collections.DictionaryEntry")
            {
                Fields = new[] { keyField, valueField },
            };
            var key = Primitive("key");
            var value = Primitive(5);
            var entry = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Struct)
                .WithType(entryType);
            entry.GetFieldHandler = field =>
                ReferenceEquals(field, keyField) ? key : value;

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    entry,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "Key", "Value" },
                children.Select(child => child.Name));
            Assert.Same(
                key,
                await children[0].GetValueAsync(CancellationToken.None));
            Assert.Same(
                value,
                await children[1].GetValueAsync(CancellationToken.None));
        }

        [Fact]
        public async Task EnumerableObjectIncludesResultsView()
        {
            var enumerableType = new FakeRuntimeType(
                "IEnumerable",
                "System.Collections.IEnumerable");
            var sequenceType = new FakeRuntimeType("Sequence")
            {
                Interfaces = new[] { enumerableType },
            };
            var sequence = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(sequenceType);

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    sequence,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Contains(children, value => value.Name == "Results View");
        }

        [Fact]
        public async Task StandardObjectUsesReferenceMemberGroupsAndOrder()
        {
            var objectType = new FakeRuntimeType("Object", "System.Object");
            var baseType = new FakeRuntimeType("BasePlayer")
            {
                BaseType = objectType,
            };
            var valueType = new FakeRuntimeType("Player")
            {
                BaseType = baseType,
                Fields = new[]
                {
                    Field("Zeta", objectType, isPublic: true),
                    Field("Alpha", objectType, isPublic: true),
                    Field("hidden", objectType, isPublic: false),
                    Field(
                        "Global",
                        objectType,
                        isPublic: true,
                        isStatic: true),
                },
            };
            var value = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(valueType);

            var children = await new ReferenceValueProviders(
                    isUserType: type => false)
                .GetChildrenAsync(
                    value,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    "base",
                    "Alpha",
                    "Zeta",
                    "Static members",
                    "Non-Public members",
                },
                children.Select(child => child.Name));
        }

        [Fact]
        public async Task UserTypeFlattensNonPublicInstanceMembers()
        {
            var objectType = new FakeRuntimeType("Object", "System.Object");
            var playerType = new FakeRuntimeType("Game.Player")
            {
                Fields = new[]
                {
                    Field("Public", objectType, isPublic: true),
                    Field("privateValue", objectType, isPublic: false),
                },
            };
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    player,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "Public", "privateValue" },
                children.Select(child => child.Name));
        }

        [Fact]
        public async Task DebuggerProxyUsesPublicProxyMembersThenRawView()
        {
            var objectType = new FakeRuntimeType("Object", "System.Object");
            var constructor = new RuntimeMethod(
                ".ctor",
                objectType,
                new[] { new RuntimeParameter("value", objectType) },
                isStatic: false,
                isPublic: true,
                isVirtual: false,
                isSpecialName: true,
                source: new object());
            var proxyType = new FakeRuntimeType("PlayerProxy")
            {
                Methods = new[] { constructor },
                Fields = new[] { Field("Display", objectType) },
            };
            var targetType = new FakeRuntimeType("Player")
            {
                DebuggerProxyType = proxyType,
            };
            var target = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(targetType);
            var proxy = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(proxyType);
            target.CreateInstanceHandler = (
                type,
                invokedConstructor,
                arguments,
                options,
                cancellationToken) =>
            {
                Assert.Same(proxyType, type);
                Assert.Same(constructor, invokedConstructor);
                Assert.Same(target, Assert.Single(arguments));
                Assert.Equal(RuntimeInvoker.EvaluationOptions, options);
                return Task.FromResult<IRuntimeValue>(proxy);
            };

            var children = await new ReferenceValueProviders()
                .GetChildrenAsync(
                    target,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "Display", "Raw View" },
                children.Select(child => child.Name));
        }

        private static FakeRuntimeValue ArrayValue(int length)
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var arrayType = new FakeRuntimeType("Int32[]", "System.Int32[]")
            {
                IsArray = true,
                ElementType = integerType,
            };
            var array = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Array)
                .WithType(arrayType);
            array.ConfiguredLength = length;
            array.GetElementHandler = index => Primitive(index);
            return array;
        }

        private static RuntimeField Field(
            string name,
            IRuntimeType type,
            bool isPublic = true,
            bool isStatic = false) =>
            new RuntimeField(
                name,
                type,
                isStatic,
                isPublic,
                isLiteral: false,
                source: new object());

        private static RuntimeMethod Method(
            string name,
            IRuntimeType returnType) =>
            new RuntimeMethod(
                name,
                returnType,
                Array.Empty<RuntimeParameter>(),
                isStatic: false,
                isPublic: true,
                isVirtual: false,
                isSpecialName: false,
                source: new object());

        private static FakeRuntimeValue Primitive(object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(new FakeRuntimeType(
                    value.GetType().Name,
                    value.GetType().FullName))
                .WithPrimitive(value);
    }
}
