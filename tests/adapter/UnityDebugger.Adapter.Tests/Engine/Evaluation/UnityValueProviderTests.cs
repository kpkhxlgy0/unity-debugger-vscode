using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class UnityValueProviderTests
    {
        [Fact]
        public async Task MainThreadFrameAddsActiveSceneAndThisGameObject()
        {
            var context = new FakeUnityContext
            {
                IsMainThread = true,
                ActiveScene = Variable("Active scene"),
                ThisGameObject = Variable("this.gameObject"),
            };
            var frame = new FrameValues(
                Variable("this"),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                null);

            var children = await new FrameProperty(
                    new FrameEnvironment(frame),
                    context)
                .GetChildrenAsync(CancellationToken.None);

            Assert.Equal(
                new[] { "Active scene", "this", "this.gameObject" },
                children.Select(value => value.Name));
        }

        [Fact]
        public async Task WorkerThreadNeverRequestsUnitySyntheticValues()
        {
            var context = new FakeUnityContext
            {
                IsMainThread = false,
                ActiveScene = Variable("must-not-appear"),
                ThisGameObject = Variable("must-not-appear"),
            };
            var frame = new FrameValues(
                Variable("this"),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                null);

            var children = await new FrameProperty(
                    new FrameEnvironment(frame),
                    context)
                .GetChildrenAsync(CancellationToken.None);

            Assert.Equal(new[] { "this" }, children.Select(value => value.Name));
        }

        [Fact]
        public async Task GameObjectUsesReferenceSyntheticChildOrder()
        {
            var context = new FakeUnityContext { IsMainThread = true };
            var gameObject = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(new FakeRuntimeType(
                    "GameObject",
                    "UnityEngine.GameObject"));

            var children = await new ReferenceValueProviders(context)
                .GetChildrenAsync(
                    gameObject,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal(
                new[] { "Components", "Children", "Scene path" },
                children.Take(3).Select(value => value.Name));
        }

        [Fact]
        public async Task SceneUsesGameObjectsSyntheticPropertyOnMainThread()
        {
            var context = new FakeUnityContext { IsMainThread = true };
            var scene = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Struct)
                .WithType(new FakeRuntimeType(
                    "Scene",
                    "UnityEngine.SceneManagement.Scene"));

            var children = await new ReferenceValueProviders(context)
                .GetChildrenAsync(
                    scene,
                    EvaluationPolicy.Explicit,
                    CancellationToken.None);

            Assert.Equal("Game objects", children[0].Name);
        }

        private static FrameVariable Variable(string name) =>
            new FrameVariable(name, Primitive(1));

        private static FakeRuntimeValue Primitive(object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(new FakeRuntimeType(
                    value.GetType().Name,
                    value.GetType().FullName))
                .WithPrimitive(value);

        private sealed class FrameEnvironment : IFrameEvaluationEnvironment
        {
            private readonly FrameValues frame;

            public FrameEnvironment(FrameValues frame)
            {
                this.frame = frame;
            }

            public FrameValues GetFrameValues() => frame;

            public bool TryGetValue(string name, out IRuntimeValue value)
            {
                value = null!;
                return false;
            }

            public bool TryGetType(
                string fullOrSimpleName,
                out IRuntimeType type)
            {
                type = null!;
                return false;
            }
        }

        private sealed class FakeUnityContext : IUnityEvaluationContext
        {
            public bool IsMainThread { get; set; }
            public FrameVariable? ActiveScene { get; set; }
            public FrameVariable? ThisGameObject { get; set; }

            public Task<IReadOnlyList<DebugProperty>> GetComponentsAsync(
                IRuntimeValue gameObject,
                CancellationToken cancellationToken) =>
                Empty();

            public Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(
                IRuntimeValue gameObject,
                CancellationToken cancellationToken) =>
                Empty();

            public Task<IRuntimeValue> GetScenePathAsync(
                IRuntimeValue gameObject,
                CancellationToken cancellationToken) =>
                Task.FromResult<IRuntimeValue>(Primitive("Root/Player"));

            public Task<IReadOnlyList<DebugProperty>> GetRootGameObjectsAsync(
                IRuntimeValue scene,
                CancellationToken cancellationToken) =>
                Empty();

            private static Task<IReadOnlyList<DebugProperty>> Empty() =>
                Task.FromResult<IReadOnlyList<DebugProperty>>(
                    Array.Empty<DebugProperty>());
        }
    }
}
