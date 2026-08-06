using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class SoftDebuggerSessionFacadeBreakpointOptionTests
    {
        [Fact]
        public void AppliesConditionHitCountAndLogpointToMatureBreakEvent()
        {
            var breakpoint = new Breakpoint("Player.cs", 12, 1);

            SoftDebuggerSessionFacade.ApplyBreakpointOptions(
                breakpoint,
                "health <= 0",
                "3",
                "health={health}");

            Assert.Equal("health <= 0", breakpoint.ConditionExpression);
            Assert.Equal(HitCountMode.EqualTo, breakpoint.HitCountMode);
            Assert.Equal(3, breakpoint.HitCount);
            Assert.Equal(HitAction.PrintExpression, breakpoint.HitAction);
            Assert.Equal("health={health}", breakpoint.TraceExpression);
        }
    }
}
