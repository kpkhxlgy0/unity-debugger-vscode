using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
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

    internal interface IMonoEvaluationConnection
    {
        bool TryGetFrameEvaluation(
            long frameId,
            out IFrameEvaluationEnvironment environment,
            out IUnityEvaluationContext? unityContext);
    }

    internal interface IMonoBreakpointEvaluationConnection
    {
        bool TryGetTopFrameEvaluation(
            long threadId,
            out IFrameEvaluationEnvironment? environment);
    }
}
