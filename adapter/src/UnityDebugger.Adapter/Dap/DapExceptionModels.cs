using System;
using Newtonsoft.Json;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class DapExceptionDetails
    {
        public DapExceptionDetails(Backend.BackendExceptionInfo value)
        {
            message = value.Description;
            typeName = value.ExceptionId;
            fullTypeName = value.ExceptionId;
            stackTrace = value.StackTrace;
        }

        public string message { get; }
        public string typeName { get; }
        public string fullTypeName { get; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? stackTrace { get; }

        public object[] innerException { get; } = Array.Empty<object>();
    }

    internal sealed class DapExceptionInfoResponseBody : ResponseBody
    {
        public DapExceptionInfoResponseBody(
            Backend.BackendExceptionInfo value)
        {
            exceptionId = value.ExceptionId;
            description = value.Description;
            breakMode = value.BreakMode;
            details = new DapExceptionDetails(value);
        }

        public string exceptionId { get; }
        public string description { get; }
        public string breakMode { get; }
        public DapExceptionDetails details { get; }
    }
}
