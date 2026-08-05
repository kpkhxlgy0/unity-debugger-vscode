using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Events;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal interface IMonoEngineConnection : IDisposable
    {
        void Connect();
        IEngineEventSource CreateEventSource();
        IStepRuntime CreateStepRuntime(EngineEventDispatcher dispatcher);
        IReadOnlyList<BackendThread> GetThreads();
        IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels);
        void Suspend();
        void Disconnect();
    }
}
