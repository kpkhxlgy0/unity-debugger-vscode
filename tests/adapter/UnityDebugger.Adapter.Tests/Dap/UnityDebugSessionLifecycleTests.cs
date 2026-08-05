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
    public sealed class UnityDebugSessionLifecycleTests
    {
        [Fact]
        public void Initialize_reports_only_current_capabilities()
        {
            var messages = Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                Request("initialize", new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                }));

            var response = Response(messages, "initialize");
            Assert.True(Required<bool>(response["success"]));
            Assert.False(Required<bool>(response.SelectToken(
                "body.supportsConfigurationDoneRequest")));
            Assert.True(Required<bool>(response.SelectToken(
                "body.supportsConditionalBreakpoints")));
            Assert.True(Required<bool>(response.SelectToken(
                "body.supportsEvaluateForHovers")));
            Assert.Equal(
                1,
                messages.Count(
                    item => OptionalText(item["event"]) == "initialized"));
        }

        [Fact]
        public void Attach_baseline_calls_backend_without_warning()
        {
            var backend = new FakeDebuggerBackend();
            var messages = Run(
                new UnityDebugSession(() => backend),
                Request("attach", ValidAttachArguments()));

            Assert.True(Required<bool>(
                Response(messages, "attach")["success"]));
            Assert.Equal(1, backend.AttachCount);
            Assert.DoesNotContain(
                messages,
                item => OptionalText(item["event"]) == "output");
        }

        [Fact]
        public void Attach_unverified_version_emits_exactly_one_warning()
        {
            var arguments = ValidAttachArguments();
            arguments["__projectVersion"] = "6000.0.50f1";
            var messages = Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                Request("attach", arguments));

            var warnings = messages
                .Where(item => OptionalText(item["event"]) == "output")
                .ToArray();
            Assert.Single(warnings);
            var warning = Required<string>(
                warnings[0].SelectToken("body.output"));
            Assert.Contains(
                "Editor 6000.0.50f1 is unverified",
                warning);
            Assert.Contains(
                "https://marketplace.visualstudio.com/items?itemName=" +
                "kpk.unity-debugger-pure#support-policy",
                warning);
        }

        [Fact]
        public void Disconnect_releases_backend_and_terminates_once()
        {
            var backend = new FakeDebuggerBackend();
            var messages = Run(
                new UnityDebugSession(() => backend),
                Request("attach", ValidAttachArguments()),
                Request("disconnect", new { }));

            Assert.True(Required<bool>(
                Response(messages, "disconnect")["success"]));
            Assert.Equal(1, backend.DisconnectCount);
            Assert.Equal(1, backend.DisposeCount);
            Assert.Equal(
                1,
                messages.Count(
                    item => OptionalText(item["event"]) == "terminated"));
        }

        [Fact]
        public void Attach_maps_backend_failure_to_one_error_response()
        {
            var backend = new FakeDebuggerBackend
            {
                AttachException = new DebuggerBackendException(
                    "Editor attach failed."),
            };
            var messages = Run(
                new UnityDebugSession(() => backend),
                Request("attach", ValidAttachArguments()));

            var response = Response(messages, "attach");
            Assert.False(Required<bool>(response["success"]));
            Assert.Contains(
                "Editor attach failed.",
                Required<string>(response["message"]));
            Assert.Equal(
                1,
                messages.Count(
                    item => OptionalText(item["type"]) == "response"));
        }

        [Fact]
        public void SetBreakpoints_binds_condition_and_returns_logical_id()
        {
            var backend = new FakeDebuggerBackend();
            var messages = Run(
                new UnityDebugSession(() => backend),
                Request("initialize", new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                }),
                Request("attach", ValidAttachArguments()),
                Request("setBreakpoints", new
                {
                    source = new
                    {
                        name = "Player.cs",
                        path = @"H:\fixture\Assets\Player.cs",
                    },
                    breakpoints = new[]
                    {
                        new
                        {
                            line = 12,
                            condition = "health <= 0",
                        },
                    },
                }));

            var response = Response(messages, "setBreakpoints");
            Assert.True(Required<bool>(response["success"]));
            Assert.Single(backend.Bound);
            Assert.Equal("health <= 0", backend.Bound[0].Condition);
            Assert.Equal(
                1,
                Required<long>(
                    response.SelectToken("body.breakpoints[0].id")));
            Assert.True(Required<bool>(
                response.SelectToken("body.breakpoints[0].verified")));
            Assert.Null(
                response.SelectToken(
                    "body.breakpoints[0].backendBreakpointId"));
        }

        [Fact]
        public void SetBreakpoints_rejects_non_managed_source()
        {
            var backend = new FakeDebuggerBackend();
            var messages = Run(
                new UnityDebugSession(() => backend),
                Request("initialize", new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                }),
                Request("attach", ValidAttachArguments()),
                Request("setBreakpoints", new
                {
                    source = new
                    {
                        name = "Surface.shader",
                        path = @"H:\fixture\Assets\Surface.shader",
                    },
                    breakpoints = new[] { new { line = 5 } },
                }));

            var response = Response(messages, "setBreakpoints");
            Assert.False(Required<bool>(response["success"]));
            Assert.Contains(
                ".cs file",
                Required<string>(response["message"]));
            Assert.Empty(backend.Bound);
        }

        [Theory]
        [InlineData("launch", false)]
        [InlineData("setFunctionBreakpoints", true)]
        [InlineData("source", true)]
        public void Temporary_handlers_send_exactly_one_response(
            string command,
            bool expectedSuccess)
        {
            var messages = Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                Request(command, new { }));

            var responses = messages.Where(
                item => OptionalText(item["type"]) == "response").ToArray();
            Assert.Single(responses);
            Assert.Equal(
                expectedSuccess,
                Required<bool>(responses[0]["success"]));
            if (!expectedSuccess)
            {
                Assert.Contains(
                    command == "launch"
                        ? "attach only"
                        : "Not implemented yet.",
                    Required<string>(responses[0]["message"]));
            }
        }

        private static JObject ValidAttachArguments()
        {
            return JObject.Parse(@"{
              '__processId': 1234,
              '__host': '127.0.0.1',
              '__port': 56234,
              '__workspaceRoot': 'H:\\fixture',
              '__projectVersion': '2022.3.62t11'
            }");
        }

        private static JObject Request(string command, object arguments)
        {
            return JObject.FromObject(new
            {
                seq = 1,
                type = "request",
                command,
                arguments,
            });
        }

        private static JObject Response(
            IReadOnlyList<JObject> messages,
            string command)
        {
            return messages.Single(
                item =>
                    OptionalText(item["type"]) == "response" &&
                    OptionalText(item["command"]) == command);
        }

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
                var json = Encoding.UTF8.GetString(
                    bytes,
                    bodyStart,
                    length);
                messages.Add(JObject.Parse(json));
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
    }
}
