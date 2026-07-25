using System;
using System.Net;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class AttachTarget
    {
        public AttachTarget(
            int processId,
            IPAddress address,
            int port,
            string workspaceRoot,
            string projectVersion)
        {
            ProcessId = processId;
            Address = address;
            Port = port;
            WorkspaceRoot = workspaceRoot;
            ProjectVersion = projectVersion;
        }

        public int ProcessId { get; }
        public IPAddress Address { get; }
        public int Port { get; }
        public string WorkspaceRoot { get; }
        public string ProjectVersion { get; }
    }

    internal sealed class BackendThread
    {
        public BackendThread(long id, string name)
        {
            Id = id;
            Name = name;
        }

        public long Id { get; }
        public string Name { get; }
    }

    internal sealed class BackendStackFrame
    {
        public BackendStackFrame(
            long id,
            long threadId,
            string name,
            string sourcePath,
            int line,
            int column)
        {
            Id = id;
            ThreadId = threadId;
            Name = name;
            SourcePath = sourcePath;
            Line = line;
            Column = column;
        }

        public long Id { get; }
        public long ThreadId { get; }
        public string Name { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
    }

    internal sealed class BackendScope
    {
        public BackendScope(
            string name,
            long variablesReference,
            bool expensive)
        {
            Name = name;
            VariablesReference = variablesReference;
            Expensive = expensive;
        }

        public string Name { get; }
        public long VariablesReference { get; }
        public bool Expensive { get; }
    }

    internal sealed class BackendVariable
    {
        public BackendVariable(
            string name,
            string displayValue,
            string typeName,
            long variablesReference)
        {
            Name = name;
            DisplayValue = displayValue;
            TypeName = typeName;
            VariablesReference = variablesReference;
        }

        public string Name { get; }
        public string DisplayValue { get; }
        public string TypeName { get; }
        public long VariablesReference { get; }
    }

    internal sealed class BackendEvaluationResult
    {
        public BackendEvaluationResult(
            string displayValue,
            string typeName,
            long variablesReference)
        {
            DisplayValue = displayValue;
            TypeName = typeName;
            VariablesReference = variablesReference;
        }

        public string DisplayValue { get; }
        public string TypeName { get; }
        public long VariablesReference { get; }
    }

    internal sealed class LogicalBreakpoint
    {
        public LogicalBreakpoint(
            long id,
            string sourcePath,
            int line,
            int column,
            string condition,
            string hitCondition,
            string logMessage)
        {
            Id = id;
            SourcePath = sourcePath;
            Line = line;
            Column = column;
            Condition = condition;
            HitCondition = hitCondition;
            LogMessage = logMessage;
        }

        public long Id { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
        public string Condition { get; }
        public string HitCondition { get; }
        public string LogMessage { get; }
    }

    internal sealed class BackendBoundBreakpoint
    {
        public BackendBoundBreakpoint(
            long id,
            bool verified,
            int line,
            string? message)
        {
            Id = id;
            Verified = verified;
            Line = line;
            Message = message;
        }

        public long Id { get; }
        public bool Verified { get; }
        public int Line { get; }
        public string? Message { get; }
    }

    internal enum BackendStopReason
    {
        Breakpoint,
        Step,
        Pause,
        Exception,
        Entry,
    }

    internal enum ExceptionBreakMode
    {
        None,
        Uncaught,
        All,
    }

    internal sealed class BackendStoppedEventArgs : EventArgs
    {
        public BackendStoppedEventArgs(
            BackendStopReason reason,
            long threadId,
            string description)
        {
            Reason = reason;
            ThreadId = threadId;
            Description = description;
        }

        public BackendStopReason Reason { get; }
        public long ThreadId { get; }
        public string Description { get; }
    }

    internal sealed class BackendThreadEventArgs : EventArgs
    {
        public BackendThreadEventArgs(long threadId, bool started)
        {
            ThreadId = threadId;
            Started = started;
        }

        public long ThreadId { get; }
        public bool Started { get; }
    }

    internal sealed class BackendBreakpointChangedEventArgs : EventArgs
    {
        public BackendBreakpointChangedEventArgs(
            BackendBoundBreakpoint breakpoint)
        {
            Breakpoint = breakpoint;
        }

        public BackendBoundBreakpoint Breakpoint { get; }
    }

    internal sealed class DebuggerBackendException : Exception
    {
        public DebuggerBackendException(string message)
            : base(message)
        {
        }

        public DebuggerBackendException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
