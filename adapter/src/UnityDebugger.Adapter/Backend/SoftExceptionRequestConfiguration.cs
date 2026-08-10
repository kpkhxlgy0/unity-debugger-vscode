namespace UnityDebugger.Adapter.Backend
{
    internal interface ISoftExceptionRequestSession
    {
        void EnableUnhandledExceptions();
        void DisableUnhandledExceptions();
        void EnableOtherExceptions();
        void DisableOtherExceptions();
    }

    internal static class SoftExceptionRequestConfiguration
    {
        public static void Apply(
            ISoftExceptionRequestSession session,
            ExceptionBreakMode mode)
        {
            if (mode == ExceptionBreakMode.All)
            {
                session.DisableUnhandledExceptions();
                session.EnableOtherExceptions();
                return;
            }

            session.DisableOtherExceptions();
            if (mode == ExceptionBreakMode.Uncaught)
            {
                session.EnableUnhandledExceptions();
                return;
            }

            session.DisableUnhandledExceptions();
        }
    }
}
