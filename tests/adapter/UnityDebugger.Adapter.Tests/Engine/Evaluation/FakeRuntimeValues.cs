using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using Xunit.Sdk;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    internal sealed class FakeRuntimeType : IRuntimeType
    {
        private readonly Dictionary<string, object> enumConstants =
            new Dictionary<string, object>();

        public FakeRuntimeType(string name, string? fullName = null)
        {
            Name = name;
            FullName = fullName ?? name;
        }

        public string Name { get; }
        public string FullName { get; }
        public bool IsEnum { get; private set; }
        public bool IsPrimitive { get; set; }
        public bool IsValueType { get; set; }
        public bool IsArray { get; set; }
        public IRuntimeType? BaseType { get; set; }
        public IReadOnlyList<RuntimeField> Fields { get; set; } =
            Array.Empty<RuntimeField>();
        public IReadOnlyList<RuntimeProperty> Properties { get; set; } =
            Array.Empty<RuntimeProperty>();
        public IReadOnlyList<RuntimeMethod> Methods { get; set; } =
            Array.Empty<RuntimeMethod>();
        public IReadOnlyDictionary<string, object> EnumConstants =>
            enumConstants;

        public static FakeRuntimeType Enum(
            string name,
            params (string Name, object Value)[] constants)
        {
            var type = new FakeRuntimeType(name)
            {
                IsEnum = true,
                IsValueType = true,
            };
            foreach (var constant in constants)
                type.enumConstants.Add(constant.Name, constant.Value);
            return type;
        }

        public FakeRuntimeValue EnumValue(string name)
        {
            if (!enumConstants.TryGetValue(name, out var value))
                throw new XunitException($"Unknown fake enum constant '{name}'.");
            return new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Enum)
                .WithType(this)
                .WithPrimitive(value);
        }

        public bool IsAssignableFrom(IRuntimeType candidate) =>
            ReferenceEquals(this, candidate) || FullName == candidate.FullName;
    }

    internal sealed class FakeRuntimeValue : IRuntimeValue
    {
        private RuntimeValueKind? kind;
        private IRuntimeType? type;
        private object? primitive;
        private bool primitiveConfigured;
        private string? stringValue;
        private bool stringConfigured;
        private long? address;
        private bool addressConfigured;

        public RuntimeValueKind Kind =>
            kind ?? throw Unconfigured(nameof(Kind));

        public IRuntimeType Type =>
            type ?? throw Unconfigured(nameof(Type));

        public object? Primitive => primitiveConfigured
            ? primitive
            : throw Unconfigured(nameof(Primitive));

        public string? String => stringConfigured
            ? stringValue
            : throw Unconfigured(nameof(String));

        public long? Address => addressConfigured
            ? address
            : throw Unconfigured(nameof(Address));

        public int? ConfiguredLength { get; set; }
        public Func<RuntimeField, IRuntimeValue>? GetFieldHandler { get; set; }
        public Action<RuntimeField, IRuntimeValue>? SetFieldHandler { get; set; }
        public Func<int, IRuntimeValue>? GetElementHandler { get; set; }
        public Func<
            RuntimeMethod,
            IReadOnlyList<IRuntimeValue>,
            InvokeOptions,
            CancellationToken,
            Task<IRuntimeValue>>? InvokeHandler { get; set; }

        public int Length =>
            ConfiguredLength ?? throw Unconfigured(nameof(Length));

        public FakeRuntimeValue WithKind(RuntimeValueKind value)
        {
            kind = value;
            return this;
        }

        public FakeRuntimeValue WithType(IRuntimeType value)
        {
            type = value;
            return this;
        }

        public FakeRuntimeValue WithPrimitive(object? value)
        {
            primitive = value;
            primitiveConfigured = true;
            return this;
        }

        public FakeRuntimeValue WithString(string? value)
        {
            stringValue = value;
            stringConfigured = true;
            return this;
        }

        public FakeRuntimeValue WithAddress(long? value)
        {
            address = value;
            addressConfigured = true;
            return this;
        }

        public IRuntimeValue GetField(RuntimeField field) =>
            GetFieldHandler?.Invoke(field) ??
            throw Unconfigured(nameof(GetField));

        public void SetField(RuntimeField field, IRuntimeValue value)
        {
            if (SetFieldHandler == null)
                throw Unconfigured(nameof(SetField));
            SetFieldHandler(field, value);
        }

        public IRuntimeValue GetElement(int index) =>
            GetElementHandler?.Invoke(index) ??
            throw Unconfigured(nameof(GetElement));

        public Task<IRuntimeValue> InvokeAsync(
            RuntimeMethod method,
            IReadOnlyList<IRuntimeValue> arguments,
            InvokeOptions options,
            CancellationToken cancellationToken) =>
            InvokeHandler?.Invoke(
                method,
                arguments,
                options,
                cancellationToken) ??
            throw Unconfigured(nameof(InvokeAsync));

        private static XunitException Unconfigured(string member) =>
            new XunitException($"Fake runtime member '{member}' was not configured.");
    }
}
