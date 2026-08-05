using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodeDebug;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ProtocolWriterTests
    {
        [Fact]
        public async Task ConcurrentEventsRemainIntactAndSequenced()
        {
            var server = new WriterProtocolServer();
            using (var input = new MemoryStream())
            using (var output = new MemoryStream())
            {
                await server.Start(input, output);

                Parallel.For(
                    0,
                    256,
                    index => server.Emit(index));

                var messages = DapTestProtocol.ParseMessages(
                    output.ToArray());
                Assert.Equal(256, messages.Count);
                Assert.Equal(
                    Enumerable.Range(0, 256),
                    messages
                        .Select(
                            item => item["body"]!["index"]!.Value<int>())
                        .OrderBy(value => value));
                Assert.Equal(
                    256,
                    messages
                        .Select(item => item["seq"]!.Value<int>())
                        .Distinct()
                        .Count());
            }
        }

        private sealed class WriterProtocolServer : ProtocolServer
        {
            public void Emit(int index)
            {
                SendEvent(
                    new Event(
                        "writer-test",
                        new { index }));
            }

            protected override void DispatchRequest(
                string command,
                dynamic args,
                Response response)
            {
            }
        }
    }
}
