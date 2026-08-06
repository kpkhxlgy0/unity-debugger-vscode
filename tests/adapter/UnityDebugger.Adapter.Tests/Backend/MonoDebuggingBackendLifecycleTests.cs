using System;
using System.Net;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MonoDebuggingBackendLifecycleTests
    {
        [Fact]
        public void AttachReadyAndAssemblyEventsDoNotCreateAUserStop()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                var stops = 0;
                backend.Stopped += (_, __) => stops++;

                backend.Attach(Target());
                facade.RaiseAssemblyUnloaded();
                facade.RaiseAssemblyLoaded();

                Assert.True(backend.IsAttached);
                Assert.Equal(0, stops);
            }
        }

        [Fact]
        public void MatureBreakpointStopIsForwardedOnceWithItsBackendId()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                BackendStoppedEventArgs? observed = null;
                var stops = 0;
                backend.Stopped += (_, value) =>
                {
                    stops++;
                    observed = value;
                };
                backend.Attach(Target());

                facade.RaiseTargetStopped(new BackendStoppedEventArgs(
                    BackendStopReason.Breakpoint,
                    42,
                    null,
                    new long[] { 7 }));

                Assert.Equal(1, stops);
                Assert.Equal(BackendStopReason.Breakpoint, observed?.Reason);
                Assert.Equal(new long[] { 7 }, observed?.BreakpointIds);
            }
        }

        [Fact]
        public void DisconnectAndDisposeReleaseTheFacadeOnlyOnce()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            var backend = new MonoDebuggingBackend(() => facade);
            backend.Attach(Target());

            backend.Disconnect();
            backend.Disconnect();
            backend.Dispose();
            backend.Dispose();

            Assert.False(backend.IsAttached);
            Assert.Equal(1, facade.DetachCount);
            Assert.Equal(1, facade.DisposeCount);
        }

        [Fact]
        public void AttachRejectsANonLoopbackTargetBeforeCreatingAFacade()
        {
            var facadeCreated = false;
            using (var backend = new MonoDebuggingBackend(
                () =>
                {
                    facadeCreated = true;
                    return new FakeSoftDebuggerSessionFacade();
                }))
            {
                var target = new AttachTarget(
                    123,
                    IPAddress.Parse("192.0.2.10"),
                    56000,
                    @"D:\Fixture",
                    "2022.3.62t12");

                Assert.Throws<DebuggerBackendException>(
                    () => backend.Attach(target));
                Assert.False(facadeCreated);
            }
        }

        [Fact]
        public void ProcessExitRaisesTerminationExactlyOnce()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                var terminations = 0;
                backend.Terminated += (_, __) => terminations++;
                backend.Attach(Target());

                facade.RaiseTargetExited();
                facade.RaiseTargetExited();

                Assert.False(backend.IsAttached);
                Assert.Equal(1, terminations);
            }
        }

        [Fact]
        public void MatureEventsAreForwardedWithoutSyntheticStops()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                BackendThreadEventArgs? thread = null;
                BackendModuleChangedEventArgs? module = null;
                BackendOutputEventArgs? output = null;
                var stops = 0;
                backend.ThreadChanged += (_, value) => thread = value;
                backend.ModuleChanged += (_, value) => module = value;
                backend.Output += (_, value) => output = value;
                backend.Stopped += (_, __) => stops++;
                backend.Attach(Target());

                var expectedThread = new BackendThreadEventArgs(9, true);
                var expectedModule = new BackendModuleChangedEventArgs(
                    new BackendModule("m1", "Fixture", null, true),
                    true);
                var expectedOutput = new BackendOutputEventArgs(
                    "stdout",
                    "ready");
                facade.RaiseThreadChanged(expectedThread);
                facade.RaiseModuleChanged(expectedModule);
                facade.RaiseOutput(expectedOutput);
                facade.RaiseTargetStarted();

                Assert.Same(expectedThread, thread);
                Assert.Same(expectedModule, module);
                Assert.Same(expectedOutput, output);
                Assert.Equal(0, stops);
            }
        }

        [Fact]
        public void ControlAndExceptionCommandsDelegateDirectly()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.Continue(1);
                backend.Pause(1);
                backend.StepIn(1, null);
                backend.StepOver(1);
                backend.StepOut(1);
                backend.ConfigureExceptions(ExceptionBreakMode.Uncaught);

                Assert.Equal(1, facade.ContinueCount);
                Assert.Equal(1, facade.PauseCount);
                Assert.Equal(1, facade.StepInCount);
                Assert.Equal(1, facade.StepOverCount);
                Assert.Equal(1, facade.StepOutCount);
                Assert.Equal(ExceptionBreakMode.Uncaught, facade.ExceptionMode);
            }
        }

        private static AttachTarget Target() => new AttachTarget(
            123,
            IPAddress.Loopback,
            56000,
            @"D:\Fixture",
            "2022.3.62t12");
    }
}
