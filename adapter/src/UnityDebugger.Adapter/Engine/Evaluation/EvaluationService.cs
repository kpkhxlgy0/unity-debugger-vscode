using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Evaluation.Properties;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;
using UnityDebugger.Adapter.Engine.Evaluation.Values;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine.Evaluation
{
    internal sealed class EvaluationService : IEngineExpressionEvaluator
    {
        private readonly SuspendedState state;
        private readonly CSharpDebugParser parser = new CSharpDebugParser();
        private readonly ValueFormatter formatter = new ValueFormatter();

        public EvaluationService(SuspendedState state)
        {
            this.state = state ??
                throw new ArgumentNullException(nameof(state));
        }

        public BackendStackFrame RegisterFrame(
            BackendStackFrame frame,
            IFrameEvaluationEnvironment environment,
            IUnityEvaluationContext? unityContext)
        {
            var id = state.RegisterFrame(
                new EvaluationFrame(environment, unityContext));
            return new BackendStackFrame(
                id,
                frame.ThreadId,
                frame.Name,
                frame.SourcePath,
                frame.Line,
                frame.Column);
        }

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (!TryGetFrame(frameId, out var frame))
                return Array.Empty<BackendScope>();

            try
            {
                using (var request = CreateRequest(
                    timeoutMilliseconds,
                    cancellationToken))
                {
                    request.Token.ThrowIfCancellationRequested();
                    var property = new FrameProperty(
                        frame.Environment,
                        frame.UnityContext);
                    var handle = state.RegisterProperty(
                        new EvaluationProperty(property, frame));
                    return new[]
                    {
                        new BackendScope(property.Name, handle, false),
                    };
                }
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<BackendScope>();
            }
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (!TryGetProperty(variablesReference, out var parent))
                return Array.Empty<BackendVariable>();

            try
            {
                using (var request = CreateRequest(
                    timeoutMilliseconds,
                    cancellationToken))
                {
                    return GetVariablesAsync(parent, mode, request.Token)
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<BackendVariable>();
            }
        }

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (!TryGetFrame(frameId, out var frame))
                return null;

            try
            {
                using (var request = CreateRequest(
                    timeoutMilliseconds,
                    cancellationToken))
                {
                    var policy = GetPolicy(mode);
                    var value = EvaluateExpression(
                        frame,
                        expression,
                        policy,
                        request.Token);
                    var display = formatter.FormatAsync(
                            value,
                            policy,
                            request.Token)
                        .GetAwaiter()
                        .GetResult();
                    var handle = RegisterExpandable(
                        new ValueProperty(expression, value),
                        frame,
                        value);
                    return new BackendEvaluationResult(
                        display,
                        value.Type.FullName,
                        handle);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ExpressionEvaluationException exception)
            {
                throw new DebuggerBackendException(
                    "Expression evaluation failed.",
                    exception);
            }
        }

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (!TryGetProperty(variablesReference, out var parent))
                return null;

            try
            {
                using (var request = CreateRequest(
                    timeoutMilliseconds,
                    cancellationToken))
                {
                    return SetVariableAsync(
                            parent,
                            name,
                            expression,
                            mode,
                            request.Token)
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ExpressionEvaluationException exception)
            {
                throw new DebuggerBackendException(
                    "Set Variable failed.",
                    exception);
            }
        }

        public EngineExpressionResult EvaluateExpression(
            IFrameEvaluationEnvironment frame,
            string expression,
            int timeoutMilliseconds)
        {
            try
            {
                using (var request = CreateRequest(
                    timeoutMilliseconds,
                    CancellationToken.None))
                {
                    var parsed = parser.ParseExpression(expression);
                    if (!parsed.CanEvaluate)
                    {
                        return EngineExpressionResult.Failed(
                            parsed.Error ??
                            "The expression could not be parsed.");
                    }
                    var policy = EvaluationPolicy.Explicit;
                    var value = new ExpressionEvaluator(frame, policy)
                        .Evaluate(parsed.Expression!, request.Token);
                    var display = formatter.FormatAsync(
                            value,
                            policy,
                            request.Token)
                        .GetAwaiter()
                        .GetResult();
                    return EngineExpressionResult.Succeeded(value, display);
                }
            }
            catch (OperationCanceledException)
            {
                return EngineExpressionResult.Timeout();
            }
            catch (Exception exception)
            {
                return EngineExpressionResult.Failed(exception.Message);
            }
        }

        private async Task<IReadOnlyList<BackendVariable>> GetVariablesAsync(
            EvaluationProperty parent,
            BackendEvaluationMode mode,
            CancellationToken cancellationToken)
        {
            var policy = GetPolicy(mode);
            var providers = new ReferenceValueProviders(
                parent.Frame.UnityContext);
            var children = await GetChildrenAsync(
                    parent.Property,
                    providers,
                    policy,
                    cancellationToken)
                .ConfigureAwait(false);
            var result = new List<BackendVariable>(children.Count);
            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var debugValue = await child.GetDebugValueAsync(
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
                var display = debugValue.Kind == DebugValueKind.Value
                    ? await formatter.FormatAsync(
                            debugValue.Value!,
                            policy,
                            cancellationToken)
                        .ConfigureAwait(false)
                    : debugValue.Display;
                var handle = debugValue.Kind == DebugValueKind.Value
                    ? RegisterExpandable(
                        child,
                        parent.Frame,
                        debugValue.Value!)
                    : 0;
                result.Add(new BackendVariable(
                    child.Name,
                    display,
                    child.TypeName,
                    handle));
            }

            return result;
        }

        private async Task<BackendSetVariableResult?> SetVariableAsync(
            EvaluationProperty parent,
            string name,
            string expression,
            BackendEvaluationMode mode,
            CancellationToken cancellationToken)
        {
            var policy = GetPolicy(mode);
            var providers = new ReferenceValueProviders(
                parent.Frame.UnityContext);
            var children = await GetChildrenAsync(
                    parent.Property,
                    providers,
                    policy,
                    cancellationToken)
                .ConfigureAwait(false);
            var property = children.FirstOrDefault(
                child => string.Equals(
                    child.Name,
                    name,
                    StringComparison.Ordinal));
            if (property == null)
                return null;

            var value = EvaluateExpression(
                parent.Frame,
                expression,
                policy,
                cancellationToken);
            await property.SetValueAsync(value, cancellationToken)
                .ConfigureAwait(false);
            var display = await formatter.FormatAsync(
                    value,
                    policy,
                    cancellationToken)
                .ConfigureAwait(false);
            var handle = RegisterExpandable(property, parent.Frame, value);
            return new BackendSetVariableResult(
                display,
                value.Type.FullName,
                handle);
        }

        private static async Task<IReadOnlyList<DebugProperty>>
            GetChildrenAsync(
                DebugProperty property,
                ReferenceValueProviders providers,
                EvaluationPolicy policy,
                CancellationToken cancellationToken)
        {
            if (
                property is FrameProperty ||
                property is ComputedProperty)
            {
                return await property.GetChildrenAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (
                property is ValueProperty ||
                property is FieldProperty ||
                property is AccessorProperty)
            {
                var value = await property.GetDebugValueAsync(
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (value.Kind != DebugValueKind.Value)
                    return Array.Empty<DebugProperty>();
                return await providers.GetChildrenAsync(
                        value.Value!,
                        policy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return await property.GetChildrenAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private IRuntimeValue EvaluateExpression(
            EvaluationFrame frame,
            string expression,
            EvaluationPolicy policy,
            CancellationToken cancellationToken)
        {
            var parsed = parser.ParseExpression(expression);
            if (!parsed.CanEvaluate)
            {
                throw new ExpressionEvaluationException(
                    parsed.Error ?? "The expression could not be parsed.");
            }
            return new ExpressionEvaluator(frame.Environment, policy)
                .Evaluate(parsed.Expression!, cancellationToken);
        }

        private int RegisterExpandable(
            DebugProperty property,
            EvaluationFrame frame,
            IRuntimeValue value)
        {
            if (!IsExpandable(value))
                return 0;
            return state.RegisterProperty(
                new EvaluationProperty(property, frame));
        }

        private static bool IsExpandable(IRuntimeValue value) =>
            value.Kind == RuntimeValueKind.Array ||
            value.Kind == RuntimeValueKind.Object ||
            value.Kind == RuntimeValueKind.Struct;

        private bool TryGetFrame(long id, out EvaluationFrame frame)
        {
            if (id <= 0 || id > int.MaxValue)
            {
                frame = default!;
                return false;
            }
            return state.TryGetFrame((int)id, out frame);
        }

        private bool TryGetProperty(
            long id,
            out EvaluationProperty property)
        {
            if (id <= 0 || id > int.MaxValue)
            {
                property = default!;
                return false;
            }
            return state.TryGetProperty((int)id, out property);
        }

        private static EvaluationPolicy GetPolicy(BackendEvaluationMode mode) =>
            mode == BackendEvaluationMode.Explicit
                ? EvaluationPolicy.Explicit
                : EvaluationPolicy.Safe;

        private static CancellationTokenSource CreateRequest(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var request = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            request.CancelAfter(Math.Max(1, timeoutMilliseconds));
            return request;
        }

        private sealed class EvaluationFrame
        {
            public EvaluationFrame(
                IFrameEvaluationEnvironment environment,
                IUnityEvaluationContext? unityContext)
            {
                Environment = environment ??
                    throw new ArgumentNullException(nameof(environment));
                UnityContext = unityContext;
            }

            public IFrameEvaluationEnvironment Environment { get; }
            public IUnityEvaluationContext? UnityContext { get; }
        }

        private sealed class EvaluationProperty
        {
            public EvaluationProperty(
                DebugProperty property,
                EvaluationFrame frame)
            {
                Property = property;
                Frame = frame;
            }

            public DebugProperty Property { get; }
            public EvaluationFrame Frame { get; }
        }
    }
}
