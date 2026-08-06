# Reference-Compatible Mature Debugger Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace UnityDebuggerPure's handwritten debugger control and expression engines with the pinned mature Mono.Debugging/NRefactory session stack, then release and install one traceable `0.4.0` VSIX whose debugger behavior is validated against installed `zlorn.vstuc 1.2.1` in MyGame.

**Architecture:** `UnityDebugSession` remains a DAP translator, `MonoDebuggingBackend` maps the DAP-neutral backend contract to one active `UnitySoftDebuggerSession`, and Mono.Debugging owns target lifecycle, stepping, breakpoints, stack frames, `ObjectValue` inspection, and NRefactory evaluation. The installed reference extension is a black-box behavioral authority only; its proprietary assemblies are never read as implementation inputs, copied, decompiled, linked, or packaged.

**Tech Stack:** C# 8/.NET Framework 4.8 x64; pinned MIT `Mono.Debugging`, `Mono.Debugging.Soft`, `Mono.Debugger.Soft`, and NRefactory sources; VS Code DAP; TypeScript 7; Node.js 26; VSCE 3.9.2; Vitest 4.1.10; xUnit; Tuanjie `2022.3.62t12`; MyGame.

## Global Constraints

- The approved design is `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`.
- The installed `zlorn.vstuc 1.2.1` debugger is the sole authority for observable debugger behavior.
- When a newly observed behavior differs from the reference, stop at that decision point and ask the user whether to replace the current implementation; do not design around or silently accept the difference.
- Every affected scenario must remain marked `reference-verified`, `aligned`, `divergent`, or `not verified` with concrete evidence.
- A `divergent` or `not verified` row blocks any claim of full reference compatibility or completion.
- Do not reuse, separate, decompile, modify, link, or redistribute any proprietary binary from `C:\Users\Admin\.vscode\extensions\zlorn.vstuc-1.2.1`.
- Do not add telemetry; sanitized local diagnostics remain the only adapter diagnostics.
- Lawful implementation inputs are the repository's pinned MIT sources and the project-owned history at commit `bfaeac62e22f17060287aecb3ea8366c50cdc852`; history is a source baseline, not behavioral authority.
- Remove the complete `adapter/src/UnityDebugger.Adapter/Engine/**` runtime path and its custom evaluator/control tests; do not retain it as a fallback.
- `unityDebuggerPure.enableImplicitEvaluation` defaults to `true`, has VS Code `resource` scope, and therefore supports user, workspace, and workspace-folder resolution.
- Automatic Hover, Locals, and variable expansion obey that setting; Watch and REPL remain explicit evaluation contexts.
- Work in the current `master` checkout without switching branches or creating a worktree.
- Preserve the six pre-existing line-ending-only `adapter/vendor/**/packages.lock.json` worktree changes unless a locked restore proves a real content update is required.
- Real Editor testing uses only `D:\Unity\TuanjieHub\Projects\MyGame` and the existing VS Code window opened from MyGame.
- The user performs VS Code UI actions. Do not invoke Computer Use; state the exact action the user needs to perform.
- Use Unity MCP for MyGame/Tuanjie state, Play Mode, Console, and supported runtime inspection.
- Release exactly `0.4.0`; do not install any intermediate VSIX.
- Install at most one final verified VSIX into VS Code. If UI acceptance exposes a defect after installation, stop and ask before replacing that installation.

---

## File Map

Reference evidence:

- Create: `docs/reference-debugger-compatibility.md` — living A/B matrix and evidence index.
- Modify: `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md` — add only a link to the living matrix; do not rewrite the approved decision.

Mature backend:

- Create: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs` — test seam exposing mature-session operations and events without adding policy.
- Create: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs` — thin `IDebuggerBackend` mapper and sole production backend.
- Create: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs` — maps `UnitySoftDebuggerSession`, stack frames, breakpoints, and `ObjectValue` handles.
- Create: `adapter/src/UnityDebugger.Adapter/Backend/MonoObjectValueStore.cs` — stop-scoped frame/ObjectValue handle registry and mechanical value mapper.
- Create: `adapter/src/UnityDebugger.Adapter/Backend/UnitySoftDebuggerSession.cs` — Unity-specific `SoftDebuggerSession` subclass.
- Create: `adapter/src/UnityDebugger.Adapter/Backend/EvaluationOptionsPolicy.cs` — approved implicit-evaluation option mapping only.
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs` — sanitized mature-stack diagnostics.
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs` — preserve mature error/value metadata required by DAP.
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs` — only signatures required to expose reference-verified mature operations/events.
- Modify: `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs` — retain DAP condition/hit/log/function intent while the mature store owns evaluation and binding.
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/DapBreakpointModels.cs` — carry only reference-supported breakpoint request fields.
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs` — translate mature backend responses and events without a second execution state machine.
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs` — construct `MonoDebuggingBackend`.

Build/dependencies:

- Modify: `UnityDebugger.sln` — restore `Mono.Debugging`, `Mono.Debugging.Soft`, and both NRefactory projects.
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj` — reference the mature projects and remove direct-engine-only dependencies when unused.
- Modify: `adapter/src/UnityDebugger.Adapter/packages.lock.json` — only if locked restore produces a substantive dependency change.
- Modify: `scripts/generate-runtime-inventory.mjs` — audit mature runtime assemblies.
- Modify: `third-party/runtime-assemblies.json` — regenerated staged assembly hashes.
- Modify: `third-party/sources.json` and `THIRD_PARTY_NOTICES.md` — describe the runtime-used pinned MIT sources accurately.

Direct-engine removal:

