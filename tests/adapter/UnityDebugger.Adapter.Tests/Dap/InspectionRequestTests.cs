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
            var messages = Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
                Request("threads", new { }),
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
                        context = "watch",
                    }));

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

            var scopeReference = Required<int>(
                Response(messages, "scopes")
                    .SelectToken("body.scopes[0].variablesReference"));
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
        }

        [Fact]
        public void Unknown_handles_return_errors_without_backend_calls()
        {
            var fixture = Fixture();
            var messages = Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
                Request(
                    "stackTrace",
                    new { threadId = 999, startFrame = 0, levels = 20 }),
                Request("scopes", new { frameId = 999 }),
                Request(
                    "variables",
                    new { variablesReference = 999 }));

            Assert.All(
                new[] { "stackTrace", "scopes", "variables" },
                command =>
                {
                    var response = Response(messages, command);
                    Assert.False(Required<bool>(response["success"]));
                    Assert.Contains(
                        "no longer available",
                        Required<string>(response["message"]));
                });
            Assert.Equal(0, fixture.Backend.StackTraceCount);
            Assert.Equal(0, fixture.Backend.ScopesCount);
            Assert.Equal(0, fixture.Backend.VariablesCount);
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

            var messages = Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
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
                "deemphasize",
                Required<string>(unavailable["presentationHint"]));
            Assert.Null(unavailable["source"]);
            Assert.Equal(
                Path.GetFullPath(fixture.SourcePath),
                Path.GetFullPath(Required<string>(
                    frames[1]!.SelectToken("source.path"))),
                ignoreCase: true);
        }

        [Theory]
        [InlineData("watch")]
        [InlineData("repl")]
        public void Explicit_evaluation_contexts_are_allowed(
            string context)
        {
            var fixture = Fixture();
            var messages = Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
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
        }

        [Fact]
        public void Hover_evaluation_is_rejected_without_backend_call()
        {
            var fixture = Fixture();
            var messages = Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
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
                        context = "hover",
                    }));

            var response = Response(messages, "evaluate");
            Assert.False(Required<bool>(response["success"]));
            Assert.Contains(
                "watch or repl",
                Required<string>(response["message"]));
            Assert.DoesNotContain(
                "SECRET_EXPRESSION",
                Required<string>(response["message"]));
            Assert.Equal(0, fixture.Backend.EvaluateCount);
        }

        [Fact]
        public void Continue_and_reload_invalidate_stop_scoped_handles()
        {
            var fixture = Fixture();
            Run(
                fixture.Session,
                Initialize(),
                Attach(fixture.Workspace),
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));

            fixture.Backend.RaiseContinued();
            var afterContinue = Run(
                fixture.Session,
                Request("scopes", new { frameId = 1 }));
            Assert.False(Required<bool>(
                Response(afterContinue, "scopes")["success"]));

            Run(
                fixture.Session,
                Request("threads", new { }),
                Request(
                    "stackTrace",
                    new { threadId = 1, startFrame = 0, levels = 20 }));
            fixture.Backend.RaiseReloadStarted();
            var afterReload = Run(
                fixture.Session,
                Request("scopes", new { frameId = 1 }));
            Assert.False(Required<bool>(
                Response(afterReload, "scopes")["success"]));
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

        private static JObject Attach(string workspace) =>
            Request(
                "attach",
                new
                {
                    __processId = 1234,
                    __host = "127.0.0.1",
                    __port = 56234,
                    __workspaceRoot = workspace,
                    __projectVersion = "2022.3.62t11",
                });

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
            using (var output = new MemoryStream())
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
                return ParseMessages(output.ToArray());
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
    }
}
