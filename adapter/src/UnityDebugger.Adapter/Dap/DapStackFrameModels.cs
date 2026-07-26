using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class DapStackFrame
    {
        public DapStackFrame(
            int id,
            string name,
            DapSource? source,
            int line,
            int column,
            string presentationHint)
        {
            this.id = id;
            this.name = name;
            this.source = source;
            this.line = line;
            this.column = column;
            this.presentationHint = presentationHint;
        }

        public int id { get; }
        public string name { get; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DapSource? source { get; }

        public int line { get; }
        public int column { get; }
        public string presentationHint { get; }
    }

    internal sealed class DapStackTraceResponseBody : ResponseBody
    {
        public DapStackTraceResponseBody(
            IEnumerable<DapStackFrame> stackFrames,
            int totalFrames)
        {
            this.stackFrames = stackFrames.ToArray();
            this.totalFrames = totalFrames;
        }

        public DapStackFrame[] stackFrames { get; }
        public int totalFrames { get; }
    }
}
