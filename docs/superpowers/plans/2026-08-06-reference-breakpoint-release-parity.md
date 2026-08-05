# Reference Breakpoint, Release, and sgproj Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task in the current checkout. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete source/domain/breakpoint/exception parity, switch production to the new Unity engine, remove the old MonoDevelop backend from the runtime, release 0.3.0 with a unique build ID, install one VSIX, and accept it against sgproj.

**Architecture:** Pending breakpoints persist across domains while bound requests belong to one loaded domain. Type-load events update source maps and bind pending breakpoints; domain unload removes only domain-owned bindings. After the complete engine passes automated tests, `Program` switches factories, obsolete runtime dependencies are removed, and one traceable VSIX is installed for sgproj testing.

**Tech Stack:** C# 8/.NET Framework 4.8 x64; Mono Soft Debugger; Roslyn 4.14.0; Node.js 26; VSCE 3.9.2; Vitest 4.1.10; Unity MCP; Cursor Computer Use only where MCP cannot perform the operation.

## Global Constraints

- Execute after the foundation and evaluation plans pass.
- Reference debugger behavior is authoritative.
- Pending breakpoints survive Domain Reload; bound requests do not.
- No reference proprietary binary or source enters the project or VSIX.
- Remove `Mono.Debugging.dll`, `Mono.Debugging.Soft.dll`, NRefactory, and old global-evaluation patches from the final runtime.
- Release exactly version `0.3.0`; do not overwrite the installed `0.2.0` directory.
- Build ID is `0.3.0+g<12-character-commit>` and is present in the packaged adapter directory and startup diagnostics.
- Do not install until all automated tests and package verification pass.
- Real testing uses `D:\workspace\sgproj`; do not modify sgproj merely to create a test hook without separate approval.
- Prefer Unity MCP for Unity state, compilation, Console, and supported actions. Use the task-authorized Computer Use for Cursor debug UI or unsupported Unity UI interactions.

---

## File Map

Breakpoint/domain engine:

- `adapter/src/UnityDebugger.Adapter/Engine/State/UnityDomainState.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/State/UnityModuleState.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Source/EngineSourceMap.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Source/EngineSourceMapManager.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/PendingBreakpoint.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/BoundBreakpoint.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineBreakpointManager.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineExceptionManager.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/LogPointEvaluator.cs`

DAP completion:

- `adapter/src/UnityDebugger.Adapter/Engine/Control/StepTargetManager.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Control/GotoManager.cs`
- `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`

Production cleanup/release:

- `adapter/src/UnityDebugger.Adapter/Program.cs`
- `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- `scripts/stage-adapter.mjs`
- `scripts/generate-runtime-inventory.mjs`
- `scripts/verify-vsix.mjs`
- `scripts/verify-release-artifact.mjs`
- `package.json`
- `third-party/sources.json`
- `third-party/runtime-assemblies.json`
- `THIRD_PARTY_NOTICES.md`
- `tests/package/vsix.test.mjs`

Obsolete production files removed after the switch:

- `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/UnitySoftDebuggerSession.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/AssemblyReloadCoordinator.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/ReconnectController.cs`
- `adapter/src/UnityDebugger.Adapter/Backend/EvaluationOptionsPolicy.cs`
- `adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs`

### Task 1: Implement domain/module/source-map state

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/State/UnityDomainState.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/State/UnityModuleState.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Source/EngineSourceMap.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Source/EngineSourceMapManager.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Source/EngineSourceMapTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/State/UnityDomainStateTests.cs`

**Interfaces:**
- `UnityDomainState` maps modules and bound breakpoints for one runtime domain.
- Assembly load/unload produces one ordered backend `ModuleChanged` event with the reference reason and module identity; TypeLoad updates source/symbol state without duplicating module-load events.
- `EngineSourceMapManager.ProcessTypeLoaded(IRuntimeType type)` recursively maps the type and nested types.
- `GetLocations(string sourcePath, int line)` returns all currently loaded matching locations using `PathEqualityComparer` semantics.

- [ ] **Step 1: Write failing source-map tests**

Use fake domains/types with complete source paths and assert:

