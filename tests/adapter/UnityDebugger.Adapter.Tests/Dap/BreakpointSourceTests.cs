using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class BreakpointSourceTests
    {
        [Theory]
        [InlineData("path", true)]
        [InlineData("uri", true)]
        [InlineData("path", false)]
        [InlineData("uri", false)]
        public void StatusChangesPreserveClientSourceInsideAndOutsideWorkspace(string pathFormat, bool insideWorkspace)
        {
            var sourcePath = Path.Combine(Path.GetTempPath(), "unity-debugger-" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(sourcePath, "class Form_BattleName { }");
            try
            {
                var workspace = insideWorkspace ? Path.GetTempPath() : Path.Combine(Path.GetTempPath(), "other-workspace");
                var clientPath = pathFormat == "uri" ? new Uri(sourcePath).AbsoluteUri : sourcePath;
                var backend = new FakeDebuggerBackend { BindAsPending = true };
                var session = new UnityDebugSession(() => backend);
                using (var recorder = new DapTestProtocol.Recorder(session))
                {
                    var messages = recorder.Send(
                        DapTestProtocol.Request("initialize", new { linesStartAt1 = true, pathFormat }),
                        DapTestProtocol.Request("attach", new
                        {
                            __processId = 1234,
                            __host = "127.0.0.1",
                            __port = 56234,
                            __workspaceRoot = workspace,
                            __projectVersion = "2022.3.62t11",
                        }),
                        DapTestProtocol.Request("setBreakpoints", new
                        {
                            source = new { path = clientPath },
                            breakpoints = new[] { new { line = 786 } },
                        }));
                    var initial = DapTestProtocol.Response(messages, "setBreakpoints")["body"]!["breakpoints"]![0]!;
                    Assert.Equal(clientPath, initial["source"]!["path"]!.Value<string>());

                    foreach (var verified in new[] { true, false, true })
                    {
                        backend.RaiseBreakpointChanged(new BackendBreakpointChangedEventArgs(
                            new BackendBoundBreakpoint(1, verified, 786, verified ? null : "Symbols are not loaded.")));
                        var changed = Assert.Single(DapTestProtocol.Events(recorder.Capture(), "breakpoint"));
                        var breakpoint = changed["body"]!["breakpoint"]!;
                        Assert.Equal(initial["id"]!.Value<long>(), breakpoint["id"]!.Value<long>());
                        Assert.Equal(verified, breakpoint["verified"]!.Value<bool>());
                        Assert.Equal(786, breakpoint["line"]!.Value<int>());
                        Assert.Equal(Path.GetFileName(sourcePath), breakpoint["source"]!["name"]!.Value<string>());
                        Assert.Equal(clientPath, breakpoint["source"]!["path"]!.Value<string>());
                        Assert.Equal(0, breakpoint["source"]!["sourceReference"]!.Value<int>());
                    }
                }
            }
            finally
            {
                File.Delete(sourcePath);
            }
        }
    }
}
