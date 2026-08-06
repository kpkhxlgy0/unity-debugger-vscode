using System.Net;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureControlTests
    {
        [Fact]
        public void TargetedStepAndGotoDelegateDirectlyToTheMatureFacade()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.StepIn(42, 51);
                backend.Goto(42, 71);

                Assert.Equal(42, facade.LastStepInThreadId);
                Assert.Equal(51, facade.LastStepInTargetId);
                Assert.Equal(42, facade.LastGotoThreadId);
                Assert.Equal(71, facade.LastGotoTargetId);
            }
        }

        [Fact]
        public void OrdinaryControlCallsRemainOneToOne()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.Continue(42);
                backend.Pause(42);
                backend.StepIn(42, null);
                backend.StepOver(42);
                backend.StepOut(42);

                Assert.Equal(1, facade.ContinueCount);
                Assert.Equal(1, facade.PauseCount);
                Assert.Equal(1, facade.StepInCount);
                Assert.Equal(1, facade.StepOverCount);
                Assert.Equal(1, facade.StepOutCount);
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
