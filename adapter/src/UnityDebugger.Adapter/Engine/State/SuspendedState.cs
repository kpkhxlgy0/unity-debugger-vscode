using System.Collections.Concurrent;
using System.Threading;

namespace UnityDebugger.Adapter.Engine.State
{
    internal sealed class SuspendedState
    {
        private readonly ObjectMap frames = new ObjectMap();
        private readonly ObjectMap properties = new ObjectMap();
        private readonly ObjectMap codePaths = new ObjectMap();
        private readonly ObjectMap codeContexts = new ObjectMap();
        private int generation;

        public int Generation => Volatile.Read(ref generation);

        public int RegisterFrame(object value) => frames.Register(value);

        public bool TryGetFrame<T>(int id, out T value) =>
            frames.TryGet(id, out value);

        public int RegisterProperty(object value) =>
            properties.Register(value);

        public bool TryGetProperty<T>(int id, out T value) =>
            properties.TryGet(id, out value);

        public int RegisterCodePath(object value) =>
            codePaths.Register(value);

        public bool TryGetCodePath<T>(int id, out T value) =>
            codePaths.TryGet(id, out value);

        public int RegisterCodeContext(object value) =>
            codeContexts.Register(value);

        public bool TryGetCodeContext<T>(int id, out T value) =>
            codeContexts.TryGet(id, out value);

        public void Reset()
        {
            Interlocked.Increment(ref generation);
            frames.Reset();
            properties.Reset();
            codePaths.Reset();
            codeContexts.Reset();
        }

        private sealed class ObjectMap
        {
            private readonly ConcurrentDictionary<int, object> values =
                new ConcurrentDictionary<int, object>();
            private int nextId;

            public int Register(object value)
            {
                var id = Interlocked.Increment(ref nextId);
                if (!values.TryAdd(id, value))
                    throw new System.InvalidOperationException(
                        "Suspended handle registration failed.");
                return id;
            }

            public bool TryGet<T>(int id, out T value)
            {
                if (
                    values.TryGetValue(id, out var found) &&
                    found is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                value = default!;
                return false;
            }

            public void Reset()
            {
                values.Clear();
                Interlocked.Exchange(ref nextId, 0);
            }
        }
    }
}
