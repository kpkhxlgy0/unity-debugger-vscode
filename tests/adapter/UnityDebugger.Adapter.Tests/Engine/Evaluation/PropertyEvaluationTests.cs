using System;
using System.Collections.Generic;
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
    public sealed class PropertyEvaluationTests
    {
        [Fact]
        public async Task FrameValuesAreReadOnlyWhenLocalsRootIsExpanded()
        {
            var environment = new PropertyFrameEnvironment();

            var property = new FrameProperty(environment);

            Assert.Equal(0, environment.FrameReadCount);
            await property.GetChildrenAsync(CancellationToken.None);
            Assert.Equal(1, environment.FrameReadCount);
        }

        [Fact]
        public async Task FrameChildrenUseReferenceCategoryOrder()
        {
            var environment = new PropertyFrameEnvironment
            {
                FrameValues = new FrameValues(
                    thisValue: Variable("this"),
                    locals: new[] { Variable("local") },
                    arguments: new[] { Variable("argument") },
                    constants: Array.Empty<FrameVariable>(),
                    closureCaptures: Array.Empty<FrameVariable>(),
                    hoistedValues: Array.Empty<FrameVariable>(),
                    currentException: null),
            };

            var children = await new FrameProperty(environment)
                .GetChildrenAsync(CancellationToken.None);

            Assert.Equal(
                new[] { "this", "local", "argument" },
                children.Select(value => value.Name));
        }

        [Fact]
        public async Task ObjectFieldsAreDiscoveredOnlyWhenParentIsExpanded()
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var field = new RuntimeField(
                "Health",
                integerType,
                isStatic: false,
                isPublic: true,
                isLiteral: false,
                source: new object());
            var playerType = new FakeRuntimeType("Player")
            {
                Fields = new[] { field },
            };
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);
            var property = new ValueProperty("player", player);

            Assert.Equal(0, playerType.FieldsAccessCount);
            var children = await property.GetChildrenAsync(
                CancellationToken.None);

            var child = Assert.Single(children);
            Assert.IsType<FieldProperty>(child);
            Assert.Equal("Health", child.Name);
            Assert.Equal(1, playerType.FieldsAccessCount);
        }

        [Fact]
        public async Task GetterUsesExactInvocationOptions()
        {
            var fixture = GetterFixture();
            InvokeOptions? received = null;
            fixture.Target.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                received = options;
                return Task.FromResult<IRuntimeValue>(fixture.Result);
            };

            var result = await fixture.Property.GetDebugValueAsync(
                EvaluationPolicy.Explicit,
                CancellationToken.None);

            Assert.Equal(DebugValueKind.Value, result.Kind);
            Assert.Same(fixture.Result, result.Value);
            Assert.Equal(
                InvokeOptions.DisableBreakpoints |
                InvokeOptions.SingleThreaded,
                received);
        }

        [Fact]
        public async Task SafePolicyDoesNotInvokeGetter()
        {
            var fixture = GetterFixture();
            fixture.Target.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
                throw new Xunit.Sdk.XunitException(
                    "Safe property evaluation invoked target code.");

            var result = await fixture.Property.GetDebugValueAsync(
                EvaluationPolicy.Safe,
                CancellationToken.None);

            Assert.Equal(DebugValueKind.NotEvaluated, result.Kind);
            Assert.Equal("{get;}", result.Display);
        }

        [Fact]
        public async Task GetterExceptionIsAPropertyValueAndFollowingPropertyRuns()
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var failingMethod = Method("get_Failing", integerType);
            var normalMethod = Method("get_Normal", integerType);
            var targetType = new FakeRuntimeType("Player")
            {
                Properties = new[]
                {
                    new RuntimeProperty(
                        "Failing",
                        integerType,
                        failingMethod,
                        null,
                        new object()),
                    new RuntimeProperty(
                        "Normal",
                        integerType,
                        normalMethod,
                        null,
                        new object()),
                },
            };
            var normalValue = Primitive(integerType, 10);
            var target = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(targetType);
            target.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                if (method.Name == "get_Failing")
                {
                    throw new RuntimeInvocationException(
                        "System.InvalidOperationException",
                        "getter failed");
                }

                return Task.FromResult<IRuntimeValue>(normalValue);
            };
            var properties = await new ValueProperty("player", target)
                .GetChildrenAsync(CancellationToken.None);

            var first = await properties[0].GetDebugValueAsync(
                EvaluationPolicy.Explicit,
                CancellationToken.None);
            var second = await properties[1].GetDebugValueAsync(
                EvaluationPolicy.Explicit,
                CancellationToken.None);

            Assert.Equal(DebugValueKind.Error, first.Kind);
            Assert.Contains("InvalidOperationException", first.Display);
            Assert.Contains("getter failed", first.Display);
            Assert.Equal(DebugValueKind.Value, second.Kind);
            Assert.Same(normalValue, second.Value);
        }

        private static (
            FakeRuntimeValue Target,
            AccessorProperty Property,
            FakeRuntimeValue Result) GetterFixture()
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var getter = Method("get_Health", integerType);
            var runtimeProperty = new RuntimeProperty(
                "Health",
                integerType,
                getter,
                null,
                new object());
            var target = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(new FakeRuntimeType("Player"));
            var result = Primitive(integerType, 100);
            return (
                target,
                new AccessorProperty(target, runtimeProperty),
                result);
        }

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
                isSpecialName: true,
                source: new object());

        private static FakeRuntimeValue Primitive(
            IRuntimeType type,
            object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(type)
                .WithPrimitive(value);

        private static FrameVariable Variable(string name) =>
            new FrameVariable(
                name,
                new FakeRuntimeValue()
                    .WithKind(RuntimeValueKind.Primitive)
                    .WithType(new FakeRuntimeType("Int32", "System.Int32"))
                    .WithPrimitive(1));

        private sealed class PropertyFrameEnvironment :
            IFrameEvaluationEnvironment
        {
            public int FrameReadCount { get; private set; }
            public FrameValues FrameValues { get; set; } = FrameValues.Empty;

            public FrameValues GetFrameValues()
            {
                FrameReadCount++;
                return FrameValues;
            }

            public bool TryGetValue(string name, out IRuntimeValue value)
            {
                value = null!;
                return false;
            }

            public bool TryGetType(
                string fullOrSimpleName,
                out IRuntimeType type)
            {
                type = null!;
                return false;
            }
        }
    }
}
