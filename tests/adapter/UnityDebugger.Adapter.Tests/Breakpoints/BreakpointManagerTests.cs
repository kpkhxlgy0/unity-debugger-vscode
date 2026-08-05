using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
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
        public void Rejected_breakpoint_remains_pending()
        {
            var backend = new FakeDebuggerBackend
            {
                RejectBreakpoint = true,
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
        public void Pending_breakpoint_becomes_verified_after_bound_status()
        {
            var backend = new FakeDebuggerBackend
            {
                BindAsPending = true,
            };
            using (var manager = new BreakpointManager(backend))
            {
                var initial = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) });

                Assert.False(initial[0].Verified);
                Assert.Equal("Symbols are not loaded.", initial[0].Message);

                backend.RaiseBreakpointChanged(
                    new BackendBreakpointChangedEventArgs(
                        new BackendBoundBreakpoint(
                            1,
                            true,
                            12,
                            null)));
                var afterStatus = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) });

                Assert.True(afterStatus[0].Verified);
                Assert.Null(afterStatus[0].Message);
            }
        }

        [Fact]
        public void Bound_change_during_bind_is_not_lost()
        {
            var backend = new FakeDebuggerBackend
            {
                BindAsPending = true,
                RaiseBoundBreakpointBeforeBindReturns = true,
            };
            using (var manager = new BreakpointManager(backend))
            {
                var result = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) });

                Assert.True(result[0].Verified);
                Assert.Null(result[0].Message);
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
        public async Task Removed_entry_discards_late_rebind_result()
        {
            var backend = new FakeDebuggerBackend();
            using (var manager = new BreakpointManager(backend))
            using (var bindEntered = new ManualResetEventSlim())
            using (var continueBind = new ManualResetEventSlim())
            {
                manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "ready") });
                backend.BindEnteredSignal = bindEntered;
                backend.ContinueBindSignal = continueBind;

                var rebind = Task.Run(
                    () => manager.ReplaceForSource(
                        @"H:\fixture\Assets\Player.cs",
                        new[]
                        {
                            new RequestedBreakpoint(12, "!ready"),
                        }));
                Assert.True(bindEntered.Wait(TimeSpan.FromSeconds(2)));

                var removed = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    Array.Empty<RequestedBreakpoint>());
                Assert.Empty(removed);

                continueBind.Set();
                await rebind;

                Assert.Empty(backend.ActiveBreakpointIds);
            }
        }

        [Fact]
        public async Task Concurrent_condition_change_and_reload_keep_one_binding()
        {
            var backend = new FakeDebuggerBackend();
            using (var manager = new BreakpointManager(backend))
            using (var removeEntered = new ManualResetEventSlim())
            using (var continueRemove = new ManualResetEventSlim())
            using (var firstBindEntered = new ManualResetEventSlim())
            using (var bindsEntered = new CountdownEvent(2))
            using (var continueBind = new ManualResetEventSlim())
            {
                manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, "ready") });
                backend.RemoveEnteredSignal = removeEntered;
                backend.ContinueRemoveSignal = continueRemove;
                backend.BindEnteredSignal = firstBindEntered;
                backend.BindsEnteredCountdown = bindsEntered;
                backend.ContinueBindSignal = continueBind;

                var conditionChange = Task.Run(
                    () => manager.ReplaceForSource(
                        @"H:\fixture\Assets\Player.cs",
                        new[]
                        {
                            new RequestedBreakpoint(12, "!ready"),
                        }));
                Assert.True(removeEntered.Wait(TimeSpan.FromSeconds(2)));

                var reloadRebind = Task.Run(manager.RebindAll);
                Assert.True(firstBindEntered.Wait(TimeSpan.FromSeconds(2)));
                continueRemove.Set();
                Assert.True(bindsEntered.Wait(TimeSpan.FromSeconds(2)));
                continueBind.Set();

                await Task.WhenAll(conditionChange, reloadRebind);

                Assert.Single(backend.ActiveBreakpointIds);
            }
        }

        [Fact]
        public void RebindPending_preserves_id_and_retries_only_pending()
        {
            var backend = new FakeDebuggerBackend
            {
                RejectBreakpoint = true,
            };
            using (var manager = new BreakpointManager(backend))
            {
                var pending = manager.ReplaceForSource(
                    @"H:\fixture\Assets\Player.cs",
                    new[] { new RequestedBreakpoint(12, null) })[0];
                backend.RejectBreakpoint = false;

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
