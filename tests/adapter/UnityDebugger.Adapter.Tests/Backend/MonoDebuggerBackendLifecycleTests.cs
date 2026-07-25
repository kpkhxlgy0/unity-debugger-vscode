using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MonoDebuggerBackendLifecycleTests
    {
        [Fact]
        public void Attach_uses_loopback_port_and_expected_retry_policy()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                backend.Attach(Target(port: 56234));

                Assert.True(backend.IsAttached);
                Assert.Equal(IPAddress.Loopback, facade.ConnectAddress);
                Assert.Equal(56234, facade.ConnectPort);
                Assert.Equal(10, facade.MaxConnectionAttempts);
                Assert.Equal(
                    500,
                    facade.ConnectionAttemptIntervalMilliseconds);
            }
        }

        [Fact]
        public void Disconnect_detaches_and_disposes_exactly_once()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                backend.Attach(Target());
                backend.Disconnect();
                backend.Disconnect();

                Assert.Equal(1, facade.DetachCount);
                Assert.Equal(1, facade.DisposeCount);
            }
        }

        [Fact]
        public void Attach_cannot_run_twice()
        {
            using (var backend = new MonoDebuggerBackend(
                () => new FakeSoftDebuggerSessionFacade()))
            {
                backend.Attach(Target());

                Assert.Throws<InvalidOperationException>(
                    () => backend.Attach(Target()));
            }
        }

        [Fact]
        public void Target_exit_raises_terminated_once()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                var count = 0;
                backend.Terminated += (_, __) => count++;
                backend.Attach(Target());

                facade.RaiseTargetExited();
                facade.RaiseTargetExited();

                Assert.Equal(1, count);
                Assert.False(backend.IsAttached);
            }
        }

        [Fact]
        public void Protocol_mismatch_returns_a_stable_actionable_error()
        {
            var facade = new FakeSoftDebuggerSessionFacade
            {
                ConnectException = new VMMismatchException(),
            };
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                var error = Assert.Throws<DebuggerBackendException>(
                    () => backend.Attach(Target()));

                Assert.Equal(
                    "Editor uses an incompatible Mono Soft Debugger protocol.",
                    error.Message);
                Assert.DoesNotContain(
                    nameof(VMMismatchException),
                    error.ToString());
            }
        }

        [Fact]
        public void Connection_failure_exposes_only_loopback_and_guidance()
        {
            const string Secret = "SECRET-TRANSPORT-MESSAGE";
            var facade = new FakeSoftDebuggerSessionFacade
            {
                ConnectException = new InvalidOperationException(Secret),
            };
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                var error = Assert.Throws<DebuggerBackendException>(
                    () => backend.Attach(Target(port: 56234)));

                Assert.Contains("127.0.0.1:56234", error.Message);
                Assert.Contains("Editor process is alive", error.Message);
                Assert.Contains(
                    "Code Optimization is set to Debug",
                    error.Message);
                Assert.Contains("local firewall", error.Message);
                Assert.DoesNotContain(Secret, error.ToString());
            }
        }

        [Fact]
        public void Attach_rejects_a_non_loopback_target_again()
        {
            using (var backend = new MonoDebuggerBackend(
                () => new FakeSoftDebuggerSessionFacade()))
            {
                var target = new AttachTarget(
                    1234,
                    IPAddress.Parse("192.168.1.20"),
                    56234,
                    @"H:\fixture",
                    "2022.3.62t11");

                Assert.Throws<DebuggerBackendException>(
                    () => backend.Attach(target));
            }
        }

        [Fact]
        public void Breakpoint_operations_and_status_changes_are_forwarded()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                BackendBreakpointChangedEventArgs? changed = null;
                backend.BreakpointChanged += (_, arguments) =>
                    changed = arguments;
                backend.Attach(Target());
                var logical = new LogicalBreakpoint(
                    7,
                    @"H:\fixture\Assets\Player.cs",
                    12,
                    1,
                    "health <= 0",
                    null,
                    null);

                var bound = backend.BindBreakpoint(logical);
                facade.RaiseBreakpointChanged(
                    new BackendBreakpointChangedEventArgs(bound));
                backend.RemoveBreakpoint(bound.Id);

                Assert.Single(facade.Bound);
                Assert.Equal("health <= 0", facade.Bound[0].Condition);
                Assert.Same(bound, changed?.Breakpoint);
                Assert.Equal(1, facade.RemoveBreakpointCount);
            }
        }

        [Fact]
        public void Inspection_operations_are_forwarded_to_the_facade()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            facade.Threads.Add(new BackendThread(42, "Main"));
            facade.Frames.Add(
                new BackendStackFrame(
                    5,
                    42,
                    "Update",
                    @"H:\fixture\Assets\Player.cs",
                    12,
                    1));
            facade.Scopes.Add(new BackendScope("Locals", 8, false));
            facade.Variables.Add(
                new BackendVariable("health", "42", "System.Int32", 0));
            facade.EvaluationResult =
                new BackendEvaluationResult("42", "System.Int32", 0);

            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                backend.Attach(Target());

                Assert.Same(facade.Threads, backend.GetThreads());
                Assert.Same(
                    facade.Frames,
                    backend.GetStackTrace(42, 0, 20));
                Assert.Same(facade.Scopes, backend.GetScopes(5));
                Assert.Same(
                    facade.Variables,
                    backend.GetVariables(8));
                Assert.Same(
                    facade.EvaluationResult,
                    backend.Evaluate(5, "health"));
            }
        }

        [Fact]
        public void Control_exception_mode_and_continued_event_are_forwarded()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggerBackend(() => facade))
            {
                var continued = 0;
                backend.Continued += (_, __) => continued++;
                backend.Attach(Target());

                backend.Pause(42);
                backend.StepIn(42);
                backend.StepOver(42);
                backend.StepOut(42);
                backend.ConfigureExceptions(ExceptionBreakMode.All);
                facade.RaiseTargetStarted();

                Assert.Equal(1, facade.PauseCount);
                Assert.Equal(1, facade.StepInCount);
                Assert.Equal(1, facade.StepOverCount);
                Assert.Equal(1, facade.StepOutCount);
                Assert.Equal(
                    ExceptionBreakMode.All,
                    facade.ExceptionMode);
                Assert.Equal(1, continued);
            }
        }

        [Fact]
        public void Live_editor_transport_exit_recreates_only_the_facade()
        {
            var first = new FakeSoftDebuggerSessionFacade();
            var second = new FakeSoftDebuggerSessionFacade();
            var factoryCalls = 0;
            using (var coordinator = new AssemblyReloadCoordinator(
                (_, __) => Task.CompletedTask))
            using (var backend = new MonoDebuggerBackend(
                () => factoryCalls++ == 0 ? first : second,
                coordinator,
                new ReconnectController(),
                _ => true,
                (_, __) => Task.CompletedTask))
            {
                var reloads = 0;
                var terminated = 0;
                backend.ReloadStarted += (_, __) => reloads++;
                backend.Terminated += (_, __) => terminated++;
                backend.Attach(Target());
                backend.ConfigureExceptions(ExceptionBreakMode.All);

                first.RaiseTargetExited();

                Assert.True(
                    SpinWait.SpinUntil(
                        () => second.ConnectCount == 1,
                        TimeSpan.FromSeconds(2)));
                Assert.True(backend.IsAttached);
                Assert.Equal(1, reloads);
                Assert.Equal(0, terminated);
                Assert.Equal(
                    ExceptionBreakMode.All,
                    second.ExceptionMode);
                Assert.Equal(1, first.DisposeCount);
            }
        }

        private static AttachTarget Target(int port = 56234) =>
            new AttachTarget(
                1234,
                IPAddress.Loopback,
                port,
                @"H:\fixture",
                "2022.3.62t11");
    }
}
