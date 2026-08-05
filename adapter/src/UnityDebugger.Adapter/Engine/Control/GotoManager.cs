using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Control
{
    internal sealed class RuntimeGotoTarget
    {
        public RuntimeGotoTarget(
            string label,
            int line,
            int column,
            int endLine,
            int endColumn,
            object codeContext)
        {
            Label = label;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
            CodeContext = codeContext;
        }

        public string Label { get; }
        public int Line { get; }
        public int Column { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
        public object CodeContext { get; }
    }

    internal interface IGotoRuntime
    {
        IReadOnlyList<RuntimeGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column);
        void Goto(long threadId, object codeContext);
    }

    internal sealed class GotoManager
    {
        private readonly SuspendedState state;
        private readonly IGotoRuntime runtime;

        public GotoManager(SuspendedState state, IGotoRuntime runtime)
        {
            this.state = state ??
                throw new ArgumentNullException(nameof(state));
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public IReadOnlyList<BackendGotoTarget> GetTargets(
            string sourcePath,
            int line,
            int column)
        {
            var targets = runtime.GetGotoTargets(sourcePath, line, column);
            var result = new List<BackendGotoTarget>(targets.Count);
            foreach (var target in targets)
            {
                var id = state.RegisterCodeContext(target.CodeContext);
                result.Add(
                    new BackendGotoTarget(
                        id,
                        target.Label,
                        target.Line,
                        target.Column,
                        target.EndLine,
                        target.EndColumn));
            }
            return result;
        }

        public bool TryGoto(long threadId, long targetId)
        {
            if (
                targetId <= 0 ||
                targetId > int.MaxValue ||
                !state.TryGetCodeContext<object>(
                    (int)targetId,
                    out var codeContext))
            {
                return false;
            }
            runtime.Goto(threadId, codeContext);
            return true;
        }
    }
}