```csharp
[Fact]
public void TypeLoadMapsNestedTypesAndMarksModuleWithSymbols()
{
    var nested = FakeRuntimeType.Class("Game.Nested", "D:\\project\\Assets\\Button.cs", 22);
    var outer = FakeRuntimeType.Class("Game.Button", "D:\\project\\Assets\\Button.cs", 10)
        .WithNested(nested);
    var manager = CreateSourceMapManager();

    manager.ProcessTypeLoaded(outer);

    Assert.Equal(new[] { 10, 22 }, manager.GetLocations("D:\\project\\Assets\\Button.cs", 0).Select(x => x.Line));
    Assert.True(manager.Modules.Single().HasSymbols);
}
```

Add tests for mapped local paths, case-insensitive Windows comparison, domain unload removing only that domain's modules, and the exact ordered `ModuleChanged` load/unload events. Assert that repeated TypeLoad events for one assembly do not emit duplicate module-load notifications.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EngineSourceMapTests|FullyQualifiedName~UnityDomainStateTests"
```

Expected: FAIL because domain/source-map state does not exist.

- [ ] **Step 3: Implement domain-owned maps**

Use concurrent dictionaries keyed by runtime identity for domains/modules and normalized full paths for sources. A module records loaded types and symbol availability. Assembly and domain events drive ordered module load/unload notifications; TypeLoad only enriches an existing module. Domain unload returns the exact bound breakpoints/modules to unbind and removes that domain from engine state.

- [ ] **Step 4: Run source/domain tests**

Run Step 2's command. Expected: PASS.

- [ ] **Step 5: Commit domain and source maps**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/State/UnityDomainState.cs adapter/src/UnityDebugger.Adapter/Engine/State/UnityModuleState.cs adapter/src/UnityDebugger.Adapter/Engine/Source tests/adapter/UnityDebugger.Adapter.Tests/Engine/Source tests/adapter/UnityDebugger.Adapter.Tests/Engine/State/UnityDomainStateTests.cs
git commit -m "feat: map Unity domains and loaded sources"
```

### Task 2: Implement pending/bound breakpoint lifecycle

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/PendingBreakpoint.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/BoundBreakpoint.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineBreakpointManager.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints/EngineBreakpointManagerTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs`

**Interfaces:**
- `RequestSourceBreakpoint(LogicalBreakpoint value)` and `RequestFunctionBreakpoint(...)` return one pending breakpoint.
- `ProcessTypeLoaded(IRuntimeType type)` binds all matching pending breakpoints.
- `ProcessBreakpointHit(IRuntimeBreakpointEvent value)` returns Stop, Resume, or LogPoint output action.
- `UnbindDomain(UnityDomainState domain)` disables domain-owned requests but retains pending breakpoints.

- [ ] **Step 1: Write failing reload lifecycle test**

```csharp
[Fact]
public void DomainReloadUnbindsThenRebindsTheSamePendingBreakpoint()
{
    var manager = CreateManager();
    var pending = manager.RequestSourceBreakpoint(SourceBreakpoint("Assets/Button.cs", 95));
    var firstType = LoadedType(domainId: 1, "Assets/Button.cs", 95);
    manager.ProcessTypeLoaded(firstType);
    var firstBoundId = pending.Bound.Single().Id;

    manager.UnbindDomain(firstType.Domain);
    Assert.Empty(pending.Bound);
    Assert.True(manager.ContainsPending(pending.Id));

    var replacement = LoadedType(domainId: 2, "Assets/Button.cs", 95);
    manager.ProcessTypeLoaded(replacement);
    Assert.Single(pending.Bound);
    Assert.NotEqual(firstBoundId, pending.Bound.Single().Id);
}
```

Add tests for nested/generic type binding, unknown Mono breakpoint hit resuming, non-debuggable location resuming, and breakpoint hit ID propagation.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~EngineBreakpointManagerTests
```

Expected: FAIL because the manager does not exist.

- [ ] **Step 3: Implement pending and bound ownership**

