using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoRuntimeType : IRuntimeType
    {
        private readonly TypeMirror type;

        public MonoRuntimeType(TypeMirror type)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public string Name => type.Name;
        public string FullName => type.FullName;
        public bool IsEnum => type.IsEnum;
        public bool IsPrimitive => type.IsPrimitive;
        public bool IsValueType => type.IsValueType;
        public bool IsArray => type.IsArray;
        public IRuntimeType? ElementType => type.HasElementType
            ? new MonoRuntimeType(type.GetElementType())
            : null;
        public IRuntimeType? BaseType =>
            type.BaseType == null ? null : new MonoRuntimeType(type.BaseType);

        public IReadOnlyList<IRuntimeType> Interfaces => type
            .GetInterfaces()
            .Select(value => (IRuntimeType)new MonoRuntimeType(value))
            .ToArray();

        public IReadOnlyList<RuntimeField> Fields => type
            .GetFields()
            .Select(CreateField)
            .ToArray();

        public IReadOnlyList<RuntimeProperty> Properties => type
            .GetProperties()
            .Select(CreateProperty)
            .ToArray();

        public IReadOnlyList<RuntimeMethod> Methods => type
            .GetMethods()
            .Select(CreateMethod)
            .ToArray();

        public IReadOnlyDictionary<string, object> EnumConstants
        {
            get
            {
                var constants = new Dictionary<string, object>();
                if (!type.IsEnum)
                    return constants;

                foreach (var monoField in type.GetFields())
                {
                    if (!monoField.IsStatic || !monoField.IsLiteral)
                        continue;
                    if (type.GetValue(monoField) is EnumMirror value)
                        constants[monoField.Name] = value.Value;
                }

                return constants;
            }
        }

        public string? DebuggerDisplay =>
            GetAttributeArgument(
                "System.Diagnostics.DebuggerDisplayAttribute") as string;

        public IRuntimeType? DebuggerProxyType =>
            GetAttributeArgument(
                "System.Diagnostics.DebuggerTypeProxyAttribute") is
                    TypeMirror proxyType
                ? new MonoRuntimeType(proxyType)
                : null;

        internal TypeMirror Mirror => type;

        public bool IsAssignableFrom(IRuntimeType candidate)
        {
            if (!(candidate is MonoRuntimeType monoCandidate))
                return false;
            return type.IsAssignableFrom(monoCandidate.type);
        }

        private object? GetAttributeArgument(string attributeType)
        {
            var attribute = type.GetCustomAttributes(inherit: false)
                .FirstOrDefault(value =>
                    value.Constructor.DeclaringType.FullName == attributeType &&
                    value.ConstructorArguments.Count == 1);
            if (attribute == null)
                return null;
            var value = attribute.ConstructorArguments[0].Value;
            return value is StringMirror stringValue
                ? stringValue.Value
                : value;
        }

        private static RuntimeField CreateField(FieldInfoMirror field) =>
            new RuntimeField(
                field.Name,
                new MonoRuntimeType(field.FieldType),
                field.IsStatic,
                field.IsPublic,
                field.IsLiteral,
                field);

        private static RuntimeProperty CreateProperty(
            PropertyInfoMirror property)
        {
            var getter = property.GetGetMethod(true);
            var setter = property.GetSetMethod(true);
            return new RuntimeProperty(
                property.Name,
                new MonoRuntimeType(property.PropertyType),
                getter == null ? null : CreateMethod(getter),
                setter == null ? null : CreateMethod(setter),
                property);
        }

        private static RuntimeMethod CreateMethod(MethodMirror method)
        {
            var parameters = method
                .GetParameters()
                .Select(value => new RuntimeParameter(
                    value.Name ?? string.Empty,
                    new MonoRuntimeType(value.ParameterType)))
                .ToArray();
            return new RuntimeMethod(
                method.Name,
                new MonoRuntimeType(method.ReturnType),
                parameters,
                method.IsStatic,
                method.IsPublic,
                method.IsVirtual,
                method.IsSpecialName,
                method,
                method.DeclaringType.FullName);
        }
    }
}
