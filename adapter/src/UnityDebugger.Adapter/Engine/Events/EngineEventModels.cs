using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.Engine.Events
{
    internal enum EngineSuspendPolicy
    {
        None,
        EventThread,
        All,
    }

    internal enum EngineEventKind
    {
        VmStarted,
        VmDied,
        VmDisconnected,
        ThreadStarted,
        ThreadExited,
        AssemblyLoaded,
        AssemblyUnloaded,
        DomainCreated,
        DomainUnloaded,
        TypeLoaded,
        UserLog,
        UserBreak,
        Breakpoint,
        Step,
        Exception,
    }

    internal sealed class EngineEvent
    {
        private EngineEvent(
            EngineEventKind kind,
            long threadId,
            object? payload)
        {
            Kind = kind;
            ThreadId = threadId;
            Payload = payload;
        }

        public EngineEventKind Kind { get; }
        public long ThreadId { get; }
        public object? Payload { get; }

        public static EngineEvent VmStarted(object? payload = null) =>
            new EngineEvent(EngineEventKind.VmStarted, 0, payload);

        public static EngineEvent VmDied(object? payload = null) =>
            new EngineEvent(EngineEventKind.VmDied, 0, payload);

        public static EngineEvent VmDisconnected(object? payload = null) =>
            new EngineEvent(EngineEventKind.VmDisconnected, 0, payload);

        public static EngineEvent ThreadStarted(
            long threadId,
            object? payload = null) =>
            new EngineEvent(
                EngineEventKind.ThreadStarted,
                threadId,
                payload);

        public static EngineEvent ThreadExited(
            long threadId,
            object? payload = null) =>
            new EngineEvent(
                EngineEventKind.ThreadExited,
                threadId,
                payload);

        public static EngineEvent Breakpoint(
            long threadId,
            object? payload = null) =>
            new EngineEvent(
                EngineEventKind.Breakpoint,
                threadId,
                payload);

        public static EngineEvent Step(
            long threadId,
            object? payload = null) =>
            new EngineEvent(EngineEventKind.Step, threadId, payload);

        public static EngineEvent Exception(
            long threadId,
            object? payload = null) =>
            new EngineEvent(
                EngineEventKind.Exception,
                threadId,
                payload);

        public static EngineEvent UserBreak(
            long threadId,
            object? payload = null) =>
            new EngineEvent(
                EngineEventKind.UserBreak,
                threadId,
                payload);

        public static EngineEvent Create(
            EngineEventKind kind,
            long threadId = 0,
            object? payload = null) =>
            new EngineEvent(kind, threadId, payload);
    }

    internal sealed class EngineEventSet
    {
        public EngineEventSet(
            EngineSuspendPolicy suspendPolicy,
            params EngineEvent[] events)
        {
            SuspendPolicy = suspendPolicy;
            Events = Array.AsReadOnly(
                events ?? throw new ArgumentNullException(nameof(events)));
        }

        public EngineSuspendPolicy SuspendPolicy { get; }
        public IReadOnlyList<EngineEvent> Events { get; }
    }
}
