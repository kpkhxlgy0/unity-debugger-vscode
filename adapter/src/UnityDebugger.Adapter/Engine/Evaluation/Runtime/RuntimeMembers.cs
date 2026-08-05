using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.Engine.Evaluation.Runtime
{
    internal sealed class RuntimeField
    {
        public RuntimeField(
            string name,
            IRuntimeType type,
            bool isStatic,
            bool isPublic,
            bool isLiteral,
            object source)
        {
            Name = name;
            Type = type;
            IsStatic = isStatic;
            IsPublic = isPublic;
            IsLiteral = isLiteral;
            Source = source;
        }

        public string Name { get; }
        public IRuntimeType Type { get; }
        public bool IsStatic { get; }
        public bool IsPublic { get; }
        public bool IsLiteral { get; }
        internal object Source { get; }
    }

    internal sealed class RuntimeProperty
    {
        public RuntimeProperty(
            string name,
            IRuntimeType type,
            RuntimeMethod? getter,
            RuntimeMethod? setter,
            object source)
        {
            Name = name;
            Type = type;
            Getter = getter;
            Setter = setter;
            Source = source;
        }

        public string Name { get; }
        public IRuntimeType Type { get; }
        public RuntimeMethod? Getter { get; }
        public RuntimeMethod? Setter { get; }
        internal object Source { get; }
    }

    internal sealed class RuntimeMethod
    {
        public RuntimeMethod(
            string name,
            IRuntimeType returnType,
            IReadOnlyList<RuntimeParameter> parameters,
            bool isStatic,
            bool isPublic,
            bool isVirtual,
            bool isSpecialName,
            object source)
        {
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
            IsStatic = isStatic;
            IsPublic = isPublic;
            IsVirtual = isVirtual;
            IsSpecialName = isSpecialName;
            Source = source;
        }

        public string Name { get; }
        public IRuntimeType ReturnType { get; }
        public IReadOnlyList<RuntimeParameter> Parameters { get; }
        public bool IsStatic { get; }
        public bool IsPublic { get; }
        public bool IsVirtual { get; }
        public bool IsSpecialName { get; }
        internal object Source { get; }
    }

    internal sealed class RuntimeParameter
    {
        public RuntimeParameter(string name, IRuntimeType type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public IRuntimeType Type { get; }
    }

    internal sealed class NullRuntimeType : IRuntimeType
    {
        public static NullRuntimeType Instance { get; } = new NullRuntimeType();

        private NullRuntimeType()
        {
        }

        public string Name => "null";
        public string FullName => "null";
        public bool IsEnum => false;
        public bool IsPrimitive => false;
        public bool IsValueType => false;
        public bool IsArray => false;
        public IRuntimeType? BaseType => null;
        public IReadOnlyList<RuntimeField> Fields => Array.Empty<RuntimeField>();
        public IReadOnlyList<RuntimeProperty> Properties =>
            Array.Empty<RuntimeProperty>();
        public IReadOnlyList<RuntimeMethod> Methods => Array.Empty<RuntimeMethod>();
        public IReadOnlyDictionary<string, object> EnumConstants =>
            new Dictionary<string, object>();

        public bool IsAssignableFrom(IRuntimeType candidate) =>
            ReferenceEquals(this, candidate);
    }
}
