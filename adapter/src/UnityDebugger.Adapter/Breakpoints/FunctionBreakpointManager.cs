using System;
using System.Collections.Generic;
using System.Linq;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Breakpoints
{
    internal sealed class RequestedFunctionBreakpoint
    {
        public RequestedFunctionBreakpoint(
            string name,
            string? condition,
            string? hitCondition)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Function name is required.", nameof(name));
            Name = name;
            Condition = condition;
            HitCondition = hitCondition;
        }

        public string Name { get; }
        public string? Condition { get; }
        public string? HitCondition { get; }
    }

    internal sealed class ManagedFunctionBreakpoint
    {
        public ManagedFunctionBreakpoint(
            long id,
            string name,
            bool verified,
            string? message)
        {
            Id = id;
            Name = name;
            Verified = verified;
            Message = message;
        }

        public long Id { get; }
        public string Name { get; }
        public bool Verified { get; }
        public string? Message { get; }
    }

    internal sealed class ManagedFunctionBreakpointChangedEventArgs :
        EventArgs
    {
        public ManagedFunctionBreakpointChangedEventArgs(
            ManagedFunctionBreakpoint breakpoint)
        {
            Breakpoint = breakpoint;
        }

        public ManagedFunctionBreakpoint Breakpoint { get; }
    }

    internal sealed class FunctionBreakpointManager : IDisposable
    {
        private const string PendingMessage = "Symbols are not loaded.";
        private readonly IDebuggerBackend backend;
        private readonly BreakpointIdAllocator idAllocator;
        private readonly object bindingLock = new object();
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<long, Entry> backendEntries =
            new Dictionary<long, Entry>();
        private readonly Dictionary<long, BackendBoundBreakpoint>
            changesDuringBind =
                new Dictionary<long, BackendBoundBreakpoint>();
        private int bindsInProgress;
        private bool disposed;

        public FunctionBreakpointManager(
            IDebuggerBackend backend,
            BreakpointIdAllocator idAllocator)
        {
            this.backend = backend ??
                throw new ArgumentNullException(nameof(backend));
            this.idAllocator = idAllocator ??
                throw new ArgumentNullException(nameof(idAllocator));
            backend.BreakpointChanged += OnBackendBreakpointChanged;
        }

        public event EventHandler<ManagedFunctionBreakpointChangedEventArgs>?
            Changed;

        public IReadOnlyList<ManagedFunctionBreakpoint> Replace(
            IEnumerable<RequestedFunctionBreakpoint> requested)
        {
            ThrowIfDisposed();
            var requests = requested.ToArray();
            var requestedNames = new HashSet<string>(
                requests.Select(item => item.Name),
                StringComparer.Ordinal);
            Entry[] removed;
            lock (bindingLock)
            {
                removed = entries.Values
                    .Where(item => !requestedNames.Contains(item.Name))
                    .ToArray();
            }
            foreach (var entry in removed)
                Remove(entry);

            var result = new List<ManagedFunctionBreakpoint>(
                requests.Length);
            foreach (var request in requests)
            {
                Entry entry;
                var shouldBind = false;
                lock (bindingLock)
                {
                    ThrowIfDisposed();
                    if (!entries.TryGetValue(request.Name, out entry!))
                    {
                        entry = new Entry(
                            idAllocator.Next(),
                            request.Name,
                            request.Condition,
                            request.HitCondition);
                        entries.Add(entry.Name, entry);
                        shouldBind = true;
                    }
                    else if (
                        !string.Equals(
                            entry.Condition,
                            request.Condition,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            entry.HitCondition,
                            request.HitCondition,
                            StringComparison.Ordinal))
                    {
                        entry.Condition = request.Condition;
                        entry.HitCondition = request.HitCondition;
                        shouldBind = true;
                    }
                }
                if (shouldBind)
                    Bind(entry);
                lock (bindingLock)
                    result.Add(Snapshot(entry));
            }
            return result;
        }

        public bool TryGetLogicalId(long backendId, out long logicalId)
        {
            lock (bindingLock)
            {
                if (backendEntries.TryGetValue(backendId, out var entry))
                {
                    logicalId = entry.Id;
                    return true;
                }
            }
            logicalId = 0;
            return false;
        }

        public void Dispose()
        {
            lock (bindingLock)
            {
                if (disposed)
                    return;
                disposed = true;
            }
            backend.BreakpointChanged -= OnBackendBreakpointChanged;
        }

        private void Bind(Entry entry)
        {
            LogicalFunctionBreakpoint logical;
            long generation;
            long? previousBackendId;
            lock (bindingLock)
            {
                if (disposed || !entry.Registered)
                    return;
                previousBackendId = DetachBinding(entry);
                logical = new LogicalFunctionBreakpoint(
                    entry.Id,
                    entry.Name,
                    entry.Condition,
                    entry.HitCondition);
                generation = entry.BindingGeneration;
                bindsInProgress++;
            }

            long? staleBackendId = null;
            try
            {
                RemoveBackendBreakpoint(previousBackendId);
                var bound = backend.BindFunctionBreakpoint(logical);
                lock (bindingLock)
                {
                    if (
                        disposed ||
                        !entry.Registered ||
                        entry.BindingGeneration != generation)
                    {
                        if (bound.Id > 0)
                        {
                            changesDuringBind.Remove(bound.Id);
                            staleBackendId = bound.Id;
                        }
                    }
                    else
                    {
                        entry.BackendId = bound.Id > 0
                            ? bound.Id
                            : (long?)null;
                        ApplyBoundState(entry, bound);
                        if (entry.BackendId.HasValue)
                        {
                            var backendId = entry.BackendId.Value;
                            backendEntries[backendId] = entry;
                            if (changesDuringBind.TryGetValue(
                                backendId,
                                out var changed))
                            {
                                changesDuringBind.Remove(backendId);
                                ApplyBoundState(entry, changed);
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                lock (bindingLock)
                {
                    if (
                        !disposed &&
                        entry.Registered &&
                        entry.BindingGeneration == generation)
                    {
                        entry.BackendId = null;
                        entry.Verified = false;
                        entry.Message = PendingMessage;
                    }
                }
            }
            finally
            {
                lock (bindingLock)
                {
                    bindsInProgress--;
                    if (bindsInProgress == 0)
                        changesDuringBind.Clear();
                }
            }
            RemoveBackendBreakpoint(staleBackendId);
        }

        private void Remove(Entry entry)
        {
            long? backendId;
            lock (bindingLock)
            {
                if (
                    !entry.Registered ||
                    !entries.TryGetValue(entry.Name, out var current) ||
                    !ReferenceEquals(entry, current))
                {
                    return;
                }
                entry.Registered = false;
                backendId = DetachBinding(entry);
                entries.Remove(entry.Name);
            }
            RemoveBackendBreakpoint(backendId);
        }

        private long? DetachBinding(Entry entry)
        {
            entry.BindingGeneration++;
            var backendId = entry.BackendId;
            if (backendId.HasValue)
                backendEntries.Remove(backendId.Value);
            entry.BackendId = null;
            entry.Verified = false;
            entry.Message = PendingMessage;
            return backendId;
        }

        private void RemoveBackendBreakpoint(long? backendId)
        {
            if (!backendId.HasValue)
                return;
            try
            {
                backend.RemoveBreakpoint(backendId.Value);
            }
            catch (InvalidOperationException)
            {
                // Disconnect may release a binding before replacement does.
            }
        }

        private void OnBackendBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments)
        {
            ManagedFunctionBreakpoint changed;
            lock (bindingLock)
            {
                if (disposed)
                    return;
                var bound = arguments.Breakpoint;
                if (!backendEntries.TryGetValue(bound.Id, out var entry))
                {
                    if (bindsInProgress > 0)
                        changesDuringBind[bound.Id] = bound;
                    return;
                }
                ApplyBoundState(entry, bound);
                changed = Snapshot(entry);
            }
            Changed?.Invoke(
                this,
                new ManagedFunctionBreakpointChangedEventArgs(changed));
        }

        private static void ApplyBoundState(
            Entry entry,
            BackendBoundBreakpoint bound)
        {
            entry.Verified = bound.Verified;
            entry.Message = bound.Verified
                ? null
                : bound.Message ?? PendingMessage;
        }

        private static ManagedFunctionBreakpoint Snapshot(Entry entry) =>
            new ManagedFunctionBreakpoint(
                entry.Id,
                entry.Name,
                entry.Verified,
                entry.Message);

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(FunctionBreakpointManager));
            }
        }

        private sealed class Entry
        {
            public Entry(
                long id,
                string name,
                string? condition,
                string? hitCondition)
            {
                Id = id;
                Name = name;
                Condition = condition;
                HitCondition = hitCondition;
                Message = PendingMessage;
                Registered = true;
            }

            public long Id { get; }
            public string Name { get; }
            public string? Condition { get; set; }
            public string? HitCondition { get; set; }
            public long? BackendId { get; set; }
            public bool Verified { get; set; }
            public string? Message { get; set; }
            public long BindingGeneration { get; set; }
            public bool Registered { get; set; }
        }
    }
}
