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
        public void Step_request_is_forwarded_without_dap_running_state()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            var response = DapTestProtocol.Response(messages, "next");
            Assert.True(DapTestProtocol.Required<bool>(
                response["success"]));
            Assert.Equal(1, backend.StepOverCount);
        }

        [Fact]
        public void Duplicate_step_requests_are_both_forwarded()
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
                    "next",
                    new { threadId = 1 }),
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            var responses = DapTestProtocol.Responses(messages, "next");
            Assert.Equal(2, responses.Count);
            Assert.All(
                responses,
                item => Assert.True(
                    DapTestProtocol.Required<bool>(item["success"])));
            Assert.Equal(2, backend.StepOverCount);
        }

        [Fact]
        public void Synchronous_backend_continued_event_is_not_forwarded()
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

            Assert.True(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(
                    messages,
                    "continue")["success"]));
            Assert.Empty(DapTestProtocol.Events(messages, "continued"));
        }

        [Fact]
        public void Late_continued_event_does_not_overwrite_new_step_stop()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            backend.Frames.Add(
                new BackendStackFrame(
                    3001,
                    42,
                    "Player.Update()",
                    @"H:\fixture\Assets\Player.cs",
                    12,
                    1));
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
                    DapTestProtocol.Request("threads", new { }));
                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        42,
                        null));
                recorder.Capture();
                recorder.Send(
                    DapTestProtocol.Request(
                        "next",
                        new { threadId = 1 }));
                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Step,
                        42,
                        null));
                Assert.Single(
                    recorder.Capture(),
                    item => item["event"]?.Value<string>() == "stopped");

                backend.RaiseContinued();
                Assert.DoesNotContain(
                    recorder.Capture(),
                    item => item["event"]?.Value<string>() == "continued");
                var inspected = recorder.Send(
                    DapTestProtocol.Request(
                        "stackTrace",
                        new
                        {
                            threadId = 1,
                            startFrame = 0,
                            levels = 20,
                        }));

                Assert.True(DapTestProtocol.Required<bool>(
                    DapTestProtocol.Response(
                        inspected,
                        "stackTrace")["success"]));
            }
        }

        [Fact]
        public void Stop_arriving_during_attach_is_forwarded()
        {
            var backend = new FakeDebuggerBackend
            {
                AttachStoppedEvent = new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null),
            };
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            var session = new UnityDebugSession(() => backend);

            var messages = DapTestProtocol.Run(
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
                    }));

            var stopped = Assert.Single(
                messages,
                item => item["event"]?.Value<string>() == "stopped");
            Assert.Equal(
                "breakpoint",
                DapTestProtocol.Required<string>(
                    stopped.SelectToken("body.reason")));
            Assert.Equal(
                1,
                DapTestProtocol.Required<int>(
                    stopped.SelectToken("body.threadId")));
        }

        [Fact]
        public void Duplicate_backend_stops_are_forwarded()
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

            Assert.Equal(
                2,
                DapTestProtocol.Events(messages, "stopped").Count);
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
        public void Pause_while_stopped_is_forwarded_without_dap_state_gate()
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

            Assert.True(DapTestProtocol.Required<bool>(
                DapTestProtocol.Response(messages, "pause")["success"]));
            Assert.Equal(1, backend.PauseCount);
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
