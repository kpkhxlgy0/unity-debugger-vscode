using System.Collections.Generic;

namespace UnityDebugger.Adapter.Engine.Evaluation.Runtime
{
    internal interface IRuntimeType
    {
        string Name { get; }
        string FullName { get; }
        bool IsEnum { get; }
        bool IsPrimitive { get; }
        bool IsValueType { get; }
        bool IsArray { get; }
        IRuntimeType? ElementType { get; }
        IRuntimeType? BaseType { get; }
        IReadOnlyList<IRuntimeType> Interfaces { get; }
        IReadOnlyList<RuntimeField> Fields { get; }
        IReadOnlyList<RuntimeProperty> Properties { get; }
        IReadOnlyList<RuntimeMethod> Methods { get; }
        IReadOnlyDictionary<string, object> EnumConstants { get; }
        string? DebuggerDisplay { get; }
        IRuntimeType? DebuggerProxyType { get; }
        bool IsAssignableFrom(IRuntimeType candidate);
    }
}
