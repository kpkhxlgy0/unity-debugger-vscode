using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal sealed class ParsingResult
    {
        public ParsingResult(ExpressionSyntax? expression, string? error)
        {
            Expression = expression;
            Error = error;
        }

        public ExpressionSyntax? Expression { get; }
        public string? Error { get; }
        public bool CanEvaluate => Expression != null && string.IsNullOrEmpty(Error);
    }
}
