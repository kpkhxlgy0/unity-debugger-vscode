using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using Mono.Debugging.Client;
using Mono.Debugging.Evaluation;
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
        private readonly object evaluationResolverLock = new object();
        private readonly object controlTargetLock = new object();
        private readonly Dictionary<long, SoftStepInTarget> stepInTargets =
            new Dictionary<long, SoftStepInTarget>();
        private readonly Dictionary<long, SoftGotoTarget> gotoTargets =
            new Dictionary<long, SoftGotoTarget>();
        private readonly MonoObjectValueStore objectValues =
            new MonoObjectValueStore();
        private long nextBreakpointId = 1;
        private long nextControlTargetId = 1;
        private ExceptionBreakMode exceptionMode;
        private BackendStopReason? expectedStopReason;
        private bool detachCompleted;
        private bool disposed;
        private const int ReferenceUnityInvokeTimeoutMilliseconds = 2000;

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

        public void StepIn(long threadId, long? targetId)
        {
            ThrowIfDisposed();
            expectedStopReason = BackendStopReason.Step;
            if (targetId.HasValue)
            {
                SoftStepInTarget target;
                lock (controlTargetLock)
                {
                    if (!stepInTargets.TryGetValue(
                        targetId.Value,
                        out target))
                    {
                        throw new DebuggerBackendException(
                            "The requested step target is unavailable.");
                    }
                }
                session.StepIntoTarget(threadId, target);
                return;
            }
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
            SoftExceptionRequestConfiguration.Apply(session, mode);
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
            var options = CreateReferenceVariableOptions(
                session.EvaluationOptions,
                timeoutMilliseconds);
            var thisReference = frame.GetThisReference(options);
            var localVariables = frame.GetLocalVariables(options);
            var parameters = frame.GetParameters(options);
            var isUnityMainThread = false;
            ObjectValue? activeScene = null;
            ObjectValue? thisGameObject = null;
            TryGetUnityFrameValues(
                frame,
                options,
                cancellationToken,
                out isUnityMainThread,
                out activeScene,
                out thisGameObject);
            if (activeScene != null)
                frame.ConnectObjectValue(activeScene);
            if (thisGameObject != null)
                frame.ConnectObjectValue(thisGameObject);
            var values = ComposeFrameLocals(
                isUnityMainThread,
                activeScene,
                thisReference,
                thisGameObject,
                localVariables,
                parameters);
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

        internal static ObjectValue[] ComposeFrameLocals(
            bool isUnityMainThread,
            ObjectValue? activeScene,
            ObjectValue? thisReference,
            ObjectValue? thisGameObject,
            IReadOnlyList<ObjectValue> localVariables,
            IReadOnlyList<ObjectValue> parameters)
        {
            var values = new List<ObjectValue>();
            if (isUnityMainThread && activeScene != null)
            {
                activeScene.Name = "Active scene";
                values.Add(activeScene);
            }
            if (thisReference != null)
            {
                thisReference.Name = "this";
                values.Add(thisReference);
            }
            if (isUnityMainThread && thisGameObject != null)
            {
                thisGameObject.Name = "this.gameObject";
                values.Add(thisGameObject);
            }
            values.AddRange(localVariables);
            values.AddRange(parameters);
            return values.ToArray();
        }

        internal static EvaluationOptions CreateReferenceVariableOptions(
            EvaluationOptions sessionOptions)
            => CreateReferenceVariableOptions(
                sessionOptions,
                sessionOptions.EvaluationTimeout);

        internal static EvaluationOptions CreateReferenceVariableOptions(
            EvaluationOptions sessionOptions,
            int timeoutMilliseconds)
        {
            if (sessionOptions == null)
                throw new ArgumentNullException(nameof(sessionOptions));
            var options = sessionOptions.Clone();
            options.FlattenHierarchy = false;
            options.GroupPrivateMembers = false;
            options.EvaluationTimeout = timeoutMilliseconds;
            options.MemberEvaluationTimeout = timeoutMilliseconds;
            return options;
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return objectValues.GetVariables(
                variablesReference,
                CreateReferenceVariableOptions(
                    session.EvaluationOptions,
                    timeoutMilliseconds),
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
            var options = CreateRequestEvaluationOptions(
                session.EvaluationOptions,
                timeoutMilliseconds);
            try
            {
                var resolvedExpression = ResolveExpression(
                    frame,
                    expression,
                    options);
                var value = frame.GetExpressionValue(
                    resolvedExpression,
                    options);
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
                    NormalizeEvaluationError(exception.Message),
                    exception);
            }
        }

        internal static string? ResolveIdentifierInFrameNamespace(
            string? namespaceName,
            string identifier,
            Func<string, bool> loadedTypeExists,
            Func<string, bool> frameAssemblyTypeExists)
        {
            if (loadedTypeExists(identifier) || frameAssemblyTypeExists(identifier))
                return identifier;
            if (string.IsNullOrWhiteSpace(namespaceName))
                return null;
            var candidate = namespaceName + "." + identifier;
            return loadedTypeExists(candidate) || frameAssemblyTypeExists(candidate)
                ? candidate
                : null;
        }

        internal static string NormalizeEvaluationError(string? message)
        {
            const string unknownIdentifierPrefix = "Unknown identifier: ";
            if (message == null || message.Trim().Length == 0)
                return "Expression evaluation failed.";
            if (
                message.StartsWith(
                    unknownIdentifierPrefix,
                    StringComparison.Ordinal))
            {
                var identifier = message.Substring(
                    unknownIdentifierPrefix.Length);
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    return "The identifier `" + identifier +
                        "` is not in the scope";
                }
            }
            return message;
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
                CreateReferenceVariableOptions(
                    session.EvaluationOptions,
                    timeoutMilliseconds),
                cancellationToken);
        }

        private static EvaluationOptions CreateRequestEvaluationOptions(
            EvaluationOptions sessionOptions,
            int timeoutMilliseconds)
        {
            if (sessionOptions == null)
                throw new ArgumentNullException(nameof(sessionOptions));
            var options = sessionOptions.Clone();
            options.EvaluationTimeout = timeoutMilliseconds;
            options.MemberEvaluationTimeout = timeoutMilliseconds;
            return options;
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
            long frameId)
        {
            ThrowIfDisposed();
            var frame = objectValues.GetFrame(frameId);
            return session.GetStepInTargets(frame)
                .Select(RegisterStepInTarget)
                .ToArray();
        }

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column)
        {
            ThrowIfDisposed();
            if (
                !session.CanSetNextStatement ||
                string.IsNullOrEmpty(sourcePath) ||
                line < 1)
            {
                return Array.Empty<BackendGotoTarget>();
            }
            return session.GetGotoTargets(
                    sourcePath,
                    line,
                    Math.Max(1, column))
                .Select(
                    target => new BackendGotoTarget(
                        RegisterGotoTarget(target),
                        $"line {target.Line}",
                        target.Line,
                        Math.Max(1, target.Column),
                        target.EndLine,
                        Math.Max(1, target.EndColumn)))
                .ToArray();
        }

        public void Goto(long threadId, long targetId)
        {
            ThrowIfDisposed();
            SoftGotoTarget target;
            lock (controlTargetLock)
            {
                if (!gotoTargets.TryGetValue(targetId, out target))
                {
                    throw new DebuggerBackendException(
                        "The requested goto target is unavailable.");
                }
            }
            session.Goto(threadId, target);
            ClearFrames();
            TargetStopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    BackendStopReason.Goto,
                    threadId,
                    null));
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
                ? CreateExceptionInfo(arguments)
                : null;
            var description = exception == null
                ? null
                : exception.ExceptionId + ": " + exception.Description;
            TargetStopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    arguments.Thread?.Id ?? 0,
                    description,
                    breakpointId,
                    exception,
                    arguments.ExceptionObjectId,
                    arguments.ExceptionRequestId,
                    arguments.ExceptionEventCount));
        }

        private BackendExceptionInfo CreateExceptionInfo(
            TargetEventArgs arguments)
        {
            var breakMode =
                arguments.Type == TargetEventType.UnhandledException
                    ? "unhandled"
                    : "always";
            return new BackendExceptionInfo(
                string.IsNullOrWhiteSpace(arguments.ExceptionTypeName)
                    ? "System.Exception"
                    : arguments.ExceptionTypeName,
                string.IsNullOrWhiteSpace(arguments.ExceptionMessage)
                    ? "Exception has occurred."
                    : arguments.ExceptionMessage,
                breakMode,
                null);
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

        private BackendStepInTarget RegisterStepInTarget(
            SoftStepInTarget target)
        {
            lock (controlTargetLock)
            {
                var id = NextControlTargetId();
                stepInTargets.Add(id, target);
                return new BackendStepInTarget(id, target.Label);
            }
        }

        private long RegisterGotoTarget(SoftGotoTarget target)
        {
            lock (controlTargetLock)
            {
                var id = NextControlTargetId();
                gotoTargets.Add(id, target);
                return id;
            }
        }

        private long NextControlTargetId()
        {
            if (nextControlTargetId == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Debugger control target handle space is exhausted.");
            }
            return nextControlTargetId++;
        }

        private void ClearFrames()
        {
            objectValues.Clear();
            lock (controlTargetLock)
            {
                stepInTargets.Clear();
                gotoTargets.Clear();
            }
        }

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
                NormalizeEvaluationError(message));
        }

        private void TryGetUnityFrameValues(
            Mono.Debugging.Client.StackFrame frame,
            EvaluationOptions options,
            CancellationToken cancellationToken,
            out bool isUnityMainThread,
            out ObjectValue? activeScene,
            out ObjectValue? thisGameObject)
        {
            isUnityMainThread = false;
            activeScene = null;
            thisGameObject = null;
            try
            {
                var context = session.CreateEvaluationContext(
                    frame,
                    options);
                var unityObjectType = context.Adapter.GetType(
                    context,
                    "UnityEngine.Object") as TypeMirror;
                if (unityObjectType == null)
                    return;
                var mainThreadMethod = unityObjectType.GetMethod(
                    "CurrentThreadIsMainThread");
                if (mainThreadMethod == null)
                    return;
                var mainThreadValue = InvokeUnityMethod(
                    context,
                    unityObjectType,
                    null,
                    mainThreadMethod,
                    cancellationToken) as PrimitiveValue;
                if (
                    mainThreadValue == null ||
                    !Convert.ToBoolean(
                        mainThreadValue.Value,
                        CultureInfo.InvariantCulture))
                {
                    return;
                }

                isUnityMainThread = true;
                activeScene = TryGetActiveScene(
                    context,
                    options,
                    cancellationToken);
                thisGameObject = TryGetThisGameObject(
                    context,
                    options,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                isUnityMainThread = false;
                activeScene = null;
                thisGameObject = null;
            }
        }

        private static ObjectValue? TryGetActiveScene(
            SoftEvaluationContext context,
            EvaluationOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var sceneManagerType = context.Adapter.GetType(
                    context,
                    "UnityEngine.SceneManagement.SceneManager") as TypeMirror;
                var method = sceneManagerType?.GetMethod("GetActiveScene");
                if (sceneManagerType == null || method == null)
                    return null;
                var value = InvokeUnityMethod(
                    context,
                    sceneManagerType,
                    null,
                    method,
                    cancellationToken);
                return CreateLiteralObjectValue(
                    context,
                    options,
                    "Active scene",
                    value);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static ObjectValue? TryGetThisGameObject(
            SoftEvaluationContext context,
            EvaluationOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var componentType = context.Adapter.GetType(
                    context,
                    "UnityEngine.Component") as TypeMirror;
                var thisReference = context.Adapter.GetThisReference(context);
                if (
                    componentType == null ||
                    thisReference == null ||
                    !(thisReference.Type is TypeMirror thisType) ||
                    !componentType.IsAssignableFrom(thisType) ||
                    !(thisReference.Value is ObjectMirror thisValue))
                {
                    return null;
                }
                var property = GetPropertyInHierarchy(thisType, "gameObject");
                var getter = property?.GetGetMethod(true);
                if (getter == null)
                    return null;
                var value = InvokeUnityMethod(
                    context,
                    getter.DeclaringType,
                    thisValue,
                    getter,
                    cancellationToken);
                return CreateLiteralObjectValue(
                    context,
                    options,
                    "this.gameObject",
                    value);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static PropertyInfoMirror? GetPropertyInHierarchy(
            TypeMirror? type,
            string name)
        {
            while (type != null)
            {
                var property = type.GetProperties().FirstOrDefault(
                    candidate => candidate.Name == name);
                if (property != null)
                    return property;
                type = type.BaseType;
            }
            return null;
        }

        private static Value? InvokeUnityMethod(
            SoftEvaluationContext context,
            TypeMirror type,
            ObjectMirror? instance,
            MethodMirror method,
            CancellationToken cancellationToken)
        {
            try
            {
                var invokeOptions = InvokeOptions.DisableBreakpoints |
                    InvokeOptions.SingleThreaded;
                var task = instance == null
                    ? type.InvokeMethodAsync(
                        context.Thread,
                        method,
                        Array.Empty<Value>(),
                        invokeOptions)
                    : instance.InvokeMethodAsync(
                        context.Thread,
                        method,
                        Array.Empty<Value>(),
                        invokeOptions);
                if (
                    !task.Wait(
                        ReferenceUnityInvokeTimeoutMilliseconds,
                        cancellationToken) ||
                    task.IsCanceled ||
                    task.IsFaulted)
                {
                    return null;
                }
                return task.Result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static ObjectValue? CreateLiteralObjectValue(
            SoftEvaluationContext context,
            EvaluationOptions options,
            string name,
            Value? value)
        {
            if (value == null)
                return null;
            return LiteralValueReference.CreateTargetObjectLiteral(
                    context,
                    name,
                    value)
                .CreateObjectValue(true, options);
        }

        private string ResolveExpression(
            Mono.Debugging.Client.StackFrame frame,
            string expression,
            EvaluationOptions options)
        {
            if (!options.UseExternalTypeResolver)
                return expression;
            lock (evaluationResolverLock)
            {
                var previousResolver = session.TypeResolverHandler;
                session.TypeResolverHandler = (identifier, _) =>
                {
                    var frameType = session.GetType(frame.FullTypeName);
                    return ResolveIdentifierInFrameNamespace(
                        frameType?.Namespace,
                        identifier,
                        candidate => session.GetType(candidate) != null,
                        candidate => frameType?.Assembly.GetType(
                            candidate,
                            false,
                            false) != null);
                };
                try
                {
                    var resolved = frame.ResolveExpression(expression);
                    options.UseExternalTypeResolver = false;
                    return resolved;
                }
                finally
                {
                    session.TypeResolverHandler = previousResolver;
                }
            }
        }

    }
}