- Remove: `adapter/src/UnityDebugger.Adapter/Engine/**`.
- Remove: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/**`.
- Replace or remove: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceControlTranscriptTests.cs` — its current custom control expectations are not reference evidence.
- Replace or remove: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs` — retain only expectations actually recorded from the reference.
- Replace or remove: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceAdvancedControlTests.cs` — retain only reference-verified capabilities and behavior.

Backend tests:

- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggingBackendLifecycleTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureBreakpointLifecycleTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureEvaluationTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureControlTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoObjectValueStoreTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/EvaluationOptionsPolicyTests.cs`.
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/MonoDebuggerLoggerTests.cs`.
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Build/RuntimeDependencyBoundaryTests.cs`.
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs`.
- Modify: `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs`.
- Modify: `tests/integration/adapter.integration.test.ts`.

Setting verification:

- Modify: `tests/extension/debugConfigurationProvider.test.ts`.
- Modify: `tests/extension/productIdentity.test.ts`.
- Modify: `tests/package/vsix.test.mjs`.
- Modify: `scripts/verify-vsix.mjs`.

Release `0.4.0`:

- Modify: `package.json`, `package-lock.json`, `CHANGELOG.md`, `README.md`, and `SECURITY.md`.
- Modify: `extension/src/productIdentity.ts` and `extension/src/versionPolicy.ts`.
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs` and `adapter/src/UnityDebugger.Adapter/Diagnostics/BuildIdentity.cs`.
- Modify: `scripts/stage-adapter.mjs`, `scripts/verify-vsix.mjs`, and `scripts/verify-release-artifact.mjs`.
- Modify: `tests/build/release-artifact.test.mjs`, `tests/build/repository-boundary.test.mjs`, and `tests/build/scaffold.test.mjs`.
- Modify: `tests/extension/debugConfigurationProvider.test.ts`, `tests/extension/productIdentity.test.ts`, and `tests/extension/versionPolicy.test.ts`.
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs` and `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/BuildIdentityTests.cs`.
- Modify: `tests/package/vsix.test.mjs`.

### Task 1: Establish the living reference contract

**Files:**

- Create: `docs/reference-debugger-compatibility.md`
- Modify: `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`

**Interfaces:**

- Consumes: approved design matrix and the user's observed MyGame failures.
- Produces: a single evidence table used as the acceptance gate by every later task.

- [ ] **Step 1: Create the matrix with the known evidence**

Use this exact schema and initial state:

```markdown
# Reference Debugger Compatibility

Reference: installed `zlorn.vstuc 1.2.1`.
Target: `D:\Unity\TuanjieHub\Projects\MyGame`, Tuanjie `2022.3.62t12`.

| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| ATT-01 | Attach in Edit Mode, then enter Play | No source-less user stop before the reachable breakpoint | Source-less transient pause | divergent | User A/B observation and Pure adapter log | Replace current control layer |
| RLD-01 | Ordinary Play/Domain Reload | No background `ThreadAbortException` stop | Background exception exposed | divergent | User A/B observation | Replace current control layer |
| BP-01 | First hit at `GameRuntimeBootstrap.Install()` line 11 | Bound breakpoint stops with current-statement marker | Hit requires continuing transitional stop | divergent | MyGame run and Pure adapter log | Replace current control layer |
| EVAL-01 | Hover/Watch `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` | Evaluates through the property Getter | `Expression evaluation failed` | divergent | MyGame run | Replace handwritten evaluator |
| EVAL-02 | Hover/Watch `RuntimeInitializeLoadType.AfterSceneLoad` | Resolves imported Unity enum member | `Expression evaluation failed` | divergent | MyGame run | Replace handwritten evaluator |
| STEP-01 | Step In, then rapid Step Over input | Smooth reference behavior; exact accepted/disabled sequence must be recorded | Warning/no-response/marker loss observed | divergent | User A/B observation | Replace current control layer |
| VAR-01 | Expand variables across resume/new stop | No stale collection warning in the same action sequence | `Variable collection is no longer available` observed | divergent | User A/B observation | Replace current handle layer |
| BP-02 | Conditional breakpoint | Not recorded | Not verified | not verified | none | Await A/B |
| BP-03 | Logpoint | Not recorded | Not verified | not verified | none | Await A/B |
| EX-01 | Caught/unhandled exception filters | Not recorded | Not verified | not verified | none | Await A/B |
| CTRL-01 | Pause, Continue, Step Out | Not fully recorded | Not verified | not verified | none | Await A/B |
| CTRL-02 | Step-in Targets and Goto capability/presentation | Not recorded | Not verified | not verified | none | Await A/B |
| EVAL-03 | Locals, Getter, `ToString()`, collection expansion | Not fully recorded | Not verified | not verified | none | Await A/B |
| EVAL-04 | Invalid expression and Getter failure presentation | Not recorded | Not verified | not verified | none | Await A/B |
| SET-01 | Set Variable | Not recorded | Not verified | not verified | none | Await A/B |
| END-01 | Detach and reattach | Not recorded | Not verified | not verified | none | Await A/B |
```

- [ ] **Step 2: Add an evidence rule and action script**

Below the table, require each evidence entry to record date, source line, exact user actions, visible result, sanitized log file, and screenshot path when available. Record the shared source fixture as:

```text
Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11
GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled
RuntimeInitializeLoadType.AfterSceneLoad
```

State that a row cannot move to `aligned` until the same action sequence is run with reference and Pure on that fixture. If an unlisted difference appears, add a `divergent` row and stop for the user's decision.

- [ ] **Step 3: Complete the reference-only baseline before production edits**

Ask the user to run the installed reference debugger in the existing MyGame VS Code window. Use the same reachable bootstrap and record these exact scenarios before Task 2 begins:

```text
BP-02: conditional source breakpoint whose condition is true, then false.
BP-03: logpoint containing one local/property expression.
EX-01: default uncaught filter, then All Exceptions, during ordinary Play/Domain Reload.
CTRL-01: Continue, Pause, Step In, Step Over, Step Out, including rapid Step Over as soon as enabled.
CTRL-02: whether Step Into Targets and Jump to Cursor/Goto are visible and what each does.
EVAL-03: Locals/this expansion, a property Getter, ToString(), and a collection child.
EVAL-04: invalid expression and one failing/unavailable property.
SET-01: whether Set Value is visible for a writable value and its visible success/error result.
END-01: detach, reattach, and first subsequent breakpoint.
```

No Pure result is collected in this step. Mark each captured reference row `reference-verified`; leave a scenario `not verified` if MyGame cannot expose it without modifying the project. Do not infer an unavailable UI capability.

- [ ] **Step 4: Link the living matrix from the approved design**

Add one sentence under the design's matrix heading:

```markdown
The maintained execution matrix and evidence index is
[`docs/reference-debugger-compatibility.md`](../../reference-debugger-compatibility.md).
```

- [ ] **Step 5: Verify and commit the contract**

Run:

```powershell
rg -n "divergent|not verified|reference-verified|aligned" docs/reference-debugger-compatibility.md
git diff --check -- docs/reference-debugger-compatibility.md docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md
```

Expected: every row has a status; no whitespace errors.

```powershell
git add -- docs/reference-debugger-compatibility.md docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md
git commit -m "docs: establish reference debugger compatibility matrix"
```

### Task 2: Restore the mature dependency boundary

**Files:**

- Modify: `UnityDebugger.sln`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Build/RuntimeDependencyBoundaryTests.cs`

**Interfaces:**

- Consumes: pinned vendor projects already present under `adapter/vendor`.
- Produces: build references for `Mono.Debugging`, `Mono.Debugging.Soft`, NRefactory, and `Mono.Debugger.Soft` without any reference-extension binary.

- [ ] **Step 1: Reverse the current dependency-boundary assertion**

Replace the current `ProductionAdapterUsesOnlyTheNewEngineDependencies` test with a compile-time boundary test:

```csharp
[Fact]
public void SolutionExposesThePinnedMatureDebuggerStack()
{
    Assert.Equal(
        "Mono.Debugging",
        typeof(Mono.Debugging.Client.EvaluationOptions).Assembly.GetName().Name);
    Assert.Equal(
        "Mono.Debugging.Soft",
        typeof(Mono.Debugging.Soft.SoftDebuggerSession).Assembly.GetName().Name);
    Assert.Equal(
        "Mono.Debugger.Soft",
        typeof(Mono.Debugger.Soft.VirtualMachine).Assembly.GetName().Name);
}
```

- [ ] **Step 2: Run the test to verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RuntimeDependencyBoundaryTests
```

Expected: compile FAIL because the solution/test dependency graph does not expose `Mono.Debugging` or `Mono.Debugging.Soft`.

- [ ] **Step 3: Restore the mature projects and adapter references**

Add the four project blocks and matching Debug/Release configurations from lawful baseline commit `bfaeac62e22f17060287aecb3ea8366c50cdc852` to `UnityDebugger.sln`:

```text
Mono.Debugging                {90C99ADB-7D4B-4EB4-98C2-40BD1B14C7D2}
Mono.Debugging.Soft           {DE40756E-57F6-4AF2-B155-55E3A88CCED8}
ICSharpCode.NRefactory       {3B2A5653-EC97-4001-BB9B-D90F1AF2C371}
ICSharpCode.NRefactory.CSharp {53DCA265-3C3C-42F9-B647-F72BA678122B}
```

Add these adapter project references:

```xml
<ProjectReference Include="..\..\vendor\debugger-libs\Mono.Debugging\Mono.Debugging.csproj" />
<ProjectReference Include="..\..\vendor\debugger-libs\Mono.Debugging.Soft\Mono.Debugging.Soft.csproj" />
```

Keep the existing `VSCodeDebug` and `Mono.Debugger.Soft` references. Do not add any file from the installed reference extension.

- [ ] **Step 4: Add a minimal compile-time mature-stack use**

Create the `UnitySoftDebuggerSession` skeleton needed to make the adapter reference both mature assemblies:

```csharp
using Mono.Debugging.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class UnitySoftDebuggerSession : SoftDebuggerSession
    {
        protected override void OnExit()
        {
            Detach();
        }
    }
}
```

In `Program.cs`, do not switch the production factory yet; Task 3 supplies the backend before the cutover.

- [ ] **Step 5: Restore and run the boundary test**

Run:

```powershell
dotnet restore UnityDebugger.sln --locked-mode
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RuntimeDependencyBoundaryTests
```

Expected: build and boundary test PASS because the solution and adapter now reference the pinned mature projects. Commit the independently green dependency boundary:

```powershell
git add -- UnityDebugger.sln adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj adapter/src/UnityDebugger.Adapter/Backend/UnitySoftDebuggerSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Build/RuntimeDependencyBoundaryTests.cs
git commit -m "test: require mature debugger runtime boundary"
```

### Task 3: Replace the production lifecycle and breakpoint control path

**Files:**

- Create: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/DapBreakpointModels.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggingBackendLifecycleTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureBreakpointLifecycleTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/MonoDebuggerLoggerTests.cs`

**Interfaces:**

- Consumes: `IDebuggerBackend`, `AttachTarget`, `LogicalBreakpoint`, and mature session events.
- Produces: `MonoDebuggingBackend(Func<ISoftDebuggerSessionFacade>) : IDebuggerBackend`, with no independent running/stopped state machine.
- Produces: `SoftDebuggerSessionFacade` as a mechanical session/ObjectValue adapter. It may store DAP handles, but may not synthesize stops, queue controls, or parse expressions.

Use this facade contract so later tasks do not invent a second engine boundary:

```csharp
internal interface ISoftDebuggerSessionFacade : IDisposable
{
    event EventHandler? TargetReady;
    event EventHandler? TargetStarted;
    event EventHandler? TargetExited;
    event EventHandler<BackendStoppedEventArgs>? TargetStopped;
    event EventHandler<BackendThreadEventArgs>? ThreadChanged;
    event EventHandler<BackendModuleChangedEventArgs>? ModuleChanged;
    event EventHandler<BackendBreakpointChangedEventArgs>? BreakpointChanged;
    event EventHandler<BackendOutputEventArgs>? Output;
    event EventHandler? AssemblyUnloaded;
    event EventHandler? AssemblyLoaded;

    bool IsRunning { get; }
    bool HasExited { get; }
    Task ConnectAsync(
        IPAddress address,
        int port,
        int maxConnectionAttempts,
        int connectionAttemptIntervalMilliseconds,
        CancellationToken cancellationToken);
    void Detach();
    void Continue();
    void Pause();
    void StepIn();
    void StepOver();
    void StepOut();
    void ConfigureExceptions(ExceptionBreakMode mode);
    IReadOnlyList<BackendThread> GetThreads();
    IReadOnlyList<BackendStackFrame> GetStackTrace(
        long threadId,
        int startFrame,
        int levels);
    IReadOnlyList<BackendScope> GetScopes(
        long frameId,
        BackendEvaluationMode mode,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
    IReadOnlyList<BackendVariable> GetVariables(
        long variablesReference,
        BackendEvaluationMode mode,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
    BackendEvaluationResult? Evaluate(
        long frameId,
        string expression,
        BackendEvaluationMode mode,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
    BackendSetVariableResult? SetVariable(
        long variablesReference,
        string name,
        string expression,
        BackendEvaluationMode mode,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
    BackendBoundBreakpoint BindBreakpoint(LogicalBreakpoint breakpoint);
    void RemoveBreakpoint(long backendBreakpointId);
    IReadOnlyList<BackendStepInTarget> GetStepInTargets(long frameId);
    IReadOnlyList<BackendGotoTarget> GetGotoTargets(
        string sourcePath,
        int line,
        int column);
    void Goto(long threadId, long targetId);
}
```

- [ ] **Step 1: Write failing lifecycle tests from the known reference rows**

Add tests with these exact assertions:

```csharp
[Fact]
public void AttachReadyAndAssemblyEventsDoNotCreateAUserStop()
{
    var facade = new FakeSoftDebuggerSessionFacade();
    using (var backend = new MonoDebuggingBackend(() => facade))
    {
        var stops = 0;
        backend.Stopped += (_, __) => stops++;

        backend.Attach(Target());
        facade.RaiseAssemblyUnloaded();
        facade.RaiseAssemblyLoaded();

        Assert.True(backend.IsAttached);
        Assert.Equal(0, stops);
    }
}

[Fact]
public void MatureBreakpointStopIsForwardedOnceWithItsBackendId()
{
    var facade = new FakeSoftDebuggerSessionFacade();
    using (var backend = new MonoDebuggingBackend(() => facade))
    {
        BackendStoppedEventArgs? observed = null;
        backend.Stopped += (_, value) => observed = value;
        backend.Attach(Target());

        facade.RaiseTargetStopped(new BackendStoppedEventArgs(
            BackendStopReason.Breakpoint,
            42,
            null,
            new long[] { 7 }));

        Assert.Equal(BackendStopReason.Breakpoint, observed?.Reason);
        Assert.Equal(new long[] { 7 }, observed?.BreakpointIds);
    }
}

[Fact]
public void OrdinaryAssemblyReloadKeepsLogicalBreakpointsInTheMatureStore()
{
    var facade = new FakeSoftDebuggerSessionFacade();
    using (var backend = new MonoDebuggingBackend(() => facade))
    {
        backend.Attach(Target());
        var first = backend.BindBreakpoint(Breakpoint(11));

        facade.RaiseAssemblyUnloaded();
        facade.RaiseAssemblyLoaded();
        facade.RaiseBreakpointChanged(new BackendBreakpointChangedEventArgs(
            new BackendBoundBreakpoint(first.Id, true, 11, null)));

        Assert.Single(facade.Bound);
        Assert.True(facade.ContainsBreakpoint(first.Id));
    }
}
```

Also test idempotent Disconnect/Dispose, non-loopback rejection, process exit termination exactly once, caught `ThreadAbortException` not becoming a stop under the reference's default exception filter, exact breakpoint changed propagation, assembly load/unload module-event translation, and preservation of mature stack-frame source paths through the existing `SourceMapper` DAP conversion.

For source breakpoints, expand `RequestedBreakpoint` and `LogicalBreakpoint` to retain `condition`, `hitCondition`, and `logMessage`. Map them mechanically to `BreakEvent.ConditionExpression`, `HitCountMode`/`HitCount`, and `HitAction.PrintExpression`/`TraceExpression` only after rows `BP-02` and `BP-03` are `reference-verified`. Add function-breakpoint binding only if the reference exposes it; otherwise remove `supportsFunctionBreakpoints` from `UnityDebugSession.Initialize` after obtaining the user's required alignment decision.

- [ ] **Step 2: Run the lifecycle tests to verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MonoDebuggingBackendLifecycleTests|FullyQualifiedName~MatureBreakpointLifecycleTests"
```

Expected: FAIL because `MonoDebuggingBackend` and the facade contract do not exist.

- [ ] **Step 3: Reintroduce the lawful mature-session bridge, not its old custom policy**

Use the corresponding files at `bfaeac62e22f17060287aecb3ea8366c50cdc852` as the source baseline for session event names, attach construction, breakpoint-store APIs, and exception filters. Make these deliberate changes while applying the code:

```csharp
// Production identity and factory.
var session = new UnityDebugSession(
    () => new MonoDebuggingBackend(
        () => new SoftDebuggerSessionFacade()));

// Mature-stack diagnostics only.
DebuggerLoggingService.CustomLogger = new MonoDebuggerLogger(log);
```

In `Program.Main`'s `finally`, set `DebuggerLoggingService.CustomLogger = null` alongside the existing protocol/internal log cleanup so repeated adapter processes do not retain a logger.

`MonoDebuggingBackend` delegates `Continue`, `Pause`, `StepIn`, `StepOver`, and `StepOut` directly to the facade. It must not include the rejected `AssemblyReloadCoordinator`, `ReconnectController`, retry queue, step queue, synthetic Continued event, or optimistic stopped/running state. `SoftDebuggerSessionFacade` binds breakpoints through `session.Breakpoints`, forwards `BreakEventStatusChanged`, and treats assembly load/unload only as lifecycle/binding events. The facade may retain one `expectedStopReason` solely to translate the next mature `TargetStopped` event after `Stop`, `StepLine`, `NextLine`, or `Finish`; it clears that value on the event and does not decide whether a command is allowed.

- [ ] **Step 4: Preserve exact mature stop origins**

Map only these session events:

```text
TargetHitBreakpoint      -> BackendStopReason.Breakpoint
TargetStopped after Stop -> BackendStopReason.Pause
TargetStopped after StepLine/NextLine/Finish -> BackendStopReason.Step
TargetInterrupted        -> BackendStopReason.Pause
TargetExceptionThrown    -> Exception only when All is configured
TargetUnhandledException -> Exception when Uncaught or All is configured
```

Do not map `TargetReady`, `TargetStarted`, `AssemblyLoaded`, `AssemblyUnloaded`, target output, or an internal `ThreadAbortException` to a user stop unless Task 1 has reference evidence for that identical scenario.

- [ ] **Step 5: Switch production and make lifecycle tests GREEN**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Backend|FullyQualifiedName~RuntimeDependencyBoundaryTests|FullyQualifiedName~MonoDebuggerLoggerTests"
```

Expected: PASS. The executable references `Mono.Debugging`, `Mono.Debugging.Soft`, and `Mono.Debugger.Soft`, and no `SyntaxTree.*` assembly.

- [ ] **Step 6: Commit the lifecycle replacement**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Backend adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs adapter/src/UnityDebugger.Adapter/Program.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/MonoDebuggerLoggerTests.cs
git commit -m "refactor: restore mature debugger session lifecycle"
```

### Task 4: Replace handwritten evaluation with ObjectValue/NRefactory evaluation

**Files:**

- Create: `adapter/src/UnityDebugger.Adapter/Backend/EvaluationOptionsPolicy.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/MonoObjectValueStore.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/EvaluationOptionsPolicyTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureEvaluationTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoObjectValueStoreTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs`
- Modify: `tests/integration/adapter.integration.test.ts`

**Interfaces:**

- Consumes: mature `Mono.Debugging.Client.StackFrame`, `ObjectValue`, `EvaluationOptions`, and `ObjectValue.SetValue` APIs.
- Produces: one frame context for Hover, Watch, REPL, Locals, expansion, and Set Variable.
- Produces: handles that are valid for exactly one mature stopped context and are cleared when the mature session resumes or terminates.
- Produces: `BackendEvaluationException(string displayMessage, Exception? innerException = null)` for a non-fatal per-expression failure.

`MonoObjectValueStore` exposes exactly this mapping surface:

```text
RegisterFrame(Mono.Debugging.Client.StackFrame frame) -> long
GetFrame(long frameId) -> Mono.Debugging.Client.StackFrame
Map(ObjectValue value) -> BackendVariable
GetVariables(long variablesReference, EvaluationOptions options, CancellationToken cancellationToken) -> IReadOnlyList<BackendVariable>
SetVariable(long variablesReference, string name, string expression, EvaluationOptions options, CancellationToken cancellationToken) -> BackendSetVariableResult
Clear() -> void
```

The implementation uses monotonically increasing `long` handles and dictionaries of mature frames/ObjectValues. `Clear()` empties both dictionaries but does not reset counters, so a stale handle can never alias a new stop.

- [ ] **Step 1: Write the evaluation-policy RED tests**

Use the approved setting contract:

```csharp
[Theory]
[InlineData(BackendEvaluationMode.Safe, false, false, false)]
[InlineData(BackendEvaluationMode.Explicit, true, true, true)]
public void CreateMapsTargetInvocationPolicyWithoutMutatingBaseline(
    BackendEvaluationMode mode,
    bool allowTargetInvoke,
    bool allowMethodEvaluation,
    bool allowToStringCalls)
{
    var baseline = EvaluationOptions.DefaultOptions.Clone();
    var result = EvaluationOptionsPolicy.Create(baseline, mode);

    Assert.Equal(allowTargetInvoke, result.AllowTargetInvoke);
    Assert.Equal(allowMethodEvaluation, result.AllowMethodEvaluation);
    Assert.Equal(allowToStringCalls, result.AllowToStringCalls);
    Assert.NotSame(baseline, result);
}
```

Add a DAP transcript test asserting Hover uses the automatic mode, Watch and REPL always use `Explicit`, and Scopes/Variables use the automatic mode.

- [ ] **Step 2: Write ObjectValue mapping RED tests**

Construct `ObjectValue.CreatePrimitive`, `ObjectValue.CreateObject`, and an error `ObjectValue`, register them in `MonoObjectValueStore`, and assert:

```csharp
var enumValue = ObjectValue.CreatePrimitive(
    null,
    new ObjectPath("loadType"),
    "UnityEngine.RuntimeInitializeLoadType",
    new EvaluationResult("AfterSceneLoad"),
    ObjectValueFlags.Variable);
var propertyError = ObjectValue.CreateError(
    null,
    new ObjectPath("Property"),
    "System.String",
    "Getter failed",
    ObjectValueFlags.Property);
var objectValue = ObjectValue.CreateObject(
    null,
    new ObjectPath("value"),
    "Fixture",
    "Fixture",
    ObjectValueFlags.Variable,
    new[] { enumValue, propertyError });
var store = new MonoObjectValueStore();

var mappedObject = store.Map(objectValue);
var mappedChildren = store.GetVariables(
    mappedObject.VariablesReference,
    EvaluationOptions.DefaultOptions,
    CancellationToken.None);

Assert.True(mappedObject.VariablesReference > 0);
Assert.Equal("AfterSceneLoad", mappedChildren[0].DisplayValue);
Assert.Equal(
    "UnityEngine.RuntimeInitializeLoadType",
    mappedChildren[0].TypeName);
Assert.Equal("Getter failed", mappedChildren[1].DisplayValue);
```

Add handle-lifetime tests: a handle remains usable while stopped, the resume event invalidates it once, and the next stop allocates a new nonzero handle. The stale request must follow the reference result recorded in matrix row `VAR-01`; do not preserve the current custom warning by assumption.

- [ ] **Step 3: Run focused tests to verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EvaluationOptionsPolicyTests|FullyQualifiedName~MatureEvaluationTests|FullyQualifiedName~ReferenceInspectionTranscriptTests"
```

Expected: FAIL because evaluation still lacks the mature ObjectValue path and exact reference error/lifetime mapping.

- [ ] **Step 4: Implement the single mature evaluation path**

The core calls must be direct:

```csharp
var options = EvaluationOptionsPolicy.Create(
    session.EvaluationOptions,
    mode);
var value = frame.GetExpressionValue(expression, options);
WaitForValue(value, options, cancellationToken);
```

Locals are built from `frame.GetThisReference(options)`, `frame.GetParameters(options)`, and `frame.GetLocalVariables(options)`. Expansion calls `ObjectValue.GetRangeOfChildren`. Set Variable locates the retained `ObjectValue`, calls `value.SetValue(expression, options)`, waits for refresh, and maps the resulting value. Do not parse enum members, source `using` directives, getters, casts, invocations, or `ToString()` in project code; NRefactory/Mono.Debugging owns all of them.

Preserve mature `ObjectValue` error display instead of collapsing every failure to `Expression evaluation failed`. Only connection/protocol loss escalates to a session-fatal backend exception.

Add this exact non-fatal error type to `BackendModels.cs`:

```csharp
internal sealed class BackendEvaluationException : Exception
{
    public BackendEvaluationException(
        string displayMessage,
        Exception? innerException = null)
        : base(displayMessage, innerException)
    {
        DisplayMessage = displayMessage;
    }

    public string DisplayMessage { get; }
}
```

- [ ] **Step 5: Update DAP inspection translation**

`UnityDebugSession.Evaluate` must return the mature display/type/reference on success and the reference-recorded DAP error on failure. Remove the unconditional generic error path:

```csharp
catch (BackendEvaluationException exception)
{
    SendErrorResponse(response, 2026, exception.DisplayMessage);
}
```

`BackendEvaluationException.DisplayMessage` must come from mature evaluation metadata and must not include expression text or an unredacted path in diagnostics.

- [ ] **Step 6: Run evaluation and integration tests GREEN**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EvaluationOptionsPolicyTests|FullyQualifiedName~MatureEvaluationTests|FullyQualifiedName~ReferenceInspectionTranscriptTests"
npm run test:integration
```

Expected: PASS; integration covers automatic-safe versus explicit Watch/REPL forwarding and stopped-context invalidation.

- [ ] **Step 7: Commit mature evaluation**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Backend adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs tests/integration/adapter.integration.test.ts
git commit -m "refactor: delegate evaluation to mature object values"
```

### Task 5: Align execution control and DAP presentation with the reference

**Files:**

- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureControlTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceControlTranscriptTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceAdvancedControlTests.cs`
- Modify: `tests/integration/adapter.integration.test.ts`
- Modify: `docs/reference-debugger-compatibility.md`

**Interfaces:**

- Consumes: Task 1 reference observations for command availability, rapid-input behavior, marker continuity, Set Variable, Goto, and Step-in Targets.
- Produces: direct mature `Continue`, `Stop`, `StepLine`, `NextLine`, `Finish`, `SetNextStatement`, and `ObjectValue.SetValue` translations only where the reference exposes them.

- [ ] **Step 1: Finish the missing reference observations before choosing capability behavior**

Ask the user to perform, in the existing MyGame VS Code window with the reference debugger, these exact actions while stopped at `GameRuntimeBootstrap.Install()` line 11:

```text
1. Press Step In once, then press Step Over repeatedly as soon as VS Code enables it.
2. Press Step Out once.
3. Open the Step Into Targets UI, if VS Code exposes it.
4. Invoke Jump to Cursor/Goto, if VS Code exposes it.
5. Change a writable local through Set Value, if one is present.
6. Continue, pause, detach, and attach again.
```

Record each visible command state, warning, marker transition, and final stop in `docs/reference-debugger-compatibility.md`. If a Pure capability is currently advertised but the reference does not advertise it, stop and ask whether to remove the Pure capability before editing `Initialize`.

- [ ] **Step 2: Replace current self-authored control tests with reference assertions**

Delete assertions such as “every rapid request reaches the backend” or “no continued event is emitted” unless Step 1 observed them. Encode the already observed smooth sequence as two accepted commands separated by their real mature stops:

```csharp
[Fact]
public void StepInThenEnabledStepOverProducesTwoUsableStopsWithoutWarning()
{
    var backend = new FakeDebuggerBackend();
    backend.Threads.Add(new BackendThread(42, "Main Thread"));
    backend.StepStoppedEvent = new BackendStoppedEventArgs(
        BackendStopReason.Step,
        42,
        null);
    var session = AttachAndStop(backend);

    var stepIn = DapTestProtocol.Run(
        session,
        DapTestProtocol.Request("stepIn", new { threadId = 1 }));
    var stepOver = DapTestProtocol.Run(
        session,
        DapTestProtocol.Request("next", new { threadId = 1 }));

    Assert.True(DapTestProtocol.Response(
        stepIn,
        "stepIn")["success"]!.Value<bool>());
    Assert.True(DapTestProtocol.Response(
        stepOver,
        "next")["success"]!.Value<bool>());
    Assert.Single(DapTestProtocol.Events(stepIn, "stopped"));
    Assert.Single(DapTestProtocol.Events(stepOver, "stopped"));
    Assert.Empty(DapTestProtocol.Events(stepIn, "output"));
    Assert.Empty(DapTestProtocol.Events(stepOver, "output"));
    Assert.Equal(1, backend.StepInCount);
    Assert.Equal(1, backend.StepOverCount);
}
```

For any additional rapid-input edge captured in Step 1, add a separate literal test beside this one before changing production; never derive the expected sequence from Pure.

- [ ] **Step 3: Run control tests to verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MatureControlTests|FullyQualifiedName~ReferenceControlTranscriptTests|FullyQualifiedName~ReferenceAdvancedControlTests"
```

Expected: FAIL at the first observable mismatch with the recorded reference sequence.

- [ ] **Step 4: Implement only direct mature control calls**

Use:

```csharp
session.Continue();
session.Stop();
session.StepLine();
session.NextLine();
session.Finish();
session.SetNextStatement(sourcePath, line, column);
```

Do not add request queues, coalescing, retry loops, artificial delays, warning suppression, or an independent stopped flag. Let the mature session accept or reject the request; translate its event/result exactly as the recorded reference presents it.

Only advertise `supportsStepInTargetsRequest`, `supportsGotoTargetsRequest`, and `supportsSetVariable` when the corresponding reference row is verified and the mature implementation passes its test. An unsupported reference capability is removed rather than simulated.

- [ ] **Step 5: Verify marker and handle event ordering**

Add/adjust transcript assertions so each real mature stop produces exactly one DAP `stopped` event after the control response and before stack/scopes requests. Resume invalidates prior handles only at the mature resume boundary. A synchronous new stop must never be discarded by cleanup belonging to the preceding request.

- [ ] **Step 6: Run control and integration tests GREEN**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MatureControlTests|FullyQualifiedName~ReferenceControlTranscriptTests|FullyQualifiedName~ReferenceAdvancedControlTests|FullyQualifiedName~UnityDebugSessionLifecycleTests"
npm run test:integration
```

Expected: PASS and no test preserves a behavior that lacks reference evidence.

- [ ] **Step 7: Commit the reference-aligned control path**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Backend adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend/MatureControlTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap tests/integration/adapter.integration.test.ts docs/reference-debugger-compatibility.md
git commit -m "fix: align mature debugger execution control"
```

### Task 6: Remove the rejected direct engines and audit the runtime

**Files:**

- Remove: `adapter/src/UnityDebugger.Adapter/Engine/**`
- Remove: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/**`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Build/RuntimeDependencyBoundaryTests.cs`
- Modify: `scripts/generate-runtime-inventory.mjs`
- Modify: `third-party/sources.json`
- Modify: `THIRD_PARTY_NOTICES.md`

**Interfaces:**

- Consumes: Tasks 3–5 mature backend.
- Produces: one production debugger path with no handwritten evaluator/control fallback and an audited package boundary.

- [ ] **Step 1: Add a failing architecture assertion**

Extend `RuntimeDependencyBoundaryTests`:

```csharp
[Fact]
public void ProductionAssemblyContainsNoRejectedDirectEngineTypes()
{
    var executable = Path.Combine(
        AppContext.BaseDirectory,
        "UnityDebuggerPure.exe");
    var names = Assembly.LoadFrom(executable)
        .GetTypes()
        .Select(value => value.FullName ?? string.Empty)
        .ToArray();

    Assert.DoesNotContain(names, name =>
        name.StartsWith("UnityDebugger.Adapter.Engine.", StringComparison.Ordinal));
    Assert.DoesNotContain(names, name => name.Contains("ExpressionEvaluator"));
    Assert.DoesNotContain(names, name => name.Contains("StepManager"));
    Assert.Contains(
        "UnityDebugger.Adapter.Backend.MonoDebuggingBackend",
        names);
}
```

- [ ] **Step 2: Run to verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RuntimeDependencyBoundaryTests
```

Expected: FAIL because direct-engine types still compile into the adapter.

- [ ] **Step 3: Delete the rejected runtime and tests**

Remove every file returned by:

```powershell
rg --files adapter/src/UnityDebugger.Adapter/Engine tests/adapter/UnityDebugger.Adapter.Tests/Engine
```

Before deletion, use `rg` to prove no mature backend file imports `UnityDebugger.Adapter.Engine`. Remove direct-engine-only package references such as the adapter's direct Roslyn parser dependency only when `dotnet build` proves the mature transitive dependency graph remains complete.

- [ ] **Step 4: Audit mature runtime origins**

Add these source-built entries in `scripts/generate-runtime-inventory.mjs`:

```javascript
["Mono.Debugging.dll", ["source-built:debugger-libs", "MIT"]],
["Mono.Debugging.Soft.dll", ["source-built:debugger-libs", "MIT"]],
["ICSharpCode.NRefactory.dll", ["source-built:nrefactory", "MIT"]],
["ICSharpCode.NRefactory.CSharp.dll", ["source-built:nrefactory", "MIT"]],
```

Keep `Mono.Debugger.Soft.dll`, `VSCodeDebug.dll`, and all audited NuGet runtime dependencies. Update notices to say these pinned MIT projects are compiled into the runtime; do not mention the reference binaries as dependencies.

- [ ] **Step 5: Build and make the boundary GREEN**

Run:

```powershell
dotnet restore UnityDebugger.sln --locked-mode
dotnet build UnityDebugger.sln -c Release --no-restore
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RuntimeDependencyBoundaryTests
rg -n "UnityDebuggerEngine|ExpressionEvaluator|StepManager|SuspendedState" adapter/src tests/adapter --glob '*.cs'
```

Expected: build/test PASS; final `rg` returns no production/custom-engine implementation hits.

- [ ] **Step 6: Commit the engine removal**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests scripts/generate-runtime-inventory.mjs third-party/sources.json THIRD_PARTY_NOTICES.md
git commit -m "refactor: remove handwritten debugger engines"
```

### Task 7: Prove user, workspace, and workspace-folder setting resolution

**Files:**

- Modify: `tests/extension/debugConfigurationProvider.test.ts`
- Modify: `tests/extension/productIdentity.test.ts`
- Modify: `tests/package/vsix.test.mjs`
- Modify: `scripts/verify-vsix.mjs`

**Interfaces:**

- Consumes: existing `vscode.workspace.getConfiguration("unityDebuggerPure", resourceUri)` wiring.
- Produces: explicit automated proof that VS Code's resource-scoped setting defaults on and resolves at all three requested scopes.

- [ ] **Step 1: Add failing manifest and resource-resolution assertions**

Assert the contributed setting exactly:

```typescript
expect(setting).toMatchObject({
  type: "boolean",
  default: true,
  scope: "resource",
});
```

Add a provider test with two workspace roots that returns different values from `readImplicitEvaluation(workspaceRoot)` and assert each attach receives its folder-specific `__enableImplicitEvaluation`. Name the test `resolves user workspace and workspace-folder precedence through the resource URI` and document that VS Code owns the precedence calculation before the callback returns.

- [ ] **Step 2: Run extension/package tests**

Run:

```powershell
npm exec vitest -- run tests/extension/debugConfigurationProvider.test.ts tests/extension/productIdentity.test.ts
```

Expected: existing code should pass the resource-resolution behavior; if it fails, fix only the folder URI passed to `getConfiguration`. Do not invent another settings store.

- [ ] **Step 3: Strengthen the VSIX verifier**

Have `scripts/verify-vsix.mjs` reject any packaged implicit-evaluation contribution whose `type`, `default`, or `scope` differs from the values above. Keep the existing negative package test and add scope mutation to its fixture.

- [ ] **Step 4: Commit setting proof**

```powershell
git add -- tests/extension/debugConfigurationProvider.test.ts tests/extension/productIdentity.test.ts tests/package/vsix.test.mjs scripts/verify-vsix.mjs
git commit -m "test: prove implicit evaluation setting scopes"
```

### Task 8: Prepare the traceable `0.4.0` release

**Files:**

- Modify every project-owned version file listed in the Release `0.4.0` section of the File Map.
- Modify: `third-party/runtime-assemblies.json`

**Interfaces:**

- Consumes: completed mature backend and setting contract.
- Produces: `0.4.0+g<12 lowercase Git characters>` build identity and `dist/unity-debugger-pure-0.4.0.vsix`.

- [ ] **Step 1: Change release tests to `0.4.0` first**

Update package/build/extension/adapter tests so they expect:

```text
version: 0.4.0
artifact: dist/unity-debugger-pure-0.4.0.vsix
tag: v0.4.0
build ID regex: ^0\.4\.0\+g[0-9a-f]{12}$
```

Run:

```powershell
npm run test:build
npm run test:extension
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BuildIdentityTests|FullyQualifiedName~AttachArgumentsTests"
```

Expected: FAIL on remaining `0.3.0` production metadata.

- [ ] **Step 2: Update production version metadata**

Change only project-owned `0.3.0` occurrences returned by:

```powershell
rg -n "0\.3\.0" CHANGELOG.md README.md SECURITY.md package.json package-lock.json extension adapter/src scripts tests/build tests/extension tests/package --glob '!**/packages.lock.json'
```

Do not change dependency versions such as `unicorn-magic 0.3.0`. Add a `CHANGELOG.md` `0.4.0` entry stating that the direct control/evaluation engines were replaced by the pinned mature session stack and that compatibility is tracked in the matrix; do not claim full parity before Task 10.

- [ ] **Step 3: Run version tests GREEN**

Run the Step 1 commands again. Expected: PASS.

- [ ] **Step 4: Build, stage, and explicitly regenerate runtime inventory**

Run:

```powershell
npm run build
npm run stage:adapter
npm run inventory:runtime:write
npm run verify:third-party
```

Expected: staging contains `UnityDebuggerPure.exe`, `Mono.Debugging.dll`, `Mono.Debugging.Soft.dll`, `Mono.Debugger.Soft.dll`, both NRefactory DLLs, `VSCodeDebug.dll`, audited transitive DLLs, and `build-info.json`; it contains no test or `SyntaxTree.*` binary.

- [ ] **Step 5: Commit release metadata and inventory**

```powershell
git add -- package.json package-lock.json CHANGELOG.md README.md SECURITY.md extension/src adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs adapter/src/UnityDebugger.Adapter/Diagnostics/BuildIdentity.cs scripts tests/build tests/extension tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/BuildIdentityTests.cs tests/package/vsix.test.mjs third-party/runtime-assemblies.json third-party/sources.json THIRD_PARTY_NOTICES.md
git commit -m "build: prepare mature debugger 0.4.0 release"
```

### Task 9: Run every automated gate and package one final candidate

**Files:**

- No intended source changes; any defect fix starts with a focused failing regression test.

**Interfaces:**

- Consumes: Tasks 1–8.
- Produces: one verified, hashed VSIX candidate; no installation yet.

- [ ] **Step 1: Verify repository and dependency boundaries**

Run:

```powershell
git diff --check
npm run verify:third-party
dotnet restore UnityDebugger.sln --locked-mode
```

Expected: no whitespace, provenance, or lock violations. Review the six pre-existing vendor lock-file status entries separately; do not stage line-ending-only changes.

- [ ] **Step 2: Run all source tests**

Run:

```powershell
npm run typecheck
npm run test:build
npm run test:extension
dotnet test UnityDebugger.sln -c Release --no-restore
npm run test:integration
```

Expected: all pass. Record exact test counts rather than reusing the earlier `257/94/19/7` counts.

- [ ] **Step 3: Package and verify**

Run:

```powershell
npm run package
npm run test:package
npm run verify:vsix
```

Expected: `dist/unity-debugger-pure-0.4.0.vsix` passes manifest, runtime inventory, setting scope, build ID, forbidden-binary, and test-binary checks.

- [ ] **Step 4: Record immutable candidate identity**

Run:

```powershell
Get-FileHash 'dist\unity-debugger-pure-0.4.0.vsix' -Algorithm SHA256
Get-FileHash 'adapter\win32-x64\UnityDebuggerPure.exe' -Algorithm SHA256
Get-Content -Raw 'adapter\win32-x64\build-info.json'
git status --short
```

Record VSIX hash, adapter hash, semantic version, build ID, commit, and worktree status in the matrix evidence section. Do not install if any automated gate failed or the matrix has a newly discovered undecided divergence.

### Task 10: Install once and perform same-project A/B acceptance

**Files:**

- Modify: `docs/reference-debugger-compatibility.md`
- External install target: VS Code user extensions.
- Read-only target: `D:\Unity\TuanjieHub\Projects\MyGame\Assets\Scripts\GamePlay\Runtime\DevTools\GameRuntimeBootstrap.cs`

**Interfaces:**

- Consumes: the Task 9 immutable artifact and Task 1 reference results.
- Produces: per-row `aligned`/`divergent` evidence; only all-aligned rows permit completion.

- [ ] **Step 1: Confirm MyGame/Tuanjie preconditions without UI automation**

Use Unity MCP to select the MyGame instance, confirm active scene `Assets/Scenes/SampleScene.scene`, Tuanjie `2022.3.62t12`, zero real compile/Console errors, and reachability of `GameRuntimeBootstrap.Install()` line 11 through `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`. State that the acceptance entry is ordinary SampleScene Play Mode and the target is line 11 before changing Play Mode.

- [ ] **Step 2: Install exactly the verified VSIX**

Run:

```powershell
& 'C:\Users\Admin\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd' --install-extension 'D:\Unity\unity-debugger-vscode\dist\unity-debugger-pure-0.4.0.vsix' --force
& 'C:\Users\Admin\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd' --list-extensions --show-versions | Select-String '^kpk\.unity-debugger-pure@0\.4\.0$'
```

Expected: exactly `kpk.unity-debugger-pure@0.4.0` is listed. Verify the installed `build-info.json` and `UnityDebuggerPure.exe` hashes match Task 9.

- [ ] **Step 3: Ask the user for one VS Code action**

Tell the user:

```text
请在现有 MyGame 的 VS Code 窗口执行一次 Developer: Reload Window；完成后告诉我“已 Reload”。
```

Do not use Computer Use and do not launch another VS Code profile.

- [ ] **Step 4: Verify installed build before behavior testing**

After the user reloads, ask them to select `Attach to Unity Debugger Pure` and start debugging once. Read the newest sanitized adapter log and require version `0.4.0` plus the Task 9 build ID before continuing.

- [ ] **Step 5: Repeat the known divergent scenarios with Pure**

Using the same source line and ordinary Play action, verify in order:

```text
ATT-01: Attach in Edit Mode, enter Play; no source-less stop.
RLD-01: Ordinary reload; no background ThreadAbortException stop.
BP-01: First reachable breakpoint binds and shows current-statement marker.
EVAL-01: Hover and Watch GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled.
EVAL-02: Hover and Watch RuntimeInitializeLoadType.AfterSceneLoad.
STEP-01: Repeat the exact reference Step In / rapid Step Over sequence.
VAR-01: Expand the same variables before and after the recorded resume/new-stop sequence.
```

The user performs breakpoint, Hover, Watch, expansion, and step actions. Unity MCP performs/observes Play Mode and Console. Capture sanitized log paths and user-provided screenshots, then update each row.

- [ ] **Step 6: Verify implicit evaluation scopes and remaining matrix rows**

With default `true`, test Hover, Locals, Getter, `ToString()`, and expansion. Then set the setting at workspace-folder scope to `false`, reattach, and confirm automatic contexts follow the reference-safe result while Watch/REPL remain explicit. Repeat user/workspace values only as needed to prove VS Code precedence; do not edit MyGame project settings unless the user chooses workspace scope.

Run the remaining reference-recorded breakpoint, exception, control, Set Variable, detach, and reattach sequences. A scenario not actually run remains `not verified`.

- [ ] **Step 7: Stop immediately on a new difference**

If any Pure result differs, add/update a `divergent` row with exact evidence and ask the user whether to replace the current implementation to align. Do not install a second build, suppress the difference, or mark the release complete. If the user approves a replacement, return to the smallest RED task, rebuild a new artifact, and obtain fresh permission before replacing the installed VSIX because the one-install constraint has been consumed.

- [ ] **Step 8: Final health and handoff**

Use Unity MCP to exit Play Mode through the same normal path and read Console. Run:

```powershell
git diff --check
git status --short
```

Commit only the evidence update:

```powershell
git add -- docs/reference-debugger-compatibility.md
git commit -m "test: record MyGame reference compatibility acceptance"
```

Report version, build ID, VSIX SHA-256, adapter SHA-256, installed path, exact automated test counts, Unity Console state, and every matrix status. Say “complete” or “reference-compatible” only when no row is `divergent` or `not verified`.
