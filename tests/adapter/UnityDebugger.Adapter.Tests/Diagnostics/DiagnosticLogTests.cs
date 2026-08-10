using System;
using System.Collections.Generic;
using System.IO;
using UnityDebugger.Adapter.Diagnostics;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Diagnostics
{
    public sealed class DiagnosticLogTests
    {
        [Fact]
        public void ResolveLogDirectory_uses_final_product_directory()
        {
            Assert.Equal(
                @"C:\Local\unity-debugger-pure\logs",
                DiagnosticLog.ResolveLogDirectory(@"C:\Local"));
        }

        [Theory]
        [InlineData("expression")]
        [InlineData("result")]
        [InlineData("sourceText")]
        [InlineData("variableValue")]
        public void Write_rejects_sensitive_field_names(string field)
        {
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(
                    @"C:\Users\alice",
                    @"H:\secret-project")))
            {
                Assert.Throws<ArgumentException>(
                    () => log.Write(
                        "test.event",
                        new Dictionary<string, object>
                        {
                            [field] = "SECRET",
                        }));
                Assert.DoesNotContain("SECRET", writer.ToString());
            }
        }

        [Fact]
        public void Write_allows_only_known_fields_and_redacts_paths()
        {
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(
                    @"C:\Users\alice",
                    @"H:\secret-project")))
            {
                log.Write(
                    "adapter.attach",
                    new Dictionary<string, object>
                    {
                        ["host"] = "127.0.0.1",
                        ["port"] = 56234,
                        ["projectVersion"] =
                            @"H:\secret-project\2022.3.62t11",
                    });

                var text = writer.ToString();
                Assert.Contains("event=adapter.attach", text);
                Assert.Contains("host=127.0.0.1", text);
                Assert.Contains("<workspace>", text);
                Assert.DoesNotContain("secret-project", text);
                Assert.Throws<ArgumentException>(
                    () => log.Write(
                        "test.event",
                        new Dictionary<string, object>
                        {
                            ["arbitrary"] = "value",
                        }));
            }
        }

        [Fact]
        public void Write_allows_opaque_exception_event_identity_fields()
        {
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(
                    @"C:\Users\alice",
                    @"H:\secret-project")))
            {
                log.Write(
                    "debugger.exception.stop",
                    new Dictionary<string, object>
                    {
                        ["threadId"] = 42,
                        ["exceptionObjectId"] = 1001,
                        ["exceptionRequestId"] = 7,
                        ["eventCount"] = 1,
                        ["stopKind"] = "always",
                    });

                var text = writer.ToString();
                Assert.Contains(
                    "event=debugger.exception.stop",
                    text);
                Assert.Contains("threadId=42", text);
                Assert.Contains("exceptionObjectId=1001", text);
                Assert.Contains("exceptionRequestId=7", text);
                Assert.Contains("eventCount=1", text);
                Assert.Contains("stopKind=always", text);
            }
        }
    }
}
