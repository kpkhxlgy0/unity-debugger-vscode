using System;
using System.Collections.Generic;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal static class InternalDebuggerLog
    {
        private static readonly HashSet<string> AllowedEventNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "unity-debugger.breakpoint.facade.bind.bound",
                "unity-debugger.breakpoint.facade.bind.pending",
                "unity-debugger.breakpoint.facade.status.bound.mapped",
                "unity-debugger.breakpoint.facade.status.bound.unmapped",
                "unity-debugger.breakpoint.facade.status.pending.mapped",
                "unity-debugger.breakpoint.facade.status.pending.unmapped",
                "unity-debugger.breakpoint.manager.bind.bound",
                "unity-debugger.breakpoint.manager.bind.pending",
                "unity-debugger.breakpoint.manager.status.bound.buffered",
                "unity-debugger.breakpoint.manager.status.bound.mapped",
                "unity-debugger.breakpoint.manager.status.bound.unmapped",
                "unity-debugger.breakpoint.manager.status.pending.accepted",
                "unity-debugger.breakpoint.manager.status.pending.buffered",
                "unity-debugger.breakpoint.manager.status.pending.mapped",
                "unity-debugger.breakpoint.manager.status.pending.suppressed",
                "unity-debugger.breakpoint.manager.status.pending.unmapped",
                "unity-debugger.breakpoint.dap.status.bound",
                "unity-debugger.breakpoint.dap.status.pending",
                "unity-debugger.reload.complete.bound",
                "unity-debugger.reload.complete.pending",
            };

        public static void Write(string eventName)
        {
            try
            {
                if (
                    DebuggerLoggingService.CustomLogger is
                        MonoDebuggerLogger logger)
                {
                    logger.LogInternalEvent(eventName);
                }
            }
            catch
            {
                // Diagnostics must never corrupt the DAP channel.
            }
        }

        public static bool IsAllowed(string eventName) =>
            AllowedEventNames.Contains(eventName);
    }
}
