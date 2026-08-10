using System.Reflection;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class ReferenceExceptionStopPolicyTests
    {
        [Theory]
        [InlineData(
            (int)ExceptionBreakMode.None,
            TargetEventType.ExceptionThrown,
            false)]
        [InlineData(
            (int)ExceptionBreakMode.None,
            TargetEventType.UnhandledException,
            false)]
        [InlineData(
            (int)ExceptionBreakMode.Uncaught,
            TargetEventType.ExceptionThrown,
            false)]
        [InlineData(
            (int)ExceptionBreakMode.Uncaught,
            TargetEventType.UnhandledException,
            true)]
        [InlineData(
            (int)ExceptionBreakMode.All,
            TargetEventType.ExceptionThrown,
            true)]
        [InlineData(
            (int)ExceptionBreakMode.All,
            TargetEventType.UnhandledException,
            true)]
        public void MapsFiltersToCaughtAndUnhandledStopEvents(
            int modeValue,
            TargetEventType eventType,
            bool expected)
        {
            var policyType = typeof(ExceptionBreakMode).Assembly.GetType(
                "UnityDebugger.Adapter.Backend.ReferenceExceptionStopPolicy");
            Assert.NotNull(policyType);
            var shouldStop = policyType!.GetMethod(
                "ShouldStop",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(shouldStop);

            var actual = (bool)shouldStop!.Invoke(
                null,
                new object[]
                {
                    (ExceptionBreakMode)modeValue,
                    eventType,
                })!;

            Assert.Equal(expected, actual);
        }
    }
}
