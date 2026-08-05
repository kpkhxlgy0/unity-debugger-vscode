using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace UnityDebugger.Adapter.Engine.Events
{
    internal sealed class EngineEventDispatcher
    {
        private readonly IEngineEventSource source;
        private readonly IEngineEventReceiver receiver;
        private readonly CancellationTokenSource cancellation =
            new CancellationTokenSource();
        private readonly ConcurrentQueue<EngineEvent> breakingEvents =
            new ConcurrentQueue<EngineEvent>();
        private readonly object dispatchedThreadLock = new object();
        private readonly Task dispatchTask;
        private long? dispatchedThreadId;
        private int resumeCount = 1;
        private int started;
        private int terminated;

        public EngineEventDispatcher(
            IEngineEventSource source,
            IEngineEventReceiver receiver)
        {
            this.source = source ??
                throw new ArgumentNullException(nameof(source));
            this.receiver = receiver ??
                throw new ArgumentNullException(nameof(receiver));
            dispatchTask = new Task(
                DispatchEvents,
                cancellation.Token,
                TaskCreationOptions.LongRunning);
        }

        public Task Completion => dispatchTask;

        public void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                throw new InvalidOperationException(
                    "Event dispatch has already started.");
            dispatchTask.Start(TaskScheduler.Default);
        }

        public void BeforeResuming()
        {
            lock (dispatchedThreadLock)
            {
                dispatchedThreadId = null;
                resumeCount--;
            }
        }

        public void AfterResuming()
        {
            EngineEvent value;
            lock (dispatchedThreadLock)
            {
                if (!breakingEvents.TryDequeue(out value))
                    return;
                dispatchedThreadId = value.ThreadId;
            }
            receiver.Process(value);
        }

        public void Stop()
        {
            cancellation.Cancel();
        }

        private void DispatchEvents()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var eventSet = source.GetNextEventSet(
                        cancellation.Token);
                    DispatchEventSet(eventSet);
                }
                TerminateOnce(null);
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
                TerminateOnce(null);
            }
            catch (Exception exception)
            {
                cancellation.Cancel();
                TerminateOnce(exception);
            }
        }

        private void DispatchEventSet(EngineEventSet eventSet)
        {
            var containsBreakingEvent = false;
            foreach (var value in eventSet.Events)
            {
                if (IsBreaking(value.Kind))
                {
                    containsBreakingEvent = true;
                    HandleBreakingEvent(value);
                }
                else
                {
                    receiver.Process(value);
                }
            }
            if (!containsBreakingEvent)
                ResumeSuspended(eventSet.SuspendPolicy);
        }

        private void HandleBreakingEvent(EngineEvent value)
        {
            var dispatch = false;
            lock (dispatchedThreadLock)
            {
                resumeCount++;
                if (
                    dispatchedThreadId.HasValue &&
                    dispatchedThreadId.Value != value.ThreadId)
                {
                    breakingEvents.Enqueue(value);
                }
                else if (
                    dispatchedThreadId.HasValue &&
                    dispatchedThreadId.Value == value.ThreadId &&
                    value.Kind == EngineEventKind.Exception)
                {
                    source.Resume();
                    resumeCount--;
                }
                else
                {
                    dispatchedThreadId = value.ThreadId;
                    dispatch = true;
                }
            }
            if (dispatch)
                receiver.Process(value);
        }

        private void ResumeSuspended(EngineSuspendPolicy suspendPolicy)
        {
            if (
                cancellation.IsCancellationRequested ||
                suspendPolicy == EngineSuspendPolicy.None)
            {
                return;
            }
            try
            {
                source.Resume();
            }
            catch
            {
            }
        }

        private void TerminateOnce(Exception? exception)
        {
            if (Interlocked.Exchange(ref terminated, 1) == 0)
                receiver.Terminate(exception);
        }

        private static bool IsBreaking(EngineEventKind kind) =>
            kind == EngineEventKind.UserBreak ||
            kind == EngineEventKind.Breakpoint ||
            kind == EngineEventKind.Step ||
            kind == EngineEventKind.Exception;
    }
}