Pending objects own user ID/path/line/condition/log message and a set of bound objects. Bound objects own the Mono request, exact location, domain, hit count, and prior condition result. Type-load filtering follows the available Mono protocol feature; lack of filter support enables ordinary TypeLoad events.

Emit breakpoint changed when a pending breakpoint first binds or rebinds. Domain unload emits internal unbound lifecycle but does not delete or visibly remove the DAP breakpoint.

- [ ] **Step 4: Run breakpoint and source-map tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EngineBreakpointManagerTests|FullyQualifiedName~EngineSourceMapTests"
```

Expected: PASS.

- [ ] **Step 5: Commit breakpoint lifecycle**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/PendingBreakpoint.cs adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/BoundBreakpoint.cs adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineBreakpointManager.cs adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints/EngineBreakpointManagerTests.cs
git commit -m "feat: preserve breakpoints across Domain Reload"
```

### Task 3: Match conditions, logpoints, and exception filters

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/LogPointEvaluator.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineExceptionManager.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints/BreakpointConditionTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints/LogPointEvaluatorTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints/EngineExceptionManagerTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/Breakpoints/EngineBreakpointManager.cs`

**Interfaces:**
- Conditions evaluate on the hit thread's top frame with a 1,000 ms limit.
- Logpoint interpolation evaluates brace expressions and returns one output string plus Resume.
- Exception manager maintains caught/unhandled requests and type-name conditions.

- [ ] **Step 1: Write failing condition tests**

Test literal true/false, changed, evaluation error, and timeout results. An error returns Stop plus a condition-error output; false returns Resume. Do not test unsupported DAP hit-condition advertisement.

- [ ] **Step 2: Write failing logpoint and exception tests**

Use `"state={currentState}"` with a fake enum and assert `state=Normal` plus Resume and no Stop. Test escaped braces. Test None, Uncaught, and All exception modes and a matching/non-matching type condition. Add an ExceptionInfo DAP transcript that asserts the stopped exception's ID, description, break mode, and nested details match the engine event.

- [ ] **Step 3: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BreakpointConditionTests|FullyQualifiedName~LogPointEvaluatorTests|FullyQualifiedName~EngineExceptionManagerTests"
```

Expected: FAIL because these components do not exist.

- [ ] **Step 4: Implement exact actions**

Use the shared evaluation service; do not add a second expression evaluator. Condition errors stop and report. Logpoint success and logpoint expression errors both follow the captured reference output/resume behavior. Exception requests use Mono caught/uncaught flags and disable obsolete requests before enabling replacements.

- [ ] **Step 5: Run and commit**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BreakpointConditionTests|FullyQualifiedName~LogPointEvaluatorTests|FullyQualifiedName~EngineExceptionManagerTests"
git add -- adapter/src/UnityDebugger.Adapter/Engine/Breakpoints tests/adapter/UnityDebugger.Adapter.Tests/Engine/Breakpoints
git commit -m "feat: match breakpoint and exception evaluation"
```

### Task 4: Complete Step-in Targets, Goto, and Set Variable DAP paths

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Control/StepTargetManager.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Control/GotoManager.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceAdvancedControlTests.cs`

**Interfaces:**
- `GetStepInTargets(frameId)` registers code paths in `SuspendedState`.
- `StepIn(threadId, targetId)` uses a registered path when present, ordinary Step Into otherwise.
- `GetGotoTargets(source,line,column)` registers code contexts.
- `Goto(threadId,targetId)` validates the code context, changes the instruction pointer, and then emits the reference-compatible Goto stopped event without resetting `SuspendedState`.

- [ ] **Step 1: Write failing request transcript tests**

Assert exact empty responses for stale frame/target IDs. Assert target labels and source ranges for valid data. Assert a successful Goto emits one Goto stopped event after the instruction-pointer change and preserves the current suspended handles. Assert Set Variable reevaluates and returns the new display value/type/reference.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReferenceAdvancedControlTests
```

Expected: FAIL because foundation hooks still return their migration-stage empty responses.

- [ ] **Step 3: Implement managers and DAP translation**

Use the current stop's frame/property/code maps only. Do not keep targets across stops. Map all line/column positions through existing SourceMapper client conversion helpers.

- [ ] **Step 4: Run all DAP tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Dap
```

