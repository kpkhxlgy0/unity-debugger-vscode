using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Source
{
    internal sealed class EngineSourceMapManager
    {
        private readonly object sync = new object();
        private readonly Dictionary<object, UnityDomainState> domains =
            new Dictionary<object, UnityDomainState>();
        private readonly List<UnityDomainState> domainOrder =
            new List<UnityDomainState>();
        private readonly Dictionary<string, List<EngineSourceLocation>> sources =
            new Dictionary<string, List<EngineSourceLocation>>(
                StringComparer.OrdinalIgnoreCase);

        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;

        public IReadOnlyList<UnityModuleState> Modules
        {
            get
            {
                lock (sync)
                {
                    return domainOrder
                        .SelectMany(domain => domain.Modules)
                        .ToArray();
                }
            }
        }

        public IReadOnlyList<EngineSourceLocation> GetLocations(
            string sourcePath,
            int line)
        {
            var normalized = NormalizePath(sourcePath);
            if (normalized == null)
                return Array.Empty<EngineSourceLocation>();
            lock (sync)
            {
                if (!sources.TryGetValue(normalized, out var values))
                    return Array.Empty<EngineSourceLocation>();
                return values
                    .Where(value => line <= 0 || value.Line == line)
                    .OrderBy(value => value.Line)
                    .ThenBy(value => value.Column)
                    .ToArray();
            }
        }

        public void ProcessTypeLoaded(IRuntimeType type)
        {
            if (!(type is IRuntimeSourceType sourceType))
                return;
            lock (sync)
                ProcessTypeLoaded(type, sourceType);
        }

        public void RemoveDomain(object domainIdentity)
        {
            List<UnityModuleState> removed;
            lock (sync)
            {
                if (!domains.TryGetValue(domainIdentity, out var domain))
                    return;
                domains.Remove(domainIdentity);
                domainOrder.Remove(domain);
                removed = domain.Modules.ToList();
                foreach (var pair in sources.ToArray())
                {
                    pair.Value.RemoveAll(
                        value => ReferenceEquals(value.Domain, domain));
                    if (pair.Value.Count == 0)
                        sources.Remove(pair.Key);
                }
            }

            foreach (var module in removed)
                RaiseModuleChanged(module, false);
        }

        public bool TryGetDomain(
            object domainIdentity,
            out UnityDomainState domain)
        {
            lock (sync)
                return domains.TryGetValue(domainIdentity, out domain!);
        }

        private void ProcessTypeLoaded(
            IRuntimeType type,
            IRuntimeSourceType sourceType)
        {
            var domain = GetOrAddDomain(sourceType.DomainIdentity);
            var createdModule = !domain.TryGetModule(
                sourceType.RuntimeModule.Id,
                out var module);
            if (createdModule)
            {
                module = domain.GetOrAddModule(sourceType.RuntimeModule);
                RaiseModuleChanged(module, true);
            }

            var validLocations = sourceType.SourceLocations
                .Select(location => new
                {
                    Location = location,
                    Path = NormalizePath(location.SourcePath),
                })
                .Where(value => value.Path != null && value.Location.Line > 0)
                .ToArray();
            var added = module.AddType(
                sourceType.RuntimeIdentity,
                validLocations.Length > 0);
            if (added)
            {
                foreach (var value in validLocations)
                {
                    if (!sources.TryGetValue(
                        value.Path!,
                        out var locations))
                    {
                        locations = new List<EngineSourceLocation>();
                        sources.Add(value.Path!, locations);
                    }
                    locations.Add(new EngineSourceLocation(
                        type,
                        domain,
                        module,
                        value.Location));
                }
            }

            foreach (var nested in sourceType.NestedTypes)
            {
                if (nested is IRuntimeSourceType nestedSource)
                    ProcessTypeLoaded(nested, nestedSource);
            }
        }

        private UnityDomainState GetOrAddDomain(object identity)
        {
            if (domains.TryGetValue(identity, out var existing))
                return existing;
            var created = new UnityDomainState(
                identity,
                identity.ToString() ?? "Unity Domain");
            domains.Add(identity, created);
            domainOrder.Add(created);
            return created;
        }

        private void RaiseModuleChanged(
            UnityModuleState module,
            bool loaded)
        {
            ModuleChanged?.Invoke(
                this,
                new BackendModuleChangedEventArgs(
                    new BackendModule(
                        module.Id,
                        module.Name,
                        module.Path,
                        module.HasSymbols),
                    loaded));
        }

        private static string? NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            try
            {
                return Path.GetFullPath(
                    value.Replace(
                        Path.AltDirectorySeparatorChar,
                        Path.DirectorySeparatorChar));
            }
            catch (Exception exception)
                when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is PathTooLongException)
            {
                return null;
            }
        }
    }
}
