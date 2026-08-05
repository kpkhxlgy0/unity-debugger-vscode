using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Source
{
    internal sealed class RuntimeModuleDescriptor
    {
        public RuntimeModuleDescriptor(string id, string name, string path)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Id { get; }
        public string Name { get; }
        public string Path { get; }
    }

    internal sealed class RuntimeSourceLocation
    {
        public RuntimeSourceLocation(
            string sourcePath,
            int line,
            int column,
            int endLine,
            int endColumn,
            object runtimeLocation)
        {
            SourcePath = sourcePath ??
                throw new ArgumentNullException(nameof(sourcePath));
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
            RuntimeLocation = runtimeLocation ??
                throw new ArgumentNullException(nameof(runtimeLocation));
        }

        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
        public object RuntimeLocation { get; }
    }

    internal interface IRuntimeSourceType
    {
        object RuntimeIdentity { get; }
        object DomainIdentity { get; }
        RuntimeModuleDescriptor RuntimeModule { get; }
        IReadOnlyList<RuntimeSourceLocation> SourceLocations { get; }
        IReadOnlyList<IRuntimeType> NestedTypes { get; }
    }

    internal sealed class EngineSourceLocation
    {
        public EngineSourceLocation(
            IRuntimeType type,
            UnityDomainState domain,
            UnityModuleState module,
            RuntimeSourceLocation location)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Domain = domain ??
                throw new ArgumentNullException(nameof(domain));
            Module = module ??
                throw new ArgumentNullException(nameof(module));
            RuntimeLocation = location?.RuntimeLocation ??
                throw new ArgumentNullException(nameof(location));
            SourcePath = location.SourcePath;
            Line = location.Line;
            Column = location.Column;
            EndLine = location.EndLine;
            EndColumn = location.EndColumn;
        }

        public IRuntimeType Type { get; }
        public UnityDomainState Domain { get; }
        public UnityModuleState Module { get; }
        public object RuntimeLocation { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
    }
}
