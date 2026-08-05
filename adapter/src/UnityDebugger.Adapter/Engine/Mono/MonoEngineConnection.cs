using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Mono.Cecil.Cil;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
using UnityDebugger.Adapter.Engine.Events;
using UnityDebugger.Adapter.Engine.Source;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoEngineConnection :
        IMonoEngineConnection,
        IMonoEvaluationConnection,
        IMonoBreakpointEvaluationConnection,
        IMonoSourceConnection,
        IEngineBreakpointRuntime,
        IEngineExceptionRuntime,
        IStepTargetRuntime,
        IGotoRuntime
    {
        private readonly AttachTarget target;
        private readonly ConcurrentDictionary<long, ThreadMirror> threads =
            new ConcurrentDictionary<long, ThreadMirror>();
        private readonly ConcurrentDictionary<long, StackFrame> frames =
            new ConcurrentDictionary<long, StackFrame>();
        private readonly ConcurrentDictionary<long, ObjectMirror> exceptions =
            new ConcurrentDictionary<long, ObjectMirror>();
        private readonly ConcurrentDictionary<long, bool>
            staticConstructorFrames =
                new ConcurrentDictionary<long, bool>();
        private readonly ConcurrentDictionary<string, byte> loadedSources =
            new ConcurrentDictionary<string, byte>(
                StringComparer.OrdinalIgnoreCase);
        private readonly List<EventRequest> lifecycleRequests =
            new List<EventRequest>();
        private readonly HashSet<EventRequest> targetedStepRequests =
            new HashSet<EventRequest>();
        private readonly object targetedStepLock = new object();
        private VirtualMachine? virtualMachine;
        private EngineEventDispatcher? dispatcher;
        private long nextFrameId;
        private int disconnected;

        public MonoEngineConnection(AttachTarget target)
        {
            this.target = target ??
                throw new ArgumentNullException(nameof(target));
        }

        public void Connect()
        {
            if (!IPAddress.IsLoopback(target.Address))
            {
                throw new DebuggerBackendException(
                    "Only loopback Editor targets are allowed.");
            }

            try
            {
                var created = VirtualMachineManager.Connect(
                    new IPEndPoint(target.Address, target.Port));
                try
                {
                    ConfigureEvents(created);
                    virtualMachine = created;
                }
                catch
                {
                    created.Detach();
                    throw;
                }
                Interlocked.Exchange(ref disconnected, 0);
            }
            catch (VMMismatchException exception)
            {
                throw new DebuggerBackendException(
                    "Editor uses an incompatible Mono Soft Debugger protocol.",
                    exception);
            }
            catch (Exception exception)
            {
                throw new DebuggerBackendException(
                    $"Could not connect to local Editor at " +
                    $"127.0.0.1:{target.Port}.",
                    exception);
            }
        }

        public IEngineEventSource CreateEventSource() =>
            new MonoEventSource(RequireVirtualMachine(), ProcessEvent);

        public IStepRuntime CreateStepRuntime(
            EngineEventDispatcher eventDispatcher)
        {
            dispatcher = eventDispatcher ??
                throw new ArgumentNullException(nameof(eventDispatcher));
            return new MonoStepRuntime(
                RequireVirtualMachine(),
                eventDispatcher,
                ResolveThread,
                _ => null,
                IsStaticConstructorFrame);
        }

        public IReadOnlyList<BackendThread> GetThreads()
        {
            var result = new List<BackendThread>();
            foreach (var thread in RequireVirtualMachine().GetThreads())
            {
                threads[thread.ThreadId] = thread;
                result.Add(
                    new BackendThread(
                        thread.ThreadId,
                        GetThreadName(thread)));
            }
            return result;
        }

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            var runtimeFrames = ResolveThread(threadId).GetFrames();
            if (runtimeFrames.Length > 0)
            {
                staticConstructorFrames[threadId] =
                    runtimeFrames[0].Method.Name == ".cctor";
            }
            var start = Math.Min(
                Math.Max(0, startFrame),
                runtimeFrames.Length);
            var count = levels <= 0 || levels == int.MaxValue
                ? runtimeFrames.Length - start
                : Math.Min(levels, runtimeFrames.Length - start);
            var result = new List<BackendStackFrame>(count);
            for (var index = start; index < start + count; index++)
            {
                var frame = runtimeFrames[index];
                var id = Interlocked.Increment(ref nextFrameId);
                frames[id] = frame;
                var location = frame.Location;
                result.Add(
                    new BackendStackFrame(
                        id,
                        threadId,
                        GetFrameName(frame),
                        location.SourceFile ?? string.Empty,
                        Math.Max(0, location.LineNumber),
                        Math.Max(1, location.ColumnNumber)));
            }
            return result;
        }

        public bool TryGetFrameEvaluation(
            long frameId,
            out IFrameEvaluationEnvironment environment,
            out IUnityEvaluationContext? unityContext)
        {
            unityContext = null;
            if (!frames.TryGetValue(frameId, out var frame))
            {
                environment = null!;
                return false;
            }
            exceptions.TryGetValue(
                frame.Thread.ThreadId,
                out var currentException);
            environment = new MonoFrameEvaluationEnvironment(
                frame,
                currentException);
            return true;
        }

        public bool TryGetTopFrameEvaluation(
            long threadId,
            out IFrameEvaluationEnvironment? environment)
        {
            var runtimeFrames = ResolveThread(threadId).GetFrames();
            if (runtimeFrames.Length == 0)
            {
                environment = null;
                return false;
            }
            exceptions.TryGetValue(threadId, out var currentException);
            staticConstructorFrames[threadId] =
                runtimeFrames[0].Method.Name == ".cctor";
            environment = new MonoFrameEvaluationEnvironment(
                runtimeFrames[0],
                currentException);
            return true;
        }

        public IReadOnlyList<IRuntimeType> GetSourceTypes(
            string sourcePath)
        {
            if (!loadedSources.TryAdd(sourcePath, 0))
                return Array.Empty<IRuntimeType>();
            var value = RequireVirtualMachine();
            try
            {
                var request = value.CreateTypeLoadRequest();
                request.SourceFileFilter = new[] { sourcePath };
                request.Enable();
                lock (lifecycleRequests)
                    lifecycleRequests.Add(request);
            }
            catch (NotSupportedException)
            {
            }
            try
            {
                return value
                    .GetTypesForSourceFile(sourcePath, true)
                    .Select(type => (IRuntimeType)new MonoRuntimeType(type))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<IRuntimeType>();
            }
        }

        public IEngineBreakpointRequest CreateBreakpoint(
            EngineSourceLocation location)
        {
            if (!(location.RuntimeLocation is Location monoLocation))
            {
                throw new ArgumentException(
                    "The breakpoint location is not a Mono location.",
                    nameof(location));
            }
            return new MonoBreakpointRequest(
                RequireVirtualMachine().CreateBreakpointRequest(
                    monoLocation));
        }

        public IEngineExceptionRequest CreateExceptionRequest(
            bool caught,
            bool uncaught) =>
            new MonoExceptionRequest(
                RequireVirtualMachine().CreateExceptionRequest(
                    null,
                    caught,
                    uncaught));

        public IReadOnlyList<RuntimeStepInTarget> GetStepInTargets(
            long runtimeFrameId)
        {
            if (!frames.TryGetValue(runtimeFrameId, out var frame))
                return Array.Empty<RuntimeStepInTarget>();
            try
            {
                var current = frame.Location;
                var endOffset = frame.Method.Locations
                    .Where(location =>
                        location.ILOffset > current.ILOffset)
                    .Select(location => location.ILOffset)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
                return frame.Method.GetMethodBody().Instructions
                    .Where(instruction =>
                        instruction.Offset >= current.ILOffset &&
                        instruction.Offset < endOffset &&
                        IsCall(instruction.OpCode) &&
                        instruction.Operand is MethodMirror)
                    .Select(instruction =>
                        (MethodMirror)instruction.Operand)
                    .Where(method => method.Locations.Count > 0)
                    .GroupBy(method => method.FullName)
                    .Select(group => group.First())
                    .Select(method => new RuntimeStepInTarget(
                        GetMethodLabel(method),
                        method))
                    .ToArray();
            }
            catch (Exception exception)
                when (IsInspectionUnavailable(exception))
            {
                return Array.Empty<RuntimeStepInTarget>();
            }
        }

        public void StepIn(long threadId, object codePath)
        {
            if (!(codePath is MethodMirror method))
                throw new InvalidOperationException("Step target expired.");
            var location = method.Locations.FirstOrDefault();
            if (location == null)
                throw new InvalidOperationException("Step target has no code.");
            var request = RequireVirtualMachine().CreateBreakpointRequest(
                location);
            request.Thread = ResolveThread(threadId);
            lock (targetedStepLock)
                targetedStepRequests.Add(request);
            try
            {
                request.Enable();
                ResumeFromControl();
            }
            catch
            {
                lock (targetedStepLock)
                    targetedStepRequests.Remove(request);
                if (request.Enabled)
                    request.Disable();
                throw;
            }
        }

        public IReadOnlyList<RuntimeGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column)
        {
            try
            {
                var locations = RequireVirtualMachine()
                    .GetTypesForSourceFile(sourcePath, true)
                    .SelectMany(type => type.GetMethods())
                    .SelectMany(method => method.Locations)
                    .Where(location => location.LineNumber == line)
                    .GroupBy(location => new
                    {
                        Method = location.Method.FullName,
                        location.ILOffset,
                    })
                    .Select(group => group.First())
                    .OrderBy(location => location.ColumnNumber)
                    .ToArray();
                return locations.Select(location =>
                    new RuntimeGotoTarget(
                        GetGotoLabel(location),
                        location.LineNumber,
                        Math.Max(1, location.ColumnNumber),
                        location.EndLineNumber > 0
                            ? location.EndLineNumber
                            : location.LineNumber,
                        location.EndColumnNumber > 0
                            ? location.EndColumnNumber
                            : Math.Max(1, location.ColumnNumber),
                        location))
                    .ToArray();
            }
            catch (Exception exception)
                when (IsInspectionUnavailable(exception))
            {
                return Array.Empty<RuntimeGotoTarget>();
            }
        }

        public void Goto(long threadId, object codeContext)
        {
            if (!(codeContext is Location location))
                return;
            ResolveThread(threadId).SetIP(location);
        }

        public void Suspend()
        {
            RequireVirtualMachine().Suspend();
        }

        public void Disconnect()
        {
            var value = virtualMachine;
            if (
                value == null ||
                Interlocked.Exchange(ref disconnected, 1) != 0)
            {
                return;
            }
            DisableTargetedSteps();
            try
            {
                value.Detach();
            }
            catch (VMDisconnectedException)
            {
            }
            finally
            {
                virtualMachine = null;
                frames.Clear();
                threads.Clear();
                exceptions.Clear();
                staticConstructorFrames.Clear();
                loadedSources.Clear();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        private EngineEventKind? ProcessEvent(Event sourceEvent)
        {
            var threadId = sourceEvent.Thread?.ThreadId ?? 0;
            if (sourceEvent is ThreadStartEvent started)
                threads[started.Thread.ThreadId] = started.Thread;
            if (sourceEvent is ThreadDeathEvent)
            {
                threads.TryRemove(threadId, out _);
                staticConstructorFrames.TryRemove(threadId, out _);
            }
            if (sourceEvent is ExceptionEvent exceptionEvent)
                exceptions[threadId] = exceptionEvent.Exception;
            else if (IsBreaking(sourceEvent.EventType))
                exceptions.TryRemove(threadId, out _);

            if (sourceEvent is BreakpointEvent breakpointEvent)
            {
                lock (targetedStepLock)
                {
                    if (targetedStepRequests.Remove(
                        breakpointEvent.Request))
                    {
                        if (breakpointEvent.Request.Enabled)
                            breakpointEvent.Request.Disable();
                        return EngineEventKind.Step;
                    }
                }
            }
            return null;
        }

        private void ConfigureEvents(VirtualMachine value)
        {
            EnableEvents(
                value,
                SuspendPolicy.All,
                EventType.ThreadStart,
                EventType.ThreadDeath,
                EventType.AssemblyUnload,
                EventType.AppDomainCreate,
                EventType.AppDomainUnload,
                EventType.UserBreak);
            EnableEvents(
                value,
                SuspendPolicy.None,
                EventType.UserLog);

            var assemblyLoad = value.CreateAssemblyLoadRequest();
            assemblyLoad.Enable();
            lifecycleRequests.Add(assemblyLoad);
        }

        private static void EnableEvents(
            VirtualMachine value,
            SuspendPolicy suspendPolicy,
            params EventType[] eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                try
                {
                    value.EnableEvents(
                        new[] { eventType },
                        suspendPolicy);
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        private ThreadMirror ResolveThread(long threadId)
        {
            if (threads.TryGetValue(threadId, out var existing))
                return existing;
            GetThreads();
            if (threads.TryGetValue(threadId, out existing))
                return existing;
            throw new InvalidOperationException(
                "The stopped thread is no longer available.");
        }

        private bool IsStaticConstructorFrame(long threadId)
        {
            if (staticConstructorFrames.TryGetValue(threadId, out var value))
                return value;
            try
            {
                var frame = ResolveThread(threadId)
                    .GetFrames()
                    .FirstOrDefault();
                value = frame?.Method.Name == ".cctor";
                staticConstructorFrames[threadId] = value;
                return value;
            }
            catch (VMNotSuspendedException)
            {
                return false;
            }
        }

        private void ResumeFromControl()
        {
            var eventDispatcher = dispatcher ??
                throw new InvalidOperationException(
                    "The event dispatcher is unavailable.");
            eventDispatcher.BeforeResuming();
            RequireVirtualMachine().Resume();
            eventDispatcher.AfterResuming();
        }

        private void DisableTargetedSteps()
        {
            lock (targetedStepLock)
            {
                foreach (var request in targetedStepRequests.ToArray())
                {
                    if (request.Enabled)
                        request.Disable();
                }
                targetedStepRequests.Clear();
            }
        }

        private VirtualMachine RequireVirtualMachine() =>
            virtualMachine ??
            throw new InvalidOperationException(
                "The Mono debugger is not connected.");

        private static string GetThreadName(ThreadMirror thread)
        {
            try
            {
                return string.IsNullOrEmpty(thread.Name)
                    ? $"Thread {thread.ThreadId}"
                    : thread.Name;
            }
            catch
            {
                return $"Thread {thread.ThreadId}";
            }
        }

        private static string GetFrameName(StackFrame frame) =>
            frame.Method.DeclaringType.FullName + "." + frame.Method.Name;

        private static string GetMethodLabel(MethodMirror method) =>
            method.DeclaringType.Name + "." + method.Name + "(" +
            string.Join(
                ", ",
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType.Name)) +
            ")";

        private static string GetGotoLabel(Location location) =>
            location.Method.DeclaringType.Name + "." +
            location.Method.Name + ": " +
            location.LineNumber;

        private static bool IsCall(OpCode code) =>
            code == OpCodes.Call ||
            code == OpCodes.Callvirt ||
            code == OpCodes.Newobj;

        private static bool IsBreaking(EventType eventType) =>
            eventType == EventType.Breakpoint ||
            eventType == EventType.Step ||
            eventType == EventType.Exception ||
            eventType == EventType.UserBreak;

        private static bool IsInspectionUnavailable(Exception exception) =>
            exception is AbsentInformationException ||
            exception is InvalidStackFrameException ||
            exception is VMNotSuspendedException ||
            exception is NotSupportedException;

        private sealed class MonoBreakpointRequest :
            IEngineBreakpointRequest
        {
            private readonly BreakpointEventRequest request;

            public MonoBreakpointRequest(BreakpointEventRequest request)
            {
                this.request = request;
            }

            public object Identity => request;
            public void Enable() => request.Enable();
            public void Disable() => request.Disable();
        }

        private sealed class MonoExceptionRequest : IEngineExceptionRequest
        {
            private readonly ExceptionEventRequest request;

            public MonoExceptionRequest(ExceptionEventRequest request)
            {
                this.request = request;
            }

            public object Identity => request;
            public void Enable() => request.Enable();
            public void Disable() => request.Disable();
        }
    }
}
