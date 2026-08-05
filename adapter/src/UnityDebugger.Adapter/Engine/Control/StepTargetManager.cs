using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Control
{
    internal sealed class RuntimeStepInTarget
    {
        public RuntimeStepInTarget(string label, object codePath)
        {
            Label = label;
            CodePath = codePath;
        }

        public string Label { get; }
        public object CodePath { get; }
    }

    internal interface IStepTargetRuntime
    {
        IReadOnlyList<RuntimeStepInTarget> GetStepInTargets(
            long runtimeFrameId);
        void StepIn(long threadId, object codePath);
    }

    internal sealed class StepTargetManager
    {
        private readonly SuspendedState state;
        private readonly IStepTargetRuntime runtime;
        private readonly ConcurrentDictionary<long, RegisteredFrame> frames =
            new ConcurrentDictionary<long, RegisteredFrame>();

        public StepTargetManager(
            SuspendedState state,
            IStepTargetRuntime runtime)
        {
            this.state = state ??
                throw new ArgumentNullException(nameof(state));
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public void RegisterFrame(long frameId, long runtimeFrameId)
        {
            frames[frameId] = new RegisteredFrame(
                runtimeFrameId,
                state.Generation);
        }

        public IReadOnlyList<BackendStepInTarget> GetTargets(long frameId)
        {
            if (
                !frames.TryGetValue(frameId, out var frame) ||
                frame.Generation != state.Generation)
            {
                return Array.Empty<BackendStepInTarget>();
            }

            var targets = runtime.GetStepInTargets(frame.RuntimeFrameId);
            var result = new List<BackendStepInTarget>(targets.Count);
            foreach (var target in targets)
            {
                var id = state.RegisterCodePath(target.CodePath);
                result.Add(new BackendStepInTarget(id, target.Label));
            }
            return result;
        }

        public bool TryStepIn(long threadId, long targetId)
        {
            if (
                targetId <= 0 ||
                targetId > int.MaxValue ||
                !state.TryGetCodePath<object>(
                    (int)targetId,
                    out var codePath))
            {
                return false;
            }
            runtime.StepIn(threadId, codePath);
            return true;
        }

        private sealed class RegisteredFrame
        {
            public RegisteredFrame(long runtimeFrameId, int generation)
            {
                RuntimeFrameId = runtimeFrameId;
                Generation = generation;
            }

            public long RuntimeFrameId { get; }
            public int Generation { get; }
        }
    }
}
