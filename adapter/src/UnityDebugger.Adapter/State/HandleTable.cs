using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.State
{
    internal sealed class HandleTable<T>
    {
        private readonly Dictionary<int, T> values =
            new Dictionary<int, T>();
        private int next = 1;

        public int Create(T value)
        {
            if (next == int.MaxValue)
                throw new InvalidOperationException(
                    "DAP handle space is exhausted.");
            var handle = next++;
            values.Add(handle, value);
            return handle;
        }

        public bool TryGet(int handle, out T value)
        {
            if (values.TryGetValue(handle, out var found))
            {
                value = found;
                return true;
            }

            value = default!;
            return false;
        }

        public void Reset()
        {
            values.Clear();
            next = 1;
        }
    }
}
