using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class SoftExceptionRequestConfigurationTests
    {
        [Fact]
        public void AllExceptionsKeepsOnlyTheAllExceptionsRequestActive()
        {
            var session = new RecordingExceptionRequestSession(
                unhandledExceptionsEnabled: true,
                otherExceptionsEnabled: true);

            SoftExceptionRequestConfiguration.Apply(
                session,
                ExceptionBreakMode.All);

            Assert.False(session.UnhandledExceptionsEnabled);
            Assert.True(session.OtherExceptionsEnabled);
        }

        [Fact]
        public void UncaughtExceptionsKeepsOnlyTheUnhandledRequestActive()
        {
            var session = new RecordingExceptionRequestSession(
                unhandledExceptionsEnabled: false,
                otherExceptionsEnabled: true);

            SoftExceptionRequestConfiguration.Apply(
                session,
                ExceptionBreakMode.Uncaught);

            Assert.True(session.UnhandledExceptionsEnabled);
            Assert.False(session.OtherExceptionsEnabled);
        }

        [Fact]
        public void NoExceptionsDisablesBothExceptionRequests()
        {
            var session = new RecordingExceptionRequestSession(
                unhandledExceptionsEnabled: true,
                otherExceptionsEnabled: true);

            SoftExceptionRequestConfiguration.Apply(
                session,
                ExceptionBreakMode.None);

            Assert.False(session.UnhandledExceptionsEnabled);
            Assert.False(session.OtherExceptionsEnabled);
        }

        private sealed class RecordingExceptionRequestSession :
            ISoftExceptionRequestSession
        {
            public RecordingExceptionRequestSession(
                bool unhandledExceptionsEnabled,
                bool otherExceptionsEnabled)
            {
                UnhandledExceptionsEnabled = unhandledExceptionsEnabled;
                OtherExceptionsEnabled = otherExceptionsEnabled;
            }

            public bool UnhandledExceptionsEnabled { get; private set; }
            public bool OtherExceptionsEnabled { get; private set; }

            public void EnableUnhandledExceptions()
            {
                UnhandledExceptionsEnabled = true;
            }

            public void DisableUnhandledExceptions()
            {
                UnhandledExceptionsEnabled = false;
            }

            public void EnableOtherExceptions()
            {
                OtherExceptionsEnabled = true;
            }

            public void DisableOtherExceptions()
            {
                OtherExceptionsEnabled = false;
            }
        }
    }
}
