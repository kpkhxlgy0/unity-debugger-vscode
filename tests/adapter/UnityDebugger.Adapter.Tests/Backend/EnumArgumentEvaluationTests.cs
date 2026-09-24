using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.Debugging.Soft;
using UnityDebugger.Adapter.Backend;
using Xunit;
using Xunit.Abstractions;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class EnumArgumentEvaluationTests
    {
        private readonly ITestOutputHelper output;

        public EnumArgumentEvaluationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [MonoTheory]
        [InlineData("FightUnitAttrUtils.GetAttrCurrValue(e, 1033)", "1133")]
        [InlineData("e.AttrId", "1034")]
        public void EvaluationAndScopesLeaveTheNextStepWithAUsableFrame(string expression, string expected)
        {
            using (var probe = new MonoEvaluationProbe())
            {
                output.WriteLine("Loaded Mono runtime: " + probe.Evaluate("Fixture.RuntimeModules"));
                Assert.Equal(expected, probe.Evaluate(expression));
                probe.InspectScopes();

                var stopped = probe.StepOver();

                Assert.Equal(BackendStopReason.Step, stopped.Reason);
                Assert.Equal(probe.BreakpointLine + 1, probe.CurrentLine);
                Assert.Equal("1034", probe.Evaluate("e.AttrId"));
            }
        }

        [MonoFact]
        public void MethodEvaluationKeepsUnrelatedWorkerStoppedBeforeTheNextStep()
        {
            using (var probe = new MonoEvaluationProbe(startWorker: true))
            {
                Assert.NotEqual("0", probe.Evaluate("Fixture.WorkerCount"));
                var workerTicks = probe.Evaluate("Fixture.WorkerTicksDuringInvoke()");
                output.WriteLine("Unrelated worker ticks during method invocation: " + workerTicks);
                probe.InspectScopes();

                var stopped = probe.StepOver();

                Assert.Equal(BackendStopReason.Step, stopped.Reason);
                Assert.Equal(probe.BreakpointLine + 1, probe.CurrentLine);
                Assert.Equal("1034", probe.Evaluate("e.AttrId"));
                Assert.Equal("0", workerTicks);
            }
        }

        [MonoTheory]
        [InlineData("GetAttrCurrValue(e, 1033)", "1133")]
        [InlineData("GetAttrCurrValue(e, 1034)", "1134")]
        [InlineData("GetAttrCurrValue(e, EFightUnitAttr.eMonsterArmor)", "1133")]
        [InlineData("GetAttrCurrValue(e, (EFightUnitAttr)1034)", "1134")]
        [InlineData("GetAttrCurrValue(e, e.AttrId)", "1134")]
        [InlineData("GetAttrCurrValue(e, e.NextId())", "1133")]
        [InlineData("GetAttrCurrValue(e, -1)", "99")]
        [InlineData("GetAttrCurrValue(e, 1033U)", "1133")]
        [InlineData("ReadWide(4294967296L)", "4294967296")]
        [InlineData("ReadByte(255)", "255")]
        [InlineData("Choose(e, 1033)", "1")]
        public void InvokesEnumArgumentsWithoutChangingNormalOverloads(string call, string expected)
        {
            using (var probe = new MonoEvaluationProbe())
            {
                Assert.Equal(expected, probe.Evaluate("FightUnitAttrUtils." + call));
                if (call.Contains("e.NextId()"))
                    Assert.Equal("1", probe.Evaluate("e.ReadCount"));
            }
        }

        [MonoTheory]
        [InlineData("Choose", "System.Int32")]
        [InlineData("Widen", "System.Int64")]
        public void NormalOverloadSelectionTakesPrecedenceOverEnumConversion(string method, string expected)
        {
            using (var probe = new MonoEvaluationProbe())
                Assert.Equal(expected, probe.ResolveSecondParameter(method));
        }

        [MonoTheory]
        [InlineData("GetAttrCurrValue(e, 1033.5)")]
        [InlineData("GetAttrCurrValue(e, \"1033\")")]
        [InlineData("GetAttrCurrValue(e, true)")]
        [InlineData("Wrong(e, 1033)")]
        [InlineData("Ambiguous(e, 1033)")]
        public void RejectsInvalidOrAmbiguousArgumentsBeforeCallingTarget(string call)
        {
            using (var probe = new MonoEvaluationProbe())
            {
                Assert.Throws<BackendEvaluationException>(() => probe.Evaluate("FightUnitAttrUtils." + call));
                Assert.Equal("0", probe.Evaluate("FightUnitAttrUtils.CallCount"));
            }
        }
    }

    public sealed class MonoFactAttribute : FactAttribute
    {
        public MonoFactAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("UNITY_DEBUGGER_TEST_MONO")))
                Skip = "Set UNITY_DEBUGGER_TEST_MONO to a standalone Mono executable.";
        }
    }

    public sealed class MonoTheoryAttribute : TheoryAttribute
    {
        public MonoTheoryAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("UNITY_DEBUGGER_TEST_MONO")))
                Skip = "Set UNITY_DEBUGGER_TEST_MONO to a standalone Mono executable.";
        }
    }

    internal sealed class MonoEvaluationProbe : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "unity-debugger-enums-" + Guid.NewGuid().ToString("N"));
        private readonly SoftDebuggerSessionFacade facade = new SoftDebuggerSessionFacade();
        private readonly string sourcePath;
        private Process? process;
        private long frameId;

        public int BreakpointLine { get; private set; }
        public int CurrentLine { get; private set; }

        public MonoEvaluationProbe(bool startWorker = false)
        {
            sourcePath = Path.Combine(directory, "Fixture.cs");
            try
            {
                Directory.CreateDirectory(directory);
                var executable = Path.Combine(directory, "Fixture.exe");
                File.WriteAllText(sourcePath, Source);
                var tree = CSharpSyntaxTree.ParseText(SourceText.From(Source, Encoding.UTF8), path: sourcePath);
                var compilation = CSharpCompilation.Create("Fixture", new[] { tree },
                    new[]
                    {
                        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Process).Assembly.Location),
                    },
                    new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Debug));
                using (var exe = File.Create(executable))
                using (var pdb = File.Create(Path.ChangeExtension(executable, ".pdb")))
                {
                    var emitted = compilation.Emit(exe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                    Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
                }

                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                var stopped = new TaskCompletionSource<BackendStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                facade.TargetStopped += (_, value) => stopped.TrySetResult(value);
                BreakpointLine = Array.FindIndex(Source.Split('\n'), value => value.Contains("GC.KeepAlive(e)")) + 1;
                facade.BindBreakpoint(new LogicalBreakpoint(1, sourcePath, BreakpointLine, 1, null, null, null));
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("UNITY_DEBUGGER_TEST_MONO"),
                    Arguments = "--debug --debugger-agent=transport=dt_socket,address=127.0.0.1:" + port +
                        ",server=y,suspend=y \"" + executable + "\"" + (startWorker ? " worker" : string.Empty),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    facade.ConnectAsync(IPAddress.Loopback, port, 20, 100, timeout.Token).GetAwaiter().GetResult();
                Assert.True(stopped.Task.Wait(TimeSpan.FromSeconds(15)), "Standalone Mono did not hit the fixture breakpoint.");
                var frame = facade.GetStackTrace(stopped.Task.Result.ThreadId, 0, 5).First(value => value.SourcePath == sourcePath);
                frameId = frame.Id;
                CurrentLine = frame.Line;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Evaluate(string expression) =>
            facade.Evaluate(frameId, expression, 3000, CancellationToken.None)!.DisplayValue;

        public void InspectScopes() =>
            Assert.Single(facade.GetScopes(frameId, 3000, CancellationToken.None));

        public BackendStoppedEventArgs StepOver()
        {
            var stopped = new TaskCompletionSource<BackendStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<BackendStoppedEventArgs> handler = (_, value) => stopped.TrySetResult(value);
            facade.TargetStopped += handler;
            try
            {
                facade.StepOver();
                Assert.True(stopped.Task.Wait(TimeSpan.FromSeconds(15)), "Standalone Mono did not stop after Step Over.");
                var result = stopped.Task.Result;
                var frame = facade.GetStackTrace(result.ThreadId, 0, 5).First(value => value.SourcePath == sourcePath);
                frameId = frame.Id;
                CurrentLine = frame.Line;
                return result;
            }
            finally
            {
                facade.TargetStopped -= handler;
            }
        }

        public string ResolveSecondParameter(string methodName)
        {
            var session = (UnitySoftDebuggerSession)typeof(SoftDebuggerSessionFacade)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facade)!;
            var store = (MonoObjectValueStore)typeof(SoftDebuggerSessionFacade)
                .GetField("objectValues", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facade)!;
            var context = session.CreateEvaluationContext(store.GetFrame(frameId), session.EvaluationOptions);
            var method = SoftDebuggerAdaptor.OverloadResolve(context,
                (Mono.Debugger.Soft.TypeMirror)context.Adapter.GetType(context, "SGBattleLogic.FightUnitAttrUtils"),
                methodName, null, new SoftDebuggerAdaptor.ArgumentType[]
                {
                    (Mono.Debugger.Soft.TypeMirror)context.Adapter.GetType(context, "WorldEntity"),
                    (Mono.Debugger.Soft.TypeMirror)context.Adapter.GetType(context, "System.Int32"),
                }, false, true, true);
            return method.GetParameters()[1].ParameterType.FullName;
        }

        public void Dispose()
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            finally
            {
                facade.Dispose();
                process?.Dispose();
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
                if (Directory.Exists(directory) && Path.GetDirectoryName(Path.GetFullPath(directory)) == tempRoot)
                    Directory.Delete(directory, true);
            }
        }

        private const string Source = @"using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using SGBattleCommon;
