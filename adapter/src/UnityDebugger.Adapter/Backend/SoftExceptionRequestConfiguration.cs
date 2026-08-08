namespace UnityDebugger.Adapter.Backend
{
    internal interface ISoftExceptionRequestSession
    {
        void EnableOtherExceptions();
        void DisableOtherExceptions();
    }

    internal static class SoftExceptionRequestConfiguration
    {
        public static void Apply(
            ISoftExceptionRequestSession session,
            ExceptionBreakMode mode)
        {
            session.DisableOtherExceptions();
            if (mode == ExceptionBreakMode.All)
                session.EnableOtherExceptions();
        }
    }
}
