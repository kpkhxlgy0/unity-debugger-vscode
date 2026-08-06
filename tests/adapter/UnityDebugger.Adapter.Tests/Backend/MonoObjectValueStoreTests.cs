using System;
using System.Threading;
using Mono.Debugging.Backend;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MonoObjectValueStoreTests
    {
        [Fact]
        public void MapsMatureObjectChildrenWithoutInterpretingTheirValues()
        {
            var enumValue = ObjectValue.CreatePrimitive(
                null,
                new ObjectPath("loadType"),
                "UnityEngine.RuntimeInitializeLoadType",
                new EvaluationResult("AfterSceneLoad"),
                ObjectValueFlags.Variable);
            var propertyError = ObjectValue.CreateError(
                null,
                new ObjectPath("Property"),
                "System.String",
                "Getter failed",
                ObjectValueFlags.Property);
            var objectValue = ObjectValue.CreateObject(
                null,
                new ObjectPath("value"),
                "Fixture",
                "Fixture",
                ObjectValueFlags.Variable,
                new[] { enumValue, propertyError });
            var store = new MonoObjectValueStore();

            var mappedObject = store.Map(objectValue);
            var mappedChildren = store.GetVariables(
                mappedObject.VariablesReference,
                EvaluationOptions.DefaultOptions,
                CancellationToken.None);

            Assert.True(mappedObject.VariablesReference > 0);
            Assert.Equal(
                "AfterSceneLoad",
                mappedChildren[0].DisplayValue);
            Assert.Equal(
                "UnityEngine.RuntimeInitializeLoadType",
                mappedChildren[0].TypeName);
            Assert.Equal("Getter failed", mappedChildren[1].DisplayValue);
        }

        [Fact]
        public void ClearInvalidatesHandlesWithoutReusingTheirIds()
        {
            var store = new MonoObjectValueStore();
            var first = store.Map(ObjectWithChild("first"));
            Assert.Single(
                store.GetVariables(
                    first.VariablesReference,
                    EvaluationOptions.DefaultOptions,
                    CancellationToken.None));

            store.Clear();

            Assert.Empty(
                store.GetVariables(
                    first.VariablesReference,
                    EvaluationOptions.DefaultOptions,
                    CancellationToken.None));
            var second = store.Map(ObjectWithChild("second"));
            Assert.True(
                second.VariablesReference > first.VariablesReference);
        }

        [Fact]
        public void FrameHandlesAreStopScopedAndMonotonic()
        {
            var store = new MonoObjectValueStore();
            var frame = new StackFrame(
                1,
                new SourceLocation(
                    "Fixture.Method",
                    "Fixture.cs",
                    12,
                    1,
                    12,
                    1),
                "C#");
            var first = store.RegisterFrame(frame);
            Assert.Same(frame, store.GetFrame(first));

            store.Clear();

            Assert.Throws<InvalidOperationException>(
                () =>
                {
                    store.GetFrame(first);
                });
            var second = store.RegisterFrame(frame);
            Assert.True(second > first);
        }

        [Fact]
        public void SetVariableUsesTheRetainedMatureObjectValue()
        {
            var source = new SettableValueSource();
            var child = ObjectValue.CreatePrimitive(
                source,
                new ObjectPath("health"),
                "System.Int32",
                new EvaluationResult("42"),
                ObjectValueFlags.Variable);
            var parent = ObjectValue.CreateObject(
                null,
                new ObjectPath("Locals"),
                string.Empty,
                string.Empty,
                ObjectValueFlags.Group,
                new[] { child });
            var store = new MonoObjectValueStore();
            var reference = store.Map(parent).VariablesReference;

            var result = store.SetVariable(
                reference,
                "health",
                "43",
                EvaluationOptions.DefaultOptions,
                CancellationToken.None);

            Assert.Equal("43", source.LastExpression);
            Assert.Equal("43", result.DisplayValue);
            Assert.Equal("System.Int32", result.TypeName);
        }

        private static ObjectValue ObjectWithChild(string value) =>
            ObjectValue.CreateObject(
                null,
                new ObjectPath("value"),
                "Fixture",
                "Fixture",
                ObjectValueFlags.Variable,
                new[]
                {
                    ObjectValue.CreatePrimitive(
                        null,
                        new ObjectPath("child"),
                        "System.String",
                        new EvaluationResult(value),
                        ObjectValueFlags.Field),
                });

        private sealed class SettableValueSource : IObjectValueSource
        {
            public string? LastExpression { get; private set; }

            public ObjectValue[] GetChildren(
                ObjectPath path,
                int index,
                int count,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public EvaluationResult SetValue(
                ObjectPath path,
                string value,
                EvaluationOptions options)
            {
                LastExpression = value;
                return new EvaluationResult(value);
            }

            public ObjectValue GetValue(
                ObjectPath path,
                EvaluationOptions options) => throw new NotSupportedException();

            public object GetRawValue(
                ObjectPath path,
                EvaluationOptions options) => throw new NotSupportedException();

            public void SetRawValue(
                ObjectPath path,
                object value,
                EvaluationOptions options) => throw new NotSupportedException();
        }
    }
}
