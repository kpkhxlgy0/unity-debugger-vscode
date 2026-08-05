using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Evaluation.Runtime;

namespace UnityDebugger.Adapter.Engine.Mono
{
    internal sealed class MonoFrameEvaluationEnvironment :
        IFrameEvaluationEnvironment
    {
        private readonly StackFrame frame;
        private readonly ObjectMirror? currentException;

        public MonoFrameEvaluationEnvironment(
            StackFrame frame,
            ObjectMirror? currentException = null)
        {
            this.frame = frame ??
                throw new ArgumentNullException(nameof(frame));
            this.currentException = currentException;
        }

        public FrameValues GetFrameValues()
        {
            var thisValue = GetThisValue();
            var locals = GetLocals();
            var arguments = GetArguments();
            var exception = currentException == null
                ? null
                : new FrameVariable(
                    "$exception",
                    new MonoRuntimeValue(currentException, frame.Thread));
            return new FrameValues(
                thisValue,
                locals,
                arguments,
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                Array.Empty<FrameVariable>(),
                exception);
        }

        public bool TryGetValue(string name, out IRuntimeValue value)
        {
            var values = GetFrameValues();
            if (
                (name == "this" || name == "base") &&
                values.ThisValue != null)
            {
                value = values.ThisValue.Value;
                return true;
            }

            foreach (var variable in Enumerate(values))
            {
                if (string.Equals(
                    variable.Name,
                    name,
                    StringComparison.Ordinal))
                {
                    value = variable.Value;
                    return true;
                }
            }

            if (values.ThisValue != null)
            {
                var field = EnumerateFields(values.ThisValue.Value.Type)
                    .FirstOrDefault(candidate =>
                        candidate.Name == name && !candidate.IsStatic);
                if (field != null)
                {
                    value = values.ThisValue.Value.GetField(field);
                    return true;
                }
            }

            value = null!;
            return false;
        }

        public bool TryGetType(
            string fullOrSimpleName,
            out IRuntimeType type)
        {
            var values = GetFrameValues();
            var candidates = Enumerate(values)
                .Select(value => value.Value.Type)
                .Concat(
                    values.ThisValue == null
                        ? Array.Empty<IRuntimeType>()
                        : new[] { values.ThisValue.Value.Type })
                .Concat(
                    new IRuntimeType[]
                    {
                        new MonoRuntimeType(frame.Method.DeclaringType),
                    });
            foreach (var candidate in ExpandTypes(candidates))
            {
                if (Matches(candidate, fullOrSimpleName))
                {
                    type = candidate;
                    return true;
                }
            }

            var virtualMachine = frame.VirtualMachine;
            foreach (var name in GetQualifiedNames(fullOrSimpleName))
            {
                var found = virtualMachine.GetTypes(name, true)
                    .FirstOrDefault();
                if (found != null)
                {
                    type = new MonoRuntimeType(found);
                    return true;
                }
            }

            type = null!;
            return false;
        }

        private FrameVariable? GetThisValue()
        {
            if (frame.Method.IsStatic)
                return null;
            try
            {
                return new FrameVariable(
                    "this",
                    new MonoRuntimeValue(frame.GetThis(), frame.Thread));
            }
            catch (Exception exception)
                when (IsUnavailable(exception))
            {
                return null;
            }
        }

        private IReadOnlyList<FrameVariable> GetLocals()
        {
            var result = new List<FrameVariable>();
            IList<LocalVariable> variables;
            try
            {
                variables = frame.GetVisibleVariables();
            }
            catch (Exception exception)
                when (IsUnavailable(exception))
            {
                return result;
            }

            foreach (var variable in variables)
            {
                if (variable.IsArg)
                    continue;
                try
                {
                    var value = frame.GetValue(variable);
                    result.Add(
                        new FrameVariable(
                            variable.Name,
                            new MonoRuntimeValue(value, frame.Thread),
                            (newValue, cancellationToken) => SetLocalAsync(
                                variable,
                                newValue,
                                cancellationToken)));
                }
                catch (Exception exception)
                    when (IsUnavailable(exception))
                {
                }
            }
            return result;
        }

        private IReadOnlyList<FrameVariable> GetArguments()
        {
            var result = new List<FrameVariable>();
            foreach (var parameter in frame.Method.GetParameters())
            {
                try
                {
                    var value = frame.GetValue(parameter);
                    result.Add(
                        new FrameVariable(
                            parameter.Name ??
                                $"arg{parameter.Position}",
                            new MonoRuntimeValue(value, frame.Thread),
                            (newValue, cancellationToken) =>
                                SetArgumentAsync(
                                    parameter,
                                    newValue,
                                    cancellationToken)));
                }
                catch (Exception exception)
                    when (IsUnavailable(exception))
                {
                }
            }
            return result;
        }

        private Task SetLocalAsync(
            LocalVariable variable,
            IRuntimeValue value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame.SetValue(
                variable,
                MonoRuntimeValue.ToMonoValue(
                    value,
                    variable.Type,
                    frame.Domain,
                    frame.VirtualMachine));
            return Task.CompletedTask;
        }

        private Task SetArgumentAsync(
            ParameterInfoMirror parameter,
            IRuntimeValue value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame.SetValue(
                parameter,
                MonoRuntimeValue.ToMonoValue(
                    value,
                    parameter.ParameterType,
                    frame.Domain,
                    frame.VirtualMachine));
            return Task.CompletedTask;
        }

        private IEnumerable<string> GetQualifiedNames(string name)
        {
            yield return name;
            if (name.IndexOf('.') >= 0)
                yield break;
            var currentNamespace = frame.Method.DeclaringType.Namespace;
            if (!string.IsNullOrEmpty(currentNamespace))
                yield return currentNamespace + "." + name;
        }

        private static IEnumerable<FrameVariable> Enumerate(
            FrameValues values)
        {
            if (values.CurrentException != null)
                yield return values.CurrentException;
            foreach (var value in values.Locals)
                yield return value;
            foreach (var value in values.Constants)
                yield return value;
            foreach (var value in values.ClosureCaptures)
                yield return value;
            foreach (var value in values.HoistedValues)
                yield return value;
            foreach (var value in values.Arguments)
                yield return value;
        }

        private static IEnumerable<IRuntimeType> ExpandTypes(
            IEnumerable<IRuntimeType> values)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                for (
                    var current = value;
                    current != null;
                    current = current.BaseType)
                {
                    if (visited.Add(current.FullName))
                        yield return current;
                }
                foreach (var field in value.Fields)
                {
                    if (visited.Add(field.Type.FullName))
                        yield return field.Type;
                }
                if (value is MonoRuntimeType monoType)
                {
                    foreach (var nested in monoType.Mirror.GetNestedTypes())
                    {
                        var runtimeType = new MonoRuntimeType(nested);
                        if (visited.Add(runtimeType.FullName))
                            yield return runtimeType;
                    }
                }
            }
        }

        private static IEnumerable<RuntimeField> EnumerateFields(
            IRuntimeType type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var field in current.Fields)
                    yield return field;
            }
        }

        private static bool Matches(IRuntimeType type, string name) =>
            string.Equals(type.FullName, name, StringComparison.Ordinal) ||
            string.Equals(type.Name, name, StringComparison.Ordinal);

        private static bool IsUnavailable(Exception exception) =>
            exception is AbsentInformationException ||
            exception is InvalidStackFrameException ||
            exception is VMNotSuspendedException ||
            exception is NotSupportedException;
    }
}
