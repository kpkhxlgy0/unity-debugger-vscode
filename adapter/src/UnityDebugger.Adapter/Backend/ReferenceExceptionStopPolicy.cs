using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Backend
{
    internal static class ReferenceExceptionStopPolicy
    {
        public static bool ShouldStop(
            ExceptionBreakMode mode,
            TargetEventType eventType)
        {
            if (eventType == TargetEventType.UnhandledException)
                return mode != ExceptionBreakMode.None;
            return
                eventType == TargetEventType.ExceptionThrown &&
                mode == ExceptionBreakMode.All;
        }
    }
}
