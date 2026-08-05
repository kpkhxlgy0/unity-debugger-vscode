using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class InspectionRequestTests
    {
        [Fact]
        public void Inspection_requests_return_stable_safe_dap_models()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            var threadMessages = Run(
                fixture.Session,
                Request("threads", new { }),
                Request("threads", new { }));
            var stackMessages = Run(
                fixture.Session,
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));
            var frameId = Required<int>(
                Response(stackMessages, "stackTrace")
                    .SelectToken("body.stackFrames[0].id"));
            var scopeMessages = Run(
                fixture.Session,
                Request("scopes", new { frameId }));
            var scopeReference = Required<int>(
                Response(scopeMessages, "scopes")
                    .SelectToken("body.scopes[0].variablesReference"));
            var variableMessages = Run(
                fixture.Session,
                Request(
                    "variables",
                    new { variablesReference = scopeReference }));
            var evaluationMessages = Run(
                fixture.Session,
                Request(
                    "evaluate",
                    new
                    {
                        frameId,
                        expression = "player.Health",
                        context = "watch",
                    }));
            var messages = threadMessages
                .Concat(stackMessages)
                .Concat(scopeMessages)
                .Concat(variableMessages)
                .Concat(evaluationMessages)
                .ToArray();

            var threadResponses = Responses(messages, "threads");
            Assert.Equal(2, threadResponses.Count);
            Assert.Equal(
                Required<int>(
                    threadResponses[0].SelectToken("body.threads[1].id")),
                Required<int>(
                    threadResponses[1].SelectToken("body.threads[1].id")));
            Assert.Equal(
                "Worker",
                Required<string>(
                    threadResponses[0].SelectToken(
                        "body.threads[1].name")));

            var frame = Response(messages, "stackTrace")
                .SelectToken("body.stackFrames[0]")!;
            Assert.Equal(27, Required<int>(frame["line"]));
            Assert.Equal(
                Path.GetFullPath(fixture.SourcePath),
                Path.GetFullPath(Required<string>(
                    frame.SelectToken("source.path"))),
                ignoreCase: true);

            Assert.True(scopeReference > 0);

            var variables = (JArray)Response(messages, "variables")
                .SelectToken("body.variables")!;
            Assert.Equal(101, variables.Count);
            Assert.Equal("...", Required<string>(variables[100]["name"]));
            Assert.Equal(
                0,
                Required<int>(variables[100]["variablesReference"]));
            Assert.True(Required<int>(
                variables[1]["variablesReference"]) > 0);

            var evaluation = Response(messages, "evaluate");
            Assert.True(Required<bool>(evaluation["success"]));
            Assert.Equal(
                "42",
                Required<string>(
                    evaluation.SelectToken("body.result")));
            Assert.True(Required<int>(
                evaluation.SelectToken(
                    "body.variablesReference")) > 0);
            Assert.Equal(1, fixture.Backend.EvaluateCount);
            Assert.Equal("player.Health", fixture.Backend.LastExpression);
            Assert.Equal(
                BackendEvaluationMode.Explicit,
                fixture.Backend.LastScopesMode);
            Assert.Equal(
                BackendEvaluationMode.Explicit,
                fixture.Backend.LastVariablesMode);
        }

        [Fact]
        public void Stale_inspection_handles_are_ignored_without_backend_calls()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            var messages = Run(
                fixture.Session,
                Request(
                    "stackTrace",
                    new { threadId = 999, startFrame = 0, levels = 20 }),
                Request("scopes", new { frameId = 999 }),
                Request(
                    "variables",
                    new { variablesReference = 999 }));

            Assert.False(Required<bool>(
                Response(messages, "stackTrace")["success"]));
            var scopes = Response(messages, "scopes");
            Assert.True(Required<bool>(scopes["success"]));
            Assert.Empty((JArray)scopes.SelectToken("body.scopes")!);
            var variables = Response(messages, "variables");
            Assert.True(Required<bool>(variables["success"]));
            Assert.Empty((JArray)variables.SelectToken("body.variables")!);
            Assert.Equal(0, fixture.Backend.StackTraceCount);
            Assert.Equal(0, fixture.Backend.ScopesCount);
            Assert.Equal(0, fixture.Backend.VariablesCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task Variables_from_an_old_stop_are_discarded_silently()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            Run(fixture.Session, Request("threads", new { }));
            var stackTrace = Run(
                fixture.Session,
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));
            var frameId = Required<int>(
                Response(stackTrace, "stackTrace")
                    .SelectToken("body.stackFrames[0].id"));
            var scopes = Run(
                fixture.Session,
                Request("scopes", new { frameId }));
            var variablesReference = Required<int>(
                Response(scopes, "scopes")
                    .SelectToken("body.scopes[0].variablesReference"));
            fixture.Backend.VariablesEnteredSignal =
                new System.Threading.ManualResetEventSlim();
            fixture.Backend.ContinueVariablesSignal =
                new System.Threading.ManualResetEventSlim();
            var request = System.Threading.Tasks.Task.Run(
                () => Run(
                    fixture.Session,
                    Request(
                        "variables",
                        new { variablesReference })));
            try
            {
                Assert.True(
                    fixture.Backend.VariablesEnteredSignal.Wait(
                        TimeSpan.FromSeconds(1)));
                fixture.Backend.RaiseReloadStarted();
                fixture.Backend.RaiseReloadCompleted();
                fixture.Backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        42,
                        null));
            }
            finally
            {
                fixture.Backend.ContinueVariablesSignal.Set();
            }

            var messages = await request;
            var response = Response(messages, "variables");
            Assert.True(Required<bool>(response["success"]));
            Assert.Empty((JArray)response.SelectToken("body.variables")!);
        }

        [Fact]
        public void Scopes_and_parent_variables_are_loaded_lazily()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            Run(
                fixture.Session,
                Request("threads", new { }));
            var stackTrace = Run(
                fixture.Session,
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));
            var frameId = Required<int>(
                Response(stackTrace, "stackTrace")
                    .SelectToken("body.stackFrames[0].id"));

            var scopes = Run(
                fixture.Session,
                Request("scopes", new { frameId }));

            Assert.Equal(0, fixture.Backend.VariablesCount);
            var localsReference = Required<int>(
                Response(scopes, "scopes")
                    .SelectToken("body.scopes[0].variablesReference"));

            var variables = Run(
                fixture.Session,
                Request(
                    "variables",
                    new { variablesReference = localsReference }));

            Assert.Equal(1, fixture.Backend.VariablesCount);
            var playerReference = Required<int>(
                Response(variables, "variables")
                    .SelectToken("body.variables[1].variablesReference"));
            Assert.True(playerReference > 0);
            Assert.Equal(1, fixture.Backend.VariablesCount);

            Run(
                fixture.Session,
                Request(
                    "variables",
                    new { variablesReference = playerReference }));

            Assert.Equal(2, fixture.Backend.VariablesCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task Slow_hover_does_not_block_step_over()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            Run(fixture.Session, Request("threads", new { }));
            var stackTrace = Run(
                fixture.Session,
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));
            var frameId = Required<int>(
                Response(stackTrace, "stackTrace")
                    .SelectToken("body.stackFrames[0].id"));
            fixture.Backend.EvaluateEnteredSignal =
                new System.Threading.ManualResetEventSlim();
            fixture.Backend.ContinueEvaluateSignal =
                new System.Threading.ManualResetEventSlim();
            System.Threading.Tasks.Task<IReadOnlyList<JObject>> run =
                System.Threading.Tasks.Task.Run(
                    () => Run(
                        fixture.Session,
                        Request(
                            "evaluate",
                            new
                            {
                                frameId,
                                expression = "slow.Getter",
                                context = "hover",
                            }),
                        Request("next", new { threadId = 1 })));
            try
            {
                Assert.True(
                    System.Threading.SpinWait.SpinUntil(
                        () => fixture.Backend.StepOverCount == 1,
                        TimeSpan.FromMilliseconds(500)));
            }
            finally
            {
                fixture.Backend.ContinueEvaluateSignal.Set();
            }

            var messages = await run;
            Assert.True(Required<bool>(
                Response(messages, "next")["success"]));
        }

        [Fact]
        public void Stack_trace_omits_source_for_unavailable_frame()
        {
            var fixture = Fixture();
            fixture.Backend.Frames.Insert(
                0,
                new BackendStackFrame(
                    3000,
                    42,
                    "UnityEngine.PlayerLoop",
                    string.Empty,
                    0,
                    1));

            AttachAndStop(fixture);
            var messages = Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));

            var frames = (JArray)Response(messages, "stackTrace")
                .SelectToken("body.stackFrames")!;
            var unavailable = frames[0]!;
            Assert.Equal(
                "UnityEngine.PlayerLoop",
                Required<string>(unavailable["name"]));
            Assert.Equal(
                "subtle",
                Required<string>(unavailable["presentationHint"]));
            Assert.Null(unavailable["source"]);
            Assert.Equal(
                Path.GetFullPath(fixture.SourcePath),
                Path.GetFullPath(Required<string>(
                    frames[1]!.SelectToken("source.path"))),
                ignoreCase: true);
        }

        [Theory]
        [InlineData("hover", (int)BackendEvaluationMode.Explicit)]
        [InlineData("watch", (int)BackendEvaluationMode.Explicit)]
        [InlineData("repl", (int)BackendEvaluationMode.Explicit)]
        public void Evaluation_context_selects_backend_mode(
            string context,
            int expectedModeValue)
        {
            var expectedMode =
                (BackendEvaluationMode)expectedModeValue;
            var fixture = Fixture();
            AttachAndStop(fixture);
            var messages = Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }),
                Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context,
                    }));

            Assert.True(Required<bool>(
                Response(messages, "evaluate")["success"]));
            Assert.Equal(1, fixture.Backend.EvaluateCount);
            Assert.Equal(expectedMode, fixture.Backend.LastEvaluationMode);
        }

        [Fact]
        public void Disabled_implicit_evaluation_keeps_automatic_inspection_safe()
        {
            var fixture = Fixture();
            AttachAndStop(fixture, false);
            var messages = Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }),
                Request("scopes", new { frameId = 1 }),
                Request("variables", new { variablesReference = 1 }),
                Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "hover",
                    }));

            Assert.True(Required<bool>(
                Response(messages, "evaluate")["success"]));
            Assert.Equal(
                BackendEvaluationMode.Safe,
                fixture.Backend.LastScopesMode);
            Assert.Equal(
                BackendEvaluationMode.Safe,
                fixture.Backend.LastVariablesMode);
            Assert.Equal(
                BackendEvaluationMode.Safe,
                fixture.Backend.LastEvaluationMode);

            var watch = Run(
                fixture.Session,
                Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "watch",
                    }));

            Assert.True(Required<bool>(
                Response(watch, "evaluate")["success"]));
            Assert.Equal(
                BackendEvaluationMode.Explicit,
                fixture.Backend.LastEvaluationMode);
        }

        [Fact]
        public void Unknown_evaluation_context_is_rejected_without_backend_call()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            var messages = Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }),
                Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "SECRET_EXPRESSION",
                        context = "clipboard",
                    }));

            var response = Response(messages, "evaluate");
            Assert.False(Required<bool>(response["success"]));
            Assert.Contains(
                "not supported",
                Required<string>(response["message"]));
            Assert.DoesNotContain(
                "SECRET_EXPRESSION",
                Required<string>(response["message"]));
            Assert.Equal(0, fixture.Backend.EvaluateCount);
        }

        [Fact]
        public void Continue_preserves_handles_until_the_next_stop()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));

            Run(
                fixture.Session,
                Request("continue", new { threadId = 1 }));
            fixture.Backend.RaiseContinued();
            var afterContinue = Run(
                fixture.Session,
                Request("scopes", new { frameId = 1 }));
            Assert.True(Required<bool>(
                Response(afterContinue, "scopes")["success"]));

            var scopesBeforeNextStop = fixture.Backend.ScopesCount;
            fixture.Backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));
            var afterNextStop = Run(
                fixture.Session,
                Request("scopes", new { frameId = 1 }));
            var response = Response(afterNextStop, "scopes");
            Assert.True(Required<bool>(response["success"]));
            Assert.Empty(response["body"]!["scopes"]!);
            Assert.Equal(
                scopesBeforeNextStop,
                fixture.Backend.ScopesCount);
        }

        [Fact]
        public void Inspection_requests_after_resume_are_not_dap_state_gated()
        {
            var fixture = Fixture();
            AttachAndStop(fixture);
            Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }),
                Request("scopes", new { frameId = 1 }));
            var stackTraceCount = fixture.Backend.StackTraceCount;
            var scopesCount = fixture.Backend.ScopesCount;
            var variablesCount = fixture.Backend.VariablesCount;
            var evaluateCount = fixture.Backend.EvaluateCount;

            var messages = Run(
                fixture.Session,
                Request("stepIn", new { threadId = 1 }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }),
                Request("scopes", new { frameId = 1 }),
                Request("variables", new { variablesReference = 1 }),
                Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "hover",
                    }));

            Assert.True(Required<bool>(
                Response(messages, "stepIn")["success"]));
            Assert.All(
                new[] { "stackTrace", "scopes", "variables", "evaluate" },
                command =>
                {
                    var response = Response(messages, command);
                    Assert.True(Required<bool>(response["success"]));
                });
            Assert.True(fixture.Backend.StackTraceCount > stackTraceCount);
            Assert.True(fixture.Backend.ScopesCount > scopesCount);
            Assert.True(fixture.Backend.VariablesCount > variablesCount);
            Assert.True(fixture.Backend.EvaluateCount > evaluateCount);
        }

        private static InspectionFixture Fixture()
        {
            var workspace = FindWorkspace();
            var sourcePath = Path.Combine(
                workspace,
                "adapter",
                "src",
                "UnityDebugger.Adapter",
                "Program.cs");
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            backend.Threads.Add(
                new BackendThread((long)int.MaxValue + 200L, "Worker"));
            backend.Frames.Add(
                new BackendStackFrame(
                    3001,
                    42,
                    "Player.Update()",
                    sourcePath.Replace('\\', '/'),
                    27,
                    3));
            backend.Scopes.Add(
                new BackendScope("Locals", 9001, false));

            var variables = new List<BackendVariable>
            {
                new BackendVariable("health", "42", "System.Int32", 0),
                new BackendVariable("player", "{Player}", "Player", 9002),
            };
            for (var index = 0; index < 100; index++)
            {
                variables.Add(
                    new BackendVariable(
                        "extra" + index,
                        index.ToString(),
                        "System.Int32",
                        0));
            }
            backend.VariablesByReference[9001] = variables;
            backend.VariablesByReference[9002] = new[]
            {
                new BackendVariable(
                    "Name",
                    "\"Hero\"",
                    "System.String",
                    0),
            };
            backend.VariablesByReference[9003] = new[]
            {
                new BackendVariable(
                    "Value",
                    "42",
                    "System.Int32",
                    0),
            };
            backend.EvaluationResult =
                new BackendEvaluationResult("42", "System.Int32", 9003);
            return new InspectionFixture(
                backend,
                new UnityDebugSession(() => backend),
                workspace,
                sourcePath);
        }

        private static string FindWorkspace()
        {
            var directory = new DirectoryInfo(
                AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(
                    Path.Combine(directory.FullName, "UnityDebugger.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new InvalidOperationException(
                "Could not locate test workspace.");
        }

        private static JObject Initialize() =>
            Request(
                "initialize",
                new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                });

        private static JObject Attach(
            string workspace,
            bool? enableImplicitEvaluation = null)
        {
            var request = Request(
                "attach",
                new
                {
                    __processId = 1234,
                    __host = "127.0.0.1",
                    __port = 56234,
                    __workspaceRoot = workspace,
                    __projectVersion = "2022.3.62t11",
                });
            if (enableImplicitEvaluation.HasValue)
            {
                request["arguments"]!["__enableImplicitEvaluation"] =
                    enableImplicitEvaluation.Value;
            }
            return request;
        }

        private static void AttachAndStop(
            InspectionFixture fixture,
            bool? enableImplicitEvaluation = null)
        {
            Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace, enableImplicitEvaluation));
            fixture.Backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));
        }

        private static JObject Request(string command, object arguments) =>
            JObject.FromObject(
                new
                {
                    seq = 1,
                    type = "request",
                    command,
                    arguments,
                });

        private static JObject Response(
            IReadOnlyList<JObject> messages,
            string command) =>
            Responses(messages, command).Single();

        private static IReadOnlyList<JObject> Responses(
            IReadOnlyList<JObject> messages,
            string command) =>
            messages.Where(
                item =>
                    OptionalText(item["type"]) == "response" &&
                    OptionalText(item["command"]) == command)
                .ToArray();

        private static string? OptionalText(JToken? token) =>
            token?.Value<string>();

        private static T Required<T>(JToken? token)
        {
            if (token == null)
                throw new InvalidDataException("Required DAP field missing.");
            return token.Value<T>()!;
        }

        private static IReadOnlyList<JObject> Run(
            UnityDebugSession session,
            params JObject[] requests)
        {
            using (var input = new MemoryStream())
            using (var output = new SynchronizedMemoryStream())
            {
                foreach (var request in requests)
                {
                    var json = request.ToString(Formatting.None);
                    var body = Encoding.UTF8.GetBytes(json);
                    var header = Encoding.ASCII.GetBytes(
                        $"Content-Length: {body.Length}\r\n\r\n");
                    input.Write(header, 0, header.Length);
                    input.Write(body, 0, body.Length);
                }
                input.Position = 0;
                session.Start(input, output).GetAwaiter().GetResult();
                Assert.True(
                    System.Threading.SpinWait.SpinUntil(
                        () => ParseMessages(output.Snapshot()).Count(
                            message =>
                                OptionalText(message["type"]) ==
                                "response") >= requests.Length,
                        TimeSpan.FromSeconds(10)),
                    "Timed out waiting for asynchronous DAP responses.");
                return ParseMessages(output.Snapshot());
            }
        }

        private static IReadOnlyList<JObject> ParseMessages(byte[] bytes)
        {
            var messages = new List<JObject>();
            var offset = 0;
            while (offset < bytes.Length)
            {
                var headerEnd = FindHeaderEnd(bytes, offset);
                var header = Encoding.ASCII.GetString(
                    bytes,
                    offset,
                    headerEnd - offset);
                var length = int.Parse(
                    header.Substring("Content-Length: ".Length));
                var bodyStart = headerEnd + 4;
                messages.Add(
                    JObject.Parse(
                        Encoding.UTF8.GetString(
                            bytes,
                            bodyStart,
                            length)));
                offset = bodyStart + length;
            }
            return messages;
        }

        private static int FindHeaderEnd(byte[] bytes, int start)
        {
            for (var index = start; index <= bytes.Length - 4; index++)
            {
                if (
                    bytes[index] == '\r' &&
                    bytes[index + 1] == '\n' &&
                    bytes[index + 2] == '\r' &&
                    bytes[index + 3] == '\n')
                {
                    return index;
                }
            }
            throw new InvalidDataException("DAP header terminator missing.");
        }

        private sealed class InspectionFixture
        {
            public InspectionFixture(
                FakeDebuggerBackend backend,
                UnityDebugSession session,
                string workspace,
                string sourcePath)
            {
                Backend = backend;
                Session = session;
                Workspace = workspace;
                SourcePath = sourcePath;
            }

            public FakeDebuggerBackend Backend { get; }
            public UnityDebugSession Session { get; }
            public string Workspace { get; }
            public string SourcePath { get; }
        }

        private sealed class SynchronizedMemoryStream : MemoryStream
        {
            private readonly object sync = new object();

            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (sync)
                {
                    base.Write(buffer, offset, count);
                }
            }

            public byte[] Snapshot()
            {
                lock (sync)
                {
                    return base.ToArray();
                }
            }
        }
    }
}
