using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Diagnostics;

namespace UnityDebugger.Adapter.Breakpoints
{
    internal sealed class RequestedBreakpoint
    {
        public RequestedBreakpoint(int line, string? condition)
        {
            if (line <= 0)
                throw new ArgumentOutOfRangeException(nameof(line));
            Line = line;
            Condition = condition;
        }

        public int Line { get; }
        public string? Condition { get; }
    }

    internal sealed class ManagedBreakpoint
    {
        public ManagedBreakpoint(
            long id,
            string sourcePath,
            int line,
            string? condition,
            long? backendId,
            bool verified,
            string? message)
        {
            Id = id;
            SourcePath = sourcePath;
            Line = line;
            Condition = condition;
            BackendId = backendId;
            Verified = verified;
            Message = message;
        }

        public long Id { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public string? Condition { get; }
        public long? BackendId { get; }
        public bool Verified { get; }
        public string? Message { get; }
    }

    internal sealed class ManagedBreakpointChangedEventArgs : EventArgs
    {
        public ManagedBreakpointChangedEventArgs(
            ManagedBreakpoint breakpoint)
        {
            Breakpoint = breakpoint;
        }

        public ManagedBreakpoint Breakpoint { get; }
    }

    internal sealed class BreakpointManager : IDisposable
    {
        private const string PendingMessage =
            "Symbols are not loaded.";
        private readonly IDebuggerBackend backend;
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<long, Entry> backendEntries =
            new Dictionary<long, Entry>();
        private readonly object bindingLock = new object();
        private readonly Dictionary<long, BackendBoundBreakpoint>
            changesDuringBind =
                new Dictionary<long, BackendBoundBreakpoint>();
        private long nextId = 1;
        private int bindsInProgress;
        private bool preserveVerifiedWhileReloading;
        private bool disposed;

        public BreakpointManager(IDebuggerBackend backend)
        {
            this.backend = backend ??
                throw new ArgumentNullException(nameof(backend));
            backend.BreakpointChanged += OnBackendBreakpointChanged;
        }

        public event EventHandler<ManagedBreakpointChangedEventArgs>?
            Changed;

        public int VerifiedCount
        {
            get
            {
                lock (bindingLock)
                    return entries.Values.Count(item => item.Verified);
            }
        }

        public int PendingCount
        {
            get
            {
                lock (bindingLock)
                {
                    return entries.Count -
                        entries.Values.Count(item => item.Verified);
                }
            }
        }

