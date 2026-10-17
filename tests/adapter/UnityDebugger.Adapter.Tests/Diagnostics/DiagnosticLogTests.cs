using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
        public void OpenSecondary_mirrors_subsequent_lines_and_preserves_the_allowlist()
        {
            using (var directory = new TemporaryDirectory())
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(
                    @"C:\Users\alice",
                    @"H:\secret-project")))
            {
                var secondaryPath = Path.Combine(
                    directory.Path,
                    "secondary.log");
                log.OpenSecondary(secondaryPath);
                log.Write(
                    "attach",
                    new Dictionary<string, object>
                    {
                        ["projectVersion"] =
                            @"H:\secret-project\2022.3.62t11",
                    });
                Assert.Throws<ArgumentException>(
                    () => log.Write(
                        "attach",
                        new Dictionary<string, object>
                        {
                            ["expressionText"] = "secret",
                        }));
                log.Dispose();

                Assert.Contains("event=attach", writer.ToString());
                var secondary = File.ReadAllLines(secondaryPath);
                Assert.Contains(
                    secondary,
                    line => line.Contains("event=attach"));
                Assert.Contains(
                    secondary,
                    line => line.Contains("<workspace>"));
                Assert.DoesNotContain(
                    secondary,
                    line => line.Contains("secret"));
            }
        }

        [Fact]
        public void OpenSecondary_is_idempotent_for_the_same_path()
        {
            using (var directory = new TemporaryDirectory())
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(@"C:\Users\alice", null)))
            {
                var secondaryPath = Path.Combine(
                    directory.Path,
                    "secondary.log");
                log.OpenSecondary(secondaryPath);
                log.OpenSecondary(secondaryPath);
                log.Write(
                    "attach",
                    new Dictionary<string, object>());
                log.Dispose();

                var secondary = File.ReadAllLines(secondaryPath);
                Assert.Single(secondary);
                Assert.Contains("event=attach", secondary[0]);
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

        // The timestamp is sampled while the write lock is held, so the order
        // of the lines in a file always follows their timestamps. If
        // formatting moves back outside the lock, the second writer below runs
        // to completion while the first is still formatting, and the file can
        // then hold the later timestamp first.
        [Fact]
        public void Write_formats_inside_the_lock_so_file_order_follows_timestamps()
        {
            using (var writer = new StringWriter())
            using (var fields = new GatedFields("port", 56234))
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(@"C:\Users\alice", null)))
            {
                Exception? firstFailure = null;
                var first = new Thread(
                    () =>
                    {
                        try
                        {
                            log.Write("first.event", fields);
                        }
                        catch (Exception exception)
                        {
                            firstFailure = exception;
                        }
                    });
                first.Start();
                Assert.True(
                    fields.FormattingEntered.Wait(
                        TimeSpan.FromSeconds(30)));

                Exception? secondFailure = null;
                var secondCompleted = new ManualResetEventSlim(false);
                var second = new Thread(
                    () =>
                    {
                        try
                        {
                            log.Write(
                                "second.event",
                                new Dictionary<string, object>());
                        }
                        catch (Exception exception)
                        {
                            secondFailure = exception;
                        }
                        finally
                        {
                            secondCompleted.Set();
                        }
                    });
                second.Start();
                try
                {
                    Assert.False(
                        secondCompleted.Wait(TimeSpan.FromSeconds(2)),
                        "a second writer completed while the first was " +
                        "still formatting, so the timestamp is sampled " +
                        "outside the lock");
                }
                finally
                {
                    fields.ReleaseFormatting.Set();
                }

                Assert.True(
                    secondCompleted.Wait(TimeSpan.FromSeconds(30)));
                Assert.True(first.Join(TimeSpan.FromSeconds(30)));
                Assert.True(second.Join(TimeSpan.FromSeconds(30)));
                Assert.Null(firstFailure);
                Assert.Null(secondFailure);

                var text = writer.ToString();
                var firstIndex = text.IndexOf(
                    "event=first.event",
                    StringComparison.Ordinal);
                var secondIndex = text.IndexOf(
                    "event=second.event",
                    StringComparison.Ordinal);
                Assert.True(firstIndex >= 0, text);
                Assert.True(secondIndex >= 0, text);
                Assert.True(
                    firstIndex < secondIndex,
                    "the lines are not in timestamp order: " + text);
            }
        }

        // Main disposes the log from its finally, after the session has
        // already ended, so a diagnostics failure there would replace a
        // finished debug session with an unhandled shutdown exception.
        [Fact]
        public void Shutdown_dispose_swallows_a_failing_log()
        {
            var log = new ThrowingDiagnosticLog();

            Program.SafeDispose(log);

            Assert.True(log.DisposeAttempted);
        }

        // The guard exists to swallow a failure, not to skip disposal: a
        // secondary sink that is never released keeps its file locked.
        [Fact]
        public void Shutdown_dispose_still_closes_a_healthy_log()
        {
            using (var directory = new TemporaryDirectory())
            {
                var secondaryPath = Path.Combine(
                    directory.Path,
                    "secondary.log");
                var log = new DiagnosticLog(
                    new StringWriter(),
                    new PathRedactor(@"C:\Users\alice", null));
                log.OpenSecondary(secondaryPath);
                log.Write("attach", new Dictionary<string, object>());

                Program.SafeDispose(log);

                // Opening exclusively succeeds only once Dispose released the
                // secondary writer, so a guard that skipped disposal fails
                // here.
                using (new FileStream(
                    secondaryPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None))
                {
                }
                Assert.Contains(
                    File.ReadAllLines(secondaryPath),
                    line => line.Contains("event=attach"));
            }
        }

        // A field bag whose enumeration blocks, so a test can hold a writer
        // inside FormatLine and observe what the log does with its lock.
        // Write's allowlist gate reads Keys and never blocks; only formatting
        // enumerates the bag itself.
        private sealed class GatedFields
            : IReadOnlyDictionary<string, object>, IDisposable
        {
            private readonly Dictionary<string, object> inner;
            private readonly ManualResetEventSlim entered =
                new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim release =
                new ManualResetEventSlim(false);

            public GatedFields(string name, object value)
            {
                inner = new Dictionary<string, object>
                {
                    [name] = value,
                };
            }

            public ManualResetEventSlim FormattingEntered => entered;

            public ManualResetEventSlim ReleaseFormatting => release;

            public IEnumerable<string> Keys => inner.Keys;

            public IEnumerable<object> Values => inner.Values;

            public int Count => inner.Count;

            public object this[string key] => inner[key];

            public bool ContainsKey(string key) =>
                inner.ContainsKey(key);

            public bool TryGetValue(string key, out object value)
            {
                return inner.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<string, object>>
                GetEnumerator()
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
                return inner.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();

            public void Dispose()
            {
                entered.Dispose();
                release.Dispose();
            }
        }

        private sealed class ThrowingDiagnosticLog : IDiagnosticLog
        {
            public bool DisposeAttempted { get; private set; }

            public void Write(
                string eventName,
                IReadOnlyDictionary<string, object> fields)
            {
            }

            public void OpenSecondary(string path)
            {
            }

            public void Dispose()
            {
                DisposeAttempted = true;
                throw new IOException("LOGFILE_WENT_AWAY");
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "unity-debugger-pure-diagnostic-log-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
