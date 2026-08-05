using System;
using System.Text;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Breakpoints
{
    internal sealed class EngineExpressionResult
    {
        private EngineExpressionResult(
            IRuntimeValue? value,
            string display,
            string? error,
            bool timedOut)
        {
            Value = value;
            Display = display;
            Error = error;
            TimedOut = timedOut;
        }

        public bool Success => Value != null && Error == null && !TimedOut;
        public IRuntimeValue? Value { get; }
        public string Display { get; }
        public string? Error { get; }
        public bool TimedOut { get; }

        public static EngineExpressionResult Succeeded(
            IRuntimeValue value,
            string display) =>
            new EngineExpressionResult(value, display, null, false);

        public static EngineExpressionResult Failed(string error) =>
            new EngineExpressionResult(null, string.Empty, error, false);

        public static EngineExpressionResult Timeout() =>
            new EngineExpressionResult(
                null,
                string.Empty,
                "Expression evaluation timed out.",
                true);
    }

    internal interface IEngineExpressionEvaluator
    {
        EngineExpressionResult EvaluateExpression(
            IFrameEvaluationEnvironment frame,
            string expression,
            int timeoutMilliseconds);
    }

    internal sealed class LogPointResult
    {
        public LogPointResult(bool success, string output)
        {
            Success = success;
            Output = output;
        }

        public bool Success { get; }
        public string Output { get; }
    }

    internal sealed class LogPointEvaluator
    {
        private readonly IEngineExpressionEvaluator evaluator;

        public LogPointEvaluator(IEngineExpressionEvaluator evaluator)
        {
            this.evaluator = evaluator ??
                throw new ArgumentNullException(nameof(evaluator));
        }

        public LogPointResult Evaluate(
            string format,
            IFrameEvaluationEnvironment frame,
            int timeoutMilliseconds)
        {
            var output = new StringBuilder(format.Length);
            for (var index = 0; index < format.Length; index++)
            {
                if (format[index] == '{')
                {
                    if (index + 1 < format.Length && format[index + 1] == '{')
                    {
                        output.Append('{');
                        index++;
                        continue;
                    }
                    var end = format.IndexOf('}', index + 1);
                    if (end < 0)
                    {
                        return new LogPointResult(
                            false,
                            "Logpoint expression is missing a closing brace.");
                    }
                    var expression = format.Substring(
                        index + 1,
                        end - index - 1);
                    var value = evaluator.EvaluateExpression(
                        frame,
                        expression,
                        timeoutMilliseconds);
                    if (!value.Success)
                    {
                        return new LogPointResult(
                            false,
                            value.Error ?? "Logpoint evaluation failed.");
                    }
                    output.Append(value.Display);
                    index = end;
                    continue;
                }
                if (
                    format[index] == '}' &&
                    index + 1 < format.Length &&
                    format[index + 1] == '}')
                {
                    output.Append('}');
                    index++;
                    continue;
                }
                output.Append(format[index]);
            }

            return new LogPointResult(true, output.ToString());
        }
    }
}
