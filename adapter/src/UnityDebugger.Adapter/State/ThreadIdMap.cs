using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.State
{
    internal sealed class ThreadIdMap
    {
        private readonly Dictionary<long, int> forward =
            new Dictionary<long, int>();
        private readonly Dictionary<int, long> reverse =
            new Dictionary<int, long>();
        private int next = 1;

        public int GetOrCreate(long backendId)
        {
            if (forward.TryGetValue(backendId, out var existing))
                return existing;
            if (next == int.MaxValue)
                throw new InvalidOperationException(
                    "DAP thread ID space is exhausted.");

            var dapId = next++;
            forward.Add(backendId, dapId);
            reverse.Add(dapId, backendId);
            return dapId;
        }

        public bool TryGetBackendId(int dapId, out long backendId) =>
            reverse.TryGetValue(dapId, out backendId);

        public bool TryGetDapId(long backendId, out int dapId) =>
            forward.TryGetValue(backendId, out dapId);

        public void Remove(long backendId)
        {
            if (!forward.TryGetValue(backendId, out var dapId))
                return;
            forward.Remove(backendId);
            reverse.Remove(dapId);
        }

        public void Reset()
        {
            forward.Clear();
            reverse.Clear();
            next = 1;
        }
    }
}
