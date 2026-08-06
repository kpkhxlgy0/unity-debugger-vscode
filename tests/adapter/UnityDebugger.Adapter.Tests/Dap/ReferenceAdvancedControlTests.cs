using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ReferenceAdvancedControlTests
    {
        [Fact]
        public void StepInTargetsAreReturnedAndSelectedTargetIsForwarded()
        {
            var backend = CreateBackend();
            backend.StepInTargets.Add(
                new BackendStepInTarget(51, "Button.Press()"));
            var session = AttachAndStop(backend);

            var targetMessages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "stepInTargets",
                    new { frameId = 10 }));
            var targets = DapTestProtocol.Response(
                targetMessages,
                "stepInTargets")["body"]!["targets"]!;

            var target = Assert.Single(targets);
            Assert.Equal(51, target["id"]!.Value<int>());
            Assert.Equal(
                "Button.Press()",
                target["label"]!.Value<string>());
            Assert.Equal(10, backend.LastStepInTargetsFrameId);

            var stepMessages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "stepIn",
                    new { threadId = 1, targetId = 51 }));

            Assert.True(
                DapTestProtocol.Response(
                    stepMessages,
                    "stepIn")["success"]!.Value<bool>());
            Assert.Equal(51, backend.LastStepInTargetId);
        }

        [Fact]
        public void StaleStepInFrameReturnsExactEmptyTargets()
        {
            var backend = CreateBackend();
            var session = AttachAndStop(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "stepInTargets",
                    new { frameId = 999 }));

            var response = DapTestProtocol.Response(
                messages,
                "stepInTargets");
            Assert.True(response["success"]!.Value<bool>());
            Assert.Empty(response["body"]!["targets"]!);
        }

        [Fact]
        public void GotoTargetsMapRangesAndGotoCompletesWithoutWarning()
        {
            var backend = CreateBackend();
            backend.GotoTargets.Add(
                new BackendGotoTarget(
                    71,
                    "line 18",
                    18,
                    5,
                    18,
                    23));
            var session = AttachAndStop(backend);

            var targetMessages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "gotoTargets",
                    new
                    {
                        source = new
                        {
                            path = @"H:\fixture\Assets\Player.cs",
                        },
                        line = 18,
                        column = 5,
                    }));
            var targets = DapTestProtocol.Response(
                targetMessages,
                "gotoTargets")["body"]!["targets"]!;

            var target = Assert.Single(targets);
            Assert.Equal(71, target["id"]!.Value<int>());
            Assert.Equal("line 18", target["label"]!.Value<string>());
            Assert.Equal(18, target["line"]!.Value<int>());
            Assert.Equal(5, target["column"]!.Value<int>());
            Assert.Equal(18, target["endLine"]!.Value<int>());
            Assert.Equal(23, target["endColumn"]!.Value<int>());

            var gotoMessages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "goto",
                    new { threadId = 1, targetId = 71 }));

            Assert.Equal(1, backend.GotoCount);
            Assert.Equal(71, backend.LastGotoTargetId);
            Assert.True(
                DapTestProtocol.Response(
                    gotoMessages,
                    "goto")["success"]!.Value<bool>());
            Assert.Empty(DapTestProtocol.Events(gotoMessages, "output"));
        }

        private static FakeDebuggerBackend CreateBackend()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            return backend;
        }

        private static UnityDebugSession AttachAndStop(
            FakeDebuggerBackend backend)
        {
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
            backend.RaiseStopped(
                new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null));
            return session;
        }

    }
}
