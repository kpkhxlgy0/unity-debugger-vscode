using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Engine.Source;

namespace UnityDebugger.Adapter.Engine.State
{
    internal sealed class UnityModuleState
    {
        private readonly object sync = new object();
        private readonly HashSet<object> loadedTypes =
            new HashSet<object>();

        public UnityModuleState(
            UnityDomainState domain,
            RuntimeModuleDescriptor descriptor)
        {
            Domain = domain ??
                throw new ArgumentNullException(nameof(domain));
            Descriptor = descriptor ??
                throw new ArgumentNullException(nameof(descriptor));
        }

        public UnityDomainState Domain { get; }
        public RuntimeModuleDescriptor Descriptor { get; }
        public string Id => Descriptor.Id;
        public string Name => Descriptor.Name;
        public string Path => Descriptor.Path;
        public bool HasSymbols { get; private set; }

        public bool AddType(object runtimeIdentity, bool hasSymbols)
        {
            lock (sync)
            {
                var added = loadedTypes.Add(runtimeIdentity);
                if (hasSymbols)
                    HasSymbols = true;
                return added;
            }
        }
    }
}
