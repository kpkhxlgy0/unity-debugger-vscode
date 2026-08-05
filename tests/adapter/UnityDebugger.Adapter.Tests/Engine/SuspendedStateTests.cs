using UnityDebugger.Adapter.Engine.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine
{
    public sealed class SuspendedStateTests
    {
        [Fact]
        public void RegisteredHandlesRemainAvailableUntilReset()
        {
            var state = new SuspendedState();
            var frame = new object();
            var id = state.RegisterFrame(frame);

            Assert.True(state.TryGetFrame<object>(id, out var mapped));
            Assert.Same(frame, mapped);
        }

        [Fact]
        public void NextStopResetsEveryHandleKindAndRestartsIds()
        {
            var state = new SuspendedState();
            var oldFrame = state.RegisterFrame(new object());
            var oldProperty = state.RegisterProperty(new object());
            var oldCodePath = state.RegisterCodePath(new object());
            var oldCodeContext = state.RegisterCodeContext(new object());

            state.Reset();

            Assert.False(state.TryGetFrame<object>(oldFrame, out _));
            Assert.False(state.TryGetProperty<object>(oldProperty, out _));
            Assert.False(state.TryGetCodePath<object>(oldCodePath, out _));
            Assert.False(state.TryGetCodeContext<object>(oldCodeContext, out _));
            Assert.Equal(1, state.RegisterFrame(new object()));
            Assert.Equal(1, state.RegisterProperty(new object()));
            Assert.Equal(1, state.RegisterCodePath(new object()));
            Assert.Equal(1, state.RegisterCodeContext(new object()));
        }

        [Fact]
        public void HandleKindsUseIndependentIdSpaces()
        {
            var state = new SuspendedState();

            Assert.Equal(1, state.RegisterFrame(new object()));
            Assert.Equal(1, state.RegisterProperty(new object()));
            Assert.Equal(1, state.RegisterCodePath(new object()));
            Assert.Equal(1, state.RegisterCodeContext(new object()));
        }

        [Fact]
        public void TypeMismatchBehavesLikeAMissingHandle()
        {
            var state = new SuspendedState();
            var frameId = state.RegisterFrame("frame");

            Assert.False(state.TryGetFrame<object[]>(frameId, out _));
        }
    }
}
