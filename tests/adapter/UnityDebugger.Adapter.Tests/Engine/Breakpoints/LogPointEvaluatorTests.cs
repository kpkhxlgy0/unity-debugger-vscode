using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.State;
using UnityDebugger.Adapter.Tests.Engine.Evaluation;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Breakpoints
{
    public sealed class LogPointEvaluatorTests
    {
        [Fact]
        public void EnumExpressionIsInterpolatedAndTheTargetResumes()
        {
            var enumType = FakeRuntimeType.Enum(
                "ButtonState",
                ("Normal", 0),
                ("Selected", 1));
            var frame = new Frame()
                .WithValue("currentState", enumType.EnumValue("Normal"))
                .WithType(enumType);
            var evaluator = new LogPointEvaluator(
                new EvaluationService(new SuspendedState()));

            var result = evaluator.Evaluate(
                "state={currentState}, normal={ButtonState.Normal}",
                frame,
                1000);

            Assert.True(result.Success);
            Assert.Equal("state=Normal, normal=Normal", result.Output);
        }

        [Fact]
        public void EscapedBracesRemainLiteral()
        {
            var evaluator = new LogPointEvaluator(
                new EvaluationService(new SuspendedState()));

            var result = evaluator.Evaluate(
                "{{state}}",
                new Frame(),
                1000);

            Assert.True(result.Success);
            Assert.Equal("{state}", result.Output);
        }

        private sealed class Frame : IFrameEvaluationEnvironment
        {
            private readonly Dictionary<string, IRuntimeValue> values =
                new Dictionary<string, IRuntimeValue>();
            private readonly Dictionary<string, IRuntimeType> types =
                new Dictionary<string, IRuntimeType>();

            public Frame WithValue(string name, IRuntimeValue value)
            {
                values.Add(name, value);
                return this;
            }

            public Frame WithType(IRuntimeType type)
            {
                types[type.Name] = type;
                types[type.FullName] = type;
                return this;
            }

            public FrameValues GetFrameValues() => FrameValues.Empty;
            public bool TryGetValue(string name, out IRuntimeValue value) =>
                values.TryGetValue(name, out value!);
            public bool TryGetType(
                string fullOrSimpleName,
                out IRuntimeType type) =>
                types.TryGetValue(fullOrSimpleName, out type!);
        }
    }
}