Expected: PASS.

- [ ] **Step 5: Commit advanced controls**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Control/StepTargetManager.cs adapter/src/UnityDebugger.Adapter/Engine/Control/GotoManager.cs adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceAdvancedControlTests.cs
git commit -m "feat: complete reference debugger control requests"
```

### Task 5: Switch production and remove the old MonoDevelop engine

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Remove: old backend/diagnostic files listed in File Map.
- Remove: obsolete tests for old facades, reload/reconnect coordinators, evaluation policy, and Mono logger.
- Modify: `third-party/sources.json`
- Modify: `THIRD_PARTY_NOTICES.md`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Build/RuntimeDependencyBoundaryTests.cs`

**Interfaces:**
- `Program` creates `new UnityDebuggerEngine()` through the `IDebuggerBackend` factory.
- Final adapter references `VSCodeDebug`, `Mono.Debugger.Soft`, Roslyn, Newtonsoft.Json, and their audited transitive assemblies only.

- [ ] **Step 1: Write the failing runtime-boundary test**

Build Release and inspect `UnityDebuggerPure.exe` assembly references. Assert the exact forbidden names are absent:

```csharp
Assert.DoesNotContain("Mono.Debugging", references);
Assert.DoesNotContain("Mono.Debugging.Soft", references);
Assert.DoesNotContain("ICSharpCode.NRefactory", references);
Assert.DoesNotContain("ICSharpCode.NRefactory.CSharp", references);
```

Also assert `Mono.Debugger.Soft` and `Microsoft.CodeAnalysis.CSharp` are present.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RuntimeDependencyBoundaryTests
```

Expected: FAIL because the old project references remain.

- [ ] **Step 3: Switch factory and remove obsolete code from the build**

Change `Program` to construct `UnityDebuggerEngine`. Remove project references to `Mono.Debugging` and `Mono.Debugging.Soft`. Delete project-owned old backend files and their tests once `rg` proves no new-engine consumer. Revert the earlier vendor modifications by applying the upstream contents recorded in `third-party/sources.json`, then remove those vendor projects from the solution/build so none of the 250 ms/global-abort patches ship.

Retain only vendor `Mono.Debugger.Soft` source and remove NRefactory project references. Update provenance text to describe the smaller copied/built subset; retain licenses for any source still in the repository.

- [ ] **Step 4: Run dependency and complete adapter tests**

```powershell
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: PASS. Release output contains no forbidden runtime DLL.

- [ ] **Step 5: Commit the production switch**

```powershell
git add -- adapter/src/UnityDebugger.Adapter adapter/vendor/debugger-libs UnityDebugger.sln tests/adapter third-party/sources.json THIRD_PARTY_NOTICES.md
git commit -m "refactor: replace MonoDevelop debugger backend"
```

### Task 6: Release 0.3.0 with a unique build ID

**Files:**
- Modify: `package.json`
- Modify: `scripts/stage-adapter.mjs`
- Modify: `scripts/generate-runtime-inventory.mjs`
- Modify: `scripts/verify-vsix.mjs`
- Modify: `scripts/verify-release-artifact.mjs`
- Modify: `tests/package/vsix.test.mjs`
- Modify: `third-party/runtime-assemblies.json`
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/BuildIdentity.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/BuildIdentityTests.cs`

**Interfaces:**
- Staging writes `adapter/win32-x64/build-info.json` with `{version, commit, buildId}`.
- Build ID is `0.3.0+g` plus the first 12 lowercase hexadecimal Git commit characters.
- Adapter startup reads the sibling file and writes version/build ID to sanitized diagnostics.

- [ ] **Step 1: Write failing package/build identity tests**

Update package tests to look for `dist/unity-debugger-pure-0.3.0.vsix`, manifest version `0.3.0`, packaged `extension/adapter/win32-x64/build-info.json`, and a build ID matching `^0\.3\.0\+g[0-9a-f]{12}$`.

Add unit tests that a missing/malformed build-info file returns `0.3.0+gunknown` without crashing, while a valid file returns its exact value.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BuildIdentityTests
npm run test:package
```

