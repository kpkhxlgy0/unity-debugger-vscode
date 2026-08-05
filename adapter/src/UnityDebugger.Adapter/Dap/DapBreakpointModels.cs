using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class DapSource
    {
        public DapSource(
            string name,
            string? path,
            int sourceReference)
        {
            this.name = name;
            this.path = path;
            this.sourceReference = sourceReference;
        }

        public string name { get; }
        public string? path { get; }
        public int sourceReference { get; }
    }

    internal sealed class DapBreakpoint
    {
        public DapBreakpoint(
            long id,
            bool verified,
            string? message,
            DapSource source,
            int line,
            int column)
        {
            this.id = id;
            this.verified = verified;
            this.message = message;
            this.source = source;
            this.line = line;
            this.column = column;
        }

        public long id { get; }
        public bool verified { get; }
        public string? message { get; }
        public DapSource source { get; }
        public int line { get; }
        public int column { get; }
    }

    internal sealed class DapSetBreakpointsResponseBody : ResponseBody
    {
        public DapSetBreakpointsResponseBody(
            IEnumerable<DapBreakpoint> breakpoints)
        {
            this.breakpoints = breakpoints.ToArray();
        }

        public DapBreakpoint[] breakpoints { get; }
    }

    internal sealed class DapStoppedEventBody
    {
        public DapStoppedEventBody(
            int threadId,
            string reason,
            string? text,
            long[]? hitBreakpointIds)
        {
            this.threadId = threadId;
            this.reason = reason;
            this.text = text;
            this.hitBreakpointIds = hitBreakpointIds;
        }

        public int threadId { get; }
        public string reason { get; }

        public string? text { get; }

        public bool allThreadsStopped => true;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long[]? hitBreakpointIds { get; }
    }
}
