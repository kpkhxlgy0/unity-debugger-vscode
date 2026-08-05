using System;
using System.Threading;

namespace UnityDebugger.Adapter.Engine.Events
{
    internal interface IEngineEventSource
    {
        EngineEventSet GetNextEventSet(
            CancellationToken cancellationToken);
        void Resume();
    }

    internal interface IEngineEventReceiver
    {
        void Process(EngineEvent value);
        void Terminate(Exception? exception);
    }
}
