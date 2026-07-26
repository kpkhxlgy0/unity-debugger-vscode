using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Breakpoints
{
    public sealed class DomainReloadTests
    {
        [Fact]
        public void Normal_reload_preserves_verified_status_when_backend_only_reports_pending()
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main"));
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
                                path =
                                    @"H:\fixture\Assets\Player.cs",
                            },
                            breakpoints = new[] { new { line = 12 } },
                        }));
                backend.RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Breakpoint,
                        42,
                        null));
                recorder.Capture();

                backend.BindAsPending = true;
                backend.RaiseReloadStarted();
                var started = recorder.Capture();

                Assert.Single(
                    started,
                    item =>
                        item["event"]?.Value<string>() == "continued");
                Assert.DoesNotContain(
                    started,
                    item =>
                        item["event"]?.Value<string>() == "breakpoint");

                backend.RaiseBreakpointChanged(
                    new BackendBreakpointChangedEventArgs(
                        new BackendBoundBreakpoint(
                            1,
                            false,
                            12,
                            "Symbols are not loaded.")));
                var backendPending = recorder.Capture();

                Assert.DoesNotContain(
                    backendPending,
                    item =>
                        item["event"]?.Value<string>() == "breakpoint");

                var blocked = recorder.Send(
                    DapTestProtocol.Request("threads", new { }),
                    DapTestProtocol.Request(
                        "next",
                        new { threadId = 1 }));
                var threads = blocked.Single(
                    item =>
                        item["type"]?.Value<string>() == "response" &&
                        item["command"]?.Value<string>() == "threads");
                Assert.True(DapTestProtocol.Required<bool>(
                    threads["success"]));
                Assert.Empty(
                    threads.SelectToken("body.threads")!.Children());
                var next = blocked.Single(
                    item =>
                        item["type"]?.Value<string>() == "response" &&
                        item["command"]?.Value<string>() == "next");
                Assert.False(DapTestProtocol.Required<bool>(
                    next["success"]));

                backend.RaiseReloadCompleted();
                var completed = recorder.Capture();

                Assert.Single(backend.Bound);
                Assert.Empty(backend.RemovedBreakpointIds);
                Assert.Contains(
                    completed,
                    item =>
                        item["event"]?.Value<string>() == "output" &&
                        item.SelectToken("body.output")?.Value<string>() ==
                            "Domain Reload complete; 1 verified, " +
                            "0 pending." +
                            System.Environment.NewLine);
            }
        }

        [Fact]
        public void Transport_reconnect_rebinds_breakpoint()
        {
            var backend = new FakeDebuggerBackend();
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
                                path =
                                    @"H:\fixture\Assets\Player.cs",
                            },
                            breakpoints = new[] { new { line = 12 } },
                        }));
                recorder.Capture();

                backend.RaiseConnectionLost();
                recorder.Capture();
                backend.RaiseReconnectCompleted();
                var completed = recorder.Capture();

                Assert.Equal(2, backend.Bound.Count);
                Assert.Single(backend.RemovedBreakpointIds);
                Assert.Equal(
                    new long[] { 1 },
                    backend.ActiveBreakpointIds);
                Assert.Contains(
                    completed,
                    item =>
                        item["event"]?.Value<string>() == "output" &&
                        item.SelectToken("body.output")?.Value<string>() ==
                            "Domain Reload complete; 1 rebound, " +
                            "0 pending." +
                            System.Environment.NewLine);

                Assert.True(backend.RaiseBreakpointHit(1, 42));
                var hit = recorder.Capture();
                Assert.Contains(
                    hit,
                    item =>
                        item["event"]?.Value<string>() == "stopped" &&
                        item.SelectToken("body.reason")?.Value<string>() ==
                            "breakpoint");
            }
        }
    }
}
