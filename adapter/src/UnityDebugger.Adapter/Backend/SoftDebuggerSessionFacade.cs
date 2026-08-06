using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        private readonly Dictionary<long, BreakEvent> breakpoints =
            new Dictionary<long, BreakEvent>();
        private readonly Dictionary<BreakEvent, long> breakpointIds =
            new Dictionary<BreakEvent, long>();
        private readonly MonoObjectValueStore objectValues =
            new MonoObjectValueStore();
        private long nextBreakpointId = 1;
        private ExceptionBreakMode exceptionMode;
        private BackendStopReason? expectedStopReason;
        private bool detachCompleted;
        private bool disposed;

        public SoftDebuggerSessionFacade()
        {
            session = new UnitySoftDebuggerSession();
            session.TargetReady += (_, __) =>
                TargetReady?.Invoke(this, EventArgs.Empty);
            session.TargetStarted += (_, __) =>
            {
                ClearFrames();
                TargetStarted?.Invoke(this, EventArgs.Empty);
            };
            session.TargetExited += (_, __) =>
            {
                ClearFrames();
                TargetExited?.Invoke(this, EventArgs.Empty);
            };
            session.TargetStopped += (_, arguments) =>
                HandleStopped(
                    arguments,
                    expectedStopReason ?? BackendStopReason.Pause);
            session.TargetInterrupted += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Pause);
            session.TargetHitBreakpoint += (_, arguments) =>
                HandleStopped(arguments, BackendStopReason.Breakpoint);
            session.TargetExceptionThrown += (_, arguments) =>
            {
                if (exceptionMode == ExceptionBreakMode.All)
                    HandleStopped(arguments, BackendStopReason.Exception);
            };
            session.TargetUnhandledException += (_, arguments) =>
            {
                if (exceptionMode != ExceptionBreakMode.None)
                {
                    HandleStopped(arguments, BackendStopReason.Exception);
                    return;
                }
                if (!session.HasExited)
                    session.Continue();
            };
            session.TargetThreadStarted += (_, arguments) =>
                RaiseThread(arguments, true);
            session.TargetThreadStopped += (_, arguments) =>
                RaiseThread(arguments, false);
            session.AssemblyLoaded += (_, arguments) =>
                RaiseAssembly(arguments);
            session.AssemblyUnloaded += (_, __) =>
                AssemblyUnloaded?.Invoke(this, EventArgs.Empty);
            session.Breakpoints.BreakEventStatusChanged +=
                OnBreakEventStatusChanged;
            session.OutputWriter = (isError, value) =>
                Output?.Invoke(
                    this,
                    new BackendOutputEventArgs(
                        isError ? "stderr" : "stdout",
                        value));
            session.LogWriter = (_, value) =>
                Output?.Invoke(
                    this,
                    new BackendOutputEventArgs("console", value));
        }

        public event EventHandler? TargetReady;
        public event EventHandler? TargetStarted;
        public event EventHandler? TargetExited;
        public event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler<BackendOutputEventArgs>? Output;
        public event EventHandler? AssemblyUnloaded;
        public event EventHandler? AssemblyLoaded;

        public bool IsRunning => session.IsRunning;
        public bool HasExited => session.HasExited;

        public async Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var completion = new TaskCompletionSource<bool>(
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
                    var options = new DebuggerSessionOptions
                    {
                        EvaluationOptions = EvaluationOptions.DefaultOptions,
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

        public void Continue()
        {
            ThrowIfDisposed();
            expectedStopReason = null;
            session.Continue();
        }

        public void Pause()
        {
            ThrowIfDisposed();
            expectedStopReason = BackendStopReason.Pause;
            session.Stop();
        }

        public void StepIn()
        {
            ThrowIfDisposed();
            expectedStopReason = BackendStopReason.Step;
            session.StepLine();
        }

        public void StepOver()
        {
            ThrowIfDisposed();
            expectedStopReason = BackendStopReason.Step;
            session.NextLine();
        }

        public void StepOut()
        {
            ThrowIfDisposed();
            expectedStopReason = BackendStopReason.Step;
            session.Finish();
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            ThrowIfDisposed();
            session.Breakpoints.RemoveCatchpoint("System.Exception");
            if (mode == ExceptionBreakMode.All)
            {
                session.Breakpoints.AddCatchpoint(
                    "System.Exception",
                    true);
            }
            exceptionMode = mode;
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
            {
                throw new InvalidOperationException(
                    "The requested debugger thread is unavailable.");
            }
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
                var frame = backtrace.GetFrame(index);
                if (frame == null)
                    continue;
                var location = frame.SourceLocation;
                result.Add(new BackendStackFrame(
                    RegisterFrame(frame),
                    threadId,
                    location?.MethodName ?? "Managed frame",
                    location?.FileName ?? string.Empty,
                    Math.Max(0, location?.Line ?? 0),
                    Math.Max(1, location?.Column ?? 1)));
            }
            return result;
        }

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var frame = objectValues.GetFrame(frameId);
            var options = session.EvaluationOptions;
            var values = new List<ObjectValue>();
            var thisReference = frame.GetThisReference(options);
            if (thisReference != null)
                values.Add(thisReference);
            values.AddRange(frame.GetParameters(options));
            values.AddRange(frame.GetLocalVariables(options));
            foreach (var value in values)
            {
                MonoObjectValueStore.WaitForValue(
                    value,
                    options,
                    cancellationToken);
            }
            var locals = ObjectValue.CreateObject(
                null,
                new ObjectPath("Locals"),
                string.Empty,
                string.Empty,
                ObjectValueFlags.Group | ObjectValueFlags.ReadOnly,
                values.ToArray());
            var mapped = objectValues.Map(locals);
            return new[]
            {
                new BackendScope(
                    "Locals",
                    mapped.VariablesReference,
                    false),
            };
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return objectValues.GetVariables(
                variablesReference,
                session.EvaluationOptions,
                cancellationToken);
        }

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var frame = objectValues.GetFrame(frameId);
            var options = session.EvaluationOptions;
            try
            {
                var value = frame.GetExpressionValue(expression, options);
                MonoObjectValueStore.WaitForValue(
                    value,
                    options,
                    cancellationToken);
                ThrowIfEvaluationFailed(value);
                var mapped = objectValues.Map(value);
                return new BackendEvaluationResult(
                    mapped.DisplayValue,
                    mapped.TypeName,
                    mapped.VariablesReference);
            }
            catch (BackendEvaluationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BackendEvaluationException(
                    string.IsNullOrWhiteSpace(exception.Message)
                        ? "Expression evaluation failed."
                        : exception.Message,
                    exception);
            }
        }

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return objectValues.SetVariable(
                variablesReference,
                name,
                expression,
                session.EvaluationOptions,
                cancellationToken);
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            ThrowIfDisposed();
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
            ApplyBreakpointOptions(
                value,
                breakpoint.Condition,
                breakpoint.HitCondition,
                breakpoint.LogMessage);
            return RegisterBreakpoint(value, breakpoint.Line);
        }

        public BackendBoundBreakpoint BindFunctionBreakpoint(
            LogicalFunctionBreakpoint breakpoint)
        {
            ThrowIfDisposed();
            var value = new FunctionBreakpoint(
                breakpoint.FunctionName,
                "C#");
            ApplyBreakpointOptions(
                value,
                breakpoint.Condition,
                breakpoint.HitCondition,
                null);
            if (!session.Breakpoints.Add(value))
            {
                return new BackendBoundBreakpoint(
                    0,
                    false,
                    0,
                    "Symbols are not loaded.");
            }
            return RegisterBreakpoint(value, 0);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            BreakEvent? value;
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

        public IReadOnlyList<BackendStepInTarget> GetStepInTargets(
            long frameId) => Array.Empty<BackendStepInTarget>();

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column) => Array.Empty<BackendGotoTarget>();

        public void Goto(long threadId, long targetId)
        {
            throw new DebuggerBackendException(
                "Goto requires mature target mapping.");
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Detach();
        }

        internal static void ApplyBreakpointOptions(
            BreakEvent value,
            string? condition,
            string? hitCondition,
            string? logMessage)
        {
            value.ConditionExpression = condition;
            if (
                int.TryParse(
                    hitCondition,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var hitCount) &&
                hitCount > 0)
            {
                value.HitCountMode = HitCountMode.EqualTo;
                value.HitCount = hitCount;
            }
            if (!string.IsNullOrEmpty(logMessage))
            {
                value.HitAction = HitAction.PrintExpression;
                value.TraceExpression = logMessage;
            }
            value.CommitChanges();
        }

        private BackendBoundBreakpoint RegisterBreakpoint(
            BreakEvent value,
            int line)
        {
            long id;
            lock (breakpointLock)
            {
                id = nextBreakpointId++;
                breakpoints.Add(id, value);
                breakpointIds.Add(value, id);
            }
            return ToBackendBreakpoint(id, value, line);
        }

        private BackendBoundBreakpoint ToBackendBreakpoint(
            long id,
            BreakEvent value,
            int line,
            BreakEventStatus? observedStatus = null)
        {
            var status = observedStatus ?? value.GetStatus(session);
            var verified = status == BreakEventStatus.Bound;
            return new BackendBoundBreakpoint(
                id,
                verified,
                line,
                verified
                    ? null
                    : status == BreakEventStatus.NotBound ||
                      status == BreakEventStatus.Disconnected
                        ? "Symbols are not loaded."
                        : "Breakpoint could not be bound.");
        }

        private void OnBreakEventStatusChanged(
            object? sender,
            BreakEventArgs arguments)
        {
            var value = arguments.BreakEvent;
            long id;
            lock (breakpointLock)
            {
                if (!breakpointIds.TryGetValue(value, out id))
                    return;
            }
            var line = value is Breakpoint source ? source.Line : 0;
            BreakpointChanged?.Invoke(
                this,
                new BackendBreakpointChangedEventArgs(
                    ToBackendBreakpoint(
                        id,
                        value,
                        line,
                        value.GetStatus(session))));
        }

        private void HandleStopped(
            TargetEventArgs arguments,
            BackendStopReason reason)
        {
            expectedStopReason = null;
            ClearFrames();
            long? breakpointId = null;
            if (arguments.BreakEvent != null)
            {
                lock (breakpointLock)
                {
                    if (breakpointIds.TryGetValue(
                        arguments.BreakEvent,
                        out var value))
                    {
                        breakpointId = value;
                    }
                }
            }
            var exception = reason == BackendStopReason.Exception
                ? new BackendExceptionInfo(
                    "System.Exception",
                    "Exception has occurred.",
                    "always",
                    null)
                : null;
            TargetStopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    arguments.Thread?.Id ?? 0,
                    null,
                    breakpointId,
                    exception));
        }

        private void RaiseThread(TargetEventArgs arguments, bool started)
        {
            ThreadChanged?.Invoke(
                this,
                new BackendThreadEventArgs(
                    arguments.Thread?.Id ?? 0,
                    started));
        }

        private void RaiseAssembly(AssemblyEventArgs arguments)
        {
            var location = arguments.Location ?? string.Empty;
            var module = new BackendModule(
                location,
                Path.GetFileName(location),
                string.IsNullOrEmpty(location) ? null : location,
                !string.IsNullOrEmpty(location) &&
                File.Exists(Path.ChangeExtension(location, ".pdb")));
            ModuleChanged?.Invoke(
                this,
                new BackendModuleChangedEventArgs(module, true));
            AssemblyLoaded?.Invoke(this, EventArgs.Empty);
        }

        private long RegisterFrame(Mono.Debugging.Client.StackFrame frame)
            => objectValues.RegisterFrame(frame);

        private void ClearFrames()
            => objectValues.Clear();

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));
            }
        }

        private static void ThrowIfEvaluationFailed(ObjectValue value)
        {
            if (
                !value.IsError &&
                !value.IsUnknown &&
                !value.IsNotSupported &&
                !value.IsImplicitNotSupported)
            {
                return;
            }
            var message = value.DisplayValue;
            throw new BackendEvaluationException(
                string.IsNullOrWhiteSpace(message)
                    ? "Expression evaluation failed."
                    : message);
        }
    }
}
