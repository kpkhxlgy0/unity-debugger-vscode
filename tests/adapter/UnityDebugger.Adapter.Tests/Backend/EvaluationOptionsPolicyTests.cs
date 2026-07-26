using System;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class EvaluationOptionsPolicyTests
    {
        [Fact]
        public void Safe_mode_disables_target_execution()
        {
            var baseline = EvaluationOptions.DefaultOptions;

            var options = EvaluationOptionsPolicy.Create(
                baseline,
                BackendEvaluationMode.Safe);

            Assert.False(options.AllowTargetInvoke);
            Assert.False(options.AllowMethodEvaluation);
            Assert.False(options.AllowToStringCalls);
            Assert.True(baseline.AllowTargetInvoke);
        }

        [Fact]
        public void Explicit_mode_enables_target_execution()
        {
            var baseline = EvaluationOptions.DefaultOptions;
            baseline.AllowTargetInvoke = false;
            baseline.AllowMethodEvaluation = false;
            baseline.AllowToStringCalls = false;

            var options = EvaluationOptionsPolicy.Create(
                baseline,
                BackendEvaluationMode.Explicit);

            Assert.True(options.AllowTargetInvoke);
            Assert.True(options.AllowMethodEvaluation);
            Assert.True(options.AllowToStringCalls);
            Assert.False(baseline.AllowTargetInvoke);
        }

        [Fact]
        public void Unknown_mode_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EvaluationOptionsPolicy.Create(
                    EvaluationOptions.DefaultOptions,
                    (BackendEvaluationMode)int.MaxValue));
        }
    }
}