Expected: unit and package tests FAIL because version/build info remain 0.2.0 or absent.

- [ ] **Step 3: Implement version and staging identity**

Change every hard-coded artifact/tag path to 0.3.0. In `stage-adapter.mjs`, invoke `git rev-parse HEAD` with `execFileSync`, validate exactly 40 lowercase hex characters, and write formatted JSON after copying binaries. Include the JSON file in VSIX verification but not in the assembly inventory.

`BuildIdentity` reads only the fixed sibling filename, validates version/build ID, and never logs an arbitrary file payload.

- [ ] **Step 4: Restore, build, stage, and regenerate audited inventory**

```powershell
dotnet restore UnityDebugger.sln --force-evaluate
npm run build
npm run stage:adapter
npm run inventory:runtime:write
npm run verify:third-party
```

Expected: success; inventory contains no old Mono.Debugging/NRefactory assemblies and includes all locked Roslyn transitive assemblies with audited origins.

- [ ] **Step 5: Commit release metadata**

```powershell
git add -- package.json scripts adapter/src/UnityDebugger.Adapter/Diagnostics/BuildIdentity.cs tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/BuildIdentityTests.cs tests/package/vsix.test.mjs third-party/runtime-assemblies.json third-party/sources.json THIRD_PARTY_NOTICES.md
git commit -m "build: prepare traceable 0.3.0 package"
```

### Task 7: Run every automated gate and package one VSIX

**Files:**
- No intended source edits; fix only defects demonstrated by a failing gate, beginning with a failing regression test.

- [ ] **Step 1: Run source checks and focused adapter tests**

