using System;
using System.Collections.Generic;
using System.Net;

namespace UnityDebugger.Adapter.Backend
{
    internal enum BackendEvaluationMode
    {
        Safe,
        Explicit,
    }

    internal sealed class AttachTarget
    {
        public AttachTarget(
            int processId,
            IPAddress address,
            int port,
            string workspaceRoot,
            string projectVersion,
            bool enableImplicitEvaluation = true)
        {
            ProcessId = processId;
            Address = address;
            Port = port;
            WorkspaceRoot = workspaceRoot;
            ProjectVersion = projectVersion;
            EnableImplicitEvaluation = enableImplicitEvaluation;
        }

        public int ProcessId { get; }
        public IPAddress Address { get; }
        public int Port { get; }
        public string WorkspaceRoot { get; }
        public string ProjectVersion { get; }
        public bool EnableImplicitEvaluation { get; }
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

    internal sealed class BackendSetVariableResult
    {
        public BackendSetVariableResult(
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
            string? condition,
            string? hitCondition,
            string? logMessage)
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
        public string? Condition { get; }
        public string? HitCondition { get; }
        public string? LogMessage { get; }
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

    internal sealed class BackendExceptionInfo
    {
        public BackendExceptionInfo(
            string exceptionId,
            string description,
            string breakMode,
            string? stackTrace)
        {
            ExceptionId = exceptionId;
            Description = description;
            BreakMode = breakMode;
            StackTrace = stackTrace;
        }

        public string ExceptionId { get; }
        public string Description { get; }
        public string BreakMode { get; }
        public string? StackTrace { get; }
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
            string? description,
            long? breakpointId = null,
            BackendExceptionInfo? exceptionInfo = null)
            : this(
                reason,
                threadId,
                description,
                breakpointId.HasValue
                    ? new[] { breakpointId.Value }
                    : Array.Empty<long>(),
                exceptionInfo)
        {
        }

        public BackendStoppedEventArgs(
            BackendStopReason reason,
            long threadId,
            string? description,
            IReadOnlyList<long> breakpointIds,
            BackendExceptionInfo? exceptionInfo = null)
        {
            Reason = reason;
            ThreadId = threadId;
            Description = description;
            BreakpointIds = breakpointIds ??
                throw new ArgumentNullException(nameof(breakpointIds));
            ExceptionInfo = exceptionInfo;
        }

        public BackendStopReason Reason { get; }
        public long ThreadId { get; }
        public string? Description { get; }
        public IReadOnlyList<long> BreakpointIds { get; }
        public BackendExceptionInfo? ExceptionInfo { get; }
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

    internal sealed class BackendModule
    {
        public BackendModule(
            string id,
            string name,
            string? path,
            bool hasSymbols)
        {
            Id = id;
            Name = name;
            Path = path;
            HasSymbols = hasSymbols;
        }

        public string Id { get; }
        public string Name { get; }
        public string? Path { get; }
        public bool HasSymbols { get; }
    }

    internal sealed class BackendModuleChangedEventArgs : EventArgs
    {
        public BackendModuleChangedEventArgs(
            BackendModule module,
            bool loaded)
        {
            Module = module;
            Loaded = loaded;
        }

        public BackendModule Module { get; }
        public bool Loaded { get; }
    }

    internal sealed class BackendOutputEventArgs : EventArgs
    {
        public BackendOutputEventArgs(string category, string output)
        {
            Category = category;
            Output = output;
        }

        public string Category { get; }
        public string Output { get; }
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
