using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Events;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoEventSource : IEngineEventSource
    {
        private readonly VirtualMachine virtualMachine;
        private readonly Func<Event, EngineEventKind?>? eventKindOverride;

        public MonoEventSource(
            VirtualMachine virtualMachine,
            Func<Event, EngineEventKind?>? eventKindOverride = null)
        {
            this.virtualMachine = virtualMachine ??
                throw new ArgumentNullException(nameof(virtualMachine));
            this.eventKindOverride = eventKindOverride;
        }

        public EngineEventSet GetNextEventSet(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceSet = virtualMachine.GetNextEventSet();
            var events = new List<EngineEvent>(sourceSet.Events.Length);
            foreach (var sourceEvent in sourceSet.Events)
            {
                if (!IsSupported(sourceEvent.EventType))
                    continue;
                var threadId = sourceEvent.Thread?.ThreadId ?? 0;
                events.Add(
                    EngineEvent.Create(
                        eventKindOverride?.Invoke(sourceEvent) ??
                            MapEventKind(sourceEvent.EventType),
                        threadId,
                        sourceEvent));
            }
            return new EngineEventSet(
                MapSuspendPolicy(sourceSet.SuspendPolicy),
                events.ToArray());
        }

        public void Resume()
        {
            virtualMachine.Resume();
        }

        internal static bool IsSupported(EventType value) =>
            value == EventType.VMStart ||
            value == EventType.VMDeath ||
            value == EventType.VMDisconnect ||
            value == EventType.ThreadStart ||
            value == EventType.ThreadDeath ||
            value == EventType.AssemblyLoad ||
            value == EventType.AssemblyUnload ||
            value == EventType.AppDomainCreate ||
            value == EventType.AppDomainUnload ||
            value == EventType.TypeLoad ||
            value == EventType.UserLog ||
            value == EventType.UserBreak ||
            value == EventType.Breakpoint ||
            value == EventType.Step ||
            value == EventType.Exception;

        internal static EngineEventKind MapEventKind(EventType value)
        {
            switch (value)
            {
                case EventType.VMStart:
                    return EngineEventKind.VmStarted;
                case EventType.VMDeath:
                    return EngineEventKind.VmDied;
                case EventType.VMDisconnect:
                    return EngineEventKind.VmDisconnected;
                case EventType.ThreadStart:
                    return EngineEventKind.ThreadStarted;
                case EventType.ThreadDeath:
                    return EngineEventKind.ThreadExited;
                case EventType.AssemblyLoad:
                    return EngineEventKind.AssemblyLoaded;
                case EventType.AssemblyUnload:
                    return EngineEventKind.AssemblyUnloaded;
                case EventType.AppDomainCreate:
                    return EngineEventKind.DomainCreated;
                case EventType.AppDomainUnload:
                    return EngineEventKind.DomainUnloaded;
                case EventType.TypeLoad:
                    return EngineEventKind.TypeLoaded;
                case EventType.UserLog:
                    return EngineEventKind.UserLog;
                case EventType.UserBreak:
                    return EngineEventKind.UserBreak;
                case EventType.Breakpoint:
                    return EngineEventKind.Breakpoint;
                case EventType.Step:
                    return EngineEventKind.Step;
                case EventType.Exception:
                    return EngineEventKind.Exception;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        internal static EngineSuspendPolicy MapSuspendPolicy(
            SuspendPolicy value)
        {
            switch (value)
            {
                case SuspendPolicy.None:
                    return EngineSuspendPolicy.None;
                case SuspendPolicy.EventThread:
                    return EngineSuspendPolicy.EventThread;
                case SuspendPolicy.All:
                    return EngineSuspendPolicy.All;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
