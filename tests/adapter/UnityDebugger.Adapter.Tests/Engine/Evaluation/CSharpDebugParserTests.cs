using Microsoft.CodeAnalysis.CSharp;
using UnityDebugger.Adapter.Engine.Evaluation;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class CSharpDebugParserTests
    {
        [Theory]
        [InlineData(
            "currentState == ButtonState.Normal",
            SyntaxKind.EqualsExpression)]
        [InlineData("items[index]", SyntaxKind.ElementAccessExpression)]
        [InlineData("(ButtonState)value", SyntaxKind.CastExpression)]
        public void ParseExpressionReturnsTheRoslynExpressionRoot(
            string text,
            SyntaxKind expectedKind)
        {
            var result = new CSharpDebugParser().ParseExpression(text);

            Assert.True(result.CanEvaluate, result.Error);
            Assert.Equal(expectedKind, result.Expression!.Kind());
        }

        [Fact]
        public void InvalidExpressionReturnsTheFirstDiagnosticWithoutThrowing()
        {
            var result = new CSharpDebugParser()
                .ParseExpression("currentState ==");

            Assert.False(result.CanEvaluate);
            Assert.NotEmpty(result.Error!);
        }
    }
}
