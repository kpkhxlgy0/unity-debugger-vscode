using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Mono;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class MonoRuntimeMappingTests
    {
        [Theory]
        [MemberData(nameof(MonoValues))]
        public void MonoValuesMapToProjectOwnedKinds(
            Value? value,
            object expected)
        {
            Assert.Equal(
                (RuntimeValueKind)expected,
                MonoRuntimeValue.Classify(value));
        }

        [Fact]
        public async Task InvocationOptionsPassThroughRuntimeBoundaryUnchanged()
        {
            var expected =
                InvokeOptions.DisableBreakpoints |
                InvokeOptions.SingleThreaded |
                InvokeOptions.Virtual;
            InvokeOptions? received = null;
            var value = new FakeRuntimeValue
            {
                InvokeHandler = (method, arguments, options, token) =>
                {
                    received = options;
                    return Task.FromResult<IRuntimeValue>(null!);
                },
            };

            await value.InvokeAsync(
                method: null!,
                arguments: Array.Empty<IRuntimeValue>(),
                options: expected,
                cancellationToken: CancellationToken.None);

            Assert.Equal(expected, received);
        }

        public static IEnumerable<object?[]> MonoValues()
        {
            return new[]
            {
                new object?[] { null, RuntimeValueKind.Null },
                new object?[]
                {
                    new PrimitiveValue(null!, null!),
                    RuntimeValueKind.Null,
                },
                new object?[]
                {
                    new PrimitiveValue(null!, 7),
                    RuntimeValueKind.Primitive,
                },
                new object?[]
                {
                    Uninitialized<StringMirror>(),
                    RuntimeValueKind.String,
                },
                new object?[]
                {
                    Uninitialized<EnumMirror>(),
                    RuntimeValueKind.Enum,
                },
                new object?[]
                {
                    Uninitialized<ArrayMirror>(),
                    RuntimeValueKind.Array,
                },
                new object?[]
                {
                    Uninitialized<StructMirror>(),
                    RuntimeValueKind.Struct,
                },
                new object?[]
                {
                    Uninitialized<ObjectMirror>(),
                    RuntimeValueKind.Object,
                },
                new object?[]
                {
                    Uninitialized<PointerValue>(),
                    RuntimeValueKind.Pointer,
                },
            };
        }

        private static T Uninitialized<T>() where T : Value =>
            (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
