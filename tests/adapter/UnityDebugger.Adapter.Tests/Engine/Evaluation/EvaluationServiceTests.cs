using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Evaluation
{
    public sealed class EvaluationServiceTests
    {
        [Fact]
        public void LocalsStayLazyAndEnumComparisonEvaluatesAsTypedValue()
        {
            var enumType = FakeRuntimeType.Enum(
                "ButtonState",
                ("Normal", 0),
                ("Selected", 1));
            var currentState = enumType.EnumValue("Normal");
            var environment = new FrameEnvironment(
                Frame(
                    new FrameVariable(
                        "currentState",
                        currentState)))
                .WithValue("currentState", currentState)
                .WithType(enumType);
            var service = CreateService(environment, out var frameId);

            var scopes = service.GetScopes(
                frameId,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None);

            Assert.Equal(0, environment.FrameReadCount);
            var scope = Assert.Single(scopes);
            Assert.Equal("Locals", scope.Name);

            var variables = service.GetVariables(
                scope.VariablesReference,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None);
            var variable = Assert.Single(variables);
            Assert.Equal("currentState", variable.Name);
            Assert.Equal("Normal", variable.DisplayValue);
            Assert.Equal(1, environment.FrameReadCount);

            var result = service.Evaluate(
                frameId,
                "currentState == ButtonState.Normal",
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("true", result!.DisplayValue);
            Assert.Equal("System.Boolean", result.TypeName);
        }

        [Fact]
        public void GetterTimeoutReturnsAnEmptyVariableCollection()
        {
            var intType = PrimitiveType("Int32", "System.Int32");
            var getter = Getter("get_Value", intType);
            var playerType = new FakeRuntimeType("Player")
            {
                Properties = new[]
                {
                    new RuntimeProperty(
                        "Value",
                        intType,
                        getter,
                        null,
                        new object()),
                },
            };
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);
            player.InvokeHandler = async (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return Primitive(intType, 1);
            };
            var environment = new FrameEnvironment(
                Frame(new FrameVariable("player", player)))
                .WithValue("player", player);
            var service = CreateService(environment, out var frameId);
            var scope = Assert.Single(service.GetScopes(
                frameId,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));
            var playerVariable = Assert.Single(service.GetVariables(
                scope.VariablesReference,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));

            var children = service.GetVariables(
                playerVariable.VariablesReference,
                BackendEvaluationMode.Explicit,
                1,
                CancellationToken.None);

            Assert.Empty(children);
        }

        [Fact]
        public void SafeVariablesShowGetterWithoutInvokingIt()
        {
            var intType = PrimitiveType("Int32", "System.Int32");
            var getter = Getter("get_Health", intType);
            var playerType = new FakeRuntimeType("Player")
            {
                Properties = new[]
                {
                    new RuntimeProperty(
                        "Health",
                        intType,
                        getter,
                        null,
                        new object()),
                },
            };
            var invocationCount = 0;
            var player = new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Object)
                .WithType(playerType);
            player.InvokeHandler = (
                method,
                arguments,
                options,
                cancellationToken) =>
            {
                invocationCount++;
                return Task.FromResult<IRuntimeValue>(
                    Primitive(intType, 42));
            };
            var environment = new FrameEnvironment(
                Frame(new FrameVariable("player", player)))
                .WithValue("player", player);
            var service = CreateService(environment, out var frameId);
            var scope = Assert.Single(service.GetScopes(
                frameId,
                BackendEvaluationMode.Safe,
                10000,
                CancellationToken.None));
            var playerVariable = Assert.Single(service.GetVariables(
                scope.VariablesReference,
                BackendEvaluationMode.Safe,
                10000,
                CancellationToken.None));

            var children = service.GetVariables(
                playerVariable.VariablesReference,
                BackendEvaluationMode.Safe,
                10000,
                CancellationToken.None);

            var health = Assert.Single(children);
            Assert.Equal("Health", health.Name);
            Assert.Equal("{get;}", health.DisplayValue);
            Assert.Equal(0, invocationCount);
        }

        [Fact]
        public void SetVariableEvaluatesAndWritesInTheOwningFrame()
        {
            IRuntimeValue? assigned = null;
            var original = Primitive(
                PrimitiveType("Int32", "System.Int32"),
                42);
            var variable = new FrameVariable(
                "health",
                original,
                (value, cancellationToken) =>
                {
                    assigned = value;
                    return Task.CompletedTask;
                });
            var environment = new FrameEnvironment(Frame(variable))
                .WithValue("health", original);
            var service = CreateService(environment, out var frameId);
            var scope = Assert.Single(service.GetScopes(
                frameId,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));

            var result = service.SetVariable(
                scope.VariablesReference,
                "health",
                "43",
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("43", result!.DisplayValue);
            Assert.NotNull(assigned);
            Assert.Equal(43, assigned!.Primitive);
        }

        [Fact]
        public void MissingHandlesReturnEmptyReferenceResults()
        {
            var service = new EvaluationService(new SuspendedState());

            Assert.Empty(service.GetScopes(
                404,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));
            Assert.Empty(service.GetVariables(
                404,
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));
            Assert.Null(service.Evaluate(
                404,
                "value",
                BackendEvaluationMode.Explicit,
                10000,
                CancellationToken.None));
        }

        private static EvaluationService CreateService(
            FrameEnvironment environment,
            out long frameId)
        {
            var service = new EvaluationService(new SuspendedState());
            var registered = service.RegisterFrame(
                new BackendStackFrame(
                    99,
                    1,
                    "Player.Update()",
                    @"H:\fixture\Player.cs",
                    10,
                    1),
                environment,
                null);
            frameId = registered.Id;
            return service;
        }

        private static FrameValues Frame(params FrameVariable[] locals) =>
            new FrameValues(
                null,
                locals,
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                null);

        private static FakeRuntimeType PrimitiveType(
            string name,
            string fullName) =>
            new FakeRuntimeType(name, fullName)
            {
                IsPrimitive = true,
                IsValueType = true,
            };

        private static FakeRuntimeValue Primitive(
            IRuntimeType type,
            object value) =>
            new FakeRuntimeValue()
                .WithKind(RuntimeValueKind.Primitive)
                .WithType(type)
                .WithPrimitive(value);

        private static RuntimeMethod Getter(
            string name,
            IRuntimeType returnType) =>
            new RuntimeMethod(
                name,
                returnType,
                Array.Empty<RuntimeParameter>(),
                isStatic: false,
                isPublic: true,
                isVirtual: false,
                isSpecialName: true,
                source: new object());

        private sealed class FrameEnvironment : IFrameEvaluationEnvironment
        {
            private readonly FrameValues frame;
            private readonly Dictionary<string, IRuntimeValue> values =
                new Dictionary<string, IRuntimeValue>();
            private readonly Dictionary<string, IRuntimeType> types =
                new Dictionary<string, IRuntimeType>();

            public FrameEnvironment(FrameValues frame)
            {
                this.frame = frame;
            }

            public int FrameReadCount { get; private set; }

            public FrameEnvironment WithValue(
                string name,
                IRuntimeValue value)
            {
                values.Add(name, value);
                return this;
            }

            public FrameEnvironment WithType(IRuntimeType type)
            {
                types[type.Name] = type;
                types[type.FullName] = type;
                return this;
            }

            public FrameValues GetFrameValues()
            {
                FrameReadCount++;
                return frame;
            }

            public bool TryGetValue(string name, out IRuntimeValue value) =>
                values.TryGetValue(name, out value!);

            public bool TryGetType(
                string fullOrSimpleName,
                out IRuntimeType type) =>
                types.TryGetValue(fullOrSimpleName, out type!);
        }
    }
}
