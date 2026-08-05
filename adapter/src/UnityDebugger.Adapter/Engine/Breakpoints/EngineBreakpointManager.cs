using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Source;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Breakpoints
{
    internal enum BreakpointHitAction
    {
        Stop,
        Resume,
        LogPoint,
    }

    internal sealed class RuntimeBreakpointHit
    {
        public RuntimeBreakpointHit(
            object requestIdentity,
            bool isDebuggable)
        {
            RequestIdentity = requestIdentity ??
                throw new ArgumentNullException(nameof(requestIdentity));
            IsDebuggable = isDebuggable;
        }

        public object RequestIdentity { get; }
        public bool IsDebuggable { get; }
    }

    internal sealed class BreakpointHitResult
    {
        public BreakpointHitResult(
            BreakpointHitAction action,
            IReadOnlyList<long> breakpointIds,
            string? output = null)
        {
            Action = action;
            BreakpointIds = breakpointIds ??
                throw new ArgumentNullException(nameof(breakpointIds));
            Output = output;
        }

        public BreakpointHitAction Action { get; }
        public IReadOnlyList<long> BreakpointIds { get; }
        public string? Output { get; }
    }

    internal sealed class EngineBreakpointManager
    {
        private readonly object sync = new object();
        private readonly EngineSourceMapManager sources;
        private readonly IEngineBreakpointRuntime runtime;
        private readonly Dictionary<long, PendingBreakpoint> pending =
            new Dictionary<long, PendingBreakpoint>();
        private readonly Dictionary<object, BoundBreakpoint> boundByRequest =
            new Dictionary<object, BoundBreakpoint>();
        private long nextBoundId = 1;

        public EngineBreakpointManager(
            EngineSourceMapManager sources,
            IEngineBreakpointRuntime runtime)
        {
            this.sources = sources ??
                throw new ArgumentNullException(nameof(sources));
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;

        public PendingBreakpoint RequestSourceBreakpoint(
            LogicalBreakpoint value)
        {
            var canonical = new LogicalBreakpoint(
                value.Id,
                Path.GetFullPath(value.SourcePath),
                value.Line,
                value.Column,
                value.Condition,
                value.HitCondition,
                value.LogMessage);
            PendingBreakpoint created;
            lock (sync)
            {
                if (pending.TryGetValue(canonical.Id, out var existing))
                    RemovePendingLocked(existing);
                created = new PendingBreakpoint(canonical);
                pending.Add(created.Id, created);
                BindPendingLocked(created);
            }
            return created;
        }

        public void ProcessTypeLoaded(IRuntimeType type)
        {
            sources.ProcessTypeLoaded(type);
            lock (sync)
            {
                foreach (var value in pending.Values)
                    BindPendingLocked(value);
            }
        }

        public BreakpointHitResult ProcessBreakpointHit(
            RuntimeBreakpointHit value)
        {
            lock (sync)
            {
                if (
                    !value.IsDebuggable ||
                    !boundByRequest.TryGetValue(
                        value.RequestIdentity,
                        out var bound))
                {
                    return Resume();
                }

                bound.HitCount++;
                return new BreakpointHitResult(
                    BreakpointHitAction.Stop,
                    new[] { bound.Pending.Id });
            }
        }

        public void UnbindDomain(UnityDomainState domain)
        {
            if (domain == null)
                throw new ArgumentNullException(nameof(domain));
            lock (sync)
            {
                foreach (var value in domain.BoundBreakpoints
                    .OfType<BoundBreakpoint>()
                    .ToArray())
                {
                    UnbindLocked(value);
                }
            }
            sources.RemoveDomain(domain.Identity);
        }

        public bool ContainsPending(long id)
        {
            lock (sync)
                return pending.ContainsKey(id);
        }

        public bool RemovePendingBreakpoint(long id)
        {
            lock (sync)
            {
                if (!pending.TryGetValue(id, out var value))
                    return false;
                RemovePendingLocked(value);
                return true;
            }
        }

        public BackendBoundBreakpoint ToBackendBreakpoint(
            PendingBreakpoint value)
        {
            lock (sync)
            {
                var first = value.Bound.FirstOrDefault();
                return new BackendBoundBreakpoint(
                    value.Id,
                    first != null,
                    first?.Location.Line ?? value.Line,
                    first == null ? "Symbols are not loaded." : null);
            }
        }

        private void BindPendingLocked(PendingBreakpoint value)
        {
            var existingLocations = new HashSet<object>(
                value.Bound.Select(bound => bound.Location.RuntimeLocation));
            var added = false;
            foreach (var location in sources.GetLocations(
                value.SourcePath,
                value.Line))
            {
                if (existingLocations.Contains(location.RuntimeLocation))
                    continue;
                var request = runtime.CreateBreakpoint(location);
                var bound = new BoundBreakpoint(
                    nextBoundId++,
                    value,
                    location,
                    request);
                request.Enable();
                value.Add(bound);
                location.Domain.AddBoundBreakpoint(bound);
                boundByRequest.Add(request.Identity, bound);
                existingLocations.Add(location.RuntimeLocation);
                added = true;
            }

            if (added)
            {
                BreakpointChanged?.Invoke(
                    this,
                    new BackendBreakpointChangedEventArgs(
                        ToBackendBreakpoint(value)));
            }
        }

        private void RemovePendingLocked(PendingBreakpoint value)
        {
            foreach (var bound in value.Bound.ToArray())
                UnbindLocked(bound);
            pending.Remove(value.Id);
        }

        private void UnbindLocked(BoundBreakpoint value)
        {
            value.Request.Disable();
            boundByRequest.Remove(value.Request.Identity);
            value.Location.Domain.RemoveBoundBreakpoint(value);
            value.Pending.Remove(value);
        }

        private static BreakpointHitResult Resume() =>
            new BreakpointHitResult(
                BreakpointHitAction.Resume,
                Array.Empty<long>());
    }
}
