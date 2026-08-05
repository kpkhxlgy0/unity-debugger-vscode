using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ReferenceControlTranscriptTests
    {
        [Fact]
        public void FirstStopDuringAttachIsNotDropped()
        {
            var backend = new FakeDebuggerBackend
            {
                AttachStoppedEvent = new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null),
            };
            backend.Threads.Add(new BackendThread(42, "Main Thread"));

            var messages = DapTestProtocol.Run(
                new UnityDebugSession(() => backend),
                InitializeRequest(),
                AttachRequest());

            var stopped = Assert.Single(
                DapTestProtocol.Events(messages, "stopped"));
            Assert.True(
                stopped["body"]!["allThreadsStopped"]!.Value<bool>());
            Assert.Equal(
                "breakpoint",
                stopped["body"]!["reason"]!.Value<string>());
        }

        [Fact]
        public void StepResponseCannotHideNewStoppedEvent()
        {
            var backend = CreateStoppedBackend();
            backend.StepStoppedEvent = new BackendStoppedEventArgs(
                BackendStopReason.Step,
                42,
                null);
            var session = AttachAndStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            Assert.Single(DapTestProtocol.Responses(messages, "next"));
            Assert.Single(DapTestProtocol.Events(messages, "stopped"));
            Assert.Empty(DapTestProtocol.Events(messages, "continued"));
        }

        [Fact]
        public void RapidDifferentStepRequestsBothReachBackend()
        {
            var backend = CreateStoppedBackend();
            var session = AttachAndStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "stepIn",
                    new { threadId = 1 }),
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            Assert.True(
                DapTestProtocol.Response(
                    messages,
                    "stepIn")["success"]!.Value<bool>());
            Assert.True(
                DapTestProtocol.Response(
                    messages,
                    "next")["success"]!.Value<bool>());
            Assert.Equal(1, backend.StepInCount);
            Assert.Equal(1, backend.StepOverCount);
            Assert.Empty(DapTestProtocol.Events(messages, "continued"));
            Assert.Empty(DapTestProtocol.Events(messages, "output"));
        }

        [Fact]
        public void RepeatedStepRequestsAreNotCoalescedByDapLayer()
        {
            var backend = CreateStoppedBackend();
            var session = AttachAndStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }),
                DapTestProtocol.Request(
                    "next",
                    new { threadId = 1 }));

            Assert.Equal(2, DapTestProtocol.Responses(messages, "next").Count);
            Assert.All(
                DapTestProtocol.Responses(messages, "next"),
                response => Assert.True(
                    response["success"]!.Value<bool>()));
            Assert.Equal(2, backend.StepOverCount);
        }

        [Fact]
        public void BackendContinuedNotificationDoesNotCreateDapEvent()
        {
            var backend = CreateStoppedBackend();
            backend.RaiseContinuedSynchronously = true;
            var session = AttachAndStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "continue",
                    new { threadId = 1 }));

            Assert.True(
                DapTestProtocol.Response(
                    messages,
                    "continue")["success"]!.Value<bool>());
            Assert.Empty(DapTestProtocol.Events(messages, "continued"));
        }

        [Fact]
        public void EveryPauseStopIsForwarded()
        {
            var backend = CreateStoppedBackend();
            backend.SynchronousStoppedEventCount = 2;
            var session = AttachWithoutStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "pause",
                    new { threadId = 1 }));

            Assert.True(
                DapTestProtocol.Response(
                    messages,
                    "pause")["success"]!.Value<bool>());
            Assert.Equal(2, DapTestProtocol.Events(messages, "stopped").Count);
        }

        [Fact]
        public void StopMapsEveryBackendBreakpointIdToLogicalHitIds()
        {
            var backend = CreateStoppedBackend();
            var session = AttachWithoutStop(backend);
            using (var recorder = new DapTestProtocol.Recorder(session))
            {
                recorder.Send(
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
                                new { line = 12 },
                                new { line = 18 },
                            },
                        }));

                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        42,
                        null,
                        new long[] { 1, 2 }));
                var stopped = Assert.Single(
                    DapTestProtocol.Events(
                        recorder.Capture(),
                        "stopped"));

                Assert.Equal(
                    new long[] { 1, 2 },
                    stopped["body"]!["hitBreakpointIds"]!
                        .Values<long>());
            }
        }

        [Fact]
        public void TypedOutputAndModuleEventsAreTranslatedDirectly()
        {
            var backend = CreateStoppedBackend();
            var session = AttachWithoutStop(backend);
            using (var recorder = new DapTestProtocol.Recorder(session))
            {
                recorder.Send(
                    DapTestProtocol.Request("threads", new { }));
                backend.RaiseOutput(
                    new BackendOutputEventArgs("stdout", "message"));
                backend.RaiseModuleChanged(
                    new BackendModuleChangedEventArgs(
                        new BackendModule(
                            "assembly-1",
                            "Assembly-CSharp",
                            @"H:\fixture\Library\ScriptAssemblies\Assembly-CSharp.dll",
                            true),
                        true));
                var messages = recorder.Capture();

                var output = Assert.Single(
                    DapTestProtocol.Events(messages, "output"));
                Assert.Equal(
                    "message",
                    output["body"]!["output"]!.Value<string>());
                var module = Assert.Single(
                    DapTestProtocol.Events(messages, "module"));
                Assert.Equal(
                    "new",
                    module["body"]!["reason"]!.Value<string>());
                Assert.Equal(
                    "Assembly-CSharp",
                    module["body"]!["module"]!["name"]!.Value<string>());
            }
        }

        private static FakeDebuggerBackend CreateStoppedBackend()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            return backend;
        }

        private static UnityDebugSession AttachAndStop(
            FakeDebuggerBackend backend)
        {
            var session = AttachWithoutStop(backend);
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));
            return session;
        }

        private static UnityDebugSession AttachWithoutStop(
            FakeDebuggerBackend backend)
        {
            var session = new UnityDebugSession(() => backend);
            DapTestProtocol.Run(
                session,
                InitializeRequest(),
                AttachRequest(),
                DapTestProtocol.Request("threads", new { }));
            return session;
        }

        private static JObject InitializeRequest() =>
            DapTestProtocol.Request(
                "initialize",
                new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                });

        private static JObject AttachRequest() =>
            DapTestProtocol.Request(
                "attach",
                new
                {
                    __processId = 1234,
                    __host = "127.0.0.1",
                    __port = 56234,
                    __workspaceRoot = @"H:\fixture",
                    __projectVersion = "2022.3.62t11",
                });
    }
}
