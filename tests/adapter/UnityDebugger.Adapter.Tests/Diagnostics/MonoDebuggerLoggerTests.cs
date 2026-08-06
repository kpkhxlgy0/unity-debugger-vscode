using System;
using UnityDebugger.Adapter.Diagnostics;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Diagnostics
{
    public sealed class MonoDebuggerLoggerTests
    {
        [Fact]
        public void ErrorLoggingEmitsOnlyTheExceptionType()
        {
            string? eventName = null;
            string? exceptionType = null;
            var logger = new MonoDebuggerLogger(
                (name, type) =>
                {
                    eventName = name;
                    exceptionType = type;
                });

            logger.LogError(
                @"secret expression at D:\Private\Fixture.cs",
                new InvalidOperationException("secret value"));

            Assert.Equal("mono-debugger-log", eventName);
            Assert.Equal("InvalidOperationException", exceptionType);
        }

        [Fact]
        public void MessageLoggingDoesNotForwardMessageContents()
        {
            string? eventName = null;
            string? exceptionType = "not-null";
            var logger = new MonoDebuggerLogger(
                (name, type) =>
                {
                    eventName = name;
                    exceptionType = type;
                });

            logger.LogMessage("value={0}", "secret");

            Assert.Equal("mono-debugger-log", eventName);
            Assert.Null(exceptionType);
        }
    }
}
