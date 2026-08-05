using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.State;
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
        public void GotoTargetsMapRangesAndGotoRaisesOneStoppedEvent()
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
            var stopped = Assert.Single(
                DapTestProtocol.Events(gotoMessages, "stopped"));
            Assert.Equal(
                "goto",
                stopped["body"]!["reason"]!.Value<string>());
        }

        [Fact]
        public void StepTargetsExpireWhenSuspendedStateResets()
        {
            var state = new SuspendedState();
            var codePath = new object();
            var runtime = new StepTargetRuntime(
                new RuntimeStepInTarget("Call", codePath));
            var manager = new StepTargetManager(state, runtime);
            manager.RegisterFrame(10, 100);

            var target = Assert.Single(manager.GetTargets(10));
            state.Reset();

            Assert.Empty(manager.GetTargets(10));
            Assert.False(manager.TryStepIn(42, target.Id));
            Assert.Null(runtime.SelectedCodePath);
        }

        [Fact]
        public void GotoPreservesCurrentSuspendedHandles()
        {
            var state = new SuspendedState();
            var property = new object();
            var propertyId = state.RegisterProperty(property);
            var codeContext = new object();
            var runtime = new GotoRuntime(
                new RuntimeGotoTarget(
                    "line 18",
                    18,
                    1,
                    18,
                    10,
                    codeContext));
            var manager = new GotoManager(state, runtime);
            var target = Assert.Single(
                manager.GetTargets("Player.cs", 18, 1));

            Assert.True(manager.TryGoto(42, target.Id));
            Assert.Same(codeContext, runtime.SelectedCodeContext);
            Assert.True(
                state.TryGetProperty<object>(
                    propertyId,
                    out var preserved));
            Assert.Same(property, preserved);
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

        private sealed class StepTargetRuntime : IStepTargetRuntime
        {
            private readonly RuntimeStepInTarget target;

            public StepTargetRuntime(RuntimeStepInTarget target)
            {
                this.target = target;
            }

            public object? SelectedCodePath { get; private set; }

            public IReadOnlyList<RuntimeStepInTarget> GetStepInTargets(
                long runtimeFrameId) => new[] { target };

            public void StepIn(long threadId, object codePath)
            {
                SelectedCodePath = codePath;
            }
        }

        private sealed class GotoRuntime : IGotoRuntime
        {
            private readonly RuntimeGotoTarget target;

            public GotoRuntime(RuntimeGotoTarget target)
            {
                this.target = target;
            }

            public object? SelectedCodeContext { get; private set; }

            public IReadOnlyList<RuntimeGotoTarget> GetGotoTargets(
                string sourcePath,
                int line,
                int column) => new[] { target };

            public void Goto(long threadId, object codeContext)
            {
                SelectedCodeContext = codeContext;
            }
        }
    }
}
