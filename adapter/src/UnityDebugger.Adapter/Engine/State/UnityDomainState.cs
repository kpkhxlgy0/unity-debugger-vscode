using System;
using System.Collections.Generic;
using System.Linq;
using UnityDebugger.Adapter.Engine.Source;

namespace UnityDebugger.Adapter.Engine.State
{
    internal sealed class UnityDomainState
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, UnityModuleState> modules =
            new Dictionary<string, UnityModuleState>(StringComparer.Ordinal);
        private readonly List<UnityModuleState> moduleOrder =
            new List<UnityModuleState>();
        private readonly HashSet<object> boundBreakpoints =
            new HashSet<object>();

        public UnityDomainState(object identity, string name)
        {
            Identity = identity ??
                throw new ArgumentNullException(nameof(identity));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public object Identity { get; }
        public string Name { get; }

        public IReadOnlyList<UnityModuleState> Modules
        {
            get
            {
                lock (sync)
                    return moduleOrder.ToArray();
            }
        }

        public IReadOnlyList<object> BoundBreakpoints
        {
            get
            {
                lock (sync)
                    return boundBreakpoints.ToArray();
            }
        }

        public UnityModuleState GetOrAddModule(
            RuntimeModuleDescriptor descriptor)
        {
            lock (sync)
            {
                if (modules.TryGetValue(descriptor.Id, out var existing))
                    return existing;
                var created = new UnityModuleState(this, descriptor);
                modules.Add(descriptor.Id, created);
                moduleOrder.Add(created);
                return created;
            }
        }

        public bool TryGetModule(string id, out UnityModuleState module)
        {
            lock (sync)
                return modules.TryGetValue(id, out module!);
        }

        public void AddBoundBreakpoint(object breakpoint)
        {
            lock (sync)
                boundBreakpoints.Add(breakpoint);
        }

        public bool RemoveBoundBreakpoint(object breakpoint)
        {
            lock (sync)
                return boundBreakpoints.Remove(breakpoint);
        }
    }
}
