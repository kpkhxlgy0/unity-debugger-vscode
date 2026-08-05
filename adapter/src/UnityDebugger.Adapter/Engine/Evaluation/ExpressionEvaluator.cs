using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private readonly RuntimeInvoker runtimeInvoker = new RuntimeInvoker();

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
                    return EvaluateMember(member, cancellationToken);
                case ElementAccessExpressionSyntax element:
                    return EvaluateElement(element, cancellationToken);
                case InvocationExpressionSyntax invocation:
                    return EvaluateInvocation(invocation, cancellationToken);
                case CastExpressionSyntax cast:
                    return EvaluateCast(cast, cancellationToken);
                case ConditionalExpressionSyntax conditional:
                    return EvaluateConditional(conditional, cancellationToken);
                case ThisExpressionSyntax _:
                    return EvaluateFrameKeyword("this");
                case BaseExpressionSyntax _:
                    return EvaluateFrameKeyword("base");
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

        private IRuntimeValue EvaluateMember(
            MemberAccessExpressionSyntax member,
            CancellationToken cancellationToken)
        {
            var typeName = member.Expression.ToString();
            var memberName = member.Name.Identifier.ValueText;
            if (
                environment.TryGetType(typeName, out var enumType) &&
                enumType.IsEnum)
            {
                if (
                    !enumType.EnumConstants.TryGetValue(
                        memberName,
                        out var constant))
                {
                    throw new ExpressionEvaluationException(
                        $"The enum '{typeName}' does not contain '{memberName}'.");
                }

                return EvaluationRuntimeValue.CreateEnum(enumType, constant);
            }

            var target = Evaluate(member.Expression, cancellationToken);
            var field = FindFields(target.Type)
                .FirstOrDefault(value => value.Name == memberName);
            if (field != null)
                return target.GetField(field);

            var property = FindProperties(target.Type)
                .FirstOrDefault(value => value.Name == memberName);
            if (property?.Getter != null)
            {
                return runtimeInvoker.InvokeAsync(
                        target,
                        property.Getter,
                        Array.Empty<IRuntimeValue>(),
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            }

            throw new ExpressionEvaluationException(
                $"The member '{memberName}' is not available on '{target.Type.Name}'.");
        }

        private IRuntimeValue EvaluateUnary(
            PrefixUnaryExpressionSyntax unary,
            CancellationToken cancellationToken)
        {
            var operand = Evaluate(unary.Operand, cancellationToken);
            if (unary.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return EvaluationRuntimeValue.CreatePrimitive(
                    !RequireBoolean(operand));
            }
            if (unary.IsKind(SyntaxKind.UnaryPlusExpression))
                return CreateNumericResult(operand.Primitive, value => +(dynamic)value);
            if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
                return CreateNumericResult(operand.Primitive, value => -(dynamic)value);
            if (unary.IsKind(SyntaxKind.BitwiseNotExpression))
            {
                var result = ApplyDynamicUnary(
                    operand.Primitive,
                    value => ~(dynamic)value);
                if (operand.Kind == RuntimeValueKind.Enum)
                    return EvaluationRuntimeValue.CreateEnum(operand.Type, result);
                return EvaluationRuntimeValue.CreatePrimitive(result);
            }

            throw Unsupported(unary);
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

            if (
                binary.IsKind(SyntaxKind.AddExpression) &&
                (leftValue.Kind == RuntimeValueKind.String ||
                    rightValue.Kind == RuntimeValueKind.String))
            {
                return EvaluationRuntimeValue.CreateString(
                    GetConcatenationValue(leftValue) +
                    GetConcatenationValue(rightValue));
            }
            if (IsArithmetic(binary.Kind()))
            {
                return EvaluationRuntimeValue.CreatePrimitive(
                    ApplyArithmetic(
                        binary.Kind(),
                        leftValue.Primitive,
                        rightValue.Primitive));
            }
            if (IsComparison(binary.Kind()))
            {
                return EvaluationRuntimeValue.CreatePrimitive(
                    ApplyComparison(
                        binary.Kind(),
                        leftValue.Primitive,
                        rightValue.Primitive));
            }
            if (IsBitwise(binary.Kind()))
            {
                var result = ApplyBitwise(
                    binary.Kind(),
                    leftValue.Primitive,
                    rightValue.Primitive);
                if (
                    leftValue.Kind == RuntimeValueKind.Enum ||
                    rightValue.Kind == RuntimeValueKind.Enum)
                {
                    RequireCompatibleEnums(leftValue, rightValue);
                    return EvaluationRuntimeValue.CreateEnum(
                        leftValue.Kind == RuntimeValueKind.Enum
                            ? leftValue.Type
                            : rightValue.Type,
                        result);
                }

                return EvaluationRuntimeValue.CreatePrimitive(result);
            }

            throw Unsupported(binary);
        }

        private IRuntimeValue EvaluateElement(
            ElementAccessExpressionSyntax element,
            CancellationToken cancellationToken)
        {
            if (element.ArgumentList.Arguments.Count != 1)
                throw Unsupported(element);
            var target = Evaluate(element.Expression, cancellationToken);
            var indexValue = Evaluate(
                element.ArgumentList.Arguments[0].Expression,
                cancellationToken);
            var index = Convert.ToInt32(
                indexValue.Primitive,
                CultureInfo.InvariantCulture);
            return target.GetElement(index);
        }

        private IRuntimeValue EvaluateInvocation(
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax member))
                throw Unsupported(invocation);

            var target = Evaluate(member.Expression, cancellationToken);
            var arguments = invocation.ArgumentList.Arguments
                .Select(value => Evaluate(value.Expression, cancellationToken))
                .ToArray();
            var methodName = member.Name.Identifier.ValueText;
            var method = FindMethods(target.Type)
                .Where(value => value.Name == methodName)
                .Where(value => value.Parameters.Count == arguments.Length)
                .FirstOrDefault(value => ParametersAccept(
                    value.Parameters,
                    arguments));
            if (method == null)
            {
                throw new ExpressionEvaluationException(
                    $"No compatible overload of '{methodName}' is available.");
            }

            return runtimeInvoker.InvokeAsync(
                    target,
                    method,
                    arguments,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        private IRuntimeValue EvaluateCast(
            CastExpressionSyntax cast,
            CancellationToken cancellationToken)
        {
            var value = Evaluate(cast.Expression, cancellationToken);
            var targetType = ResolveType(cast.Type.ToString());
            if (targetType.IsEnum)
            {
                if (
                    value.Kind == RuntimeValueKind.Enum &&
                    value.Type.FullName == targetType.FullName)
                {
                    return value;
                }
                if (value.Primitive == null)
                {
                    throw new ExpressionEvaluationException(
                        "A null value cannot be converted to an enum.");
                }
                return EvaluationRuntimeValue.CreateEnum(
                    targetType,
                    value.Primitive);
            }

            if (TryGetSystemType(targetType.FullName, out var systemType))
            {
                if (value.Primitive == null)
                {
                    throw new ExpressionEvaluationException(
                        $"A null value cannot be converted to '{targetType.Name}'.");
                }
                try
                {
                    var converted = Convert.ChangeType(
                        value.Primitive,
                        systemType,
                        CultureInfo.InvariantCulture);
                    return EvaluationRuntimeValue.CreatePrimitive(converted);
                }
                catch (Exception exception)
                {
                    throw new ExpressionEvaluationException(
                        $"The value cannot be converted to '{targetType.Name}': " +
                        exception.Message);
                }
            }

            if (
                value.Kind == RuntimeValueKind.Null &&
                !targetType.IsValueType)
            {
                return value;
            }
            if (targetType.IsAssignableFrom(value.Type))
                return value;
            throw new ExpressionEvaluationException(
                $"The value cannot be converted to '{targetType.Name}'.");
        }

        private IRuntimeValue EvaluateConditional(
            ConditionalExpressionSyntax conditional,
            CancellationToken cancellationToken)
        {
            var condition = RequireBoolean(
                Evaluate(conditional.Condition, cancellationToken));
            return Evaluate(
                condition ? conditional.WhenTrue : conditional.WhenFalse,
                cancellationToken);
        }

        private IRuntimeValue EvaluateFrameKeyword(string name)
        {
            if (environment.TryGetValue(name, out var value))
                return value;
            throw new ExpressionEvaluationException(
                $"'{name}' is not available in the current frame.");
        }

        private IRuntimeType ResolveType(string name)
        {
            if (environment.TryGetType(name, out var runtimeType))
                return runtimeType;
            var systemType = GetAliasType(name);
            if (systemType != null)
                return EvaluationRuntimeType.From(systemType);
            throw new ExpressionEvaluationException(
                $"The type '{name}' is not available in the current context.");
        }

        private static Type? GetAliasType(string name)
        {
            switch (name)
            {
                case "bool":
                    return typeof(bool);
                case "byte":
                    return typeof(byte);
                case "sbyte":
                    return typeof(sbyte);
                case "short":
                    return typeof(short);
                case "ushort":
                    return typeof(ushort);
                case "int":
                    return typeof(int);
                case "uint":
                    return typeof(uint);
                case "long":
                    return typeof(long);
                case "ulong":
                    return typeof(ulong);
                case "char":
                    return typeof(char);
                case "float":
                    return typeof(float);
                case "double":
                    return typeof(double);
                case "decimal":
                    return typeof(decimal);
                case "string":
                    return typeof(string);
                case "object":
                    return typeof(object);
                default:
                    return null;
            }
        }

        private static bool TryGetSystemType(
            string fullName,
            out Type type)
        {
            type = Type.GetType(fullName, throwOnError: false)!;
            return type != null &&
                (type.IsPrimitive ||
                    type == typeof(decimal) ||
                    type == typeof(string));
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

        private static IEnumerable<RuntimeMethod> FindMethods(IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var method in current.Methods)
                    yield return method;
            }
        }

        private static bool ParametersAccept(
            IReadOnlyList<RuntimeParameter> parameters,
            IReadOnlyList<IRuntimeValue> arguments)
        {
            for (var index = 0; index < parameters.Count; index++)
            {
                var argument = arguments[index];
                if (
                    argument.Kind == RuntimeValueKind.Null &&
                    !parameters[index].Type.IsValueType)
                {
                    continue;
                }
                if (
                    parameters[index].Type.FullName == argument.Type.FullName ||
                    parameters[index].Type.IsAssignableFrom(argument.Type))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsArithmetic(SyntaxKind kind) =>
            kind == SyntaxKind.AddExpression ||
            kind == SyntaxKind.SubtractExpression ||
            kind == SyntaxKind.MultiplyExpression ||
            kind == SyntaxKind.DivideExpression ||
            kind == SyntaxKind.ModuloExpression;

        private static bool IsComparison(SyntaxKind kind) =>
            kind == SyntaxKind.LessThanExpression ||
            kind == SyntaxKind.LessThanOrEqualExpression ||
            kind == SyntaxKind.GreaterThanExpression ||
            kind == SyntaxKind.GreaterThanOrEqualExpression;

        private static bool IsBitwise(SyntaxKind kind) =>
            kind == SyntaxKind.BitwiseAndExpression ||
            kind == SyntaxKind.BitwiseOrExpression ||
            kind == SyntaxKind.ExclusiveOrExpression ||
            kind == SyntaxKind.LeftShiftExpression ||
            kind == SyntaxKind.RightShiftExpression;

        private static object ApplyArithmetic(
            SyntaxKind kind,
            object? left,
            object? right)
        {
            try
            {
                dynamic dynamicLeft = left ?? throw NonNumeric();
                dynamic dynamicRight = right ?? throw NonNumeric();
                checked
                {
                    switch (kind)
                    {
                        case SyntaxKind.AddExpression:
                            return dynamicLeft + dynamicRight;
                        case SyntaxKind.SubtractExpression:
                            return dynamicLeft - dynamicRight;
                        case SyntaxKind.MultiplyExpression:
                            return dynamicLeft * dynamicRight;
                        case SyntaxKind.DivideExpression:
                            return dynamicLeft / dynamicRight;
                        case SyntaxKind.ModuloExpression:
                            return dynamicLeft % dynamicRight;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
            catch (ExpressionEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw OperationFailed(kind, exception);
            }
        }

        private static bool ApplyComparison(
            SyntaxKind kind,
            object? left,
            object? right)
        {
            try
            {
                dynamic dynamicLeft = left ?? throw NonNumeric();
                dynamic dynamicRight = right ?? throw NonNumeric();
                switch (kind)
                {
                    case SyntaxKind.LessThanExpression:
                        return dynamicLeft < dynamicRight;
                    case SyntaxKind.LessThanOrEqualExpression:
                        return dynamicLeft <= dynamicRight;
                    case SyntaxKind.GreaterThanExpression:
                        return dynamicLeft > dynamicRight;
                    case SyntaxKind.GreaterThanOrEqualExpression:
                        return dynamicLeft >= dynamicRight;
                    default:
                        throw new InvalidOperationException();
                }
            }
            catch (ExpressionEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw OperationFailed(kind, exception);
            }
        }

        private static object ApplyBitwise(
            SyntaxKind kind,
            object? left,
            object? right)
        {
            try
            {
                dynamic dynamicLeft = left ?? throw NonNumeric();
                dynamic dynamicRight = right ?? throw NonNumeric();
                switch (kind)
                {
                    case SyntaxKind.BitwiseAndExpression:
                        return dynamicLeft & dynamicRight;
                    case SyntaxKind.BitwiseOrExpression:
                        return dynamicLeft | dynamicRight;
                    case SyntaxKind.ExclusiveOrExpression:
                        return dynamicLeft ^ dynamicRight;
                    case SyntaxKind.LeftShiftExpression:
                        return dynamicLeft << dynamicRight;
                    case SyntaxKind.RightShiftExpression:
                        return dynamicLeft >> dynamicRight;
                    default:
                        throw new InvalidOperationException();
                }
            }
            catch (ExpressionEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw OperationFailed(kind, exception);
            }
        }

        private static IRuntimeValue CreateNumericResult(
            object? value,
            Func<object, object> operation) =>
            EvaluationRuntimeValue.CreatePrimitive(
                ApplyDynamicUnary(value, operation));

        private static object ApplyDynamicUnary(
            object? value,
            Func<object, object> operation)
        {
            if (value == null)
                throw NonNumeric();
            try
            {
                return operation(value);
            }
            catch (Exception exception)
            {
                throw new ExpressionEvaluationException(
                    "The unary operation failed: " + exception.Message);
            }
        }

        private static void RequireCompatibleEnums(
            IRuntimeValue left,
            IRuntimeValue right)
        {
            if (
                left.Kind != RuntimeValueKind.Enum ||
                right.Kind != RuntimeValueKind.Enum ||
                left.Type.FullName != right.Type.FullName)
            {
                throw new ExpressionEvaluationException(
                    "Enum values must have compatible types.");
            }
        }

        private static string GetConcatenationValue(IRuntimeValue value)
        {
            if (value.Kind == RuntimeValueKind.Null)
                return string.Empty;
            if (value.Kind == RuntimeValueKind.String)
                return value.String ?? string.Empty;
            return Convert.ToString(
                    value.Primitive,
                    CultureInfo.InvariantCulture) ??
                string.Empty;
        }

        private static ExpressionEvaluationException NonNumeric() =>
            new ExpressionEvaluationException(
                "The expression requires numeric operands.");

        private static ExpressionEvaluationException OperationFailed(
            SyntaxKind kind,
            Exception exception) =>
            new ExpressionEvaluationException(
                $"The '{kind}' operation failed: {exception.Message}");

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

            public Task<IRuntimeValue> CreateInstanceAsync(
                IRuntimeType type,
                RuntimeMethod constructor,
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
            public IRuntimeType? ElementType => null;
            public IRuntimeType? BaseType => null;
            public IReadOnlyList<IRuntimeType> Interfaces =>
                Array.Empty<IRuntimeType>();
            public IReadOnlyList<RuntimeField> Fields => NoFields;
            public IReadOnlyList<RuntimeProperty> Properties => NoProperties;
            public IReadOnlyList<RuntimeMethod> Methods => NoMethods;
            public IReadOnlyDictionary<string, object> EnumConstants =>
                NoConstants;
            public string? DebuggerDisplay => null;
            public IRuntimeType? DebuggerProxyType => null;

            public static IRuntimeType From(Type type) =>
                new EvaluationRuntimeType(type);

            public bool IsAssignableFrom(IRuntimeType candidate) =>
                FullName == candidate.FullName;
        }
    }
}
