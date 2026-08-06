using Xunit;

namespace UnityDebugger.Adapter.Tests.Build
{
    public sealed class RuntimeDependencyBoundaryTests
    {
        [Fact]
        public void SolutionExposesThePinnedMatureDebuggerStack()
        {
            Assert.Equal(
                "Mono.Debugging",
                typeof(Mono.Debugging.Client.EvaluationOptions)
                    .Assembly.GetName().Name);
            Assert.Equal(
                "Mono.Debugging.Soft",
                typeof(Mono.Debugging.Soft.SoftDebuggerSession)
                    .Assembly.GetName().Name);
            Assert.Equal(
                "Mono.Debugger.Soft",
                typeof(Mono.Debugger.Soft.VirtualMachine)
                    .Assembly.GetName().Name);
        }
    }
}
