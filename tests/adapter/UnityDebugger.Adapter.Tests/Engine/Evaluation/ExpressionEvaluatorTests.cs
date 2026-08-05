using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class ExpressionEvaluatorTests
    {
        [Fact]
        public void EnumMemberEqualityUsesTheEnumType()
        {
            var stateType = FakeRuntimeType.Enum(
                "ButtonState",
                ("Normal", 0),
                ("Selected", 1));
            var environment = new FakeFrameEnvironment()
                .WithType(stateType)
                .WithValue("currentState", stateType.EnumValue("Normal"));
            var expression = Parse(
                "currentState == ButtonState.Normal");

            var result = new ExpressionEvaluator(environment)
                .Evaluate(expression, CancellationToken.None);

            Assert.Equal(RuntimeValueKind.Primitive, result.Kind);
            Assert.Equal(true, result.Primitive);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("42", 42)]
        [InlineData("!false", true)]
        [InlineData("true && false", false)]
        [InlineData("true || false", true)]
        [InlineData("42 == 42", true)]
        [InlineData("42 != 42", false)]
        [InlineData("(42 == 42)", true)]
        public void PrimitiveExpressionsReturnLiteralValues(
            string text,
            object expected)
        {
            var result = new ExpressionEvaluator(new FakeFrameEnvironment())
                .Evaluate(Parse(text), CancellationToken.None);

            Assert.Equal(expected, result.Primitive);
        }

        [Fact]
        public void IdentifierReturnsTheFrameValue()
        {
            var expected = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(new FakeRuntimeType("Int32") { IsPrimitive = true })
                .WithPrimitive(3);
            var environment = new FakeFrameEnvironment()
                .WithValue("count", expected);

            var result = new ExpressionEvaluator(environment)
                .Evaluate(Parse("count"), CancellationToken.None);

            Assert.Same(expected, result);
        }

        [Fact]
        public void EnumInequalityRequiresCompatibleEnumTypes()
        {
            var firstType = FakeRuntimeType.Enum("FirstState", ("Ready", 1));
            var secondType = FakeRuntimeType.Enum("SecondState", ("Ready", 1));
            var environment = new FakeFrameEnvironment()
                .WithValue("first", firstType.EnumValue("Ready"))
                .WithValue("second", secondType.EnumValue("Ready"));

            Assert.Throws<ExpressionEvaluationException>(() =>
                new ExpressionEvaluator(environment).Evaluate(
                    Parse("first != second"),
                    CancellationToken.None));
        }

        [Fact]
        public void UnknownIdentifierReturnsTypedEvaluationFailure()
        {
            Assert.Throws<ExpressionEvaluationException>(() =>
                new ExpressionEvaluator(new FakeFrameEnvironment()).Evaluate(
                    Parse("missing"),
                    CancellationToken.None));
        }

        private static ExpressionSyntax Parse(string text)
        {
            var result = new CSharpDebugParser().ParseExpression(text);
            Assert.True(result.CanEvaluate, result.Error);
            return result.Expression!;
        }

        private sealed class FakeFrameEnvironment : IFrameEvaluationEnvironment
        {
            private readonly Dictionary<string, IRuntimeValue> values =
                new Dictionary<string, IRuntimeValue>();
            private readonly Dictionary<string, IRuntimeType> types =
                new Dictionary<string, IRuntimeType>();

            public FakeFrameEnvironment WithValue(
                string name,
                IRuntimeValue value)
            {
                values.Add(name, value);
                return this;
            }

            public FakeFrameEnvironment WithType(IRuntimeType type)
            {
                types.Add(type.Name, type);
                types[type.FullName] = type;
                return this;
            }

            public bool TryGetValue(string name, out IRuntimeValue value) =>
                values.TryGetValue(name, out value!);

            public bool TryGetType(
                string fullOrSimpleName,
                out IRuntimeType type) =>
                types.TryGetValue(fullOrSimpleName, out type!);
        }
    }
}
