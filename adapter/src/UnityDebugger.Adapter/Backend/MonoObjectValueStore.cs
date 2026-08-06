using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class MonoObjectValueStore
    {
        private const int MaximumChildren = 100;
        private readonly object storeLock = new object();
        private readonly Dictionary<long, StackFrame> frames =
            new Dictionary<long, StackFrame>();
        private readonly Dictionary<long, ObjectValue> values =
            new Dictionary<long, ObjectValue>();
        private long nextFrameId = 1;
        private long nextValueId = 1;

        public long RegisterFrame(StackFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            lock (storeLock)
            {
                if (nextFrameId == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Debugger frame handle space is exhausted.");
                }
                var id = nextFrameId++;
                frames.Add(id, frame);
                return id;
            }
        }

        public StackFrame GetFrame(long frameId)
        {
            lock (storeLock)
            {
                if (frames.TryGetValue(frameId, out var frame))
                    return frame;
            }
            throw new InvalidOperationException(
                "The requested stack frame is unavailable.");
        }

        public BackendVariable Map(ObjectValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var reference = 0L;
            if (value.HasChildren || value.IsObject || value.IsArray)
                reference = RegisterValue(value);
            return new BackendVariable(
                value.Name ?? string.Empty,
                value.DisplayValue ?? string.Empty,
                value.TypeName ?? string.Empty,
                reference);
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            EvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryGetValue(variablesReference, out var parent))
                return Array.Empty<BackendVariable>();
            cancellationToken.ThrowIfCancellationRequested();
            var children = parent.GetRangeOfChildren(
                0,
                MaximumChildren + 1,
                options);
            var result = new List<BackendVariable>(children.Length);
            foreach (var child in children)
            {
                WaitForValue(child, options, cancellationToken);
                result.Add(Map(child));
            }
            return result;
        }

        public BackendSetVariableResult SetVariable(
            long variablesReference,
            string name,
            string expression,
            EvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryGetValue(variablesReference, out var parent))
            {
                throw new BackendEvaluationException(
                    "The requested variable is unavailable.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var child = parent.GetRangeOfChildren(
                    0,
                    MaximumChildren + 1,
                    options)
                .FirstOrDefault(
                    item => string.Equals(
                        item.Name,
                        name,
                        StringComparison.Ordinal));
            if (child == null)
            {
                throw new BackendEvaluationException(
                    "The requested variable is unavailable.");
            }

            try
            {
                WaitForValue(child, options, cancellationToken);
                child.SetValue(expression, options);
                WaitForValue(child, options, cancellationToken);
                var mapped = Map(child);
                return new BackendSetVariableResult(
                    mapped.DisplayValue,
                    mapped.TypeName,
                    mapped.VariablesReference);
            }
            catch (BackendEvaluationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BackendEvaluationException(
                    string.IsNullOrWhiteSpace(exception.Message)
                        ? "Set Variable failed."
                        : exception.Message,
                    exception);
            }
        }

        public void Clear()
        {
            lock (storeLock)
            {
                frames.Clear();
                values.Clear();
            }
        }

        internal static void WaitForValue(
            ObjectValue value,
            EvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (!value.IsEvaluating && !value.IsEvaluatingGroup)
                return;
            var timeout = options.EvaluationTimeout > 0
                ? options.EvaluationTimeout
                : Timeout.Infinite;
            var result = WaitHandle.WaitAny(
                new[] { value.WaitHandle, cancellationToken.WaitHandle },
                timeout);
            if (result == 1)
                throw new OperationCanceledException(cancellationToken);
            if (result == WaitHandle.WaitTimeout)
            {
                throw new BackendEvaluationException(
                    "Evaluation timed out.");
            }
        }

        private long RegisterValue(ObjectValue value)
        {
            lock (storeLock)
            {
                if (nextValueId == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Debugger variable handle space is exhausted.");
                }
                var id = nextValueId++;
                values.Add(id, value);
                return id;
            }
        }

        private bool TryGetValue(
            long variablesReference,
            out ObjectValue value)
        {
            lock (storeLock)
                return values.TryGetValue(variablesReference, out value!);
        }
    }
}
