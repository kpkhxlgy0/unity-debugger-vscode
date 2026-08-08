using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Mono.Debugging.Backend;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;
using DebugStackFrame = Mono.Debugging.Client.StackFrame;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class SoftDebuggerSessionFacadeInspectionTests
    {
        [Fact]
        public void ScopesReturnWithoutWaitingForEvaluatingLocals()
        {
            using (var facade = new SoftDebuggerSessionFacade())
            {
                var session = (DebuggerSession)typeof(
                        SoftDebuggerSessionFacade)
                    .GetField(
                        "session",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(facade)!;
                typeof(DebuggerSession).GetField(
                        "options",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(
                        session,
                        new DebuggerSessionOptions
                        {
                            EvaluationOptions =
                                EvaluationOptions.DefaultOptions,
                        });
                var backtrace = new EvaluatingBacktrace();
                var frame = new DebugStackFrame(
                    1,
                    string.Empty,
                    new SourceLocation(
                        "Fixture.Method",
                        "Fixture.cs",
                        12,
                        1,
                        12,
                        1),
                    "C#",
                    false,
                    true,
                    string.Empty,
                    "Fixture");
                typeof(DebugStackFrame).GetField(
                        "sourceBacktrace",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(frame, backtrace);
                typeof(DebugStackFrame).GetField(
                        "session",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(frame, session);
                var store = (MonoObjectValueStore)typeof(
                        SoftDebuggerSessionFacade)
                    .GetField(
                        "objectValues",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(facade)!;
                var frameId = store.RegisterFrame(frame);
                var stopwatch = Stopwatch.StartNew();

                var scopes = facade.GetScopes(
                    frameId,
                    4321,
                    CancellationToken.None);

                stopwatch.Stop();
                Assert.Single(scopes);
                Assert.True(
                    stopwatch.Elapsed < TimeSpan.FromMilliseconds(500),
                    $"Scopes blocked for {stopwatch.Elapsed}.");
                Assert.Equal(4321, backtrace.ObservedEvaluationTimeout);
                Assert.Equal(4321, backtrace.ObservedMemberTimeout);
            }
        }

        [Fact]
        public void ExceptionStopPreservesTheRuntimeTypeAndMessage()
        {
            using (var facade = new SoftDebuggerSessionFacade())
            {
                var session = SetSessionOptions(facade);
                var backtrace = new ExceptionBacktrace(
                    "System.Threading.Tasks.TaskCanceledException",
                    "A task was canceled.");
                var managedBacktrace = new Backtrace(backtrace);
                typeof(Backtrace).GetMethod(
                        "Attach",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(managedBacktrace, new object[] { session });
                var arguments = new TargetEventArgs(
                    TargetEventType.ExceptionThrown)
                {
                    Thread = new ThreadInfo(
                        1,
                        42,
                        "Worker",
                        string.Empty,
                        managedBacktrace),
                    Backtrace = managedBacktrace,
                };
                BackendStoppedEventArgs? stopped = null;
                facade.TargetStopped += (_, value) => stopped = value;

                var runtimeException = arguments.Backtrace
                    .GetFrame(0)
                    .GetException(EvaluationOptions.DefaultOptions);
                Assert.Equal(
                    "System.Threading.Tasks.TaskCanceledException",
                    runtimeException.Type);
                Assert.Equal("A task was canceled.", runtimeException.Message);

                typeof(SoftDebuggerSessionFacade).GetMethod(
                        "HandleStopped",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(
                        facade,
                        new object[]
                        {
                            arguments,
                            BackendStopReason.Exception,
                        });

                Assert.NotNull(stopped);
                Assert.NotNull(stopped!.ExceptionInfo);
                Assert.Equal(
                    "System.Threading.Tasks.TaskCanceledException",
                    stopped.ExceptionInfo!.ExceptionId);
                Assert.Equal(
                    "A task was canceled.",
                    stopped.ExceptionInfo.Description);
                Assert.Equal("always", stopped.ExceptionInfo.BreakMode);
            }
        }

        private static DebuggerSession SetSessionOptions(
            SoftDebuggerSessionFacade facade)
        {
            var session = (DebuggerSession)typeof(
                    SoftDebuggerSessionFacade)
                .GetField(
                    "session",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(facade)!;
            typeof(DebuggerSession).GetField(
                    "options",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(
                    session,
                    new DebuggerSessionOptions
                    {
                        EvaluationOptions = EvaluationOptions.DefaultOptions,
                    });
            return session;
        }

        private sealed class EvaluatingBacktrace : IBacktrace
        {
            private readonly ObjectValue evaluating =
                ObjectValue.CreateEvaluating(
                    new IdleUpdater(),
                    new ObjectPath("pending"),
                    ObjectValueFlags.Field);

            public int ObservedEvaluationTimeout { get; private set; }
            public int ObservedMemberTimeout { get; private set; }
            public int FrameCount => 1;

            public DebugStackFrame[] GetStackFrames(
                int firstIndex,
                int lastIndex) => Array.Empty<DebugStackFrame>();

            public ObjectValue[] GetLocalVariables(
                int frameIndex,
                EvaluationOptions options)
            {
                ObservedEvaluationTimeout = options.EvaluationTimeout;
                ObservedMemberTimeout = options.MemberEvaluationTimeout;
                return new[] { evaluating };
            }

            public ObjectValue[] GetParameters(
                int frameIndex,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public ObjectValue GetThisReference(
                int frameIndex,
                EvaluationOptions options) => null!;

            public ExceptionInfo GetException(
                int frameIndex,
                EvaluationOptions options) => null!;

            public ObjectValue[] GetAllLocals(
                int frameIndex,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public ObjectValue[] GetExpressionValues(
                int frameIndex,
                string[] expressions,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public CompletionData GetExpressionCompletionData(
                int frameIndex,
                string expression) => null!;

            public AssemblyLine[] Disassemble(
                int frameIndex,
                int firstLine,
                int count) => Array.Empty<AssemblyLine>();

            public ValidationResult ValidateExpression(
                int frameIndex,
                string expression,
                EvaluationOptions options) => default;
        }

        private sealed class ExceptionBacktrace : IBacktrace
        {
            private readonly DebugStackFrame frame;
            private readonly ExceptionInfo exception;

            public ExceptionBacktrace(string typeName, string message)
            {
                frame = new DebugStackFrame(
                    1,
                    string.Empty,
                    new SourceLocation(
                        "Fixture.Method",
                        "Fixture.cs",
                        12,
                        1,
                        12,
                        1),
                    "C#",
                    false,
                    true,
                    string.Empty,
                    "Fixture");
                var messageValue = ObjectValue.CreatePrimitive(
                    null,
                    new ObjectPath("Message"),
                    "System.String",
                    new EvaluationResult(message),
                    ObjectValueFlags.Property);
                exception = new ExceptionInfo(
                    ObjectValue.CreateObject(
                        null,
                        new ObjectPath("$exception"),
                        typeName,
                        "{" + typeName + "}",
                        ObjectValueFlags.Variable,
                        new[] { messageValue }));
            }

            public int FrameCount => 1;

            public DebugStackFrame[] GetStackFrames(
                int firstIndex,
                int lastIndex) => new[] { frame };

            public ObjectValue[] GetLocalVariables(
                int frameIndex,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public ObjectValue[] GetParameters(
                int frameIndex,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public ObjectValue GetThisReference(
                int frameIndex,
                EvaluationOptions options) => null!;

            public ExceptionInfo GetException(
                int frameIndex,
                EvaluationOptions options) => exception;

            public ObjectValue[] GetAllLocals(
                int frameIndex,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public ObjectValue[] GetExpressionValues(
                int frameIndex,
                string[] expressions,
                EvaluationOptions options) => Array.Empty<ObjectValue>();

            public CompletionData GetExpressionCompletionData(
                int frameIndex,
                string expression) => null!;

            public AssemblyLine[] Disassemble(
                int frameIndex,
                int firstLine,
                int count) => Array.Empty<AssemblyLine>();

            public ValidationResult ValidateExpression(
                int frameIndex,
                string expression,
                EvaluationOptions options) => default;
        }

        private sealed class IdleUpdater : IObjectValueUpdater
        {
            public void RegisterUpdateCallbacks(UpdateCallback[] callbacks)
            {
            }
        }
    }
}
