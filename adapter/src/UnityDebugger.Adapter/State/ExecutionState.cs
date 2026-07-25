using System;

namespace UnityDebugger.Adapter.State
{
    internal enum ExecutionStatus
    {
        Disconnected,
        Running,
        Stopped,
        Reloading,
    }

    internal sealed class ExecutionState
    {
        public ExecutionStatus Status { get; private set; } =
            ExecutionStatus.Disconnected;

        public void Attached() =>
            Transition(
                ExecutionStatus.Disconnected,
                ExecutionStatus.Running);

        public void Stopped() =>
            Transition(
                ExecutionStatus.Running,
                ExecutionStatus.Stopped);

        public void Continued() =>
            Transition(
                ExecutionStatus.Stopped,
                ExecutionStatus.Running);

        public void Disconnected()
        {
            Status = ExecutionStatus.Disconnected;
        }

        public bool ReloadStarted()
        {
            if (Status == ExecutionStatus.Reloading)
                return false;
            if (Status == ExecutionStatus.Disconnected)
            {
                throw new InvalidOperationException(
                    "Domain Reload requires an attached target.");
            }
            var wasStopped = Status == ExecutionStatus.Stopped;
            Status = ExecutionStatus.Reloading;
            return wasStopped;
        }

        public bool ReloadCompleted()
        {
            if (Status != ExecutionStatus.Reloading)
                return false;
            Status = ExecutionStatus.Running;
            return true;
        }

        public void RequireStopped(string operation)
        {
            if (Status != ExecutionStatus.Stopped)
            {
                throw new InvalidOperationException(
                    $"{operation} requires a stopped target.");
            }
        }

        public void RequireRunning(string operation)
        {
            if (Status != ExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"{operation} requires a running target.");
            }
        }

        private void Transition(
            ExecutionStatus expected,
            ExecutionStatus next)
        {
            if (Status != expected)
            {
                throw new InvalidOperationException(
                    $"Cannot transition execution state from " +
                    $"{Status} to {next}.");
            }
            Status = next;
        }
    }
}
