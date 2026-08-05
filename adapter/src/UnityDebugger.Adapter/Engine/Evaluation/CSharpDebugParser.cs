using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal sealed class CSharpDebugParser
    {
        public ParsingResult ParseExpression(string text)
        {
            var expression = SyntaxFactory.ParseExpression(
                text,
                options: new CSharpParseOptions(LanguageVersion.Latest));
            var error = expression
                .GetDiagnostics()
                .FirstOrDefault(value => value.Severity == DiagnosticSeverity.Error)
                ?.GetMessage();
            return new ParsingResult(expression, error);
        }
    }
}