        public IReadOnlyList<ManagedBreakpoint> ReplaceForSource(
            string sourcePath,
            IEnumerable<RequestedBreakpoint> requested)
        {
            ThrowIfDisposed();
            var canonicalPath = Path.GetFullPath(sourcePath);
            var requests = requested.ToArray();
            var requestedLines = new HashSet<int>(
                requests.Select(item => item.Line));

            Entry[] removed;
            lock (bindingLock)
            {
                ThrowIfDisposed();
                removed = entries.Values
                    .Where(
                        item =>
                            string.Equals(
                                item.SourcePath,
                                canonicalPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            !requestedLines.Contains(
                                item.RequestedLine))
                    .ToArray();
            }
            foreach (var entry in removed)
                Remove(entry);

            var result = new List<ManagedBreakpoint>(requests.Length);
            foreach (var request in requests)
            {
                var key = Key(canonicalPath, request.Line);
                Entry entry;
                var shouldBind = false;
                lock (bindingLock)
                {
                    if (!entries.TryGetValue(key, out entry!))
                    {
                        entry = new Entry(
                            nextId++,
                            canonicalPath,
                            request.Line,
                            request.Condition);
                        entries.Add(key, entry);
                        shouldBind = true;
                    }
                    else if (!string.Equals(
                        entry.Condition,
                        request.Condition,
                        StringComparison.Ordinal))
                    {
                        entry.Condition = request.Condition;
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

        public bool TryGetLogicalId(
            long backendId,
            out long logicalId)
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

        public void MarkAllPending(string reason)
        {
            ManagedBreakpoint[] changed;
            lock (bindingLock)
            {
                ThrowIfDisposed();
                changed = entries.Values
                    .Select(
                        entry =>
                        {
                            entry.Verified = false;
                            entry.Message = reason;
                            return Snapshot(entry);
                        })
                    .ToArray();
            }
            foreach (var breakpoint in changed)
                RaiseChanged(breakpoint);
        }

        public void BeginReload(
            bool preserveVerified,
            string pendingReason)
        {
            lock (bindingLock)
            {
                ThrowIfDisposed();
                preserveVerifiedWhileReloading = preserveVerified;
            }
            if (!preserveVerified)
                MarkAllPending(pendingReason);
        }

        public void CompleteReload()
        {
            lock (bindingLock)
            {
                ThrowIfDisposed();
                preserveVerifiedWhileReloading = false;
            }
        }

        public void RebindAll()
        {
            Entry[] values;
            lock (bindingLock)
            {
                ThrowIfDisposed();
                values = entries.Values.ToArray();
            }
            foreach (var entry in values)
            {
                Bind(entry);
                ManagedBreakpoint changed;
                lock (bindingLock)
                    changed = Snapshot(entry);
                RaiseChanged(changed);
            }
        }

        public void RebindPending()
        {
            Entry[] values;
            lock (bindingLock)
            {
                ThrowIfDisposed();
                values = entries.Values
                    .Where(item => !item.Verified)
                    .ToArray();
            }
            foreach (var entry in values)
            {
                Bind(entry);
                ManagedBreakpoint changed;
                lock (bindingLock)
                    changed = Snapshot(entry);
                RaiseChanged(changed);
            }
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
            LogicalBreakpoint logical;
            long bindingGeneration;
            long? bindingToRemove;
            lock (bindingLock)
            {
                if (disposed || !entry.Registered)
                    return;
                bindingToRemove = DetachBackendBindingLocked(entry);
                logical = new LogicalBreakpoint(
                    entry.Id,
                    entry.SourcePath,
                    entry.RequestedLine,
                    1,
                    entry.Condition,
                    null,
                    null);
                bindingGeneration = entry.BindingGeneration;
                bindsInProgress++;
            }
            long? staleBackendId = null;
            try
            {
                RemoveBackendBreakpoint(bindingToRemove);
                var bound = backend.BindBreakpoint(logical);
                InternalDebuggerLog.Write(
                    bound.Verified
                        ? "unity-debugger.breakpoint.manager.bind.bound"
                        : "unity-debugger.breakpoint.manager.bind.pending");
                lock (bindingLock)
                {
                    if (
                        disposed ||
                        !entry.Registered ||
                        entry.BindingGeneration != bindingGeneration)
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
                        entry.BindingGeneration == bindingGeneration)
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
            RemoveStaleBackendBreakpoint(staleBackendId);
        }

        private void Remove(Entry entry)
        {
            long? backendId = null;
            lock (bindingLock)
            {
                var key = Key(entry.SourcePath, entry.RequestedLine);
                if (
                    !entry.Registered ||
                    !entries.TryGetValue(key, out var current) ||
                    !ReferenceEquals(current, entry))
                {
                    return;
                }
                entry.Registered = false;
                backendId = DetachBackendBindingLocked(entry);
                entries.Remove(key);
            }
            RemoveBackendBreakpoint(backendId);
        }

        private long? DetachBackendBindingLocked(Entry entry)
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
            if (backendId.HasValue)
            {
                backend.RemoveBreakpoint(backendId.Value);
            }
        }

        private void RemoveStaleBackendBreakpoint(long? backendId)
        {
            try
            {
                RemoveBackendBreakpoint(backendId);
            }
            catch (InvalidOperationException)
            {
                // A concurrent disconnect can dispose the stale backend
                // binding before this cleanup request reaches it.
            }
        }

        private void OnBackendBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments)
        {
            var bound = arguments.Breakpoint;
            Entry entry;
            ManagedBreakpoint changed;
            lock (bindingLock)
            {
                if (disposed)
                    return;
                if (!backendEntries.TryGetValue(bound.Id, out entry!))
                {
                    if (bindsInProgress > 0)
                    {
                        InternalDebuggerLog.Write(
                            bound.Verified
                                ? "unity-debugger.breakpoint.manager.status.bound.buffered"
                                : "unity-debugger.breakpoint.manager.status.pending.buffered");
                        changesDuringBind[bound.Id] = bound;
                    }
                    else
                    {
                        InternalDebuggerLog.Write(
                            bound.Verified
                                ? "unity-debugger.breakpoint.manager.status.bound.unmapped"
                                : "unity-debugger.breakpoint.manager.status.pending.unmapped");
                    }
                    return;
                }
                if (
                    preserveVerifiedWhileReloading &&
                    entry.Verified &&
                    !bound.Verified)
                {
                    InternalDebuggerLog.Write(
                        "unity-debugger.breakpoint.manager.status.pending.suppressed");
                    return;
                }
                InternalDebuggerLog.Write(
                    bound.Verified
                        ? "unity-debugger.breakpoint.manager.status.bound.mapped"
                        : "unity-debugger.breakpoint.manager.status.pending.mapped");
                ApplyBoundState(entry, bound);
                changed = Snapshot(entry);
            }
            RaiseChanged(changed);
        }

        private static void ApplyBoundState(
            Entry entry,
            BackendBoundBreakpoint bound)
        {
            var verified = bound.Verified || bound.Id > 0;
            if (verified && !bound.Verified)
            {
                InternalDebuggerLog.Write(
                    "unity-debugger.breakpoint.manager.status.pending.accepted");
            }
            entry.Verified = verified;
            if (bound.Line > 0)
                entry.BoundLine = bound.Line;
            entry.Message = verified
                ? null
                : bound.Message ?? PendingMessage;
        }

        private void RaiseChanged(ManagedBreakpoint breakpoint)
        {
            Changed?.Invoke(
                this,
                new ManagedBreakpointChangedEventArgs(breakpoint));
        }

        private static ManagedBreakpoint Snapshot(Entry entry) =>
            new ManagedBreakpoint(
                entry.Id,
                entry.SourcePath,
                entry.BoundLine,
                entry.Condition,
                entry.BackendId,
                entry.Verified,
                entry.Message);

        private static string Key(string sourcePath, int line) =>
            sourcePath + "\0" + line;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(BreakpointManager));
        }

        private sealed class Entry
        {
            public Entry(
                long id,
                string sourcePath,
                int line,
                string? condition)
            {
                Id = id;
                SourcePath = sourcePath;
                RequestedLine = line;
                BoundLine = line;
                Condition = condition;
                Message = PendingMessage;
                Registered = true;
            }

            public long Id { get; }
            public string SourcePath { get; }
            public int RequestedLine { get; }
            public int BoundLine { get; set; }
            public string? Condition { get; set; }
            public long? BackendId { get; set; }
            public bool Verified { get; set; }
            public string? Message { get; set; }
            public long BindingGeneration { get; set; }
            public bool Registered { get; set; }
        }
    }
}
