using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;

namespace UnityDebugger.Adapter.Engine.Evaluation.Properties
{
    internal sealed class FrameProperty : DebugProperty
    {
        private readonly IFrameEvaluationEnvironment environment;
        private readonly IUnityEvaluationContext? unityContext;

        public FrameProperty(
            IFrameEvaluationEnvironment environment,
            IUnityEvaluationContext? unityContext = null)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
            this.unityContext = unityContext;
        }

        public override string Name => "Locals";
        public override string TypeName => string.Empty;

        public override Task<IRuntimeValue> GetValueAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The Locals root does not represent a target value.");

        public override Task SetValueAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken) =>
            throw ReadOnly(Name);

        public override Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = environment.GetFrameValues();
            var children = new List<DebugProperty>();
            Add(children, frame.CurrentException);
            if (unityContext?.IsMainThread == true)
                Add(children, unityContext.ActiveScene);
            Add(children, frame.ThisValue);
            if (unityContext?.IsMainThread == true)
                Add(children, unityContext.ThisGameObject);
            Add(children, frame.Locals);
            Add(children, frame.Constants);
            Add(children, frame.ClosureCaptures);
            Add(children, frame.HoistedValues);
            Add(children, frame.Arguments);
            return Task.FromResult<IReadOnlyList<DebugProperty>>(children);
        }

        private static void Add(
            ICollection<DebugProperty> children,
            FrameVariable? variable)
        {
            if (variable != null)
                children.Add(CreateProperty(variable));
        }

        private static void Add(
            ICollection<DebugProperty> children,
            IReadOnlyList<FrameVariable> variables)
        {
            foreach (var variable in variables)
                children.Add(CreateProperty(variable));
        }

        private static ValueProperty CreateProperty(FrameVariable variable) =>
            new ValueProperty(
                variable.Name,
                variable.Value,
                variable.CanSet ? variable.SetValueAsync : null);
    }
}
