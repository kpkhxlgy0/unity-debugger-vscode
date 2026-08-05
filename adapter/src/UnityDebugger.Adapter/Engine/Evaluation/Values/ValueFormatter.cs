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

            if (!string.IsNullOrEmpty(value.Type.DebuggerDisplay))
            {
                var display = await TryFormatDebuggerDisplayAsync(
                        value,
                        value.Type.DebuggerDisplay!,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (display != null)
                    return display;
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

        private async Task<string?> TryFormatDebuggerDisplayAsync(
            IRuntimeValue value,
            string format,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = new StringBuilder(format.Length);
                for (var index = 0; index < format.Length; index++)
                {
                    if (format[index] != '{')
                    {
                        result.Append(format[index]);
                        continue;
                    }
                    if (index + 1 < format.Length && format[index + 1] == '{')
                    {
                        result.Append('{');
                        index++;
                        continue;
                    }

                    var end = format.IndexOf('}', index + 1);
                    if (end < 0)
                        return null;
                    var expression = format.Substring(
                        index + 1,
                        end - index - 1);
                    var parts = expression.Split(',');
                    var member = await EvaluateDisplayMemberAsync(
                            value,
                            parts[0].Trim(),
                            policy,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var noQuotes = parts.Skip(1).Any(part =>
                        string.Equals(
                            part.Trim(),
                            "nq",
                            StringComparison.OrdinalIgnoreCase));
                    if (noQuotes && member.Kind == RuntimeValueKind.String)
                        result.Append(member.String ?? string.Empty);
                    else
                    {
                        result.Append(await FormatAsync(
                                member,
                                policy,
                                cancellationToken)
                            .ConfigureAwait(false));
                    }
                    index = end;
                }

                return result.ToString();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private async Task<IRuntimeValue> EvaluateDisplayMemberAsync(
            IRuntimeValue value,
            string expression,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            var current = value;
            var members = expression.Split('.');
            foreach (var memberName in members)
            {
                if (memberName == "this" || memberName.Length == 0)
                    continue;
                var runtimeField = FindFields(current.Type)
                    .FirstOrDefault(field => field.Name == memberName);
                if (runtimeField != null)
                {
                    current = current.GetField(runtimeField);
                    continue;
                }

                var property = FindProperties(current.Type)
                    .FirstOrDefault(candidate => candidate.Name == memberName);
                if (
                    property?.Getter == null ||
                    !policy.AllowTargetInvoke ||
                    !policy.AllowGetters)
                {
                    throw new InvalidOperationException(
                        $"DebuggerDisplay member '{memberName}' is unavailable.");
                }
                current = await invoker.InvokeAsync(
                        current,
                        property.Getter,
                        Array.Empty<IRuntimeValue>(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return current;
        }

        private static IEnumerable<RuntimeField> FindFields(IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var runtimeField in current.Fields)
                    yield return runtimeField;
            }
        }

        private static IEnumerable<RuntimeProperty> FindProperties(
            IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.Properties)
                    yield return property;
            }
        }

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