using SGBattleLogic;
public class WorldEntity
{
    public int AttrId = 1034;
    public int ReadCount;
    public int NextId() { ReadCount++; return 1033; }
}
public class Fixture
{
    public static string RuntimeModules;
    public static int WorkerCount;
    public static int WorkerTicksDuringInvoke()
    {
        var before = Interlocked.CompareExchange(ref WorkerCount, 0, 0);
        Thread.Sleep(200);
        return Interlocked.CompareExchange(ref WorkerCount, 0, 0) - before;
    }
    public static void Main(string[] args)
    {
        var modules = new StringBuilder();
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.ModuleName.StartsWith(""mono-2.0-""))
                modules.Append(module.FileName).Append(""; "");
        }
        RuntimeModules = modules.ToString();
        if (args.Length != 0)
        {
            var worker = new Thread(() =>
            {
                while (true)
                {
                    Interlocked.Increment(ref WorkerCount);
                    Thread.Sleep(1);
                }
            });
            worker.IsBackground = true;
            worker.Start();
            while (Interlocked.CompareExchange(ref WorkerCount, 0, 0) == 0)
                Thread.Sleep(1);
        }
        var e = new WorldEntity();
        GC.KeepAlive(e);
        e.AttrId++;
        GC.KeepAlive(e);
    }
}
namespace SGBattleCommon
{
    public enum EFightUnitAttr { eMonsterArmor = 1033, eMonsterArmorMax = 1034 }
    public enum OtherAttr { Armor = 1033 }
    public enum WideAttr : long { Big = 4294967296L }
    public enum ByteAttr : byte { Max = 255 }
}
namespace SGBattleLogic
{
    public static class FightUnitAttrUtils
    {
        public static int CallCount;
        public static int GetAttrCurrValue(WorldEntity e, EFightUnitAttr attr) { CallCount++; return 100 + (int)attr; }
        public static long ReadWide(WideAttr attr) { return (long)attr; }
        public static int ReadByte(ByteAttr attr) { return (int)attr; }
        public static int Choose(WorldEntity e, EFightUnitAttr attr) { CallCount++; return -1; }
        public static int Choose(WorldEntity e, int attr) { CallCount++; return 1; }
        public static int Widen(WorldEntity e, EFightUnitAttr attr) { CallCount++; return -2; }
        public static int Widen(WorldEntity e, long attr) { CallCount++; return 2; }
        public static int Wrong(string e, EFightUnitAttr attr) { CallCount++; return 3; }
        public static int Ambiguous(WorldEntity e, EFightUnitAttr attr) { CallCount++; return 4; }
        public static int Ambiguous(WorldEntity e, OtherAttr attr) { CallCount++; return 5; }
    }
}";
    }
}
