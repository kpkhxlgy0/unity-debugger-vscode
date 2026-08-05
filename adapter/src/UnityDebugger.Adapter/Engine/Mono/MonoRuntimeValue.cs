using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoRuntimeValue : IRuntimeValue
    {
        private readonly Value? value;
        private readonly ThreadMirror? thread;

        public MonoRuntimeValue(Value? value, ThreadMirror? thread)
        {
            this.value = value;
            this.thread = thread;
        }

        public RuntimeValueKind Kind => Classify(value);

        public IRuntimeType Type
        {
            get
            {
                if (value is ObjectMirror objectValue)
                    return new MonoRuntimeType(objectValue.Type);
                if (value is StructMirror structValue)
                    return new MonoRuntimeType(structValue.Type);
                if (value is PointerValue pointerValue)
                    return new MonoRuntimeType(pointerValue.Type);
                if (
                    value is PrimitiveValue primitiveValue &&
                    primitiveValue.Value != null)
                {
                    var primitiveType = primitiveValue.VirtualMachine
                        .RootDomain
                        .GetCorrespondingType(primitiveValue.Value.GetType());
                    return new MonoRuntimeType(primitiveType);
                }

                return NullRuntimeType.Instance;
            }
        }

        public object? Primitive
        {
            get
            {
                if (value is PrimitiveValue primitiveValue)
                    return primitiveValue.Value;
                if (value is EnumMirror enumValue)
                    return enumValue.Value;
                return null;
            }
        }

        public string? String =>
            value is StringMirror stringValue ? stringValue.Value : null;

        public long? Address
        {
            get
            {
                if (value is PointerValue pointerValue)
                    return pointerValue.Address;
                if (value is ObjectMirror objectValue)
                    return objectValue.Address;
                return null;
            }
        }

        public int Length
        {
            get
            {
                if (value is ArrayMirror arrayValue)
                    return arrayValue.Length;
                if (value is StringMirror stringValue)
                    return stringValue.Length;
                throw new InvalidOperationException(
                    "The runtime value does not have a length.");
            }
        }

        internal Value? Mirror => value;

        public static RuntimeValueKind Classify(Value? value)
        {
            if (value == null)
                return RuntimeValueKind.Null;
            if (
                value is PrimitiveValue primitiveValue &&
                primitiveValue.Value == null)
            {
                return RuntimeValueKind.Null;
            }
            if (value is PrimitiveValue)
                return RuntimeValueKind.Primitive;
            if (value is StringMirror)
                return RuntimeValueKind.String;
            if (value is EnumMirror)
                return RuntimeValueKind.Enum;
            if (value is ArrayMirror)
                return RuntimeValueKind.Array;
            if (value is StructMirror)
                return RuntimeValueKind.Struct;
            if (value is ObjectMirror)
                return RuntimeValueKind.Object;
            if (value is PointerValue)
                return RuntimeValueKind.Pointer;
            throw new NotSupportedException(
                $"Unsupported Mono debugger value '{value.GetType().FullName}'.");
        }

        public IRuntimeValue GetField(RuntimeField field)
        {
            var monoField = GetMonoField(field);
            Value fieldValue;
            if (monoField.IsStatic)
                fieldValue = monoField.DeclaringType.GetValue(monoField);
            else if (value is ObjectMirror objectValue)
                fieldValue = objectValue.GetValue(monoField);
            else if (value is StructMirror structValue)
                fieldValue = structValue[monoField.Name];
            else
                throw new InvalidOperationException(
                    "The runtime value does not expose instance fields.");
            return new MonoRuntimeValue(fieldValue, thread);
        }

        public void SetField(RuntimeField field, IRuntimeValue newValue)
        {
            var monoField = GetMonoField(field);
            var monoValue = ToMonoValue(
                newValue,
                monoField.FieldType,
                ResolveDomain(monoField.FieldType),
                monoField.VirtualMachine);
            if (monoField.IsStatic)
                monoField.DeclaringType.SetValue(monoField, monoValue);
            else if (value is ObjectMirror objectValue)
                objectValue.SetValue(monoField, monoValue);
            else if (value is StructMirror structValue)
                structValue[monoField.Name] = monoValue;
            else
            {
                throw new InvalidOperationException(
                    "The runtime value does not expose instance fields.");
            }
        }

        public IRuntimeValue GetElement(int index)
        {
            if (!(value is ArrayMirror arrayValue))
                throw new InvalidOperationException(
                    "The runtime value does not expose indexed elements.");
            return new MonoRuntimeValue(arrayValue[index], thread);
        }

        public Task<IRuntimeValue> InvokeAsync(
            RuntimeMethod method,
            IReadOnlyList<IRuntimeValue> arguments,
            InvokeOptions options,
            CancellationToken cancellationToken)
        {
            if (thread == null)
                throw new InvalidOperationException(
                    "A stopped thread is required for target invocation.");
            if (!(method.Source is MethodMirror monoMethod))
                throw new ArgumentException(
                    "The method does not belong to the Mono runtime.",
                    nameof(method));

            var parameters = monoMethod.GetParameters();
            var domain = ResolveDomain(monoMethod.DeclaringType);
            var monoArguments = arguments
                .Select((value, index) => ToMonoValue(
                    value,
                    parameters[index].ParameterType,
                    domain,
                    monoMethod.VirtualMachine))
                .ToArray();
            IInvokable invokable = method.IsStatic
                ? monoMethod.DeclaringType
                : value as IInvokable ?? throw new InvalidOperationException(
                    "The runtime value cannot invoke instance methods.");

            return InvokeMonoAsync(
                invokable,
                monoMethod,
                monoArguments,
                options,
                cancellationToken);
        }

        public Task<IRuntimeValue> CreateInstanceAsync(
            IRuntimeType runtimeType,
            RuntimeMethod constructor,
            IReadOnlyList<IRuntimeValue> arguments,
            InvokeOptions options,
            CancellationToken cancellationToken)
        {
            if (thread == null)
                throw new InvalidOperationException(
                    "A stopped thread is required for target invocation.");
            if (!(runtimeType is MonoRuntimeType monoType))
            {
                throw new ArgumentException(
                    "The type does not belong to the Mono runtime.",
                    nameof(runtimeType));
            }
            if (!(constructor.Source is MethodMirror monoConstructor))
            {
                throw new ArgumentException(
                    "The constructor does not belong to the Mono runtime.",
                    nameof(constructor));
            }

            var parameters = monoConstructor.GetParameters();
            var domain = ResolveDomain(monoConstructor.DeclaringType);
            var monoArguments = arguments
                .Select((value, index) => ToMonoValue(
                    value,
                    parameters[index].ParameterType,
                    domain,
                    monoConstructor.VirtualMachine))
                .ToArray();
            return InvokeMonoAsync(
                monoType.Mirror,
                monoConstructor,
                monoArguments,
                options,
                cancellationToken);
        }

        private async Task<IRuntimeValue> InvokeMonoAsync(
            IInvokable invokable,
            MethodMirror method,
            Value[] arguments,
            InvokeOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = (IInvokeAsyncResult)invokable.BeginInvokeMethod(
                thread!,
                method,
                arguments,
                options,
                null,
                null);
            using (cancellationToken.Register(
                () => AbortInvocation(invocation)))
            {
                try
                {
                    var result = await Task<Value>.Factory.FromAsync(
                            invocation,
                            invokable.EndInvokeMethod)
                        .ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new MonoRuntimeValue(result, thread);
                }
                catch (InvocationException exception)
                {
                    throw CreateInvocationException(exception);
                }
                catch (Exception)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        private static void AbortInvocation(IInvokeAsyncResult invocation)
        {
            try
            {
                invocation.Abort();
            }
            catch
            {
            }
        }

        private static RuntimeInvocationException CreateInvocationException(
            InvocationException exception)
        {
            var targetException = exception.Exception;
            var typeName = targetException.Type.FullName;
            var message = GetTargetExceptionMessage(targetException);
            return new RuntimeInvocationException(typeName, message);
        }

        private static string GetTargetExceptionMessage(
            ObjectMirror targetException)
        {
            try
            {
                for (
                    var current = targetException.Type;
                    current != null;
                    current = current.BaseType)
                {
                    var messageField = current.GetFields().FirstOrDefault(
                        candidate =>
                            !candidate.IsStatic &&
                            (candidate.Name == "_message" ||
                                candidate.Name == "message"));
                    if (
                        messageField != null &&
                        targetException.GetValue(messageField) is StringMirror value)
                    {
                        return value.Value;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static FieldInfoMirror GetMonoField(RuntimeField field)
        {
            if (field.Source is FieldInfoMirror monoField)
                return monoField;
            throw new ArgumentException(
                "The field does not belong to the Mono runtime.",
                nameof(field));
        }

        internal static Value ToMonoValue(
            IRuntimeValue runtimeValue,
            TypeMirror expectedType,
            AppDomainMirror domain,
            VirtualMachine virtualMachine)
        {
            if (
                runtimeValue is MonoRuntimeValue monoValue &&
                monoValue.value != null)
            {
                return monoValue.value;
            }

            if (runtimeValue.Kind == RuntimeValueKind.Null)
                return new PrimitiveValue(virtualMachine, null);
            if (runtimeValue.Kind == RuntimeValueKind.String)
                return domain.CreateString(runtimeValue.String ?? string.Empty);
            if (runtimeValue.Kind == RuntimeValueKind.Enum)
            {
                var primitive = new PrimitiveValue(
                    virtualMachine,
                    ConvertPrimitive(
                        GetEnumUnderlyingType(expectedType),
                        runtimeValue.Primitive));
                return virtualMachine.CreateEnumMirror(
                    expectedType,
                    primitive);
            }
            if (runtimeValue.Kind == RuntimeValueKind.Primitive)
            {
                return new PrimitiveValue(
                    virtualMachine,
                    ConvertPrimitive(
                        expectedType,
                        runtimeValue.Primitive));
            }

            throw new ArgumentException(
                "The value does not belong to the Mono runtime.",
                nameof(runtimeValue));
        }

        private AppDomainMirror ResolveDomain(TypeMirror expectedType)
        {
            if (value is ObjectMirror objectValue)
                return objectValue.Domain;
            if (thread != null)
                return thread.Domain;
            try
            {
                return expectedType.Assembly.Domain;
            }
            catch (NotSupportedException)
            {
                return expectedType.VirtualMachine.RootDomain;
            }
        }

        private static TypeMirror GetEnumUnderlyingType(TypeMirror type) =>
            type.GetFields()
                .First(field =>
                    !field.IsStatic &&
                    field.Name == "value__")
                .FieldType;

        private static object? ConvertPrimitive(
            TypeMirror expectedType,
            object? value)
        {
            if (value == null)
                return null;
            switch (expectedType.FullName)
            {
                case "System.Boolean":
                    return Convert.ToBoolean(value);
                case "System.Byte":
                    return Convert.ToByte(value);
                case "System.SByte":
                    return Convert.ToSByte(value);
                case "System.Char":
                    return Convert.ToChar(value);
                case "System.Int16":
                    return Convert.ToInt16(value);
                case "System.UInt16":
                    return Convert.ToUInt16(value);
                case "System.Int32":
                    return Convert.ToInt32(value);
                case "System.UInt32":
                    return Convert.ToUInt32(value);
                case "System.Int64":
                    return Convert.ToInt64(value);
                case "System.UInt64":
                    return Convert.ToUInt64(value);
                case "System.Single":
                    return Convert.ToSingle(value);
                case "System.Double":
                    return Convert.ToDouble(value);
                default:
                    return value;
            }
        }
    }
}
