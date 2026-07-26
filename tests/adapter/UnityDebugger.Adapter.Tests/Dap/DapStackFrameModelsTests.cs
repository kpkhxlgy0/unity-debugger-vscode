using UnityDebugger.Adapter.Dap;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class DapStackFrameModelsTests
    {
        [Fact]
        public void Stack_frame_coordinates_are_never_negative()
        {
            var frame = new DapStackFrame(
                1,
                "Unavailable",
                null,
                -5,
                -7,
                "subtle");

            Assert.Equal(0, frame.line);
            Assert.Equal(0, frame.column);
        }
    }
}
