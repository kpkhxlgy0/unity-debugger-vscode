using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
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
