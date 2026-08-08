using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class SoftExceptionRequestConfigurationTests
    {
        [Fact]
        public void AllExceptionsUsesTheDedicatedOtherExceptionsRequest()
        {
            var session = new RecordingExceptionRequestSession();

            SoftExceptionRequestConfiguration.Apply(
                session,
                ExceptionBreakMode.All);

            Assert.Equal(1, session.DisableOtherExceptionsCount);
            Assert.Equal(1, session.EnableOtherExceptionsCount);
        }

        [Fact]
        public void ModesWithoutThrownExceptionsDisableTheOtherRequest()
        {
            var noneSession = new RecordingExceptionRequestSession();
            var uncaughtSession = new RecordingExceptionRequestSession();

            SoftExceptionRequestConfiguration.Apply(
                noneSession,
                ExceptionBreakMode.None);
            SoftExceptionRequestConfiguration.Apply(
                uncaughtSession,
                ExceptionBreakMode.Uncaught);

            Assert.Equal(1, noneSession.DisableOtherExceptionsCount);
            Assert.Equal(0, noneSession.EnableOtherExceptionsCount);
            Assert.Equal(1, uncaughtSession.DisableOtherExceptionsCount);
            Assert.Equal(0, uncaughtSession.EnableOtherExceptionsCount);
        }

        private sealed class RecordingExceptionRequestSession :
            ISoftExceptionRequestSession
        {
            public int EnableOtherExceptionsCount { get; private set; }
            public int DisableOtherExceptionsCount { get; private set; }

            public void EnableOtherExceptions()
            {
                EnableOtherExceptionsCount++;
            }

            public void DisableOtherExceptions()
            {
                DisableOtherExceptionsCount++;
            }
        }
    }
}
