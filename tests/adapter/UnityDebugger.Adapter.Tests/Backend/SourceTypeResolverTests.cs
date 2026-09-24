using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    [Collection("Source parser console output")]
    public sealed class SourceTypeResolverTests
    {
        [Fact]
        public void GlobalFrameResolvesImportedBattleType()
        {
            var resolver = SourceTypeResolver.FromSource(
                "using SGBattleLogic;\npublic class Form_BattleName { void Refresh() { } }", 2, 55);

            Assert.Equal("SGBattleLogic.FightUnitAttrUtils",
                resolver.Resolve("", "FightUnitAttrUtils", Exists("SGBattleLogic.FightUnitAttrUtils")));
        }

        [Fact]
        public void ImportsBelongToTheStoppedNamespaceDeclaration()
        {
            const string source = "namespace View { using Wrong; class Other { } }\n" +
                "namespace View { using SGBattleLogic; class Form { void Refresh() { } } }";
            var resolver = SourceTypeResolver.FromSource(source, 2, 65);

            Assert.Equal("SGBattleLogic.FightUnitAttrUtils", resolver.Resolve("View", "FightUnitAttrUtils",
                Exists("Wrong.FightUnitAttrUtils", "SGBattleLogic.FightUnitAttrUtils")));
        }

        [Fact]
        public void InnerTypeAliasOverridesOuterAlias()
        {
            const string source = "using Attrs = Wrong.FightUnitAttrUtils;\n" +
                "namespace View { using Attrs = SGBattleLogic.FightUnitAttrUtils; class Form { } }";
            var resolver = SourceTypeResolver.FromSource(source, 2, 75);

            Assert.Equal("SGBattleLogic.FightUnitAttrUtils", resolver.Resolve("View", "Attrs",
                Exists("Wrong.FightUnitAttrUtils", "SGBattleLogic.FightUnitAttrUtils")));
        }

        [Fact]
        public void CurrentNamespaceWinsOverImportedType()
        {
            var resolver = SourceTypeResolver.FromSource("using Imported;\nnamespace View { class Form { } }", 2, 25);

            Assert.Equal("View.Helper", resolver.Resolve("View", "Helper", Exists("View.Helper", "Imported.Helper")));
        }

        [Fact]
        public void AmbiguousImportsDoNotPickAnArbitraryType()
        {
            var resolver = SourceTypeResolver.FromSource("using First; using Second;\nclass Form { }", 2, 10);

            Assert.Null(resolver.Resolve("", "Helper", Exists("First.Helper", "Second.Helper")));
        }

        [Fact]
        public void UnimportedTypesStayOutOfScope()
        {
            var resolver = SourceTypeResolver.FromSource("class Form { }", 1, 10);

            Assert.Null(resolver.Resolve("", "FightUnitAttrUtils", Exists("SGBattleLogic.FightUnitAttrUtils")));
            Assert.Equal("SGBattleLogic.FightUnitAttrUtils",
                resolver.Resolve("", "SGBattleLogic.FightUnitAttrUtils", Exists("SGBattleLogic.FightUnitAttrUtils")));
        }

        [Fact]
        public void CommentedImportsAreNotInScope()
        {
            var resolver = SourceTypeResolver.FromSource("// using Wrong;\nclass Form { }", 2, 10);

            Assert.Null(resolver.Resolve("", "Helper", Exists("Wrong.Helper")));
        }

        [Fact]
        public void ModernSourceDoesNotWriteToTheDebugProtocolStream()
        {
            var output = Console.Out;
            using (var captured = new StringWriter())
            {
                try
                {
                    Console.SetOut(captured);
                    var resolver = SourceTypeResolver.FromSource(
                        "using SGBattleLogic;\nclass Form { string label = $\"value {1}\"; }", 2, 25);
                    Assert.Equal("SGBattleLogic.FightUnitAttrUtils",
                        resolver.Resolve("", "FightUnitAttrUtils", Exists("SGBattleLogic.FightUnitAttrUtils")));
                    Assert.Equal(string.Empty, captured.ToString());
                }
                finally
                {
                    Console.SetOut(output);
                }
            }
        }

        [Fact]
        public void MatureResolverQualifiesTheWatchCallAndPreservesArguments()
        {
            var resolver = SourceTypeResolver.FromSource(
                "using SGBattleLogic;\npublic class Form_BattleName { void Refresh() { } }", 2, 55);
            using (var session = new UnitySoftDebuggerSession())
            {
                typeof(DebuggerSession).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(session, SoftDebuggerSessionFacade.CreateReferenceSessionOptions());
                session.TypeResolverHandler = (identifier, _) =>
                    resolver.Resolve("", identifier, Exists("SGBattleLogic.FightUnitAttrUtils"));

                var expression = session.ResolveExpression("FightUnitAttrUtils.GetAttrCurrValue(e, 1033)",
                    new SourceLocation("Refresh", "Form_BattleNameArmor.cs", 2, 55, 2, 56));

                Assert.Equal("global::SGBattleLogic.FightUnitAttrUtils.GetAttrCurrValue(e, 1033)", expression);
            }
        }

        private static Func<string, bool> Exists(params string[] names)
        {
            var types = new HashSet<string>(names, StringComparer.Ordinal);
            return types.Contains;
        }
    }

    [CollectionDefinition("Source parser console output", DisableParallelization = true)]
    public sealed class SourceParserConsoleCollection
    {
    }
}
