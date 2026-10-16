using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    // The vendored VSCodeDebug.Capabilities shape cannot express the reference
    // debugger's observed initialize body: it always emits the legacy
    // supportsExceptionOptions flag, and its ExceptionBreakpointsFilter has no
    // description, supportsCondition, or conditionDescription. This adapter-owned
    // body carries the observed capability surface instead of editing vendored
    // source.
    internal sealed class DapCapabilities : ResponseBody
    {
        public DapCapabilities(
            int processId,
            IEnumerable<DapExceptionBreakpointsFilter> filters)
        {
            pid = processId;
            exceptionBreakpointFilters = filters.ToArray();
        }

        public bool supportsConfigurationDoneRequest { get; } = false;
        public bool supportsFunctionBreakpoints { get; } = true;
        public bool supportsConditionalBreakpoints { get; } = true;
        public bool supportsHitConditionalBreakpoints { get; } = false;
        public bool supportsEvaluateForHovers { get; } = true;
        public bool supportsStepBack { get; } = false;
        public bool supportsSetVariable { get; } = true;
        public bool supportsGotoTargetsRequest { get; } = true;
        public bool supportsStepInTargetsRequest { get; } = true;
        public bool supportsRestartRequest { get; } = false;
        public bool supportsLogPoints { get; } = true;
        public bool supportsTerminateRequest { get; } = true;
        public bool supportsDataBreakpoints { get; } = false;
        public bool supportsReadMemoryRequest { get; } = false;
        public bool supportsWriteMemoryRequest { get; } = false;
        public bool supportsDisassembleRequest { get; } = false;
        public bool supportsSteppingGranularity { get; } = false;
        public bool supportsInstructionBreakpoints { get; } = false;
        public bool supportsExceptionFilterOptions { get; } = true;
        public bool supportsSingleThreadExecutionRequests { get; } = false;
        public bool supportsExceptionConditions { get; } = true;
        public DapExceptionBreakpointsFilter[] exceptionBreakpointFilters { get; }
        public int pid { get; }
    }

    internal sealed class DapExceptionBreakpointsFilter
    {
        public DapExceptionBreakpointsFilter(
            string filter,
            string label,
            string description,
            bool defaultValue,
            bool supportsCondition,
            string conditionDescription)
        {
            this.filter = filter;
            this.label = label;
            this.description = description;
            this.defaultValue = defaultValue;
            this.supportsCondition = supportsCondition;
            this.conditionDescription = conditionDescription;
        }

        public string filter { get; }
        public string label { get; }
        public string description { get; }

        [JsonProperty("default")]
        public bool defaultValue { get; }

        public bool supportsCondition { get; }
        public string conditionDescription { get; }
    }
}
