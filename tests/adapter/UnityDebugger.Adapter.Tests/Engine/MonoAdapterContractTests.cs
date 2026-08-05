using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Events;
using UnityDebugger.Adapter.Engine.Mono;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine
{
    public sealed class MonoAdapterContractTests
    {
        [Theory]
        [InlineData(EventType.VMStart, (int)EngineEventKind.VmStarted)]
        [InlineData(EventType.VMDeath, (int)EngineEventKind.VmDied)]
        [InlineData(EventType.VMDisconnect, (int)EngineEventKind.VmDisconnected)]
        [InlineData(EventType.ThreadStart, (int)EngineEventKind.ThreadStarted)]
        [InlineData(EventType.ThreadDeath, (int)EngineEventKind.ThreadExited)]
        [InlineData(EventType.AssemblyLoad, (int)EngineEventKind.AssemblyLoaded)]
        [InlineData(EventType.AssemblyUnload, (int)EngineEventKind.AssemblyUnloaded)]
        [InlineData(EventType.AppDomainCreate, (int)EngineEventKind.DomainCreated)]
        [InlineData(EventType.AppDomainUnload, (int)EngineEventKind.DomainUnloaded)]
        [InlineData(EventType.TypeLoad, (int)EngineEventKind.TypeLoaded)]
        [InlineData(EventType.UserLog, (int)EngineEventKind.UserLog)]
        [InlineData(EventType.UserBreak, (int)EngineEventKind.UserBreak)]
        [InlineData(EventType.Breakpoint, (int)EngineEventKind.Breakpoint)]
        [InlineData(EventType.Step, (int)EngineEventKind.Step)]
        [InlineData(EventType.Exception, (int)EngineEventKind.Exception)]
        public void MonoEventKindsMapWithoutExecutionStateInference(
            EventType source,
            int expected)
        {
            Assert.Equal(
                (EngineEventKind)expected,
                MonoEventSource.MapEventKind(source));
        }

        [Theory]
        [InlineData(SuspendPolicy.None, (int)EngineSuspendPolicy.None)]
        [InlineData(SuspendPolicy.EventThread, (int)EngineSuspendPolicy.EventThread)]
        [InlineData(SuspendPolicy.All, (int)EngineSuspendPolicy.All)]
        public void SuspendPolicyMapsByMeaning(
            SuspendPolicy source,
            int expected)
        {
            Assert.Equal(
                (EngineSuspendPolicy)expected,
                MonoEventSource.MapSuspendPolicy(source));
        }

        [Theory]
        [InlineData((int)EngineStepDepth.Into, StepDepth.Into)]
        [InlineData((int)EngineStepDepth.Over, StepDepth.Over)]
        [InlineData((int)EngineStepDepth.Out, StepDepth.Out)]
        public void StepDepthMapsByMeaning(
            int source,
            StepDepth expected)
        {
            Assert.Equal(
                expected,
                MonoStepRuntime.MapDepth((EngineStepDepth)source));
        }

        [Fact]
        public void StepSizeAndFiltersMapToExactMonoValues()
        {
            var filters =
                EngineStepFilter.StaticConstructor |
                EngineStepFilter.DebuggerHidden |
                EngineStepFilter.DebuggerStepThrough |
                EngineStepFilter.DebuggerNonUserCode;

            Assert.Equal(
                StepSize.Line,
                MonoStepRuntime.MapSize(EngineStepSize.Line));
            Assert.Equal(
                StepFilter.StaticCtor |
                StepFilter.DebuggerHidden |
                StepFilter.DebuggerStepThrough |
                StepFilter.DebuggerNonUserCode,
                MonoStepRuntime.MapFilter(filters));
        }

        [Theory]
        [InlineData(EventType.KeepAlive)]
        [InlineData(EventType.Crash)]
        [InlineData(EventType.MethodEntry)]
        [InlineData(EventType.MethodExit)]
        public void ReferenceIgnoredEventsAreNotTranslated(EventType source)
        {
            Assert.False(MonoEventSource.IsSupported(source));
        }
    }
}
