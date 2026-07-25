using System;

namespace VSCodeDebug
{
    public static class ProtocolTrace
    {
        public static Action<string> Sink { get; set; } = _ => { };

        public static void CommandReceived(string command)
        {
            Sink(command);
        }
    }
}
