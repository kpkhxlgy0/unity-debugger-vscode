using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal static class InternalDebuggerLog
    {
        public static Action<string>? Sink { get; set; }

        // A permissive gate, not a mirror of the writers, and the difference is
        // deliberate rather than drift: several entries below already have no
        // producer under adapter/src and are retained as the permitted
        // vocabulary for their producer family. A name that is emitted without
        // an entry here is silently dropped instead of logged, so a new writer
        // must add its name.
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
                if (IsAllowed(eventName))
                    Sink?.Invoke(eventName);
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
