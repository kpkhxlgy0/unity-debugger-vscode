using Newtonsoft.Json.Linq;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class FunctionBreakpointTests
    {
        [Fact]
        public void FullyQualifiedFunctionBreakpointBindsAndReturnsAResult()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);
            var name =
                "MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles";

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "setFunctionBreakpoints",
                    new
                    {
                        breakpoints = new[]
                        {
                            new
                            {
                                name,
                                condition = (string?)null,
                                hitCondition = (string?)null,
                            },
                        },
                    }));

            var response = DapTestProtocol.Response(
                messages,
                "setFunctionBreakpoints");
            Assert.True(response["success"]!.Value<bool>());
            Assert.True(
                response.SelectToken("body.breakpoints[0].verified")!
                    .Value<bool>());
            Assert.Equal(name, Assert.Single(backend.FunctionBound).FunctionName);
        }

        [Fact]
        public void ReplacingFunctionBreakpointsRemovesTheOldBinding()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);
            DapTestProtocol.Run(
                session,
                FunctionRequest("Fixture.First"));

            DapTestProtocol.Run(
                session,
                FunctionRequest("Fixture.Second"));

            Assert.Equal(2, backend.FunctionBound.Count);
            Assert.Single(backend.RemovedBreakpointIds);
        }

        [Fact]
        public void FunctionBreakpointStopReportsTheLogicalBreakpointId()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);
            using (var recorder = new DapTestProtocol.Recorder(session))
            {
                var responseMessages = recorder.Send(
                    FunctionRequest("Fixture.Target"));
                var logicalId = DapTestProtocol.Required<long>(
                    DapTestProtocol.Response(
                        responseMessages,
                        "setFunctionBreakpoints")
                        .SelectToken("body.breakpoints[0].id"));
                var backendId = backend.ActiveBreakpointIds.Single();

                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        17,
                        null,
                        backendId));

                var stopped = DapTestProtocol.Events(
                    recorder.Capture(),
                    "stopped").Single();
                Assert.Equal(
                    logicalId,
                    DapTestProtocol.Required<long>(
                        stopped.SelectToken("body.hitBreakpointIds[0]")));
            }
        }

        private static JObject FunctionRequest(string name) =>
            DapTestProtocol.Request(
                "setFunctionBreakpoints",
                new
                {
                    breakpoints = new[]
                    {
                        new
                        {
                            name,
                            condition = (string?)null,
                            hitCondition = (string?)null,
                        },
                    },
                });

        private static UnityDebugSession AttachedSession(
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
                    }));
            return session;
        }
    }
}
