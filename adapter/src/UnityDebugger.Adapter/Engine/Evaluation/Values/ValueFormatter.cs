using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Values
{
    internal sealed class ValueFormatter
    {
        private static readonly TimeSpan ToStringTimeout =
            TimeSpan.FromMilliseconds(2000);
        private readonly Func<
            Task<IRuntimeValue>,
            TimeSpan,
            CancellationToken,
            Task<IRuntimeValue?>> waitForValue;
        private readonly RuntimeInvoker invoker = new RuntimeInvoker();

        public ValueFormatter(
            Func<
                Task<IRuntimeValue>,
                TimeSpan,
                CancellationToken,
                Task<IRuntimeValue?>>? waitForValue = null)
        {
            this.waitForValue = waitForValue ?? WaitForValueAsync;
        }

        public async Task<string> FormatAsync(
            IRuntimeValue value,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (value.Kind)
            {
                case RuntimeValueKind.Null:
                    return "null";
                case RuntimeValueKind.String:
                    return Quote(value.String ?? string.Empty);
                case RuntimeValueKind.Primitive:
                    return FormatPrimitive(value.Primitive);
                case RuntimeValueKind.Enum:
                    return FormatEnum(value);
                case RuntimeValueKind.Pointer:
                    return value.Address.HasValue
                        ? $"0x{value.Address.Value:x}"
                        : "0x0";
            }

            var fallback = $"{{{value.Type.Name}}}";
            if (!policy.AllowTargetInvoke || !policy.AllowToString)
                return fallback;
            var toString = value.Type.Methods.FirstOrDefault(IsOverriddenToString);
            if (toString == null)
                return fallback;

            try
            {
                var invocation = invoker.InvokeAsync(
                    value,
                    toString,
                    Array.Empty<IRuntimeValue>(),
                    cancellationToken);
                var result = await waitForValue(
                        invocation,
                        ToStringTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (result == null)
                    return fallback;
                if (result.Kind == RuntimeValueKind.String)
                    return result.String ?? string.Empty;
                return FormatPrimitive(result.Primitive);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsOverriddenToString(RuntimeMethod method) =>
            method.Name == "ToString" &&
            !method.IsStatic &&
            method.Parameters.Count == 0 &&
            method.DeclaringTypeName != "System.Object" &&
            method.DeclaringTypeName != "System.ValueType";

        private static string FormatPrimitive(object? value)
        {
            if (value == null)
                return "null";
            if (value is bool boolean)
                return boolean ? "true" : "false";
            if (value is char character)
                return $"'{Escape(character.ToString())}'";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ??
                string.Empty;
        }

        private static string FormatEnum(IRuntimeValue value)
        {
            var exact = value.Type.EnumConstants.FirstOrDefault(
                constant => NumbersEqual(constant.Value, value.Primitive));
            if (!string.IsNullOrEmpty(exact.Key))
                return exact.Key;

            if (value.Primitive == null)
                return string.Empty;
            var remaining = Convert.ToUInt64(
                value.Primitive,
                CultureInfo.InvariantCulture);
            var names = new List<string>();
            foreach (var constant in value.Type.EnumConstants)
            {
                var flag = Convert.ToUInt64(
                    constant.Value,
                    CultureInfo.InvariantCulture);
                if (flag == 0 || (remaining & flag) != flag)
                    continue;
                names.Add(constant.Key);
                remaining &= ~flag;
            }

            return remaining == 0 && names.Count > 0
                ? string.Join(", ", names)
                : FormatPrimitive(value.Primitive);
        }

        private static bool NumbersEqual(object left, object? right)
        {
            if (right == null)
                return false;
            return Convert.ToDecimal(
                    left,
                    CultureInfo.InvariantCulture) ==
                Convert.ToDecimal(right, CultureInfo.InvariantCulture);
        }

        private static string Quote(string value) =>
            $"\"{Escape(value)}\"";

        private static string Escape(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '"':
                        result.Append("\\\"");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    default:
                        result.Append(character);
                        break;
                }
            }

            return result.ToString();
        }

        private static async Task<IRuntimeValue?> WaitForValueAsync(
            Task<IRuntimeValue> task,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var delay = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == task)
                return await task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
