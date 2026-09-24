using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class SourceTypeResolver
    {
        private readonly List<SyntaxNode> scopes = new List<SyntaxNode>();

        public static SourceTypeResolver FromLocation(SourceLocation location)
        {
            try
            {
                if (!string.IsNullOrEmpty(location.FileName) && File.Exists(location.FileName))
                    return FromSource(File.ReadAllText(location.FileName), location.Line, location.Column);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return new SourceTypeResolver();
        }

        public static SourceTypeResolver FromSource(string source, int line, int column)
        {
            var resolver = new SourceTypeResolver();
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var lines = tree.GetText().Lines;
            if (line > 0 && line <= lines.Count && root.FullSpan.Length > 0)
            {
                var sourceLine = lines[line - 1];
                var position = Math.Min(sourceLine.Start + Math.Max(0, column - 1), sourceLine.End);
                var node = root.FindToken(Math.Min(position, root.FullSpan.End - 1)).Parent;
                if (node != null)
                    resolver.scopes.AddRange(node.AncestorsAndSelf().OfType<NamespaceDeclarationSyntax>());
            }
            resolver.scopes.Add(root);
            return resolver;
        }

        public string? Resolve(string? namespaceName, string identifier, Func<string, bool> typeExists)
        {
            if (identifier.StartsWith("global::", StringComparison.Ordinal))
                identifier = identifier.Substring("global::".Length);
            if (identifier.Contains(".") && typeExists(identifier))
                return identifier;

            foreach (var scope in scopes)
            {
                var scopeNamespace = string.Join(".", scope.AncestorsAndSelf()
                    .OfType<NamespaceDeclarationSyntax>().Reverse().Select(value => value.Name.ToString()));
                var imports = scope is NamespaceDeclarationSyntax declaration
                    ? declaration.Usings
                    : ((CompilationUnitSyntax)scope).Usings;
                var alias = imports.FirstOrDefault(value => value.Alias?.Name.Identifier.ValueText == identifier);
                if (alias != null)
                    return alias.Name == null ? null : ResolveQualifiedName(scopeNamespace, alias.Name.ToString(), typeExists);

                var enclosing = string.IsNullOrEmpty(scopeNamespace) ? identifier : scopeNamespace + "." + identifier;
                if (typeExists(enclosing))
                    return enclosing;

                var matches = new HashSet<string>(StringComparer.Ordinal);
                foreach (var import in imports)
                {
                    if (import.Alias != null || !import.StaticKeyword.IsKind(SyntaxKind.None) || import.Name == null)
                        continue;
                    var candidate = ResolveQualifiedName(scopeNamespace, import.Name + "." + identifier, typeExists);
                    if (candidate != null)
                        matches.Add(candidate);
                }
                if (matches.Count > 1)
                    return null;
                if (matches.Count == 1)
                    return matches.Single();
            }

            return SoftDebuggerSessionFacade.ResolveIdentifierInFrameNamespace(
                namespaceName, identifier, typeExists, typeExists);
        }

        private static string? ResolveQualifiedName(string namespaceName, string name, Func<string, bool> typeExists)
        {
            if (name.StartsWith("global::", StringComparison.Ordinal))
            {
                var absoluteName = name.Substring("global::".Length);
                return typeExists(absoluteName) ? absoluteName : null;
            }
            var currentNamespace = namespaceName;
            while (!string.IsNullOrEmpty(currentNamespace))
            {
                var candidate = currentNamespace + "." + name;
                if (typeExists(candidate))
                    return candidate;
                var separator = currentNamespace.LastIndexOf('.');
                currentNamespace = separator < 0 ? string.Empty : currentNamespace.Substring(0, separator);
            }
            return typeExists(name) ? name : null;
        }
    }
}
