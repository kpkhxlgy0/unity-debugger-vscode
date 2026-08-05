using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Evaluation.Values
{
    internal interface IUnityEvaluationContext
    {
        bool IsMainThread { get; }
        FrameVariable? ActiveScene { get; }
        FrameVariable? ThisGameObject { get; }
        Task<IReadOnlyList<DebugProperty>> GetComponentsAsync(
            IRuntimeValue gameObject,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
            IRuntimeValue gameObject,
            CancellationToken cancellationToken);
        Task<IRuntimeValue> GetScenePathAsync(
            IRuntimeValue gameObject,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<DebugProperty>> GetRootGameObjectsAsync(
            IRuntimeValue scene,
            CancellationToken cancellationToken);
    }

    internal sealed class UnityValueProviders
    {
        private readonly IUnityEvaluationContext context;

        public UnityValueProviders(IUnityEvaluationContext context)
        {
            this.context = context ??
                throw new ArgumentNullException(nameof(context));
        }

        public Task<IReadOnlyList<DebugProperty>?> TryGetChildrenAsync(
            IRuntimeValue value,
            CancellationToken cancellationToken)
        {
            if (!context.IsMainThread)
            {
                return Task.FromResult<IReadOnlyList<DebugProperty>?>(null);
            }

            switch (value.Type.FullName)
            {
                case "UnityEngine.GameObject":
                    return Task.FromResult<IReadOnlyList<DebugProperty>?>(
                        GameObjectChildren(value));
                case "UnityEngine.SceneManagement.Scene":
                    return Task.FromResult<IReadOnlyList<DebugProperty>?>(
                        SceneChildren(value));
                default:
                    return Task.FromResult<IReadOnlyList<DebugProperty>?>(null);
            }
        }

        private IReadOnlyList<DebugProperty> GameObjectChildren(
            IRuntimeValue value) =>
            new DebugProperty[]
            {
                new ComputedProperty(
                    "Components",
                    value.Type.FullName,
                    cancellationToken => Task.FromResult(value),
                    cancellationToken => context.GetComponentsAsync(
                        value,
                        cancellationToken)),
                new ComputedProperty(
                    "Children",
                    value.Type.FullName,
                    cancellationToken => Task.FromResult(value),
                    cancellationToken => context.GetChildrenAsync(
                        value,
                        cancellationToken)),
                new ComputedProperty(
                    "Scene path",
                    "System.String",
                    cancellationToken => context.GetScenePathAsync(
                        value,
                        cancellationToken)),
            };

        private IReadOnlyList<DebugProperty> SceneChildren(
            IRuntimeValue value) =>
            new DebugProperty[]
            {
                new ComputedProperty(
                    "Game objects",
                    value.Type.FullName,
                    cancellationToken => Task.FromResult(value),
                    cancellationToken => context.GetRootGameObjectsAsync(
                        value,
                        cancellationToken)),
            };
    }
}
