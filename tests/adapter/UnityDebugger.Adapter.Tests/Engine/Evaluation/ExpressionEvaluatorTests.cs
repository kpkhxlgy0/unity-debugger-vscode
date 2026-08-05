using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Debugger.Soft;
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

        [Theory]
        [InlineData("count + 2", 5)]
        [InlineData("count - 2", 1)]
        [InlineData("count * 2", 6)]
        [InlineData("count >= 3", true)]
        [InlineData("count < 3", false)]
        [InlineData("enabled ? count : 0", 3)]
        public void CommonExpressionFamiliesReturnExpectedValues(
            string text,
            object expected)
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32")
            {
                IsPrimitive = true,
                IsValueType = true,
            };
            var booleanType = new FakeRuntimeType("Boolean", "System.Boolean")
            {
                IsPrimitive = true,
                IsValueType = true,
            };
            var environment = new FakeFrameEnvironment()
                .WithValue(
                    "count",
                    new FakeRuntimeValue()
                        .WithKind(RuntimeValueKind.Primitive)
                        .WithType(integerType)
                        .WithPrimitive(3))
                .WithValue(
                    "enabled",
                    new FakeRuntimeValue()
                        .WithKind(RuntimeValueKind.Primitive)
                        .WithType(booleanType)
                        .WithPrimitive(true));

            var result = new ExpressionEvaluator(environment)
                .Evaluate(Parse(text), CancellationToken.None);

            Assert.Equal(expected, result.Primitive);
        }

        [Fact]
        public void EnumBitwiseOperationRetainsEnumType()
        {
            var flagsType = FakeRuntimeType.Enum(
                "ButtonFlags",
                ("None", 0),
                ("Pressed", 2));
            var environment = new FakeFrameEnvironment()
                .WithType(flagsType)
                .WithValue(
                    "flags",
                    new FakeRuntimeValue()
                        .WithKind(RuntimeValueKind.Enum)
                        .WithType(flagsType)
                        .WithPrimitive(3));

            var result = new ExpressionEvaluator(environment).Evaluate(
                Parse("flags & ButtonFlags.Pressed"),
                CancellationToken.None);

            Assert.Equal(RuntimeValueKind.Enum, result.Kind);
            Assert.Same(flagsType, result.Type);
            Assert.Equal(2, result.Primitive);
        }

        [Fact]
        public void ElementAccessReadsTheRequestedIndex()
        {
            var element = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(new FakeRuntimeType("Int32", "System.Int32"))
                .WithPrimitive(20);
            var items = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Array)
                .WithType(new FakeRuntimeType("Int32[]") { IsArray = true });
            items.GetElementHandler = index =>
            {
                Assert.Equal(1, index);
                return element;
            };
            var environment = new FakeFrameEnvironment()
                .WithValue("items", items);

            var result = new ExpressionEvaluator(environment).Evaluate(
                Parse("items[1]"),
                CancellationToken.None);

            Assert.Same(element, result);
        }

        [Fact]
        public void MemberAccessReadsTheMatchingField()
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var healthField = new RuntimeField(
                "Health",
                integerType,
                isStatic: false,
                isPublic: true,
                isLiteral: false,
                source: new object());
            var playerType = new FakeRuntimeType("Player")
            {
                Fields = new[] { healthField },
            };
            var expected = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(integerType)
                .WithPrimitive(100);
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);
            player.GetFieldHandler = field =>
            {
                Assert.Same(healthField, field);
                return expected;
            };

            var result = new ExpressionEvaluator(
                    new FakeFrameEnvironment().WithValue("player", player))
                .Evaluate(Parse("player.Health"), CancellationToken.None);

            Assert.Same(expected, result);
        }

        [Fact]
        public void MethodInvocationUsesExactReferenceOptions()
        {
            var integerType = new FakeRuntimeType("Int32", "System.Int32");
            var method = new RuntimeMethod(
                "GetHealth",
                integerType,
                Array.Empty<RuntimeParameter>(),
                isStatic: false,
                isPublic: true,
                isVirtual: false,
                isSpecialName: false,
                source: new object());
            var playerType = new FakeRuntimeType("Player")
            {
                Methods = new[] { method },
            };
            var expected = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(integerType)
                .WithPrimitive(100);
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);
            player.InvokeHandler = (
                invokedMethod,
                arguments,
                options,
                cancellationToken) =>
            {
                Assert.Same(method, invokedMethod);
                Assert.Empty(arguments);
                Assert.Equal(
                    InvokeOptions.DisableBreakpoints |
                    InvokeOptions.SingleThreaded,
                    options);
                return Task.FromResult<IRuntimeValue>(expected);
            };

            var result = new ExpressionEvaluator(
                    new FakeFrameEnvironment().WithValue("player", player))
                .Evaluate(Parse("player.GetHealth()"), CancellationToken.None);

            Assert.Same(expected, result);
        }

        [Fact]
        public void EnumCastToIntegerReturnsUnderlyingValue()
        {
            var stateType = FakeRuntimeType.Enum("ButtonState", ("Normal", 0));
            var environment = new FakeFrameEnvironment()
                .WithValue("state", stateType.EnumValue("Normal"));

            var result = new ExpressionEvaluator(environment).Evaluate(
                Parse("(int)state"),
                CancellationToken.None);

            Assert.Equal(RuntimeValueKind.Primitive, result.Kind);
            Assert.Equal(0, result.Primitive);
            Assert.Equal("System.Int32", result.Type.FullName);
        }

        [Theory]
        [InlineData("this")]
        [InlineData("base")]
        public void ThisAndBaseResolveFromTheFrame(string expression)
        {
            var expected = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(new FakeRuntimeType("Player"));
            var environment = new FakeFrameEnvironment()
                .WithValue(expression, expected);

            var result = new ExpressionEvaluator(environment).Evaluate(
                Parse(expression),
                CancellationToken.None);

            Assert.Same(expected, result);
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
