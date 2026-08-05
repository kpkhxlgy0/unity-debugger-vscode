using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Source;
using UnityDebugger.Adapter.Engine.State;
using UnityDebugger.Adapter.Tests.Engine.Evaluation;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Breakpoints
{
    public sealed class BreakpointConditionTests
    {
        [Theory]
        [InlineData(true, 0)]
        [InlineData(false, 1)]
        public void BooleanConditionControlsTheHit(
            bool condition,
            int expectedValue)
        {
            var expected = (BreakpointHitAction)expectedValue;
            var fixture = new Fixture(
                "flag",
                null,
                new MutableFrame().WithValue("flag", Primitive(condition)));

            var result = fixture.Hit();

            Assert.Equal(expected, result.Action);
        }

        [Fact]
        public void ChangedConditionStopsOnlyAfterTheResultChanges()
        {
            var frame = new MutableFrame().WithValue("flag", Primitive(true));
            var fixture = new Fixture("flag", "changed", frame);

            Assert.Equal(BreakpointHitAction.Resume, fixture.Hit().Action);
            Assert.Equal(BreakpointHitAction.Resume, fixture.Hit().Action);
            frame.WithValue("flag", Primitive(false));

            Assert.Equal(BreakpointHitAction.Stop, fixture.Hit().Action);
        }

        [Fact]
        public void ConditionErrorStopsAndReportsOnlyThatHit()
        {
            var fixture = new Fixture(
                "missing",
                null,
                new MutableFrame());

            var result = fixture.Hit();

            Assert.Equal(BreakpointHitAction.Stop, result.Action);
            Assert.Contains("condition", result.Output!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConditionTimeoutStopsAndReportsOnlyThatHit()
        {
            var intType = PrimitiveType("Int32", "System.Int32");
            var getter = new RuntimeMethod(
                "get_Value",
                intType,
                Array.Empty<RuntimeParameter>(),
                false,
                true,
                false,
                true,
                new object());
            var slowType = new FakeRuntimeType("Slow")
            {
                Properties = new[]
                {
                    new RuntimeProperty(
                        "Value",
                        intType,
                        getter,
                        null,
                        new object()),
                },
            };
            var slow = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(slowType);
            slow.InvokeHandler = async (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return Primitive(1);
            };
            var fixture = new Fixture(
                "slow.Value > 0",
                null,
                new MutableFrame().WithValue("slow", slow),
                conditionTimeoutMilliseconds: 10);

            var result = fixture.Hit();

            Assert.Equal(BreakpointHitAction.Stop, result.Action);
            Assert.Contains("timed out", result.Output!,
                StringComparison.OrdinalIgnoreCase);
        }

        private static FakeRuntimeType PrimitiveType(
            string name,
            string fullName) =>
            new FakeRuntimeType(name, fullName)
            {
                IsPrimitive = true,
                IsValueType = true,
            };

        private static FakeRuntimeValue Primitive(object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(PrimitiveType(
                    value.GetType().Name,
                    "System." + value.GetType().Name))
                .WithPrimitive(value);

        private sealed class Fixture
        {
            private readonly EngineBreakpointManager manager;
            private readonly FakeRequest request;
            private readonly MutableFrame frame;

            public Fixture(
                string condition,
                string? hitCondition,
                MutableFrame frame,
                int conditionTimeoutMilliseconds = 1000)
            {
                this.frame = frame;
                var runtime = new FakeRuntime();
                var sources = new EngineSourceMapManager();
                var evaluation = new EvaluationService(new SuspendedState());
                manager = new EngineBreakpointManager(
                    sources,
                    runtime,
                    evaluation,
                    conditionTimeoutMilliseconds);
                manager.RequestSourceBreakpoint(
                    new LogicalBreakpoint(
                        7,
                        @"D:\project\Assets\Button.cs",
                        95,
                        1,
                        condition,
                        hitCondition,
                        null));
                manager.ProcessTypeLoaded(
                    new FakeRuntimeType("Button", "Game.Button")
                        .WithSource(
                            "domain",
                            new RuntimeModuleDescriptor(
                                "assembly",
                                "Assembly-CSharp",
                                "Assembly-CSharp.dll"),
                            @"D:\project\Assets\Button.cs",
                            95));
                request = runtime.Request;
            }

            public BreakpointHitResult Hit() =>
                manager.ProcessBreakpointHit(
                    new RuntimeBreakpointHit(
                        request.Identity,
                        true,
                        frame));
        }

        private sealed class FakeRuntime : IEngineBreakpointRuntime
        {
            public FakeRequest Request { get; } = new FakeRequest();

            public IEngineBreakpointRequest CreateBreakpoint(
                EngineSourceLocation location) => Request;
        }

        private sealed class FakeRequest : IEngineBreakpointRequest
        {
            public object Identity { get; } = new object();
            public void Enable()
            {
            }
            public void Disable()
            {
            }
        }

        private sealed class MutableFrame : IFrameEvaluationEnvironment
        {
            private readonly Dictionary<string, IRuntimeValue> values =
                new Dictionary<string, IRuntimeValue>();

            public MutableFrame WithValue(string name, IRuntimeValue value)
            {
                values[name] = value;
                return this;
            }

            public FrameValues GetFrameValues() => FrameValues.Empty;
            public bool TryGetValue(string name, out IRuntimeValue value) =>
                values.TryGetValue(name, out value!);
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
