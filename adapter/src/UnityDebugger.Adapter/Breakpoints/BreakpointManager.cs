using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityDebugger.Adapter.Backend;

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
        private long nextId = 1;
        private bool disposed;

        public BreakpointManager(IDebuggerBackend backend)
        {
            this.backend = backend ??
                throw new ArgumentNullException(nameof(backend));
            backend.BreakpointChanged += OnBackendBreakpointChanged;
        }

        public event EventHandler<ManagedBreakpointChangedEventArgs>?
            Changed;

        public IReadOnlyList<ManagedBreakpoint> ReplaceForSource(
            string sourcePath,
            IEnumerable<RequestedBreakpoint> requested)
        {
            ThrowIfDisposed();
            var canonicalPath = Path.GetFullPath(sourcePath);
            var requests = requested.ToArray();
            var requestedLines = new HashSet<int>(
                requests.Select(item => item.Line));

            foreach (var entry in entries.Values
                .Where(
                    item =>
                        string.Equals(
                            item.SourcePath,
                            canonicalPath,
                            StringComparison.OrdinalIgnoreCase) &&
                        !requestedLines.Contains(item.RequestedLine))
                .ToArray())
            {
                Remove(entry);
            }

            var result = new List<ManagedBreakpoint>(requests.Length);
            foreach (var request in requests)
            {
                var key = Key(canonicalPath, request.Line);
                if (!entries.TryGetValue(key, out var entry))
                {
                    entry = new Entry(
                        nextId++,
                        canonicalPath,
                        request.Line,
                        request.Condition);
                    entries.Add(key, entry);
                    Bind(entry);
                }
                else if (!string.Equals(
                    entry.Condition,
                    request.Condition,
                    StringComparison.Ordinal))
                {
                    RemoveBackendBinding(entry);
                    entry.Condition = request.Condition;
                    Bind(entry);
                }

                result.Add(Snapshot(entry));
            }

            return result;
        }

        public void MarkAllPending(string reason)
        {
            ThrowIfDisposed();
            backendEntries.Clear();
            foreach (var entry in entries.Values)
            {
                entry.BackendId = null;
                entry.Verified = false;
                entry.Message = reason;
                RaiseChanged(entry);
            }
        }

        public void RebindAll()
        {
            ThrowIfDisposed();
            foreach (var entry in entries.Values.ToArray())
            {
                RemoveBackendBinding(entry);
                Bind(entry);
            }
        }

        public void RebindPending()
        {
            ThrowIfDisposed();
            foreach (var entry in entries.Values
                .Where(item => !item.Verified)
                .ToArray())
            {
                RemoveBackendBinding(entry);
                Bind(entry);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            backend.BreakpointChanged -= OnBackendBreakpointChanged;
        }

        private void Bind(Entry entry)
        {
            var logical = new LogicalBreakpoint(
                entry.Id,
                entry.SourcePath,
                entry.RequestedLine,
                1,
                entry.Condition,
                null,
                null);
            try
            {
                var bound = backend.BindBreakpoint(logical);
                entry.BackendId = bound.Id > 0
                    ? bound.Id
                    : (long?)null;
                entry.Verified = bound.Verified;
                entry.BoundLine = bound.Line > 0
                    ? bound.Line
                    : entry.RequestedLine;
                entry.Message = bound.Verified
                    ? null
                    : bound.Message ?? PendingMessage;
                if (entry.BackendId.HasValue)
                    backendEntries[entry.BackendId.Value] = entry;
            }
            catch (InvalidOperationException)
            {
                entry.BackendId = null;
                entry.Verified = false;
                entry.Message = PendingMessage;
            }
        }

        private void Remove(Entry entry)
        {
            RemoveBackendBinding(entry);
            entries.Remove(Key(entry.SourcePath, entry.RequestedLine));
        }

        private void RemoveBackendBinding(Entry entry)
        {
            if (entry.BackendId.HasValue)
            {
                var backendId = entry.BackendId.Value;
                backendEntries.Remove(backendId);
                backend.RemoveBreakpoint(backendId);
            }
            entry.BackendId = null;
            entry.Verified = false;
            entry.Message = PendingMessage;
        }

        private void OnBackendBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments)
        {
            var bound = arguments.Breakpoint;
            if (!backendEntries.TryGetValue(bound.Id, out var entry))
                return;

            entry.Verified = bound.Verified;
            if (bound.Line > 0)
                entry.BoundLine = bound.Line;
            entry.Message = bound.Verified
                ? null
                : bound.Message ?? PendingMessage;
            RaiseChanged(entry);
        }

        private void RaiseChanged(Entry entry)
        {
            Changed?.Invoke(
                this,
                new ManagedBreakpointChangedEventArgs(Snapshot(entry)));
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
            }

            public long Id { get; }
            public string SourcePath { get; }
            public int RequestedLine { get; }
            public int BoundLine { get; set; }
            public string? Condition { get; set; }
            public long? BackendId { get; set; }
            public bool Verified { get; set; }
            public string? Message { get; set; }
        }
    }
}
