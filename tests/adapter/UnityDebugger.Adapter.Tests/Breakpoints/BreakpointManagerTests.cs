using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Breakpoints;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Breakpoints
{
    public sealed class BreakpointManagerTests
    {
        [Fact]
        public void ReplaceForSource_preserves_breakpoint_and_condition()
        {
            var backend = new FakeDebuggerBackend();
            using (var manager = new BreakpointManager(backend))
            {
                var result = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[]
                    {
                        new RequestedBreakpoint(12, "health <= 0"),
                    });

                Assert.Single(result);
                Assert.True(result[0].Verified);
                Assert.Equal(
                    "health <= 0",
                    backend.Bound.Single().Condition);
            }
        }

        [Fact]
        public void Missing_symbols_leave_breakpoint_pending()
        {
            var backend = new FakeDebuggerBackend
            {
                BindAsPending = true,
            };
            using (var manager = new BreakpointManager(backend))
            {
                var result = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) });

                Assert.False(result[0].Verified);
                Assert.Equal(
                    "Symbols are not loaded.",
                    result[0].Message);
            }
        }

        [Fact]
        public void Unchanged_breakpoint_keeps_logical_id_without_rebinding()
        {
            var backend = new FakeDebuggerBackend();
            using (var manager = new BreakpointManager(backend))
            {
                var first = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "ready") });
                var second = manager.ReplaceForSource(
                    @"h:\FIXTURE\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "ready") });

                Assert.Equal(first[0].Id, second[0].Id);
                Assert.Single(backend.Bound);
                Assert.Empty(backend.RemovedBreakpointIds);
            }
        }

        [Fact]
        public void Changed_condition_removes_and_rebinds_same_logical_id()
        {
            var backend = new FakeDebuggerBackend();
            using (var manager = new BreakpointManager(backend))
            {
                var first = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "ready") });
                var second = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "!ready") });

                Assert.Equal(first[0].Id, second[0].Id);
                Assert.Equal(2, backend.Bound.Count);
                Assert.Single(backend.RemovedBreakpointIds);
                Assert.Equal("!ready", backend.Bound[1].Condition);
            }
        }

        [Fact]
        public void RebindPending_preserves_id_and_retries_only_pending()
        {
            var backend = new FakeDebuggerBackend
            {
                BindAsPending = true,
            };
            using (var manager = new BreakpointManager(backend))
            {
                var pending = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) })[0];
                backend.BindAsPending = false;

                manager.RebindPending();
                var rebound = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) })[0];

                Assert.Equal(pending.Id, rebound.Id);
                Assert.True(rebound.Verified);
                Assert.Equal(2, backend.Bound.Count);
            }
        }

        [Fact]
        public void Serialized_DAP_breakpoint_never_exposes_backend_id()
        {
            var breakpoint = new DapBreakpoint(
                42,
                true,
                null,
                new DapSource("Player.cs", @"H:\fixture\Player.cs", 0),
                12,
                1);

            var json = JObject.Parse(
                JsonConvert.SerializeObject(breakpoint));

            Assert.Equal(42, json.Value<long>("id"));
            Assert.Null(json["backendId"]);
            Assert.Null(json["BackendId"]);
        }
    }
}
