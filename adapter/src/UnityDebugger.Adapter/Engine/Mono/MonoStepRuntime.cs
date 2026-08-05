using System;
using System.Collections.Generic;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Events;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoStepRuntime : IStepRuntime
    {
        private readonly VirtualMachine virtualMachine;
        private readonly EngineEventDispatcher dispatcher;
        private readonly Func<long, ThreadMirror> resolveThread;
        private readonly Func<long, IList<AssemblyMirror>?> assemblyFilter;
        private readonly Func<long, bool> isStaticConstructorFrame;

        public MonoStepRuntime(
            VirtualMachine virtualMachine,
            EngineEventDispatcher dispatcher,
            Func<long, ThreadMirror> resolveThread,
            Func<long, IList<AssemblyMirror>?> assemblyFilter,
            Func<long, bool> isStaticConstructorFrame)
        {
            this.virtualMachine = virtualMachine ??
                throw new ArgumentNullException(nameof(virtualMachine));
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
            this.resolveThread = resolveThread ??
                throw new ArgumentNullException(nameof(resolveThread));
            this.assemblyFilter = assemblyFilter ??
                throw new ArgumentNullException(nameof(assemblyFilter));
            this.isStaticConstructorFrame = isStaticConstructorFrame ??
                throw new ArgumentNullException(
                    nameof(isStaticConstructorFrame));
        }

        public bool IsStaticConstructorFrame(long threadId) =>
            isStaticConstructorFrame(threadId);

        public IStepRequest CreateStepRequest(
            long threadId,
            StepRequestOptions options)
        {
            var request = virtualMachine.CreateStepRequest(
                resolveThread(threadId));
            request.Depth = MapDepth(options.Depth);
            request.Size = MapSize(options.Size);
            request.AssemblyFilter = assemblyFilter(threadId);
            request.Filter = virtualMachine.Version.AtLeast(2, 26)
                ? MapFilter(options.Filters)
                : StepFilter.None;
            return new MonoStepRequest(request);
        }

        public void Resume()
        {
            try
            {
                dispatcher.BeforeResuming();
                virtualMachine.Resume();
                dispatcher.AfterResuming();
            }
            catch (InvalidOperationException)
            {
            }
        }

        internal static StepDepth MapDepth(EngineStepDepth value)
        {
            switch (value)
            {
                case EngineStepDepth.Into:
                    return StepDepth.Into;
                case EngineStepDepth.Over:
                    return StepDepth.Over;
                case EngineStepDepth.Out:
                    return StepDepth.Out;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        internal static StepSize MapSize(EngineStepSize value)
        {
            if (value == EngineStepSize.Line)
                return StepSize.Line;
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal static StepFilter MapFilter(EngineStepFilter value)
        {
            var result = StepFilter.None;
            if ((value & EngineStepFilter.StaticConstructor) != 0)
                result |= StepFilter.StaticCtor;
            if ((value & EngineStepFilter.DebuggerHidden) != 0)
                result |= StepFilter.DebuggerHidden;
            if ((value & EngineStepFilter.DebuggerStepThrough) != 0)
                result |= StepFilter.DebuggerStepThrough;
            if ((value & EngineStepFilter.DebuggerNonUserCode) != 0)
                result |= StepFilter.DebuggerNonUserCode;
            return result;
        }

        private sealed class MonoStepRequest : IStepRequest
        {
            private readonly StepEventRequest request;

            public MonoStepRequest(StepEventRequest request)
            {
                this.request = request;
            }

            public void Enable()
            {
                request.Enable();
            }

            public void Disable()
            {
                request.Disable();
            }
        }
    }
}
