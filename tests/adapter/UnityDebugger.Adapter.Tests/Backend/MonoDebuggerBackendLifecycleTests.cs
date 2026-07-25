using System;
using System.Net;
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

        private static AttachTarget Target(int port = 56234) =>
            new AttachTarget(
                1234,
                IPAddress.Loopback,
                port,
                @"H:\fixture",
                "2022.3.62t11");
    }
}
