using System;
using System.Collections.Generic;
using System.IO;
using UnityDebugger.Adapter.Diagnostics;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Diagnostics
{
    public sealed class MonoDebuggerLoggerTests
    {
        [Fact]
        public void Logger_discards_messages_arguments_and_exception_text()
        {
            const string Secret = "SECRET-UPSTREAM-DATA";
            var records = new List<string>();
            var logger = new MonoDebuggerLogger(
                (eventName, exceptionType) =>
                    records.Add($"{eventName}:{exceptionType ?? "<none>"}"));
            var originalOut = Console.Out;
            var originalError = Console.Error;
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            {
                try
                {
                    Console.SetOut(output);
                    Console.SetError(error);
                    logger.LogError(
                        Secret,
                        new InvalidOperationException(Secret));
                    logger.LogAndShowException(
                        Secret,
                        new ArgumentException(Secret));
                    logger.LogMessage(
                        Secret + " {0}",
                        new object[] { Secret });

                    Assert.Null(logger.GetNewDebuggerLogFilename());
                    Assert.Equal("", output.ToString());
                    Assert.Equal("", error.ToString());
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            }

            Assert.Equal(
                new[]
                {
                    "mono-debugger-log:InvalidOperationException",
                    "mono-debugger-log:ArgumentException",
                    "mono-debugger-log:<none>",
                },
                records);
            Assert.DoesNotContain(
                records,
                record => record.Contains(Secret));
        }

        [Fact]
        public void Logger_preserves_only_fixed_internal_event_names()
        {
            const string Secret = "SECRET-UPSTREAM-DATA";
            var records = new List<string>();
            var logger = new MonoDebuggerLogger(
                (eventName, exceptionType) =>
                    records.Add($"{eventName}:{exceptionType ?? "<none>"}"));

            logger.LogInternalEvent(
                "unity-debugger.breakpoint.facade.status.bound.mapped");
            logger.LogInternalEvent(
                "unity-debugger.SECRET-UPSTREAM-DATA");
            logger.LogMessage(
                Secret,
                Array.Empty<object>());

            Assert.Equal(
                new[]
                {
                    "unity-debugger.breakpoint.facade.status.bound.mapped:<none>",
                    "mono-debugger-log:<none>",
                    "mono-debugger-log:<none>",
                },
                records);
            Assert.DoesNotContain(
                records,
                record => record.Contains(Secret));
        }

        [Fact]
        public void Logger_rejects_prefixed_upstream_text()
        {
            const string Secret =
                "unity-debugger.SECRET path=C:\\private value=42\r\nnext";
            var records = new List<string>();
            var logger = new MonoDebuggerLogger(
                (eventName, exceptionType) =>
                    records.Add($"{eventName}:{exceptionType ?? "<none>"}"));

            logger.LogMessage(Secret, Array.Empty<object>());

            Assert.Equal(
                new[] { "mono-debugger-log:<none>" },
                records);
            Assert.DoesNotContain(
                records,
                record => record.Contains("SECRET"));
            Assert.DoesNotContain(
                records,
                record => record.Contains("private"));
            Assert.DoesNotContain(
                records,
                record => record.Contains("42"));
        }
    }
}
