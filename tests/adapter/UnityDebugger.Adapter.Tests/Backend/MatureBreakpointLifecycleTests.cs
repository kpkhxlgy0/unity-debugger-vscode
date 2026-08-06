using System.IO;
using System.Net;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureBreakpointLifecycleTests
    {
        [Fact]
        public void OrdinaryAssemblyReloadKeepsLogicalBreakpointsInTheMatureStore()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());
                var first = backend.BindBreakpoint(Breakpoint(11));

                facade.RaiseAssemblyUnloaded();
                facade.RaiseAssemblyLoaded();
                facade.RaiseBreakpointChanged(
                    new BackendBreakpointChangedEventArgs(
                        new BackendBoundBreakpoint(
                            first.Id,
                            true,
                            11,
                            null)));

                Assert.Single(facade.Bound);
                Assert.True(facade.ContainsBreakpoint(first.Id));
            }
        }

        [Fact]
        public void SourceBreakpointMetadataPassesThroughUnchanged()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());
                var requested = new LogicalBreakpoint(
                    5,
                    Path.Combine(@"D:\Fixture", "Player.cs"),
                    11,
                    1,
                    "health <= 0",
                    "3",
                    "health={health}");

                backend.BindBreakpoint(requested);

                Assert.Same(requested, Assert.Single(facade.Bound));
            }
        }

        [Fact]
        public void FullyQualifiedFunctionBreakpointUsesTheMatureStore()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());
                var requested = new LogicalFunctionBreakpoint(
                    8,
                    "MyGame.Runtime.DevTools.GamePrototypeRuntime." +
                    "EnsureStyles",
                    null,
                    null);

                var bound = backend.BindFunctionBreakpoint(requested);

                Assert.True(bound.Verified);
                Assert.Same(requested, Assert.Single(facade.FunctionBound));
                Assert.True(facade.ContainsBreakpoint(bound.Id));
            }
        }

        [Fact]
        public void MatureBreakpointStatusIsForwardedExactly()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                BackendBreakpointChangedEventArgs? observed = null;
                backend.BreakpointChanged += (_, value) => observed = value;
                backend.Attach(Target());
                var expected = new BackendBreakpointChangedEventArgs(
                    new BackendBoundBreakpoint(
                        4,
                        false,
                        11,
                        "Symbols are not loaded."));

                facade.RaiseBreakpointChanged(expected);

                Assert.Same(expected, observed);
            }
        }

        private static AttachTarget Target() => new AttachTarget(
            123,
            IPAddress.Loopback,
            56000,
            @"D:\Fixture",
            "2022.3.62t12");

        private static LogicalBreakpoint Breakpoint(int line) =>
            new LogicalBreakpoint(
                line,
                Path.Combine(@"D:\Fixture", "Player.cs"),
                line,
                1,
                null,
                null,
                null);
    }
}
