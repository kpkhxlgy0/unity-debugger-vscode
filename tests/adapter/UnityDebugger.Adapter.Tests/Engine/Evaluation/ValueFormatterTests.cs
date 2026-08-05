using System;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class ValueFormatterTests
    {
        [Fact]
        public async Task OverriddenToStringUsesTwoSecondReferenceTimeout()
        {
            var fixture = ToStringFixture();
            TimeSpan? receivedTimeout = null;
            var formatter = new ValueFormatter(
                async (task, timeout, cancellationToken) =>
                {
                    receivedTimeout = timeout;
                    return await task;
                });

            var result = await formatter.FormatAsync(
                fixture.Target,
                EvaluationPolicy.Explicit,
                CancellationToken.None);

            Assert.Equal("Player", result);
            Assert.Equal(TimeSpan.FromMilliseconds(2000), receivedTimeout);
            Assert.Equal(
                InvokeOptions.DisableBreakpoints |
                InvokeOptions.SingleThreaded,
                fixture.ReceivedOptions());
        }

        [Fact]
        public async Task ToStringTimeoutFallsBackOnceWithoutPlaceholderUpdate()
        {
            var fixture = ToStringFixture();
            var formatter = new ValueFormatter(
                (task, timeout, cancellationToken) =>
                    Task.FromResult<IRuntimeValue?>(null));

            var result = await formatter.FormatAsync(
                fixture.Target,
                EvaluationPolicy.Explicit,
                CancellationToken.None);

            Assert.Equal("{Player}", result);
        }

        [Fact]
        public async Task DefaultObjectToStringFallsBackWithoutTargetInvocation()
        {
            var type = new FakeRuntimeType("Player");
            var target = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(type);
            target.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
                throw new Xunit.Sdk.XunitException(
                    "Default ToString invoked target code.");

            var result = await new ValueFormatter().FormatAsync(
                target,
                EvaluationPolicy.Explicit,
                CancellationToken.None);

            Assert.Equal("{Player}", result);
        }

        [Fact]
        public async Task SafePolicyNeverInvokesOverriddenToString()
        {
            var fixture = ToStringFixture();

            var result = await new ValueFormatter().FormatAsync(
                fixture.Target,
                EvaluationPolicy.Safe,
                CancellationToken.None);

            Assert.Equal("{Player}", result);
            Assert.Null(fixture.ReceivedOptions());
        }

        [Theory]
        [InlineData("hello\nworld", "\"hello\\nworld\"")]
        [InlineData(true, "true")]
        [InlineData(12.5, "12.5")]
        public async Task PrimitiveAndStringFormattingMatchesDebuggerDisplay(
            object value,
            string expected)
        {
            var runtimeValue = value is string stringValue
                ? String(stringValue)
                : Primitive(value);

            var result = await new ValueFormatter().FormatAsync(
                runtimeValue,
                EvaluationPolicy.Safe,
                CancellationToken.None);

            Assert.Equal(expected, result);
        }

        private static (
            FakeRuntimeValue Target,
            Func<InvokeOptions?> ReceivedOptions) ToStringFixture()
        {
            var stringType = new FakeRuntimeType("String", "System.String");
            var method = new RuntimeMethod(
                "ToString",
                stringType,
                Array.Empty<RuntimeParameter>(),
                isStatic: false,
                isPublic: true,
                isVirtual: true,
                isSpecialName: false,
                source: new object());
            var type = new FakeRuntimeType("Player")
            {
                Methods = new[] { method },
            };
            InvokeOptions? received = null;
            var target = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(type);
            target.InvokeHandler = (
                invokedMethod,
                arguments,
                options,
                cancellationToken) =>
            {
                received = options;
                return Task.FromResult<IRuntimeValue>(String("Player"));
            };
            return (target, () => received);
        }

        private static FakeRuntimeValue Primitive(object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(new FakeRuntimeType(
                    value.GetType().Name,
                    value.GetType().FullName))
                .WithPrimitive(value);

        private static FakeRuntimeValue String(string value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.String)
                .WithType(new FakeRuntimeType("String", "System.String"))
                .WithString(value);
    }
}
