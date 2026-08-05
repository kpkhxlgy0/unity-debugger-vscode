using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ExecutionControlTests
    {
        [Theory]
        [InlineData("continue")]
        [InlineData("next")]
        [InlineData("stepIn")]
        [InlineData("stepOut")]
        public void Resume_requests_call_one_backend_method_with_thread(
            string command)
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    command,
                    new { threadId = 1 }));

            Assert.True(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(messages, command)["success"]));
            Assert.Equal(42, backend.LastControlThreadId);
            Assert.Equal(
                1,
                command == "continue"
                    ? backend.ContinueCount
                    : command == "next"
                        ? backend.StepOverCount
                        : command == "stepIn"
                            ? backend.StepInCount
                            : backend.StepOutCount);
        }

        [Fact]
        public void Pause_calls_backend_only_while_running()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "pause",
                    new { threadId = 1 }));

            Assert.True(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(messages, "pause")["success"]));
            Assert.Equal(1, backend.PauseCount);
            Assert.Equal(42, backend.LastControlThreadId);
        }

        [Fact]
        public void Step_while_running_fails_without_calling_backend()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            var response = DapTestProtocol.Response(messages, "next");
            Assert.False(DapTestProtocol.Required<bool>(
                response["success"]));
            Assert.Contains(
                "requires a stopped target",
                DapTestProtocol.Required<string>(response["message"]));
            Assert.Equal(0, backend.StepOverCount);
        }

        [Fact]
        public void Synchronous_continued_event_is_sent_after_response()
        {
            var backend = new FakeDebuggerBackend
            {
                RaiseContinuedSynchronously = true,
            };
            var session = AttachedSession(backend);
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "continue",
                    new { threadId = 1 }));

            var responseIndex = messages
                .Select((message, index) => new { message, index })
                .Single(
                    item =>
                        item.message["type"]?.Value<string>() ==
                            "response" &&
                        item.message["command"]?.Value<string>() ==
                            "continue")
                .index;
            var eventIndex = messages
                .Select((message, index) => new { message, index })
                .Single(
                    item =>
                        item.message["event"]?.Value<string>() ==
                            "continued")
                .index;
            Assert.True(responseIndex < eventIndex);
            Assert.Equal(
                1,
                DapTestProtocol.Required<int>(
                    messages[eventIndex].SelectToken("body.threadId")));
        }

        [Fact]
        public void Duplicate_backend_events_are_suppressed()
        {
            var backend = new FakeDebuggerBackend
            {
                SynchronousStoppedEventCount = 2,
            };
            var session = AttachedSession(backend);
            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "pause",
                    new { threadId = 1 }));

            Assert.Single(
                messages,
                item =>
                    item["event"]?.Value<string>() == "stopped");
        }

        [Fact]
        public void Breakpoint_stop_reports_logical_hit_id_for_client_highlight()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            var session = new UnityDebugSession(() => backend);
            using (var recorder = new DapTestProtocol.Recorder(session))
            {
                recorder.Send(
                    DapTestProtocol.Request(
                        "initialize",
                        new
                        {
                            linesStartAt1 = true,
                            pathFormat = "path",
                        }),
                    DapTestProtocol.Request(
                        "attach",
                        new
                        {
                            __processId = 1234,
                            __host = "127.0.0.1",
                            __port = 56234,
                            __workspaceRoot = @"H:\fixture",
                            __projectVersion = "2022.3.62t11",
                        }),
                    DapTestProtocol.Request(
                        "setBreakpoints",
                        new
                        {
                            source = new
                            {
                                path = @"H:\fixture\Assets\Player.cs",
                            },
                            breakpoints = new[]
                            {
                                new { line = 12, condition = "ready" },
                            },
                        }),
                    DapTestProtocol.Request(
                        "setBreakpoints",
                        new
                        {
                            source = new
                            {
                                path = @"H:\fixture\Assets\Player.cs",
                            },
                            breakpoints = new[]
                            {
                                new { line = 12, condition = "!ready" },
                            },
                        }));

                Assert.True(backend.RaiseBreakpointHit(2, 42));
                var stopped = recorder.Capture().Single(
                    item =>
                        item["event"]?.Value<string>() == "stopped");

                Assert.Equal(
                    1,
                    DapTestProtocol.Required<int>(
                        stopped.SelectToken("body.hitBreakpointIds[0]")));
                Assert.Equal(
                    JTokenType.Null,
                    stopped.SelectToken("body.text")?.Type);
            }
        }

        [Fact]
        public void Unmapped_breakpoint_stop_omits_hit_ids()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            var session = new UnityDebugSession(() => backend);
            using (var recorder = new DapTestProtocol.Recorder(session))
            {
                recorder.Send(
                    DapTestProtocol.Request(
                        "initialize",
                        new
                        {
                            linesStartAt1 = true,
                            pathFormat = "path",
                        }),
                    DapTestProtocol.Request(
                        "attach",
                        new
                        {
                            __processId = 1234,
                            __host = "127.0.0.1",
                            __port = 56234,
                            __workspaceRoot = @"H:\fixture",
                            __projectVersion = "2022.3.62t11",
                        }));

                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        42,
                        null,
                        999));
                var stopped = recorder.Capture().Single(
                    item =>
                        item["event"]?.Value<string>() == "stopped");

                Assert.Null(
                    stopped.SelectToken("body.hitBreakpointIds"));
            }
        }

        [Fact]
        public void Failed_resume_restores_stopped_state_and_redacts_error()
        {
            var backend = new FakeDebuggerBackend
            {
                ControlException = new System.InvalidOperationException(
                    "SECRET_BACKEND_CONTROL_ERROR"),
            };
            var session = AttachedSession(backend);
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));

            var failed = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));
            var failedResponse = DapTestProtocol.Response(failed, "next");
            Assert.False(DapTestProtocol.Required<bool>(
                failedResponse["success"]));
            Assert.DoesNotContain(
                "SECRET_BACKEND_CONTROL_ERROR",
                DapTestProtocol.Required<string>(
                    failedResponse["message"]));

            backend.ControlException = null;
            var retried = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));
            Assert.True(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(retried, "next")["success"]));
        }

        [Fact]
        public void Pause_while_stopped_fails_without_backend_call()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "pause",
                    new { threadId = 1 }));

            Assert.False(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(messages, "pause")["success"]));
            Assert.Equal(0, backend.PauseCount);
        }

        private static UnityDebugSession AttachedSession(
            FakeDebuggerBackend backend)
        {
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            var session = new UnityDebugSession(() => backend);
            DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "initialize",
                    new
                    {
                        linesStartAt1 = true,
                        pathFormat = "path",
                    }),
                DapTestProtocol.Request(
                    "attach",
                    new
                    {
                        __processId = 1234,
                        __host = "127.0.0.1",
                        __port = 56234,
                        __workspaceRoot = @"H:\fixture",
                        __projectVersion = "2022.3.62t11",
                    }),
                DapTestProtocol.Request("threads", new { }));
            return session;
        }
    }
}
