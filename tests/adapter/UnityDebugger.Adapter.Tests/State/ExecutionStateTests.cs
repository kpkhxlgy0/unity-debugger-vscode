using System;
using UnityDebugger.Adapter.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.State
{
    public sealed class ExecutionStateTests
    {
        [Fact]
        public void Supports_the_normal_attach_stop_continue_lifecycle()
        {
            var state = new ExecutionState();

            Assert.Equal(ExecutionStatus.Disconnected, state.Status);
            state.Attached();
            Assert.Equal(ExecutionStatus.Running, state.Status);
            state.Stopped();
            Assert.Equal(ExecutionStatus.Stopped, state.Status);
            state.RequireStopped("Step");
            state.Continued();
            Assert.Equal(ExecutionStatus.Running, state.Status);
            state.Disconnected();
            Assert.Equal(ExecutionStatus.Disconnected, state.Status);
        }

        [Fact]
        public void RequireStopped_rejects_running_and_disconnected_states()
        {
            var state = new ExecutionState();
            Assert.Throws<InvalidOperationException>(
                () => state.RequireStopped("Step"));

            state.Attached();

            var error = Assert.Throws<InvalidOperationException>(
                () => state.RequireStopped("Step"));
            Assert.Equal(
                "Step requires a stopped target.",
                error.Message);
        }

        [Fact]
        public void Invalid_transitions_are_rejected()
        {
            var state = new ExecutionState();
            Assert.Throws<InvalidOperationException>(() => state.Stopped());
            state.Attached();
            Assert.Throws<InvalidOperationException>(() => state.Attached());
            Assert.Throws<InvalidOperationException>(() => state.Continued());
        }
    }
}
