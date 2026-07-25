using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Dap;

namespace UnityDebugger.Adapter.Tests.Dap
{
    internal static class DapTestProtocol
    {
        public static JObject Request(string command, object arguments) =>
            JObject.FromObject(
                new
                {
                    seq = 1,
                    type = "request",
                    command,
                    arguments,
                });

        public static JObject Response(
            IReadOnlyList<JObject> messages,
            string command) =>
            Responses(messages, command).Single();

        public static IReadOnlyList<JObject> Responses(
            IReadOnlyList<JObject> messages,
            string command) =>
            messages.Where(
                item =>
                    item["type"]?.Value<string>() == "response" &&
                    item["command"]?.Value<string>() == command)
                .ToArray();

        public static T Required<T>(JToken? token)
        {
            if (token == null)
                throw new InvalidDataException("Required DAP field missing.");
            return token.Value<T>()!;
        }

        public static IReadOnlyList<JObject> Run(
            UnityDebugSession session,
            params JObject[] requests)
        {
            using (var input = new MemoryStream())
            using (var output = new MemoryStream())
            {
                foreach (var request in requests)
                {
                    var json = request.ToString(Formatting.None);
                    var body = Encoding.UTF8.GetBytes(json);
                    var header = Encoding.ASCII.GetBytes(
                        $"Content-Length: {body.Length}\r\n\r\n");
                    input.Write(header, 0, header.Length);
                    input.Write(body, 0, body.Length);
                }
                input.Position = 0;
                session.Start(input, output).GetAwaiter().GetResult();
                return ParseMessages(output.ToArray());
            }
        }

        internal static IReadOnlyList<JObject> ParseMessages(byte[] bytes)
        {
            var messages = new List<JObject>();
            var offset = 0;
            while (offset < bytes.Length)
            {
                var headerEnd = FindHeaderEnd(bytes, offset);
                var header = Encoding.ASCII.GetString(
                    bytes,
                    offset,
                    headerEnd - offset);
                var length = int.Parse(
                    header.Substring("Content-Length: ".Length));
                var bodyStart = headerEnd + 4;
                messages.Add(
                    JObject.Parse(
                        Encoding.UTF8.GetString(
                            bytes,
                            bodyStart,
                            length)));
                offset = bodyStart + length;
            }
            return messages;
        }

        internal sealed class Recorder : System.IDisposable
        {
            private readonly UnityDebugSession session;
            private readonly MemoryStream output = new MemoryStream();
            private int capturedLength;

            public Recorder(UnityDebugSession session)
            {
                this.session = session;
            }

            public IReadOnlyList<JObject> Send(
                params JObject[] requests)
            {
                using (var input = new MemoryStream())
                {
                    foreach (var request in requests)
                    {
                        var json = request.ToString(Formatting.None);
                        var body = Encoding.UTF8.GetBytes(json);
                        var header = Encoding.ASCII.GetBytes(
                            $"Content-Length: {body.Length}\r\n\r\n");
                        input.Write(header, 0, header.Length);
                        input.Write(body, 0, body.Length);
                    }
                    input.Position = 0;
                    session.Start(input, output).GetAwaiter().GetResult();
                }
                return Capture();
            }

            public IReadOnlyList<JObject> Capture()
            {
                var all = output.ToArray();
                var added = new byte[all.Length - capturedLength];
                System.Buffer.BlockCopy(
                    all,
                    capturedLength,
                    added,
                    0,
                    added.Length);
                capturedLength = all.Length;
                return ParseMessages(added);
            }

            public void Dispose()
            {
                output.Dispose();
            }
        }

        private static int FindHeaderEnd(byte[] bytes, int start)
        {
            for (var index = start; index <= bytes.Length - 4; index++)
            {
                if (
                    bytes[index] == '\r' &&
                    bytes[index + 1] == '\n' &&
                    bytes[index + 2] == '\r' &&
                    bytes[index + 3] == '\n')
                {
                    return index;
                }
            }
            throw new InvalidDataException("DAP header terminator missing.");
        }
    }
}
