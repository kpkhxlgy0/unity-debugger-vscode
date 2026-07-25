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
        public void Reload_marks_breakpoint_pending_then_rebinds_it()
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

                backend.RaiseReloadStarted();
                var started = recorder.Capture();

                Assert.Single(
                    started,
                    item =>
                        item["event"]?.Value<string>() == "continued");
                var pending = started.Single(
                    item =>
                        item["event"]?.Value<string>() == "breakpoint");
                Assert.False(DapTestProtocol.Required<bool>(
                    pending.SelectToken("body.breakpoint.verified")));
                Assert.Equal(
                    "Waiting for assemblies after Domain Reload.",
                    DapTestProtocol.Required<string>(
                        pending.SelectToken(
                            "body.breakpoint.message")));

                var blocked = recorder.Send(
                    DapTestProtocol.Request("threads", new { }),
                    DapTestProtocol.Request(
                        "next",
                        new { threadId = 1 }));
                Assert.All(
                    blocked.Where(
                        item =>
                            item["type"]?.Value<string>() == "response"),
                    item => Assert.False(
                        DapTestProtocol.Required<bool>(
                            item["success"])));

                backend.RaiseReloadCompleted();
                var completed = recorder.Capture();

                Assert.Equal(2, backend.Bound.Count);
                Assert.Contains(
                    completed,
                    item =>
                        item["event"]?.Value<string>() ==
                            "breakpoint" &&
                        item.SelectToken(
                            "body.breakpoint.verified")?.Value<bool>() ==
                            true);
            }
        }
    }
}