```powershell
git diff --check
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: no whitespace errors; all adapter tests pass.

- [ ] **Step 2: Run extension/build/integration tests**

```powershell
npm run typecheck
npm run test:build
npm run test:extension
npm run test:integration
```

Expected: all pass.

- [ ] **Step 3: Build and verify the VSIX**

```powershell
npm run package
npm run test:package
npm run verify:vsix
```

Expected: one `dist/unity-debugger-pure-0.3.0.vsix`, correct runtime inventory, no test binaries, no old engine DLLs, and a valid build-info file.

- [ ] **Step 4: Hash and inspect the artifact**

```powershell
Get-FileHash 'dist\unity-debugger-pure-0.3.0.vsix' -Algorithm SHA256
Get-Item 'dist\unity-debugger-pure-0.3.0.vsix' | Select-Object FullName,Length,LastWriteTime
git status --short
```

Record the hash, build ID, test counts, and remaining intended worktree changes. Do not install if any gate failed.

### Task 8: Install one 0.3.0 build and reload Cursor

**Files:**
- External install only; no repository mutation.

- [ ] **Step 1: Verify the exact Cursor CLI path and current installed versions**

```powershell
$cursorCli = Join-Path $env:LOCALAPPDATA 'Programs\cursor\resources\app\bin\cursor.cmd'
if (!(Test-Path -LiteralPath $cursorCli)) { throw 'Cursor CLI not found.' }
& $cursorCli --list-extensions --show-versions | Select-String '^kpk\.unity-debugger-pure@'
```

- [ ] **Step 2: Install the verified VSIX**

```powershell
& $cursorCli --install-extension 'D:\Unity\unity-debugger-vscode\dist\unity-debugger-pure-0.3.0.vsix' --force
```

Expected: CLI reports successful installation of 0.3.0.

- [ ] **Step 3: Verify installed files before reload**

Check `C:\Users\Admin\.cursor\extensions\kpk.unity-debugger-pure-0.3.0`, read its package version/build-info, and hash `UnityDebuggerPure.exe`. The build ID and executable hash must match the staged artifact.

- [ ] **Step 4: Reload the existing sgproj Cursor window**

Use the task-authorized Computer Use to run `Developer: Reload Window` in the existing Cursor window opened for `D:\workspace\sgproj`. Do not launch an isolated Cursor profile.

- [ ] **Step 5: Confirm the active adapter build**

Start one attach request, then read the newest sanitized adapter diagnostic log. It must report version 0.3.0 and the packaged build ID before functional acceptance begins.

### Task 9: Accept against existing reachable sgproj code

**Files:**
- Read-only target: `D:\workspace\sgproj\Assets\Plugins\Common\Component\UIButton.cs`.
- Existing prefab evidence includes `D:\workspace\sgproj\Assets\UI\Prefabs\UI_Team\Form_Team.prefab` and other prefabs referencing script GUID `83f985a60fba473489a3fa544557c4ee`.

- [ ] **Step 1: Verify Unity project health and an active UIButton instance**

Use Unity MCP to read compilation/Console state and query the active scene/object graph for a live `UIButton`. If no active instance exists, use the normal sgproj PlayTest/UI flow to open a screen already containing the component; do not modify source or synthesize a test object.

Success condition: a concrete active object and user action are identified that call `UIButton.OnPointerDown`, which reaches `IsPressableState()` at lines 92–96.

- [ ] **Step 2: Set the first breakpoint and attach**

In the existing sgproj Cursor window, set a breakpoint on the return expression in `UIButton.IsPressableState()`, select `Attach to Unity Debugger Pure`, and start debugging.

Trigger the verified UI action. Success: the very first hit changes Cursor from a breakpoint circle to the current-statement marker, and diagnostics record one Breakpoint stop with its DAP breakpoint ID.

- [ ] **Step 3: Verify enum Hover and Watch**

While stopped, Hover `currentState`, `ButtonState.Normal`, and the full expression `currentState == ButtonState.Normal`. Add the full expression to Watch.

Success: Hover and Watch both display a boolean result with no `Expression evaluation failed`; enum values display their enum names.

- [ ] **Step 4: Verify Locals, Getter, ToString, and expansion**

Expand `this`, `currentState`, Unity object members, and at least one available property that uses a Getter/`ToString()`. Toggle `unityDebuggerPure.enableImplicitEvaluation` at workspace scope for a second attach and verify automatic contexts become safe while Watch stays explicit.

Success: no `Variable collection is no longer available`, Getter errors remain individual values, and no debugger session warning/corruption occurs.

- [ ] **Step 5: Verify rapid stepping and marker continuity**

Perform Step In followed by repeated Step Over clicks as soon as Cursor re-enables the command, then Step Out. Observe every real stop.

Success: no `Step over requires a stopped target` warning, no ignored control response, and every actual stopped target has a current-statement marker and usable stack/variables.

- [ ] **Step 6: Verify Domain Reload persistence**

Use Unity MCP to exit and re-enter Play Mode through the same normal sgproj PlayTest entry established in Step 1, with the project's ordinary Domain Reload setting unchanged. State that exact PlayTest entry before changing Play Mode, and read Console after the reload. Do not edit or touch a script merely to force compilation.

Success: the breakpoint remains visible, unload/rebind diagnostics occur in order, the breakpoint becomes bound again, and the next reachable action stops normally.

- [ ] **Step 7: Verify detach/reconnect and collect evidence**

Disconnect, attach again, hit the same reachable breakpoint, and read the final diagnostic log. Capture only the relevant Cursor marker/variables screenshots and sanitized event-order log.

Success: clean detach, one termination event, successful reattach, and no new Cursor/Unity errors.

### Task 10: Final verification and handoff

**Files:**
- Review-only unless acceptance exposes a reproducible defect.

- [ ] **Step 1: If real acceptance exposes a defect, return to RED**

Write the smallest automated regression reproducing the observed DAP/event/evaluation failure, verify it fails for the observed reason, implement one fix, rerun focused tests, then repeat Tasks 7–9. Do not patch by log intuition alone.

- [ ] **Step 2: Re-run final automated verification after any acceptance fix**

```powershell
npm run test
npm run package
npm run test:package
git diff --check
```

Expected: all pass and the installed VSIX matches the newly verified artifact. If the artifact changed, reinstall and reload before claiming acceptance.

- [ ] **Step 3: Report exact outcome**

Report version, build ID, VSIX SHA-256, installed path, automated test counts, sgproj acceptance results for each scenario, and any remaining evidence-backed reference difference. Do not label untested behavior complete.
