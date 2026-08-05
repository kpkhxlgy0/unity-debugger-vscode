using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal sealed class ExpressionEvaluationException : Exception
    {
        public ExpressionEvaluationException(string message)
            : base(message)
        {
        }
    }

    internal sealed class ExpressionEvaluator
    {
        private readonly IFrameEvaluationEnvironment environment;

        public ExpressionEvaluator(IFrameEvaluationEnvironment environment)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
        }

        public IRuntimeValue Evaluate(
            ExpressionSyntax expression,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    return EvaluateLiteral(literal);
                case IdentifierNameSyntax identifier:
                    return EvaluateIdentifier(identifier);
                case ParenthesizedExpressionSyntax parenthesized:
                    return Evaluate(parenthesized.Expression, cancellationToken);
                case PrefixUnaryExpressionSyntax unary:
                    return EvaluateUnary(unary, cancellationToken);
                case BinaryExpressionSyntax binary:
                    return EvaluateBinary(binary, cancellationToken);
                case MemberAccessExpressionSyntax member:
                    return EvaluateEnumMember(member);
                default:
                    throw Unsupported(expression);
            }
        }

        private static IRuntimeValue EvaluateLiteral(
            LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.NullLiteralExpression))
                return EvaluationRuntimeValue.CreateNull();
            if (literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return EvaluationRuntimeValue.CreateString(
                    (string)literal.Token.Value!);
            }

            return EvaluationRuntimeValue.CreatePrimitive(literal.Token.Value!);
        }

        private IRuntimeValue EvaluateIdentifier(IdentifierNameSyntax identifier)
        {
            var name = identifier.Identifier.ValueText;
            if (environment.TryGetValue(name, out var value))
                return value;
            throw new ExpressionEvaluationException(
                $"The name '{name}' is not available in the current context.");
        }

        private IRuntimeValue EvaluateEnumMember(
            MemberAccessExpressionSyntax member)
        {
            var typeName = member.Expression.ToString();
            var memberName = member.Name.Identifier.ValueText;
            if (!environment.TryGetType(typeName, out var type))
            {
                throw new ExpressionEvaluationException(
                    $"The type '{typeName}' is not available in the current context.");
            }
            if (!type.IsEnum)
            {
                throw new ExpressionEvaluationException(
                    $"The type '{typeName}' is not an enum.");
            }
            if (!type.EnumConstants.TryGetValue(memberName, out var constant))
            {
                throw new ExpressionEvaluationException(
                    $"The enum '{typeName}' does not contain '{memberName}'.");
            }

            return EvaluationRuntimeValue.CreateEnum(type, constant);
        }

        private IRuntimeValue EvaluateUnary(
            PrefixUnaryExpressionSyntax unary,
            CancellationToken cancellationToken)
        {
            if (!unary.IsKind(SyntaxKind.LogicalNotExpression))
                throw Unsupported(unary);
            var operand = Evaluate(unary.Operand, cancellationToken);
            return EvaluationRuntimeValue.CreatePrimitive(
                !RequireBoolean(operand));
        }

        private IRuntimeValue EvaluateBinary(
            BinaryExpressionSyntax binary,
            CancellationToken cancellationToken)
        {
            if (binary.IsKind(SyntaxKind.LogicalAndExpression))
            {
                var left = RequireBoolean(
                    Evaluate(binary.Left, cancellationToken));
                if (!left)
                    return EvaluationRuntimeValue.CreatePrimitive(false);
                return EvaluationRuntimeValue.CreatePrimitive(
                    RequireBoolean(Evaluate(binary.Right, cancellationToken)));
            }
            if (binary.IsKind(SyntaxKind.LogicalOrExpression))
            {
                var left = RequireBoolean(
                    Evaluate(binary.Left, cancellationToken));
                if (left)
                    return EvaluationRuntimeValue.CreatePrimitive(true);
                return EvaluationRuntimeValue.CreatePrimitive(
                    RequireBoolean(Evaluate(binary.Right, cancellationToken)));
            }

            var leftValue = Evaluate(binary.Left, cancellationToken);
            var rightValue = Evaluate(binary.Right, cancellationToken);
            if (binary.IsKind(SyntaxKind.EqualsExpression))
            {
                return EvaluationRuntimeValue.CreatePrimitive(
                    AreEqual(leftValue, rightValue));
            }
            if (binary.IsKind(SyntaxKind.NotEqualsExpression))
            {
                return EvaluationRuntimeValue.CreatePrimitive(
                    !AreEqual(leftValue, rightValue));
            }

            throw Unsupported(binary);
        }

        private static bool AreEqual(
            IRuntimeValue left,
            IRuntimeValue right)
        {
            if (
                left.Kind == RuntimeValueKind.Enum ||
                right.Kind == RuntimeValueKind.Enum)
            {
                if (
                    left.Kind != RuntimeValueKind.Enum ||
                    right.Kind != RuntimeValueKind.Enum ||
                    left.Type.FullName != right.Type.FullName)
                {
                    throw new ExpressionEvaluationException(
                        "Enum values must have compatible types.");
                }

                return NormalizeNumber(left.Primitive).Equals(
                    NormalizeNumber(right.Primitive));
            }

            if (
                left.Kind == RuntimeValueKind.Null ||
                right.Kind == RuntimeValueKind.Null)
            {
                return left.Kind == right.Kind;
            }
            if (
                IsNumber(left.Primitive) &&
                IsNumber(right.Primitive))
            {
                return NormalizeNumber(left.Primitive).Equals(
                    NormalizeNumber(right.Primitive));
            }
            if (
                left.Kind == RuntimeValueKind.String &&
                right.Kind == RuntimeValueKind.String)
            {
                return string.Equals(
                    left.String,
                    right.String,
                    StringComparison.Ordinal);
            }

            return Equals(left.Primitive, right.Primitive);
        }

        private static bool RequireBoolean(IRuntimeValue value)
        {
            if (value.Primitive is bool boolean)
                return boolean;
            throw new ExpressionEvaluationException(
                "The expression does not evaluate to a Boolean value.");
        }

        private static bool IsNumber(object? value)
        {
            if (value == null)
                return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.Char:
                    return true;
                default:
                    return false;
            }
        }

        private static decimal NormalizeNumber(object? value)
        {
            try
            {
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new ExpressionEvaluationException(
                    $"The value '{value}' is not numeric: {exception.Message}");
            }
        }

        private static ExpressionEvaluationException Unsupported(
            ExpressionSyntax expression) =>
            new ExpressionEvaluationException(
                $"Expression syntax '{expression.Kind()}' is not supported.");

        private sealed class EvaluationRuntimeValue : IRuntimeValue
        {
            private readonly object? primitive;
            private readonly string? stringValue;

            private EvaluationRuntimeValue(
                RuntimeValueKind kind,
                IRuntimeType type,
                object? primitive,
                string? stringValue)
            {
                Kind = kind;
                Type = type;
                this.primitive = primitive;
                this.stringValue = stringValue;
            }

            public RuntimeValueKind Kind { get; }
            public IRuntimeType Type { get; }
            public object? Primitive => primitive;
            public string? String => stringValue;
            public long? Address => null;
            public int Length => stringValue?.Length ?? throw UnsupportedValue();

            public static EvaluationRuntimeValue CreateNull() =>
                new EvaluationRuntimeValue(
                    RuntimeValueKind.Null,
                    NullRuntimeType.Instance,
                    null,
                    null);

            public static EvaluationRuntimeValue CreatePrimitive(object value) =>
                new EvaluationRuntimeValue(
                    RuntimeValueKind.Primitive,
                    EvaluationRuntimeType.From(value.GetType()),
                    value,
                    null);

            public static EvaluationRuntimeValue CreateString(string value) =>
                new EvaluationRuntimeValue(
                    RuntimeValueKind.String,
                    EvaluationRuntimeType.String,
                    null,
                    value);

            public static EvaluationRuntimeValue CreateEnum(
                IRuntimeType type,
                object value) =>
                new EvaluationRuntimeValue(
                    RuntimeValueKind.Enum,
                    type,
                    value,
                    null);

            public IRuntimeValue GetField(RuntimeField field) =>
                throw UnsupportedValue();

            public void SetField(RuntimeField field, IRuntimeValue value) =>
                throw UnsupportedValue();

            public IRuntimeValue GetElement(int index) =>
                throw UnsupportedValue();

            public Task<IRuntimeValue> InvokeAsync(
                RuntimeMethod method,
                IReadOnlyList<IRuntimeValue> arguments,
                InvokeOptions options,
                CancellationToken cancellationToken) =>
                throw UnsupportedValue();

            private static InvalidOperationException UnsupportedValue() =>
                new InvalidOperationException(
                    "A computed value does not expose target runtime members.");
        }

        private sealed class EvaluationRuntimeType : IRuntimeType
        {
            private static readonly IReadOnlyList<RuntimeField> NoFields =
                Array.Empty<RuntimeField>();
            private static readonly IReadOnlyList<RuntimeProperty> NoProperties =
                Array.Empty<RuntimeProperty>();
            private static readonly IReadOnlyList<RuntimeMethod> NoMethods =
                Array.Empty<RuntimeMethod>();
            private static readonly IReadOnlyDictionary<string, object>
                NoConstants = new Dictionary<string, object>();

            private EvaluationRuntimeType(Type type)
            {
                Name = type.Name;
                FullName = type.FullName ?? type.Name;
                IsPrimitive = type.IsPrimitive || type == typeof(decimal);
                IsValueType = type.IsValueType;
            }

            public static IRuntimeType String { get; } =
                new EvaluationRuntimeType(typeof(string));

            public string Name { get; }
            public string FullName { get; }
            public bool IsEnum => false;
            public bool IsPrimitive { get; }
            public bool IsValueType { get; }
            public bool IsArray => false;
            public IRuntimeType? BaseType => null;
            public IReadOnlyList<RuntimeField> Fields => NoFields;
            public IReadOnlyList<RuntimeProperty> Properties => NoProperties;
            public IReadOnlyList<RuntimeMethod> Methods => NoMethods;
            public IReadOnlyDictionary<string, object> EnumConstants =>
                NoConstants;

            public static IRuntimeType From(Type type) =>
                new EvaluationRuntimeType(type);

            public bool IsAssignableFrom(IRuntimeType candidate) =>
                FullName == candidate.FullName;
        }
    }
}
