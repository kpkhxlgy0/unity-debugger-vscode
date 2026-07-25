using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class SoftDebuggerSessionFacade :
        ISoftDebuggerSessionFacade
    {
        private readonly UnitySoftDebuggerSession session;
        private readonly object breakpointLock = new object();
        private readonly Dictionary<long, Breakpoint> breakpoints =
            new Dictionary<long, Breakpoint>();
        private readonly Dictionary<BreakEvent, long> breakpointIds =
            new Dictionary<BreakEvent, long>();
        private readonly object inspectionLock = new object();
        private readonly Dictionary<long, Mono.Debugging.Client.StackFrame>
            frames =
                new Dictionary<long, Mono.Debugging.Client.StackFrame>();
        private readonly Dictionary<long, ObjectValue[]> variables =
            new Dictionary<long, ObjectValue[]>();
        private long nextBreakpointId = 1;
        private long nextFrameId = 1;
        private long nextVariablesReference = 1;
        private bool detachCompleted;
        private bool disposed;

        public SoftDebuggerSessionFacade()
        {
            session = new UnitySoftDebuggerSession();
            session.TargetReady += (_, __) =>
                TargetReady?.Invoke(this, EventArgs.Empty);
            session.TargetExited += (_, __) =>
            {
                ClearInspectionState();
                TargetExited?.Invoke(this, EventArgs.Empty);
            };
            session.TargetStarted += (_, __) => ClearInspectionState();
            session.TargetStopped += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Pause);
            session.TargetInterrupted += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Pause);
            session.TargetHitBreakpoint += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Breakpoint);
            session.TargetExceptionThrown += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Exception);
            session.TargetUnhandledException += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Exception);
            session.TargetThreadStarted += (_, arguments) =>
                RaiseThread(arguments, true);
            session.TargetThreadStopped += (_, arguments) =>
                RaiseThread(arguments, false);
            session.AssemblyUnloaded += (_, __) =>
                AssemblyUnloaded?.Invoke(this, EventArgs.Empty);
            session.AssemblyLoaded += (_, __) =>
                AssemblyLoaded?.Invoke(this, EventArgs.Empty);
            session.Breakpoints.BreakEventStatusChanged +=
                OnBreakEventStatusChanged;
        }

        public event EventHandler? TargetReady;
        public event EventHandler? TargetExited;
        public event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler? AssemblyUnloaded;
        public event EventHandler? AssemblyLoaded;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;

        public bool IsRunning => session.IsRunning;
        public bool HasExited => session.HasExited;

        public async Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken)
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));

            var completion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<TargetEventArgs>? readyHandler = null;
            EventHandler<TargetEventArgs>? exitHandler = null;
            readyHandler = (_, __) => completion.TrySetResult(true);
            exitHandler = (_, __) => completion.TrySetException(
                new InvalidOperationException(
                    "Editor exited before debugger attach completed."));
            session.TargetReady += readyHandler;
            session.TargetExited += exitHandler;
            session.ExceptionHandler = exception =>
            {
                completion.TrySetException(exception);
                return true;
            };

            using (cancellationToken.Register(
                () => completion.TrySetCanceled()))
            {
                try
                {
                    var connectArgs = new SoftDebuggerConnectArgs(
                        string.Empty,
                        address,
                        port)
                    {
                        MaxConnectionAttempts = maxConnectionAttempts,
                        TimeBetweenConnectionAttempts =
                            connectionAttemptIntervalMilliseconds,
                    };
                    var evaluationOptions =
                        EvaluationOptions.DefaultOptions.Clone();
                    evaluationOptions.AllowTargetInvoke = false;
                    evaluationOptions.AllowMethodEvaluation = false;
                    evaluationOptions.AllowToStringCalls = false;
                    var options = new DebuggerSessionOptions
                    {
                        EvaluationOptions = evaluationOptions,
                    };
                    session.SetOutputOptions(new OutputOptions());
                    session.Run(
                        new SoftDebuggerStartInfo(connectArgs),
                        options);
                    await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    session.TargetReady -= readyHandler;
                    session.TargetExited -= exitHandler;
                    session.ExceptionHandler = null;
                }
            }
        }

        public void Continue()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));
            session.Continue();
        }

        public IReadOnlyList<BackendThread> GetThreads()
        {
            ThrowIfDisposed();
            return session.GetProcesses()
                .SelectMany(process => process.GetThreads())
                .Select(thread => new BackendThread(
                    thread.Id,
                    thread.Name ?? string.Empty))
                .ToArray();
        }

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            ThrowIfDisposed();
            var thread = session.GetProcesses()
                .SelectMany(process => process.GetThreads())
                .FirstOrDefault(item => item.Id == threadId);
            if (thread == null)
                throw new InvalidOperationException(
                    "The requested debugger thread is unavailable.");

            var backtrace = thread.Backtrace;
            if (backtrace == null || startFrame >= backtrace.FrameCount)
                return Array.Empty<BackendStackFrame>();
            var first = Math.Max(0, startFrame);
            var count = Math.Min(
                Math.Max(0, levels),
                backtrace.FrameCount - first);
            var result = new List<BackendStackFrame>(count);
            for (var index = first; index < first + count; index++)
            {
                var value = backtrace.GetFrame(index);
                if (value == null)
                    continue;
                var location = value.SourceLocation;
                var id = RegisterFrame(value);
                result.Add(
                    new BackendStackFrame(
                        id,
                        threadId,
                        location?.MethodName ?? "Managed frame",
                        location?.FileName ?? string.Empty,
                        Math.Max(0, location?.Line ?? 0),
                        Math.Max(1, location?.Column ?? 1)));
            }
            return result;
        }

        public IReadOnlyList<BackendScope> GetScopes(long frameId)
        {
            ThrowIfDisposed();
            Mono.Debugging.Client.StackFrame frame;
            lock (inspectionLock)
            {
                if (!frames.TryGetValue(frameId, out frame!))
                {
                    throw new InvalidOperationException(
                        "The requested stack frame is unavailable.");
                }
            }

            var options = SafeEvaluationOptions();
            var values = new List<ObjectValue>();
            var thisReference = frame.GetThisReference(options);
            if (thisReference != null)
                values.Add(thisReference);
            values.AddRange(frame.GetParameters(options));
            values.AddRange(frame.GetLocalVariables(options));
            foreach (var value in values)
                WaitForValue(value, options);
            return new[]
            {
                new BackendScope(
                    "Locals",
                    RegisterVariables(values.ToArray()),
                    false),
            };
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference)
        {
            ThrowIfDisposed();
            ObjectValue[] values;
            lock (inspectionLock)
            {
                if (!variables.TryGetValue(
                    variablesReference,
                    out values!))
                {
                    throw new InvalidOperationException(
                        "The requested variables are unavailable.");
                }
            }
            return ConvertVariables(values, SafeEvaluationOptions());
        }

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression)
        {
            ThrowIfDisposed();
            Mono.Debugging.Client.StackFrame frame;
            lock (inspectionLock)
            {
                if (!frames.TryGetValue(frameId, out frame!))
                {
                    throw new InvalidOperationException(
                        "The requested stack frame is unavailable.");
                }
            }

            var options = ExplicitEvaluationOptions();
            try
            {
                var value = frame.GetExpressionValue(
                    expression,
                    options);
                WaitForValue(value, options);
                var reference = value.HasChildren
                    ? RegisterVariables(
                        value.GetRangeOfChildren(
                            0,
                            101,
                            options))
                    : 0;
                return new BackendEvaluationResult(
                    value.DisplayValue ?? string.Empty,
                    value.TypeName ?? string.Empty,
                    reference);
            }
            catch (Exception exception)
                when (
                    !(exception is ObjectDisposedException) &&
                    !(exception is InvalidOperationException))
            {
                throw new DebuggerBackendException(
                    "Expression evaluation failed.");
            }
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));

            var value = session.Breakpoints.Add(
                breakpoint.SourcePath,
                breakpoint.Line,
                Math.Max(1, breakpoint.Column));
            if (value == null)
            {
                return new BackendBoundBreakpoint(
                    0,
                    false,
                    breakpoint.Line,
                    "Symbols are not loaded.");
            }

            value.ConditionExpression = breakpoint.Condition;
            value.CommitChanges();
            long id;
            lock (breakpointLock)
            {
                id = nextBreakpointId++;
                breakpoints.Add(id, value);
                breakpointIds.Add(value, id);
            }
            return ToBackendBreakpoint(id, value);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            Breakpoint? value;
            lock (breakpointLock)
            {
                if (!breakpoints.TryGetValue(
                    backendBreakpointId,
                    out value))
                {
                    return;
                }
                breakpoints.Remove(backendBreakpointId);
                breakpointIds.Remove(value);
            }
            session.Breakpoints.Remove(value);
        }

        public void Detach()
        {
            if (detachCompleted)
                return;
            detachCompleted = true;

            try
            {
                if (session.IsConnected)
                {
                    if (!session.IsRunning && !session.HasExited)
                        session.Continue();
                    session.Detach();
                }
            }
            finally
            {
                session.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Detach();
        }

        private void HandleStopped(
            TargetEventArgs arguments,
            BackendStopReason reason)
        {
            ClearInspectionState();
            RaiseStopped(arguments, reason);
        }

        private long RegisterFrame(
            Mono.Debugging.Client.StackFrame frame)
        {
            lock (inspectionLock)
            {
                if (nextFrameId == long.MaxValue)
                    throw new InvalidOperationException(
                        "Debugger frame handle space is exhausted.");
                var id = nextFrameId++;
                frames.Add(id, frame);
                return id;
            }
        }

        private long RegisterVariables(ObjectValue[] values)
        {
            lock (inspectionLock)
            {
                if (nextVariablesReference == long.MaxValue)
                    throw new InvalidOperationException(
                        "Debugger variable handle space is exhausted.");
                var id = nextVariablesReference++;
                variables.Add(id, values);
                return id;
            }
        }

        private IReadOnlyList<BackendVariable> ConvertVariables(
            IEnumerable<ObjectValue> values,
            EvaluationOptions options)
        {
            const int MaximumChildren = 100;
            var result = new List<BackendVariable>();
            foreach (var value in values.Take(MaximumChildren + 1))
            {
                WaitForValue(value, options);
                var reference = value.HasChildren
                    ? RegisterVariables(
                        value.GetRangeOfChildren(
                            0,
                            MaximumChildren + 1,
                            options))
                    : 0;
                result.Add(
                    new BackendVariable(
                        value.Name ?? string.Empty,
                        value.DisplayValue ?? string.Empty,
                        value.TypeName ?? string.Empty,
                        reference));
            }
            return result;
        }

        private EvaluationOptions SafeEvaluationOptions()
        {
            var options = session.EvaluationOptions.Clone();
            options.AllowTargetInvoke = false;
            options.AllowMethodEvaluation = false;
            options.AllowToStringCalls = false;
            return options;
        }

        private EvaluationOptions ExplicitEvaluationOptions()
        {
            var options = session.EvaluationOptions.Clone();
            options.AllowTargetInvoke = true;
            options.AllowMethodEvaluation = true;
            options.AllowToStringCalls = true;
            return options;
        }

        private static void WaitForValue(
            ObjectValue value,
            EvaluationOptions options)
        {
            if (!value.WaitHandle.WaitOne(options.EvaluationTimeout))
            {
                throw new DebuggerBackendException(
                    "Timed out while reading a managed value.");
            }
        }

        private void ClearInspectionState()
        {
            lock (inspectionLock)
            {
                frames.Clear();
                variables.Clear();
                nextFrameId = 1;
                nextVariablesReference = 1;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));
        }

        private void OnBreakEventStatusChanged(
            object? sender,
            BreakEventArgs arguments)
        {
            if (!(arguments.BreakEvent is Breakpoint value))
                return;

            long id;
            lock (breakpointLock)
            {
                if (!breakpointIds.TryGetValue(value, out id))
                    return;
            }
            BreakpointChanged?.Invoke(
                this,
                new BackendBreakpointChangedEventArgs(
                    ToBackendBreakpoint(id, value)));
        }

        private BackendBoundBreakpoint ToBackendBreakpoint(
            long id,
            Breakpoint value)
        {
            var status = value.GetStatus(session);
            var verified = status == BreakEventStatus.Bound;
            return new BackendBoundBreakpoint(
                id,
                verified,
                value.Line,
                verified
                    ? null
                    : status == BreakEventStatus.NotBound ||
                      status == BreakEventStatus.Disconnected
                        ? "Symbols are not loaded."
                        : "Breakpoint could not be bound.");
        }

        private void RaiseStopped(
            TargetEventArgs arguments,
            BackendStopReason reason)
        {
            TargetStopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    arguments.Thread?.Id ?? 0,
                    null));
        }

        private void RaiseThread(
            TargetEventArgs arguments,
            bool started)
        {
            ThreadChanged?.Invoke(
                this,
                new BackendThreadEventArgs(
                    arguments.Thread?.Id ?? 0,
                    started));
        }
    }
}
