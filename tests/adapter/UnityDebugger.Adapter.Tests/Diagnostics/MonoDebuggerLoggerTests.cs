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
    }
}
