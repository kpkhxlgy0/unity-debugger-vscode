# VSTU User-Visible Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Unity Debugger Pure's user-visible debugger experience match the official Microsoft `VisualStudioToolsForUnity.vstuc` 1.3.2 extension for local Unity/Tuanjie 2022.3 Mono debugging, without adopting its dependencies, telemetry, or binaries.

**Architecture:** Two deployable units that ship together. The C# debug adapter (`adapter/`, xunit) owns the DAP contract; the TypeScript extension (`extension/`, vitest) owns discovery, configuration, and UI. Every reference behaviour is pinned by a test derived from observed reference evidence, never from preference. Documentation carries the compatibility matrix that `AGENTS.md` requires.

**Tech Stack:** TypeScript + esbuild + vitest 4.1.11 (extension, Node 24.17.0 installed); C# `net48` + xunit + `dotnet test` on SDK 10.0.401 (adapter); vendored MIT `Mono.Debugging*`/NRefactory session stack; `@vscode/vsce` for packaging.

**Spec:** [`docs/superpowers/specs/2026-10-16-vstuc-user-visible-alignment-design.md`](../specs/2026-10-16-vstuc-user-visible-alignment-design.md)

## Global Constraints

Copied verbatim from the spec. Every task's requirements implicitly include this section.

- No C# Dev Kit dependency, and no `ms-dotnettools.*` runtime dependency.
- No Unity-side package requirement.
- No telemetry and no network egress other than the debug transport and the multicast discovery socket.
- No Microsoft/SyntaxTree binary in the VSIX; the existing package audit and runtime inventory continue to enforce this.
- The sanitized diagnostics allowlist stays closed: no expression text, variable values, or source contents in logs.
- Debug type stays `unity-debugger-pure`; publisher `kpk`; extension name `unity-debugger-pure`.
- Platform stays Windows x64; `os: ["win32"]`, `extensionKind: ["workspace"]`.
- Advertised exception filter ids become `all` and `unhandled`; `uncaught` continues to be accepted on input.
- Exact reference strings for the aligned messages (from the reference `out/nls.bundle.json`):
  `Unity is not running for the current project`, `No Unity endPoint to connect to`,
  `No Process to attach to`. **Singular.** An earlier revision of this very list said
  "No processes to attach to", which the bundle does not contain — a list asserting exactness
  is the worst place to carry a paraphrase, and this one was found only by grepping the
  corrected string back across the plan after fixing Task 6's Interfaces block.
- `logFile` is implemented, not stubbed.
- Dead domain-reload code is deleted, not wired up.
- Environment deviation, recorded not hidden: the repo declares Node `>=26.5.0` (`.node-version` 26.5.0) but **24.17.0** is installed. `dotnet --version` is **10.0.401**, exactly the `global.json` pin. `npx vitest run` was verified working under Node 24; do not "fix" Node.

## Review Focus

Input classes and failure modes the spec implies but that no task's tests would otherwise cover. Each line's test is added to the owning task.

1. **`endPoint` pointing at a host that is not a Unity debugger, or is unreachable.** A reasonable person expects a clear connection error, not a hang or a silent no-op. → Task 2.
2. **`projectPath` naming a directory that is not a Unity project** (no `Packages/`, no `Library/`). Expect the "Unity is not running for the current project" path, not a crash or an unhandled rejection. → Task 6.
3. **Two Players on different hosts that advertise the same `debuggerPort`.** Both carry `processId: 0`, so a `processId:port` dedupe key collapses them into one target. Expect two. → Task 5.
4. **A Player advertisement with `debug` absent or `0`.** Expect it to be filtered out, not offered as an attachable target. → Task 5.
5. **`endPoint` supplied while an Editor is also discoverable.** Expect `endPoint` to win and discovery to be skipped entirely (the reference short-circuits the same way). → Task 7.

---

## File Structure

| File | Responsibility | Task |
| --- | --- | --- |
| `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs` | DAP request translation; owns the capability block and filter handling | 1, 3 |
| `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs` | Strict validation of the private attach arguments | 2, 3 |
| `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs` | Mature-session bridge; currently enforces loopback | 2 |
| `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs` | Allowlisted, path-redacted adapter log; gains a secondary file sink | 3 |
| `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs` | Logical breakpoint store; loses its dead reload API | 4 |
| `adapter/src/UnityDebugger.Adapter/Backend/UnavailableDebuggerBackend.cs` | Placeholder backend; loses undeclared events | 4 |
| `extension/src/model.ts` | Shared extension types; host widens from a literal to `string` | 5, 7 |
| `extension/src/editorDiscovery.ts` | UDP discovery; begins surfacing Player targets | 5, 6 |
| `extension/src/projectVersion.ts` | `ProjectVersion.txt` reader; becomes tolerant of a missing file | 6 |
| `extension/src/diagnostics.ts` | Extension-side command ids and log access; gains the aligned message constants | 6 |
| `extension/src/debugConfigurationProvider.ts` | Attach resolution; gains `endPoint`/`projectPath` and the aligned messages | 6, 7 |
| `extension/src/attachTargetPicker.ts` | **New.** The `attachUnityDebugger` QuickPick and its label/filter rules | 8 |
| `extension/src/functionBreakpointWarning.ts` | **New.** The reference's function-breakpoint performance warning | 9 |
| `extension/src/unityApiReference.ts` | **New.** Selection → Unity ScriptReference URL | 10 |
| `extension/src/outputChannel.ts` | **New.** The extension's `LogOutputChannel` | 11 |
| `extension/src/productIdentity.ts` | Single source of truth for ids; gains command and setting ids | 8, 10, 11 |
| `extension/src/extension.ts` | Activation; registers commands, the picker, and the warning | 8–11 |
| `package.json` | Contributed commands, settings, debugger attributes | 7, 8, 9, 10 |
| `docs/reference-debugger-compatibility.md` | The required compatibility matrix and evidence log | 12 |
| `AGENTS.md`, `README.md`, `docs/superpowers/specs/2026-08-06-*.md` | Reference identity, acceptance project, security disclosure, amended scope | 12 |

---

### Task 1: Align the adapter's advertised capabilities and exception filters

The reference's `initialize` body was observed live from the shipped adapter. This task makes ours match it field by field.

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:51-74` (capabilities), `:419-467` (set-exception-breakpoints)
- Test: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceCapabilityTests.cs`

**Interfaces:**
- Produces: an `initialize` response whose body matches the reference for every field listed in the spec's capability table; `exceptionBreakpointFilters` items are `{ filter, label, description, default, supportsCondition, conditionDescription }`.
- Consumes: the existing `DapTestProtocol` harness already used by `ReferenceCapabilityTests`.

- [ ] **Step 1: Write the failing test**

Add to `ReferenceCapabilityTests.cs` a test that drives a real `initialize` through `DapTestProtocol` and asserts the exact body:

```csharp
[Fact]
public async Task InitializeBodyMatchesObservedReference()
{
    var body = await protocol.InitializeAsync();
    Assert.False(body.SupportsExceptionOptions);
    Assert.True(body.SupportsExceptionFilterOptions);
    Assert.True(body.SupportsExceptionConditions);
    Assert.False(body.SupportsHitConditionalBreakpoints);
    Assert.Equal(2, body.ExceptionBreakpointFilters.Length);
    Assert.Equal("all", body.ExceptionBreakpointFilters[0].Filter);
    Assert.Equal("All Exceptions", body.ExceptionBreakpointFilters[0].Label);
    Assert.Equal("Break on all exceptions", body.ExceptionBreakpointFilters[0].Description);
    Assert.True(body.ExceptionBreakpointFilters[0].SupportsCondition);
    Assert.Equal("List of exceptions ...", body.ExceptionBreakpointFilters[0].ConditionDescription);
    Assert.False(body.ExceptionBreakpointFilters[0].Default);
    Assert.Equal("unhandled", body.ExceptionBreakpointFilters[1].Filter);
    Assert.Equal("User-Unhandled Exceptions", body.ExceptionBreakpointFilters[1].Label);
    Assert.Equal("Break only on unhandled exceptions", body.ExceptionBreakpointFilters[1].Description);
    Assert.NotNull(body.Pid);
}
```

Adapt the accessor names to the harness and `Capabilities` type already in use; if the harness exposes raw JSON instead, assert on the parsed JSON property names from the spec.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~InitializeBodyMatchesObservedReference"`
Expected: FAIL — `supportsExceptionOptions` is currently `true`, the second filter id is `uncaught`, and the description/condition fields are absent.

- [ ] **Step 3: Implement the capability block**

In `UnityDebugSession.cs`, in the `Capabilities` initializer: delete `supportsExceptionOptions`; add `supportsExceptionFilterOptions = true` and `supportsExceptionConditions = true`; replace both `ExceptionBreakpointsFilter` entries with the full six-field form above.

**Also accept `filterOptions[].filterId`.** This is required, not optional scope creep: the client switches request shape on the very capability you are advertising. VS Code/Cursor `sendExceptionBreakpoints` reads `capabilities.supportsExceptionFilterOptions ? { filters: [], filterOptions: e.map(...) } : { filters: e.map(...) }` (verified in `%LOCALAPPDATA%\Programs\cursor\resources\app\out\vs\workbench\workbench.desktop.main.js`), so once the capability is advertised the client sends an **empty** `filters` array with the ids in `filterOptions`. A handler that reads only `filters` sets no exception breakpoints at all and silently disables both filters. Read the ids from `filters` and from `filterOptions[].filterId`; ignore `condition` (recorded as a divergence).

In `SetExceptionBreakpoints`, accept `all`, `unhandled`, **and** `uncaught` (mapping `uncaught` to the same mode as `unhandled`); keep the existing `2030` error only for genuinely unknown ids.

**Do not edit vendored source.** The vendored `VSCodeDebug` types cannot express this body: `Capabilities` lives in `vendored/vscode-mono-debug/DebugSession.cs` (not `Protocol.cs`), has no `supportsExceptionFilterOptions`/`supportsExceptionConditions`/`pid`, and its non-nullable `supportsExceptionOptions` is always serialized, so "removed" is unreachable; `ExceptionBreakpointsFilter` is immutable with only `filter`/`label`/`default`. Add adapter-owned DAP model types instead, following the repository's existing local-model convention (`DapSource`, `DapBreakpoint`, `DapStackTraceResponseBody`), and keep `scripts/verify-third-party.mjs` passing. Creating such a file is the expected solution here, not a deviation.

For `pid`: set it to the adapter's own process id. If the vendored type cannot carry it either, the same adapter-owned model carries it; verify on a real framed DAP exchange that the emitted value equals the spawned adapter's own PID, matching the reference's semantics. If instead you cannot carry `pid` at all, say so explicitly — never omit it silently.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~ReferenceCapabilityTests"`
Expected: PASS, and no previously passing capability test regresses.

- [ ] **Step 5: Commit**

```bash
git add adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceCapabilityTests.cs
git commit -m "fix: align initialize capabilities with the reference adapter"
```

---

### Task 2: Accept a non-loopback attach host

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs`, `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs:44-48`
- Test: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs`, and `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggingBackendLifecycleTests.cs` (its `AttachRejectsANonLoopbackTargetBeforeCreatingAFacade` asserts exactly the behaviour this task removes)

**Interfaces:**
- Consumes: `__host` from the extension (Task 7 supplies it from `endPoint`).
- Produces: `AttachArguments` accepts a non-loopback **IP literal**; `MonoDebuggingBackend` no longer rejects non-loopback addresses. A reachable-but-refusing address still surfaces `DebuggerBackendException` carrying `Failed to attach debugger to {host}:{port}`.
- Produces: a **non-IP** host, or a missing/zero port, is rejected with an explicit error.
- **Out of scope: host names.** The reference resolves nothing — it answers `success:true` and attaches to nothing for `localhost:1`, `unity-box.local:1`, or garbage (see the spec's `endPoint` table). Do **not** add host-name resolution, do **not** change `AttachTarget` in `BackendModels.cs`, and do **not** add an `IPAddress` constructor overload. Rejecting a host name loudly is a deliberate divergence in our favour, recorded in the spec.

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("127.0.0.1")]
[InlineData("192.168.1.50")]
[InlineData("10.30.154.73")]
public void AttachArgumentsAcceptsAnyIpLiteral(string host)
{
    var arguments = AttachArguments.Parse(ValidJson(host: host, port: 56234));
    Assert.Equal(host, arguments.Host);
}
```

Add three more tests:

```csharp
[Theory]
[InlineData("unity-box.local")]   // a host name is NOT resolved, matching the reference
[InlineData("not a valid endpoint")]
[InlineData("")]
public void AttachArgumentsRejectsANonIpHost(string host)
{
    Assert.Throws<ArgumentException>(
        () => AttachArguments.Parse(ValidJson(host: host, port: 56234)));
}
```

and one asserting that an unreachable **non-loopback** address produces a `DebuggerBackendException` (not a hang, and never a silent success) by pointing `MonoDebuggingBackend` at a closed port with a short timeout, asserting the thrown type **and** that the message is exactly `` $"Failed to attach debugger to {host}:{port}" ``. Add the zero/missing-port rejection as the fourth.

Do not reproduce the reference's silent no-op: with an endpoint it cannot parse, the reference returns `success:true` and attaches to nothing. Failing loudly here is intentional.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~AttachArgumentsTests"`
Expected: FAIL — non-loopback hosts are rejected today, and the failure message is not that text.

- [ ] **Step 3: Implement**

In `AttachArguments.cs`, replace the loopback assertion with a check that the host parses as an `IPAddress` and the port is non-zero, throwing `ArgumentException` otherwise, and keep the existing `AttachArguments` → `AttachTarget` conversion untouched so `BackendModels.cs` needs no change. In `MonoDebuggingBackend.cs:44-48`, remove the loopback rejection, and make the connection-failure path throw with the observed message format. Keep every other validation and retry behaviour unchanged. Then update `MonoDebuggingBackendLifecycleTests.AttachRejectsANonLoopbackTargetBeforeCreatingAFacade` to the new behaviour — retarget it rather than deleting it, since "no facade is created before validation passes" is worth keeping.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~AttachArgumentsTests"`
Expected: PASS. Then run the full adapter suite once before committing.

- [ ] **Step 5: Commit**

```bash
git add adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs \
        adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggingBackend.cs \
        tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs \
        tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggingBackendLifecycleTests.cs
git commit -m "feat: accept a non-loopback attach endpoint"
```

Every changed file must be staged. Check `git status --short` afterwards and confirm nothing of yours is left uncommitted.

---

### Task 3: Honor the `logFile` attach property

The reference declares `logFile` and never reads it. We implement it: the adapter mirrors its existing allowlisted log to the requested path.

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs`, `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs`, `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Test: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/DiagnosticLogTests.cs`

**Interfaces:**
- Produces: `DiagnosticLog.OpenSecondary(string path)` — opens an additional append stream; every subsequent allowlisted line is written to both the default file and the secondary file; idempotent for the same path.
- Produces: `AttachArguments.ParseLogFile(JObject?)` returning a nullable `string`, read from the `__logFile` attach key. **As delivered** — the brief originally named this `AttachArguments.LogFile`, but parsed request values live on `AttachTarget` in `Backend/BackendModels.cs`, which this task's file list excludes and which an earlier ruling forbids changing, so the implementer kept it a static parser in `AttachArguments`. Accepted: it solves the same problem without contradicting a standing ruling. Task 7 consumes the **`__logFile` wire key**, not a member name, so nothing downstream depends on the literal.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void OpenSecondaryMirrorsSubsequentLinesAndPreservesTheAllowlist()
{
    var log = DiagnosticLog.CreateForTest(tempDir);
    log.OpenSecondary(secondaryPath);
    log.Write("event", "attach");
    log.Write("expressionText", "secret");   // not on the allowlist
    log.Dispose();

    var secondary = File.ReadAllLines(secondaryPath);
    Assert.Contains(secondary, line => line.Contains("event=attach"));
    Assert.DoesNotContain(secondary, line => line.Contains("secret"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~DiagnosticLogTests"`
Expected: FAIL — `OpenSecondary` does not exist.

- [ ] **Step 3: Implement**

Add `OpenSecondary` to `DiagnosticLog` reusing the existing allowlist gate (`DiagnosticLog.cs:21-42`, `:129-137`) so unknown fields are still rejected on both sinks. Add `LogFile` to `AttachArguments`. In `UnityDebugSession.Attach`, when `__logFile` is present, call `OpenSecondary` and log `event=logFile.redirect` with no path value. Mirroring starts at attach because the log is created in `Program.Main` before any request is read; record that reason in a comment.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore --filter "FullyQualifiedName~DiagnosticLogTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/DiagnosticLogTests.cs
git commit -m "feat: honor the logFile attach property"
```

---

### Task 4: Delete the dead domain-reload and reconnect code

The reference has no domain-reload-specific code; our rebinding is owned by the mature vendored session (matrix row RLD-01 was verified `aligned` on 0.3.0).

**The premise "these members have zero callers" is wrong as written, and the real reference map was verified before dispatching this task.** Use this instead of rediscovering it, and treat it as the expected output of Step 1:

| Symbol | Real status |
| --- | --- |
| `UnityDebugSession.NotImplemented(Response)` (`:832`) | **No references anywhere** — not in `adapter/src`, not in `tests`. Genuinely dead; safe to delete. |
| `BreakpointManager.MarkAllPending` (`:231`), `BeginReload` (`:251`), `CompleteReload` (`:264`) | No production callers. `MarkAllPending` is reached only from `BeginReload` (`:261`), so the three form a closed, unreferenced group. |
| `BreakpointManager.RebindAll` (`:273`), `RebindPending` (`:291`) | **No production callers, but referenced by tests**: `BreakpointManagerTests.cs:235` and `:268` (`RebindPending_preserves_id_and_retries_only_pending`). They are not "zero caller" — they are test-only. |
| `UnavailableDebuggerBackend:18-21` (`ReloadStarted`, `ReloadProgress`, `ReloadCompleted`, `ReconnectFailed`) | Declared under `#pragma warning disable CS0067` and **never raised, and never subscribed to anywhere in `adapter/src`**. Note they are **not** on `IDebuggerBackend`, whose events are only `Stopped`, `ThreadChanged`, `ModuleChanged`, `BreakpointChanged`, `Output`, `Terminated` — so they are extra, unreferenced surface on a concrete class. |
| `Continued` | **Not authorized by this task.** It is also absent from `IDebuggerBackend`, and the fakes raise it in timing-sensitive paths (`ExecutionControlTests.cs:118,194`, `InspectionRequestTests.cs:443`). If you find it is production-dead, report that as a separate finding rather than folding it in. |

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs:231-309`, `adapter/src/UnityDebugger.Adapter/Backend/UnavailableDebuggerBackend.cs:11-21`, `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:835-838`, `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs:26-37`, `tests/adapter/UnityDebugger.Adapter.Tests/Breakpoints/BreakpointManagerTests.cs`

**Interfaces:**
- Produces: `BreakpointManager` keeps its binding API (`ReplaceForSource`, `BindingGeneration`) and loses `MarkAllPending`, `BeginReload`, `CompleteReload`, `RebindAll`, `RebindPending` **only if** they are unreferenced after the check below.

- [ ] **Step 1: Confirm the reference set**

Run: `git grep -n -E "MarkAllPending|BeginReload|CompleteReload|RebindAll|RebindPending|ReloadStarted|ReloadProgress|ReloadCompleted|ReconnectFailed|NotImplemented\(" -- adapter/src tests`

Expected output is the table above. **If any production call site exists for anything you intend to delete, stop and report it — do not delete.** Also search for subscribers, not just names: `git grep -n -E "ReloadStarted|ReloadProgress|ReloadCompleted|ReconnectFailed" -- adapter/src | Select-String '\+='` returns nothing today, and if that changes, the events are live.

- [ ] **Step 2: Delete and re-run the affected suites to prove nothing depended on them**

Delete the dead members and the unit tests that only exercise them. Run: `dotnet test UnityDebugger.sln -c Release --no-restore`
Expected: PASS with a reduced test count. If any test fails, the member was live — restore it and report.

**Report the coverage you removed, explicitly.** Deleting `RebindAll`/`RebindPending` means deleting the `RebindPending_preserves_id_and_retries_only_pending` test and the `RebindAll` reference at `BreakpointManagerTests.cs:235`. Name every test you delete in your report and state why the loss is acceptable — the methods are production-unreachable, so the property they assert cannot be exercised by the product. Do **not** delete a test whose subject survives, and do not quietly drop an assertion to make the suite green. Give the before and after test counts so the delta is checkable.

- [ ] **Step 3: Commit**

```bash
git add adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs \
        adapter/src/UnityDebugger.Adapter/Backend/UnavailableDebuggerBackend.cs \
        adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs \
        tests/adapter/UnityDebugger.Adapter.Tests/Breakpoints/BreakpointManagerTests.cs \
        tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs
git commit -m "refactor: remove dead domain-reload and reconnect code"
```

Do **not** use `git add -A` here: the six pre-existing `adapter/vendor/*/packages.lock.json` modifications must stay uncommitted.

---

### Task 5: Widen the host type and surface Player targets

**Files:**
- Modify: `extension/src/model.ts:18`, `:29`, `extension/src/editorDiscovery.ts:110-126`, `extension/src/playerAdvertisement.ts`, `extension/src/publicApiTypes.ts`
- Test: `tests/extension/editorDiscovery.test.ts`, `tests/extension/playerAdvertisement.test.ts`, `tests/extension/attachRequestRegistry.test.ts`, `tests/extension/debugConfigurationProvider.test.ts`, `tests/extension/publicApi.test.ts`

**`publicApiTypes.ts` is a ninth file, and the earlier enumeration missed it.** `PublicTargetSource` is declared as `"advertisement" | "derived-port"` — a union that **mirrors `EditorCandidate.source`**. Adding `"player-advertisement"` to the candidate without adding it here breaks the assignment at `attachRequestRegistry.ts:51` (`source: scopedCandidate.source`), because the wider union is not assignable to the narrower one. Add the new member to `PublicTargetSource`.

The generalisable form, since this is the second enumeration gap in this task: widening a union in one type requires widening **every exported type that mirrors it**, and a construction-site grep does not find those — they are assignments, not literals. Grep for the *member name* (`"derived-port"`) rather than for the type.

What genuinely does **not** need changing: `PublicEditorTarget` carries `targetId`, `processId`, `projectName`, `workspaceRoot`, `projectVersion` and `source` — it has **neither `host` nor `machine`**, so widening `EditorCandidate.host` does not touch it and it does not need `machine` added. The picker works from `EditorCandidate`, which it already receives.

**Interfaces:**
- Produces: `EditorCandidate.host: string` and `UnityAttachConfiguration.__host: string` (was the literal `"127.0.0.1"`).
- Produces: `EditorCandidate.source` gains the value `"player-advertisement"`.
- Produces: `EditorCandidate.machine: string` — the machine label used by Task 8's picker label. For an Editor it is the local host name; for a Player it comes from the advertisement's `[Id]` field.
- Produces: `PlayerAdvertisement.machine: string`, parsed from `[Id]`. The live record is `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)`; the machine is the second comma-separated component inside the parentheses. When `[Id]` does not match `^([^()]*)\(([^,]*),([^)]*)\)$`, `machine` is the advertised `ip`.
- Consumes: `parsePlayerAdvertisement` (`playerAdvertisement.ts`) — this task changes it.

- [ ] **Step 1: Write the failing tests**

```ts
it("offers a player advertisement as its own target", async () => {
  const discovery = new EditorDiscovery({
    collectAdvertisements: async () => [
      { ip: "192.168.1.77", debuggerPort: 56001, projectName: "Other", id: "p1", allowDebugging: true },
    ],
  });
  const found = await discovery.discover(["H:\\fixture"]);
  expect(found).toContainEqual(expect.objectContaining({
    host: "192.168.1.77", port: 56001, source: "player-advertisement",
  }));
});

it("does not offer a player that has not enabled debugging", async () => {
  // same shape, allowDebugging: false -> absent from the result
});

it("keeps two players on different hosts that share a port", async () => {
  const discovery = new EditorDiscovery({
    collectAdvertisements: async () => [
      { ip: "192.168.1.77", debuggerPort: 56001, projectName: "A", id: "p1", allowDebugging: true },
      { ip: "192.168.1.78", debuggerPort: 56001, projectName: "B", id: "p2", allowDebugging: true },
    ],
  });
  const found = await discovery.discover(["H:\\fixture"]);
  expect(found.filter((c) => c.source === "player-advertisement")).toHaveLength(2);
});
```

The third test must fail before the change: both players carry `processId: 0`, so the current `${processId}:${port}` key collapses them.

Add two more tests, both driven by the live advertisement captured during planning:

```ts
it("parses the machine label out of the [Id] field", () => {
  const parsed = parsePlayerAdvertisement(
    "[IP] 10.30.154.73 [Port] 55000 [Flags] 19 [Guid] 3598034322 [EditorId] 2737672203 " +
    "[Version] 1048832 [Id] AndroidPlayer(11,samsung_SM-S9260@10.30.154.73) [Debug] 0 " +
    "[PackageName] AndroidPlayer [ProjectName] 头号禁区",
  );
  expect(parsed.machine).toBe("samsung_SM-S9260@10.30.154.73");
  expect(parsed.allowDebugging).toBe(false);
});

it("does not offer an advertisement that carries no debugger port", async () => {
  // the same real record: it has [Port] 55000 but no [DebuggerPort], so
  // parsePlayerAdvertisement yields debuggerPort 0 and discovery must omit it
  const found = await discovery.discover(["H:\\fixture"]);
  expect(found.filter((c) => c.source === "player-advertisement")).toHaveLength(0);
});
```

The second test encodes the recorded divergence: the reference attaches to that exact player by obtaining a debugger port through the device (adb port forwarding, `/proc/net/tcp`, usbmuxd), which the spec puts out of scope. Offering a target we cannot attach to would be worse than omitting it, so the explicit `endPoint` path (Task 7) is the supported way to reach a player. State this in the test's comment so the omission is never read as a bug.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx vitest run tests/extension/editorDiscovery.test.ts`
Expected: FAIL — the first two because players are dropped, the third only if dedupe is already keyed by port (confirm the current key before asserting).

- [ ] **Step 3: Implement**

In `playerAdvertisement.ts`, parse `machine` from `[Id]` per the rule in Interfaces. The rule was verified against the live record: `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)` yields `samsung_SM-S9260@10.30.154.73`, exactly the reference's `machine`, and an `[Id]` with no parentheses correctly fails to match and falls back to the advertised `ip`. Widen `host`/`__host` to `string` in `model.ts` and add `machine: string` to `EditorCandidate`. In `discover`, set `machine: os.hostname()` (import `node:os`) on Editor candidates — **use `os.hostname()`, which returns `xiaolonghan` here and matches the reference's label exactly; do not use `process.env.COMPUTERNAME`, which is upper-cased to `XIAOLONGHAN` and would not match** — then append one candidate per remaining advertisement (those not consumed as the current Editor's port source) that has `allowDebugging === true` and a positive `debuggerPort`, with `source: "player-advertisement"`, `machine` from the advertisement, `workspaceRoot` set to the first root, `processId: 0`, and `projectVersion: ""`. Adding a required field to `EditorCandidate` breaks every existing literal. The **complete** set was enumerated by grep before dispatch, so use this list rather than searching for one:

| Site | Needs |
| --- | --- |
| `tests/extension/editorDiscovery.test.ts:69,96,111,158,181` | add `machine` to five literals |
| `tests/extension/attachRequestRegistry.test.ts:5,84` | add `machine` to two literals |
| `tests/extension/debugConfigurationProvider.test.ts:8` | add `machine` |
| `tests/extension/publicApi.test.ts:21` | add `machine` |
| `extension/src/attachRequestRegistry.ts:36` | **no change needed** — it builds its scoped candidate with `...candidate` and overrides only `workspaceRoot`, so a new required field flows through. Do not edit it, and do not widen `PublicEditorTarget` to carry `machine`: Task 8's label function takes an `EditorCandidate`, which the picker already has |

Update those five test files' literals before running the suite. Change the dedupe key from `${processId}:${port}` (`editorDiscovery.ts:75`) to `${host}:${port}`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension`
Expected: PASS, including the pre-existing `editorDiscovery` and `debugConfigurationProvider` suites.

- [ ] **Step 5: Commit**

```bash
git add extension/src/model.ts extension/src/editorDiscovery.ts extension/src/playerAdvertisement.ts \
        extension/src/publicApiTypes.ts \
        tests/extension/editorDiscovery.test.ts tests/extension/playerAdvertisement.test.ts \
        tests/extension/attachRequestRegistry.test.ts tests/extension/debugConfigurationProvider.test.ts \
        tests/extension/publicApi.test.ts
git commit -m "feat: surface Unity player targets from discovery"
```

**An earlier revision of this step staged only three of those nine paths**, omitting `playerAdvertisement.ts` — which holds the `machine` parsing this task's own Interfaces promise, and which Task 8 consumes — plus four test files and `publicApiTypes.ts`. That would have committed a tree that does not satisfy its own requirements. Run `git status --short` after staging and confirm nothing you edited is left unstaged; the six `adapter/vendor/*/packages.lock.json` files are the only entries that should remain, and they must stay uncommitted.

---

### Task 6: Discovery robustness and the aligned failure messages

**Files:**
- Modify: `extension/src/projectVersion.ts:13-23`, `extension/src/editorDiscovery.ts`, `extension/src/debugConfigurationProvider.ts:97-113`, `extension/src/diagnostics.ts`

**Line numbers for `editorDiscovery.ts` were deliberately dropped, and here are the current anchors instead.** Task 5 rewrote that file and grew it from **251 to 303 lines**, so an earlier revision's `:89-145` now points into the middle of unrelated code. Trust these symbols, not line numbers:

| What you need | Where it is now |
| --- | --- |
| the `readProjectVersion` call inside Editor discovery | `:137` |
| the `catch` that currently swallows the failure and returns `[]` | `:168` |
| the other `catch` blocks, so you can tell them apart | `:213`, `:226`, `:231`, `:253` |
| the `catch (error)` that already inspects the error | `:300` |

Handle **only** the discovery path this task is about — the one that fails because `readProjectVersion` throws on a project with no `ProjectSettings\ProjectVersion.txt`. The others are unrelated and swallowing more broadly than the brief asks would be a change nobody requested. If you cannot tell which one is which from the symbols, ask rather than widening all of them.

`debugConfigurationProvider.ts:97-113` and `projectVersion.ts:13-23` were re-checked and are still accurate.
- Test: `tests/extension/projectVersion.test.ts`, `tests/extension/debugConfigurationProvider.test.ts`

**Interfaces:**
- Produces: `readProjectVersion` returns `""` when the file is missing or has no `m_EditorVersion` line, instead of throwing.
- Produces: exported constants `UNITY_NOT_RUNNING_MESSAGE = "Unity is not running for the current project"`, `NO_UNITY_ENDPOINT_MESSAGE = "No Unity endPoint to connect to"`, `NO_PROCESSES_MESSAGE = "No Process to attach to"`.

**Two corrections to this block, both verified against the reference's own string bundle (`out/nls.bundle.json`) rather than paraphrased:**

1. **The third message is singular: `No Process to attach to`.** An earlier revision said `"No processes to attach to"` — a string **the reference does not contain**. It was corrected in the spec but not here, so do not reintroduce it. Deviation from the reference's English text is the one thing this work item exists to prevent, and a plausible-sounding paraphrase is exactly how it happens.
2. **Only `UNITY_NOT_RUNNING_MESSAGE` is exercised in this task.** The other two are exported now for **Task 8**, which owns the picker: it shows `NO_UNITY_ENDPOINT_MESSAGE` when no endpoint can be resolved and `NO_PROCESSES_MESSAGE` when the picker is empty. Declaring them here avoids a second edit to the same module later. Say in your report that you exported-but-did-not-use two of the three, so a reader does not go looking for their call sites in this task's diff.
3. **All three constants live in `extension/src/diagnostics.ts`**, which is why that file is in the list above. It is already the home for user-facing strings — it holds `NO_SANITIZED_DIAGNOSTICS` at `:12` alongside the exported command ids — and the File Structure table records it as gaining the aligned message constants. Do not put them in `debugConfigurationProvider.ts`: that file *uses* the messages, and defining them at their first call site would leave Task 8 importing a provider module to reach a string. If you find a reason to disagree, say so in your report rather than choosing silently — a constant's home is exactly the kind of decision that looks obvious to whoever made it and arbitrary to everyone after.

- [ ] **Step 1: Write the failing tests**

```ts
it("survives a project with no ProjectVersion.txt", async () => {
  await expect(readProjectVersion("H:\\fixture")).resolves.toBe("");
});

it("reports Unity not running when nothing is discovered", async () => {
  const configurationUi = ui();
  await provider(async () => [])
    .resolveDebugConfiguration(folder, attach);
  expect(configurationUi.showError).toHaveBeenCalledWith(
    "Unity is not running for the current project",
  );
});
```

Also add a test for Review Focus 2: `projectPath` naming a non-Unity directory resolves to the same "Unity is not running for the current project" message and does not reject.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx vitest run tests/extension/projectVersion.test.ts tests/extension/debugConfigurationProvider.test.ts`
Expected: FAIL — `readProjectVersion` rejects today, and the provider shows "No local debug-enabled Tuanjie or Unity Editor was found. Switch Code Optimization to Debug, then refresh."

- [ ] **Step 3: Implement**

Make `readProjectVersion` tolerant. Keep the existing refresh affordance on the first failure of the *attach* path, but replace both messages with `UNITY_NOT_RUNNING_MESSAGE`. Do not change the "Open a Unity or Tuanjie project folder before attaching." message: it covers a different condition (no folder at all).

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/projectVersion.ts extension/src/editorDiscovery.ts \
        extension/src/debugConfigurationProvider.ts extension/src/diagnostics.ts \
        tests/extension/projectVersion.test.ts tests/extension/debugConfigurationProvider.test.ts
git commit -m "fix: tolerate missing project metadata and align failure messages"
```

An earlier revision staged the whole `tests/extension` directory. Name the two test files explicitly instead: a directory path silently picks up any scratch file another agent leaves there, and the six `adapter/vendor/*/packages.lock.json` files are the only entries that should remain unstaged. Run `git status --short` after staging and confirm nothing you edited is left out.

---

### Task 7: Attach configuration surface

**Files:**
- Modify: `package.json:66-106`, `extension/src/debugConfigurationProvider.ts:39-127`, `extension/src/versionPolicy.ts`, `extension/src/editorDiscovery.ts`, `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs`
- Test: `tests/extension/debugConfigurationProvider.test.ts`, `tests/extension/versionPolicy.test.ts`, `tests/extension/editorDiscovery.test.ts`, `tests/build/scaffold.test.mjs`, `tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs`

**This task spans TypeScript and C#, and you must run both suites** (`npx vitest run tests/extension` and `dotnet test UnityDebugger.sln -c Release --no-restore`). That is deliberate and explained below; it is the only task in this plan that does so.

`versionPolicy.ts` and its test file were added to this list after Task 6 escalated the version-gate finding described below; `AttachArguments.cs` and its test file were added because `endPoint` cannot work without it. `model.ts` is **not** listed because no type change is needed unless you decide `VersionDecision` needs a new level, in which case say so in your report rather than widening the interface silently.

**Interfaces:**
- Consumes: `EditorCandidate.host: string` (Task 5).
- Produces: the provider accepts `endPoint` (`"host:port"`) and `projectPath` (string, `${workspaceFolder}` expanded) and emits `__host`/`__port` from `endPoint` when present.

**`__host` must stop being the literal `"127.0.0.1"`, and this is the one requirement an earlier revision of this brief left implicit.** `toAttachConfiguration` (`debugConfigurationProvider.ts:154-172`) currently hardcodes `__host: "127.0.0.1"` at `:164`. Task 5 just made `EditorCandidate.host` a real per-target value — Editor candidates carry `127.0.0.1`, and Player candidates carry the advertised LAN address — so the correct rule is:

1. `endPoint` present → `__host`/`__port` come from parsing it, as specified above.
2. `endPoint` absent → `__host` comes from **`candidate.host`** and `__port` from `candidate.port`, not from a literal.

Why this is not optional: Task 5's implementer flagged the cross-task window directly. After Task 5 a Player target has `host: "192.168.1.77"` and is offered by discovery; if this task leaves the literal in place, selecting that Player resolves a configuration pointing at **loopback**, so the debugger attaches to whatever Editor is on the local machine instead of the Player the user chose — or fails, if none is listening. That is a silent wrong-host attach, the same failure class as the non-canonical IP defect closed in Task 2, and it would look like "Player support does not work" rather than like an address bug.

**Add a test for it**, since nothing currently pins it: a Player candidate (`host: "192.168.1.77"`, `source: "player-advertisement"`) with **no** `endPoint` must resolve to `__host: "192.168.1.77"`. The existing `endPoint` test covers only the branch where the property is present, which is exactly the branch that already worked.

**An unknown Editor version must not block attaching — this task owns that fix, and it was found by Task 6 escalating rather than overreaching.** Task 6 made the project-version read tolerant, so an Editor whose project has no `ProjectSettings\ProjectVersion.txt` is now discovered with `projectVersion: ""`. It then proved with a throwaway probe (deleted, not committed) that **attach still fails**: `classifyVersion("")` (`versionPolicy.ts:7-14`) tests `""` against `VERSION_PATTERN`, fails, and returns `level: "unsupported"` with the warning `Malformed or unsupported Editor version: ` — note the trailing space where the version should be. `applyCandidatePolicy` (`debugConfigurationProvider.ts:127-133`) rejects `unsupported` outright, so `resolveDebugConfiguration` returns `undefined`. The user sees a message about a malformed version they do not have.

The rule to implement: **an absent version is not a malformed one.** `classifyVersion` must treat an empty version as *unknown* and return a level that does **not** block attaching — `compatible-unverified` is the natural fit, with a warning that says the version could not be determined rather than one that implies the version is wrong. Everything non-empty keeps its current behaviour exactly, including the two `unsupported` branches.

Why this is Task 7's and not a rider on Task 6: it is a semantic change to the compatibility policy affecting **every** candidate, not just Editors with a missing file. Task 5's Player candidates carry `projectVersion: ""` **by design** — a Player's Editor version is genuinely unknown — so before this change every Player from the picker would be rejected at the gate too. Task 6's implementer declined to apply a `=== ""` bypass precisely because it would silently change Player behaviour, which was the right instinct and is why the change needs its own tests. `endPoint` is **not** affected either way: that path skips discovery entirely and never reaches `applyCandidatePolicy`.

Tests to add, in `tests/extension/versionPolicy.test.ts`: `""` yields a non-blocking level; a whitespace-only string is **not** silently treated as absent (decide and state which, rather than letting `trim()` fall out by accident); and the existing malformed and out-of-policy cases still return `unsupported` unchanged — that last one matters most, because the cheap version of this fix loosens the gate for everything.

**One more thing in this task, found by Task 6's reviewer: the `EditorInstance.json` fallback can currently make things *worse*, and after your change it still can.** Task 6's fallback (`editorDiscovery.ts:138-140`) supplies `version` from `EditorInstance.json` as `projectVersion`. The real file reads `2022.3.23f1`, which is fine — but the helper (`:274-286`) accepts **any** string, so a suffix-bearing value of the shape Unity uses elsewhere (`m_EditorVersionWithRevision: 2022.3.62t11 (b1a0a2c)`, the format in Task 6's own fixture) would reach `classifyVersion`, fail `VERSION_PATTERN`, and return `unsupported` — **blocking** an attach that, after your change, an *empty* version would have allowed.

So the fallback can reduce permissiveness in exactly the class of project this work item exists to fix. Make it strictly non-harmful: **supply the extracted version only when it is one the gate can actually use, and otherwise supply `""`.** A version the policy will reject must degrade to *unknown*, which your change now treats as attachable, rather than to *malformed*, which blocks. `editorDiscovery.ts` is added to this task's file list for that reason; keep the guard next to the extraction and export nothing new if you can avoid it.

- Why the guard belongs here rather than in Task 6: the shape the gate accepts is defined by `versionPolicy.ts`, which is this task's file, and the fix is only correct *relative to* your relaxation of it. Task 6 is approved and closed; reopening it to depend on a file it was forbidden to touch would be worse.
- Worth testing, since the failure is silent: a suffix-bearing `version` yields `""` and therefore attaches, while the same suffix-bearing value supplied directly as `__projectVersion` still blocks. Those two facts together are what make the guard's purpose legible.

**`__processId` blocks both new attach paths, and this is the most consequential thing in your task. Read this section before writing any code.**

An earlier revision of this brief **never mentioned `__processId` at all**. It is emitted in exactly one place — `toAttachConfiguration`, `debugConfigurationProvider.ts:159`, as `__processId: candidate.processId` — and the adapter requires it to be a **positive** integer (`AttachArguments.cs:25-28`):

```csharp
var processId = RequiredInteger(arguments, "__processId");
if (processId <= 0)
    throw new AttachArgumentException("__processId must be a positive integer.");
```

Two of this work item's headline features therefore cannot attach as specified:

1. **`endPoint`.** That path skips discovery entirely — your own test asserts `discover` is not called — so there is **no candidate** and therefore no `candidate.processId`. Whatever you do, the field must still be present and positive or the adapter throws. Omitting it gives `"__processId is required."`; sending `0` gives `"__processId must be a positive integer."`. Either way the feature fails at the adapter, after the UI has already accepted it.
2. **Player targets.** Task 5 deliberately sets `processId: 0` for Player candidates, because a Player's process id on this machine is meaningless.

**`__projectVersion` fails the same way, and relaxing only `__processId` would leave both features still broken.** The adapter does not merely parse the version, it **re-imposes the whole compatibility policy in C#** (`AttachArguments.cs:76-93`): `RequiredString(arguments, "__projectVersion")`, then `VersionPattern.IsMatch(projectVersion)` → `"__projectVersion is malformed."`, then a `2022.3.`/`6000.` prefix check → `"The Editor version is outside the version 0.3.2 compatibility policy."`.

So the policy exists in **two** places, and they disagree about `""`:

- `versionPolicy.ts` — the copy you are relaxing in this task, so it stops blocking an unknown version.
- `AttachArguments.cs:76-93` — the copy that still rejects it.

A Player candidate carries `projectVersion: ""`, and the `endPoint` path has no candidate to read a version from at all, so before this change the extension would wave the attach through and the **adapter** would then refuse it with `"__projectVersion is malformed."` — one layer later, with a worse message, and only after the UI had accepted the configuration. Relaxing one copy and not the other produces a feature that looks fixed in the TypeScript tests and fails in the product.

**The fix for that half is the same shape as `__processId`:** accept an **empty** `__projectVersion` as "unknown" and skip both checks for it, while a non-empty value keeps **exactly** today's validation — pattern and policy prefix included. Do not delete the check; the defect is that the two copies disagree about what an absent value means, not that the adapter validates. And do not make the adapter accept an *out-of-policy* version, which is the cheap misreading of this instruction: only the empty case changes.

**`__workspaceRoot` is a third field with no source on the `endPoint` path**, and unlike the other two it does not need the adapter relaxed — `RequiredString` plus a non-empty and valid-path check (`:56-74`) is reasonable, and the value is genuinely available: use the `projectPath` when supplied, otherwise the workspace folder. Just make sure you **emit** it, because `endPoint` skipping discovery means `candidate.workspaceRoot` has nothing behind it, and `RequiredString` throws on a missing field. Add it to the same test that pins `__processId: 0`.

**The fix, and why relaxing the adapter is the right half of it.** `AttachTarget.ProcessId` is **assigned and never read** — a repo-wide search finds it in exactly two places, `BackendModels.cs:16` (the assignment) and `:23` (the property declaration), with no consumer anywhere in the adapter. So the `<= 0` check guards a value that nothing uses, and it is currently blocking two features that have no other obstacle. Change it to accept **zero** while still rejecting negative values, and document `0` as "unknown process id" — which is already the convention Task 5 established for Players, so the two halves agree rather than inventing a second sentinel.

Keep the presence requirement: a missing `__processId` is still an error. Keep rejecting negatives, which are always malformed.

**One existing test asserts the behaviour you are changing, and you must edit it deliberately rather than deleting it to go green.** `AttachArgumentsTests.cs:111-124` is a theory:

```csharp
[Theory]
[InlineData("__processId", 0)]
[InlineData("__port", 0)]
[InlineData("__port", 65536)]
public void Parse_rejects_invalid_numeric_fields(string property, int value)
{
    var json = ValidArguments();
    json[property] = value;
    Assert.Throws<AttachArgumentException>(() => AttachArguments.Parse(json));
}
```

Remove **only** the `[InlineData("__processId", 0)]` row; the two `__port` rows must stay, because the port range is still enforced. Do not weaken the theory to an assertion on the message or the type in order to make it pass, and do not delete the test.

Then add explicit coverage for the behaviour you introduce: `__processId: 0` is **accepted**; a negative `__processId` is rejected; an absent one is rejected. The last two are already covered (`:177-194` removes each field in turn, and negatives have no row yet — add one).

**For `__projectVersion`, check what must *not* change before you touch anything.** Three existing tests pin the checks you are keeping, and all three must still pass unmodified. **They live in `tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs`, and every line number this paragraph used to cite was stale by about fifty lines — verified against the file and corrected here:**

| Test | Line | Pins |
| --- | --- | --- |
| `Parse_accepts_unverified_compatible_versions` | `:189-198` | `2022.3.61f1` / `6000.0.50f1` / `6000.1.4f1` |
| `Parse_rejects_unsupported_versions` | `:203-214` | `2021.3.45f1` and `2023.2.20f1`, asserting the exact policy message |
| `Parse_rejects_malformed_version` | `:217-224` | `"latest"` |

If any of those three needs editing, your relaxation is wider than the empty case and that is a defect — stop and say so.

- **The rule this paragraph states is right and its citations were wrong, which is the worst combination for a brief.** A reader who checks the line numbers finds unrelated tests, concludes the paragraph is stale, and may then discount the *instruction* along with the citation — when the instruction is exactly what prevents a too-wide relaxation. **Stale line numbers do not degrade gracefully; they undermine the claim they decorate.** This is the third time this session a cited range has drifted (round 79's command arrays, the earlier `scaffold.test.mjs` corrections, now here), and the durable fix is to cite the **file and test name** as the primary identifier with lines as a convenience, which is what the table above does.

Add only the new case: an **empty** `__projectVersion` is accepted.

**A method note, because it cost me a wrong claim.** I previously recorded that no test covered the `<= 0` check, having grepped `tests/adapter` for the message `"must be a positive integer"` and found nothing. That was wrong — the theory above covers it and asserts only the **exception type**, so a message grep cannot find it. Grep for the *field name* when checking whether a validation is covered, not for its message.

**And emit it explicitly on both paths, which is the other half.** Your provider must always send `__processId`: `candidate.processId` when there is a candidate, and `0` on the `endPoint` path. Do **not** fabricate a positive placeholder — a made-up pid is worse than an honest "unknown", and it would be indistinguishable from a real one in a log.

Add a TypeScript test that the `endPoint` path resolves to `__processId: 0`, since nothing currently pins the field's presence on that branch and that is exactly how this gap survived the plan.
- Produces: the provider emits `__logFile` whenever the `logFile` property is present and non-empty, so Task 3's adapter-side reader is actually reachable. This is the only producer of that field. **The reader is `AttachArguments.ParseLogFile(JObject?)`, not an `AttachArguments.LogFile` member** — an earlier revision of this line named the latter, which does not exist; Task 3 kept it a static parser because parsed request values live on `AttachTarget` in a file it was forbidden to change. You are emitting a wire key (`__logFile`), so nothing here depends on the member name; the correction is so you do not go looking for a property that is not there.

- [ ] **Step 1: Write the failing tests**

```ts
it("uses endPoint and skips discovery entirely", async () => {
  const discover = vi.fn(async () => [candidate]);   // Review Focus 5
  const resolved = await provider(discover).resolveDebugConfiguration(folder, {
    ...attach, endPoint: "192.168.1.50:56234",
  });
  expect(resolved).toMatchObject({ __host: "192.168.1.50", __port: 56234 });
  expect(discover).not.toHaveBeenCalled();
});

it("expands workspaceFolder in projectPath", async () => {
  // projectPath "${workspaceFolder}/sub" -> discovery called with the expanded root
});

it("passes logFile through as the private log field", async () => {
  const resolved = await provider(async () => [candidate]).resolveDebugConfiguration(folder, {
    ...attach, logFile: "H:\\logs\\unity.log",
  });
  expect(resolved).toMatchObject({ __logFile: "H:\\logs\\unity.log" });
});

it("rejects a malformed endPoint without starting discovery", async () => {
  const discover = vi.fn(async () => [candidate]);
  const configurationUi = ui();
  await provider(discover, configurationUi).resolveDebugConfiguration(folder, {
    ...attach, endPoint: "not-a-host-port",
  });
  expect(configurationUi.showError).toHaveBeenCalled();
  expect(discover).not.toHaveBeenCalled();
});
```

Note on the message split, which mirrors the reference exactly: an empty **discovery** result is `UNITY_NOT_RUNNING_MESSAGE` (Task 6 owns that path, and the reference reaches `unityNotRunningError` the same way); `NO_UNITY_ENDPOINT_MESSAGE` belongs to the no-folder case, which this provider already short-circuits earlier, so do not assert it here.

Add a `tests/build/scaffold.test.mjs` assertion that `configurationAttributes.attach` has `required: []`, the three properties, and `initialConfigurations[0].projectPath === "${workspaceFolder}"`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx vitest run tests/extension/debugConfigurationProvider.test.ts && node --test tests/build/scaffold.test.mjs`
Expected: FAIL — `endPoint`/`projectPath` are ignored and `required` is `[name, type, request]`.

- [ ] **Step 3: Implement**

Add the properties to the manifest with `required: []` and `projectPath` defaulting to `${workspaceFolder}`; add `projectPath` to `initialConfigurations[0]`. In the provider, parse `endPoint` as `host:port` (reject and `showError` on a malformed value), skip discovery when present, use `projectPath` (after `${workspaceFolder}` expansion) as the discovery root, and pass `__logFile` through from `logFile` in `toAttachConfiguration` (`debugConfigurationProvider.ts:154-171`).

**Where these properties go matters, and the reference settles it.** They are **not** settings — they are debug-configuration attributes under `contributes.debuggers[].configurationAttributes.attach.properties`. The reference's `contributes.configuration` holds only five unrelated settings (`enableMessageCompletion`, `unityMessageScope`, `refreshOnSave`, `enableCodeModel`, `displayBackgroundProcesses`), none of which are attach properties. Its verified `attach` shape is:

- `required: []`
- `projectPath`: string, default `${workspaceFolder}`, description `Project Path`
- `logFile`: string, default ``, description `Path to the log file for the debugger`
- `endPoint`: string, default ``, description `ip:port`

and its `initialConfigurations[0]` is `{"name": "Unity Debugger", "type": "vstuc", "request": "attach", "projectPath": "${workspaceFolder}"}`.

Use those description strings verbatim for the matching properties; they are the values the reference ships in its English NLS bundle. Note that `ip:port` is the reference's own description for `endPoint`, which independently corroborates the spec's ruling that `endPoint` takes an IP literal and not a host name.

The reference localizes those strings through `package.nls*.json` (14 locales). We ship no localizations and this plan does not add any — recorded as a divergence for the user to decide on, not something to silently ignore or to expand this task into.

**Two names must NOT be aligned, and this is deliberate — do not "fix" them.** Verified against the current manifest: our debug type is `unity-debugger-pure` with label `Unity Debugger Pure`, and `initialConfigurations[0]` is `{"name":"Attach to Unity Debugger Pure","type":"unity-debugger-pure","request":"attach"}`. The reference's are type `vstuc`, label `Unity`, and initial name `Unity Debugger`.

Both extensions can be installed at the same time — the acceptance project already has the reference installed — so our label and initial-config name exist to let a user tell the two apart in the Run and Debug dropdown and in a shared `launch.json`. Aligning them to `Unity` / `Unity Debugger` would make two entries indistinguishable, which is worse for the user than the difference it removes. Keep our names, add `projectPath` to `initialConfigurations[0]`, and record the label/name pair in Task 12 as a **deliberate coexistence divergence** rather than a miss.

**How the coexistence names stay consistent — corrected, because the earlier instruction was impossible.** An earlier revision said to "write the manifest's initial-configuration name and debug-type label from those values [in `productIdentity.ts`], not from a fresh string". **`package.json` is static JSON and cannot import a TypeScript module**, and no build step generates it (verified: no script writes it). So the manifest must carry the **literals** `Unity Debugger Pure` and `Attach to Unity Debugger Pure`.

Consistency is enforced by tests instead, and that mechanism already exists: `tests/build/scaffold.test.mjs:9` asserts `manifest.displayName === "Unity Debugger Pure"` as a literal, and `:59` asserts `initialConfigurations` against an expected object. So the three coexistence names are **pinned by the scaffold test**, not by shared code — which is the thing to preserve. If you change any of them, change the test in the same commit; do not delete the assertion to make a rename pass.

**One coupling in that test will break, and this is the part the earlier revision missed.** `expectedAttachConfiguration` (`scaffold.test.mjs:50-54`) is shared: it is compared against **both** `configurationSnippets[0].body` (`:55-58`) and `initialConfigurations` (`:59-61`). Adding `projectPath` to `initialConfigurations[0]` alone breaks `:59`. **Add `"projectPath": "${workspaceFolder}"` to `expectedAttachConfiguration` and to the snippet body as well**, so both keep resolving to the same object. Rationale: `projectPath` defaults to `${workspaceFolder}` in the manifest, so a snippet without it still works — but then a user who creates a config from the snippet and one who takes the initial configuration get different `launch.json` entries for the same product, which is the kind of inconsistency this work item exists to remove. Keeping the shared expected object also keeps the test a single source of truth rather than two literals that can drift.

**Recorded additive divergence for Task 12:** the reference declares **no `configurationSnippets` at all** (verified), while we declare one. That is extra user-visible surface, not a conflict — a snippet pre-fills an attach configuration in the Run and Debug dropdown. Keep it, and note it in the matrix as additive rather than as a miss.

`settingIds` does **not** exist in `productIdentity.ts` yet — Task 8 adds that block.

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension && node --test tests/build/scaffold.test.mjs`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add package.json extension/src/debugConfigurationProvider.ts \
        tests/extension/debugConfigurationProvider.test.ts tests/build/scaffold.test.mjs
git commit -m "feat: add endPoint and projectPath attach properties"
```

An earlier revision staged the whole `tests/extension` and `tests/build` directories. Name the two test files explicitly — a directory path silently picks up any scratch file another agent leaves there. Then run `git status --short` and confirm nothing you edited is left unstaged; the six `adapter/vendor/*/packages.lock.json` files are the only entries that should remain.

---

### Task 8: The attach command, its picker, and the background-process setting

**Files:**
- Create: `extension/src/attachTargetPicker.ts`
- Modify: `extension/src/productIdentity.ts`, `extension/src/extension.ts`, `extension/src/model.ts`, `extension/src/editorDiscovery.ts`, `package.json`, `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Test: `tests/extension/attachTargetPicker.test.ts`, `tests/build/scaffold.test.mjs`, `tests/extension/editorDiscovery.test.ts`, `tests/extension/attachRequestRegistry.test.ts`, `tests/extension/debugConfigurationProvider.test.ts`, `tests/extension/publicApi.test.ts`, `tests/adapter/UnityDebugger.Adapter.Tests/Dap/UnityDebugSessionLifecycleTests.cs`

**Like Task 7, this task spans both languages — because of defect (i) below, which is C#.** Run **both** suites: `npx vitest run tests/extension` and `dotnet test UnityDebugger.sln -c Release --no-restore` (building first). Baselines on the current tree: `tests/extension` **131 passed / 11 files**, adapter **Failed 0, Passed 206, Skipped 5, Total 211**, `tests/build` 20/20, `tests/integration` 9/9, `tsc` exit 0.

**Two user-visible defects Task 7 found and correctly declined to fix, both yours.** Task 7 was scoped to enumerable adapter validations; it flagged these instead of widening itself, which was right. They are yours because Task 8 either owns the code or owns the label format, and because **Task 13 would otherwise record both as observed behaviour** — it exercises the `endPoint` path and the picker explicitly.

**(i) `UnityDebugSession.cs:125-136` misidentifies the target and renders a double space.** The warning is emitted whenever the version differs from the baseline:

```csharp
$"Editor {target.ProjectVersion} is unverified; " +
```

With an empty version — which is now reachable on **two** paths Task 7 unblocked, a Player candidate and any `endPoint` attach — that renders `Editor  is unverified; …` with two spaces and no version. Worse than the spacing: it says **"Editor" for a Player**, and it claims a version it does not have. Fix by giving the unknown-version case its own target-neutral wording rather than by interpolating an empty string, and make it say plainly that the version could not be determined while still stating the supported baseline and the policy URL. Keep it an `OutputEvent` on `console`. The existing `Attach_unverified_version_emits_exactly_one_warning` test must still pass — the reference behaviour it pins is *one warning*, not one specific string.

- Why this is not cosmetic: a Player attach is now a headline feature, and the first thing a user sees on a successful Player attach would be a message calling their Player an Editor and naming a version that does not exist. That is precisely the class of thing this work item exists to remove.
- **And Task 7's reviewer established that this message is the *only* thing the user sees on those paths.** The provider renders `decision.warning` only when the level is `unsupported` (`debugConfigurationProvider.ts:206-210`), so the new `compatible-unverified` "The Editor version could not be determined…" warning is computed and then **never surfaced**. On a Player or `endPoint` attach the adapter's message is therefore the sole feedback, which turns a cosmetic double space into the entire user-facing account of what happened. Fix the wording as though it were the only message — because for those paths it is.

**(ii) `extension.ts:153-155` renders a broken label for Players.** The configuration-flow picker builds `` `${projectName} — PID ${processId} — ${projectVersion} — port ${port}` ``, and a Player has `processId: 0` and `projectVersion: ""`, so it renders `PID 0 —  — port N`. Reachable only now, because Task 5 is what made Players discoverable.

- **Fix it by using `formatTargetLabel` from (1) rather than by guarding this one string.** You are already writing that function, it already produces the reference's `Unity <type>, <projectName>, <machine>, <connection>` format, and a Player has a real `machine` and a real `connection` — so one label format serves both the command's picker and the configuration-flow picker, and the second one stops being an ad-hoc string that drifts. That is strictly better than special-casing zero and empty, and it removes the duplicate format rather than patching it.
- This makes the config-flow picker **reference-aligned as a side effect**, which is a small bonus: the reference shows one label style everywhere, and after this we will too. Note the change in your report so Task 12 can record that the config-flow picker's old `PID … — port …` format is gone rather than diverging.

**`isBackground` is a new *required* field, and an earlier revision of this file list named almost none of what it breaks.** Step 3 adds it to `EditorCandidate`, which is the same shape of change as Task 5's `machine` — and it touches the *same set of files*, for the same reason. Enumerated by grep rather than assumed, because this is the fourth time in this plan a required-field addition has had an incomplete file list:

| Site | Needs |
| --- | --- |
| `extension/src/model.ts:14-25` | add `readonly isBackground: boolean` to the interface |
| `extension/src/editorDiscovery.ts` | set it at **both** construction sites — the Editor candidate and the appended Player candidate |
| `tests/extension/editorDiscovery.test.ts:72,101,118,168,198` | five expectation objects; these are compared with `toEqual`, so an extra actual property fails them even though they are not typed literals — which is why a grep for `: EditorCandidate = {` does not find them |
| `tests/extension/attachRequestRegistry.test.ts:6,86` | two typed literals |
| `tests/extension/debugConfigurationProvider.test.ts:9` | one typed literal |
| `tests/extension/publicApi.test.ts:22` | one typed literal |
| `extension/src/attachRequestRegistry.ts:36` | **no change** — it spreads the candidate |

Note the trap in that table: four sites are typed literals a compiler will catch, but the five in `editorDiscovery.test.ts` are **`toEqual` expectations** that fail at *runtime* with a confusing diff. The compiler will not point at them, so fix them deliberately rather than by chasing errors.

**Also extend the two hardcoded command arrays in `tests/build/scaffold.test.mjs`.** Adding a command breaks `:62-69` (`contributes.commands.map(c => c.command)`) and `:70-77` (`.title`), both compared against fixed three-element arrays and both order-sensitive. Task 10 makes the same two edits for its own command; whichever lands second rebases onto the other's array rather than rewriting its line.

**Interfaces:**
- Produces: `formatTargetLabel(candidate: EditorCandidate): string` returning `Unity <type>, <projectName>, <machine>, <connection>` with empty parts omitted, joined by `", "`.
  - `<type>` is `Editor` when `source` is `"advertisement"` or `"derived-port"`, and `Player` when `source` is `"player-advertisement"` (the reference's `displayType` values, observed as `"Editor"` and `"Player"`).
  - `<machine>` is `candidate.machine`, produced by Task 5 (`os.hostname()` for Editors, the `[Id]` component for Players). The label function reads the field rather than touching the host, so it stays testable.
  - `<connection>` is `` `Port:${port} (Mono)` ``, matching the reference's observed format `Port:<debuggerPort> (<debuggerEngine>)`, e.g. `Port:56548 (VSTU)` for its local Editor. Our engine token differs; Task 12 records that as an accepted cosmetic divergence.
- Produces: `selectTarget(candidates, options): Promise<EditorCandidate | undefined>` where `options` carries `displayBackgroundProcesses` and the QuickPick factory, so the picker is testable without VS Code.
- Produces: the QuickPick **title** is `Select Unity Instance`, verbatim from the reference's English bundle (`out\nls.bundle.json`). The reference's placeholder is `Detecting... press any key to filter`, which reveals that it opens the picker **before** discovery finishes and populates it as results arrive; we collect first and open afterwards, so we have no equivalent placeholder. **Use the reference's title; do not fake the placeholder.** Record the streaming-population difference as a divergence in Task 12 rather than restructuring the picker here — it is a real perceived-responsiveness difference, but turning the picker into a streaming UI is well beyond this task and needs its own design.
- Produces: `PRODUCT_IDENTITY.commandIds.attachUnityDebugger = "unity-debugger-pure.attachUnityDebugger"` and `PRODUCT_IDENTITY.settingIds.displayBackgroundProcesses = "unity-debugger-pure.displayBackgroundProcesses"`. The setting **description** is `Display background processes created by the main Unity process`, verbatim from the reference's bundle — I resolved it out of the reference's **`package.nls.json`** under `configuration.displayBackgroundProcesses.description`, since its manifest declares `markdownDescription` with an NLS placeholder and the key is absent from `out/nls.bundle.json`. Our command ids keep our own `unity-debugger-pure.` prefix because the id is not user-visible; the reference's are `vstuc.`-prefixed. **Give the setting `default: false`** — the reference declares `vstuc.displayBackgroundProcesses` as a boolean defaulting to `False`, and our id differs only in following our own extension name, exactly as the reference follows its own. Both are `<extensionName>.<settingName>`. **The command title is NOT the reference's** — an earlier revision of this line said the title was `Attach Unity Debugger` "verbatim from the reference's bundle", which was true when Task 8 landed and stopped being true when Task 10 applied the user's prefix decision. It is now `Unity Debugger Pure: Attach Unity Debugger`; see Task 10's title section for the approval.

- [ ] **Step 1: Write the failing tests**

```ts
it("formats a label like the reference", () => {
  expect(formatTargetLabel({ ...candidate, projectName: "sgproj", machine: "xiaolonghan" }))
    .toBe("Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)");
});

it("hides background processes unless the setting is on", async () => {
  // two candidates, one background; assert one item at default, two when enabled
});

it("sorts non-background targets first", async () => { /* ... */ });

it("reports no processes to attach to when the list is empty", async () => {
  // resolves to NO_PROCESSES_MESSAGE
});
```

Assert in `tests/build/scaffold.test.mjs` that the command is contributed and that `unity-debugger-pure.displayBackgroundProcesses` is a boolean setting defaulting to `false`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx vitest run tests/extension/attachTargetPicker.test.ts`
Expected: FAIL — the module does not exist.

- [ ] **Step 3: Implement**

Create the module with the two exported functions. Add `isBackground: boolean` to `EditorCandidate` and set it to `false` at every construction site: our UDP advertisement and `Library/EditorInstance.json` expose no parent-process relationship, so this release cannot identify a Unity background process. The filter and sort logic must still be implemented and unit-tested against synthetic candidates, but the setting is inert against real discovery — record that as a `divergent` matrix row in Task 12 rather than inventing detection. Register the command in `extension.ts` and the manifest, and contribute the setting.

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension && node --test tests/build/scaffold.test.mjs`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/attachTargetPicker.ts extension/src/model.ts \
        extension/src/productIdentity.ts extension/src/extension.ts \
        extension/src/editorDiscovery.ts package.json \
        adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs \
        tests/extension/attachTargetPicker.test.ts tests/extension/editorDiscovery.test.ts \
        tests/extension/attachRequestRegistry.test.ts tests/extension/debugConfigurationProvider.test.ts \
        tests/extension/publicApi.test.ts tests/build/scaffold.test.mjs \
        tests/adapter/UnityDebugger.Adapter.Tests/Dap/UnityDebugSessionLifecycleTests.cs
git commit -m "feat: add the attach Unity debugger command and picker"
```

An earlier revision staged the whole `tests` directory — the broadest path in this plan, and it would sweep in `tests/extension`, `tests/build`, `tests/integration` and `tests/package` alike. Name the files explicitly. Then run `git status --short` and confirm the only entries remaining are the six `adapter/vendor/*/packages.lock.json` files plus whatever the controller is editing; if you see a file you did not intend to change staged, unstage it rather than committing it.

---

### Task 9: Function-breakpoint performance warning

**Files:**
- Create: `extension/src/functionBreakpointWarning.ts`, `extension/src/outputChannel.ts`
- Modify: `extension/src/extension.ts:45-67`
- Test: `tests/extension/functionBreakpointWarning.test.ts`, `tests/extension/outputChannel.test.ts`

**Task 9 creates the log channel; Task 11 consumes it.** The reordering is forced by a fact I verified against the shipped bundle this round: the reference emits this warning through `logWarning` into a `LogOutputChannel`, not through `showWarningMessage`, so this task cannot land correct behaviour without a channel to write to. `outputChannel.test.ts` is therefore in scope here as well — build the module to the signature Task 11's brief states under "Produces", so that task needs no change to it.

**Interfaces:**
- Produces: `shouldWarnAboutFunctionBreakpoints(breakpoints: readonly { functionName?: string }[]): boolean` and `FUNCTION_BREAKPOINT_WARNING` holding the reference text verbatim: `You are using function breakpoints. Even if they are fully supported, they have a major negative impact on debugging performance. Use them only when necessary.`

**The parameter shape was wrong in an earlier revision of this brief, and the fix matters.** It said `readonly { type?: string }[]` with tests passing `{ type: "function" }`. But the call site is `createDebugAdapterDescriptor`, whose only source of breakpoints is `vscode.debug.breakpoints` — and `vscode.Breakpoint` has **no `type` property**. It is an abstract class with `id`, `enabled`, `condition?`, `hitCondition?`, `logMessage?`; `FunctionBreakpoint` adds `functionName: string` and `SourceBreakpoint` adds `location`. So a `{ type?: string }` predicate could not be fed from the real call site without an invented mapping layer, and the test would have been exercising a shape that never occurs in production.

Use `functionName` instead. It is real, it is discriminative — only `FunctionBreakpoint` has it — and `readonly Breakpoint[]` is structurally assignable to `readonly { functionName?: string }[]`, so the real call site type-checks with no adapter:

```ts
export function shouldWarnAboutFunctionBreakpoints(
  breakpoints: readonly { functionName?: string }[],
): boolean {
  return breakpoints.some(
    (breakpoint) =>
      typeof breakpoint.functionName === "string" &&
      breakpoint.functionName.length > 0,
  );
}
```

Write the tests against that shape (`{ functionName: "Foo" }` for a function breakpoint, `{}` or `{ location: … }` for a source one), and say in your report which shape you settled on if you find a reason to differ. Do **not** reach for `instanceof vscode.FunctionBreakpoint`: it would make the predicate untestable outside a VS Code host, which is the whole reason this is a separate pure module.

- [ ] **Step 1: Write the failing test**

```ts
it("warns only when a function breakpoint exists", () => {
  expect(shouldWarnAboutFunctionBreakpoints([{ location: sourceLocation }])).toBe(false);
  expect(shouldWarnAboutFunctionBreakpoints([{ functionName: "Foo" }])).toBe(true);
});
it("ignores a function breakpoint with an empty name", () => {
  expect(shouldWarnAboutFunctionBreakpoints([{ functionName: "" }])).toBe(false);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run tests/extension/functionBreakpointWarning.test.ts`
Expected: FAIL — the module does not exist.

- [ ] **Step 3: Implement**

Add the module, then create the log channel and call it from `createDebugAdapterDescriptor` in `extension.ts` — that is line 49, inside the `:45-67` block the Files list names — **after the launcher succeeds**, warning when the predicate returns true:

```ts
const descriptor = launcher.createDescriptor(context.extensionPath);
if (shouldWarnAboutFunctionBreakpoints(vscode.debug.breakpoints)) {
  outputChannel.warn(FUNCTION_BREAKPOINT_WARNING);
}
```

**This task also creates `extension/src/outputChannel.ts`, because the warning needs it.** The reference does **not** show a popup here — I verified it in the shipped bundle this round — so an earlier revision of this brief that said `vscode.window.showWarningMessage(...)` was wrong, and wrong in the direction of being *more* intrusive than the reference:

```js
// reference, out/extension.js, in createDebugAdapterDescriptor
s.debug.breakpoints.find(e => e instanceof s.FunctionBreakpoint)
  && p.logWarning(h.debuggerStrings.functionBreakpoints);
// and p.logWarning is:  logWarning = e => l(u.warn, e)
// where u = window.createOutputChannel(displayName, { log: true })
```

- So the message goes to a **`LogOutputChannel` at warn level**, and the user sees it only if they open the channel. A popup would interrupt on every session start for anyone with a function breakpoint — strictly worse than the reference while claiming to match it.
- `createOutputChannel(factory)` is therefore built here with the same shape Task 11 needs: name `PRODUCT_IDENTITY.displayName`, `{ log: true }`, and the `factory` injected so tests need no real VS Code. **Task 11 consumes this module rather than creating it** — its brief has been updated to say so. Keep the factory-injection signature exactly as Task 11's brief specifies, so the two fit without either changing.

Two things to get right and to state in your report. First, warn **after** the launcher succeeds, not before: a descriptor that throws already surfaces an error, and adding a performance warning on top of a failure would be noise at the worst moment. Second, this fires on **each** session start, so a user with a function breakpoint gets a line every launch. That matches the reference, and it is harmless in a log channel precisely because it does **not** interrupt — which is why the emission target matters and not just the text. Do not add "don't show again" state: that is surface the reference does not have and it is not in this task's scope.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run tests/extension/functionBreakpointWarning.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/functionBreakpointWarning.ts extension/src/outputChannel.ts extension/src/extension.ts tests/extension/functionBreakpointWarning.test.ts tests/extension/outputChannel.test.ts
git commit -m "feat: warn about function breakpoint performance"
```

---

### Task 10: Unity API Reference command

**Files:**
- Create: `extension/src/unityApiReference.ts`
- Modify: `extension/src/productIdentity.ts`, `extension/src/extension.ts`, `package.json`
- Test: `tests/extension/unityApiReference.test.ts`, **`tests/build/scaffold.test.mjs`**

**This task must also update `tests/build/scaffold.test.mjs`, and an earlier revision of this file list omitted it.** Adding a command breaks **two** existing assertions in that file — and the line numbers an earlier revision of this brief cited (`:62-69`, `:70-77`) are **stale**, because Task 8 added a command and pushed them down. They are now **`:98-106`** (ids) and **`:107-115`** (titles), both `assert.deepEqual` against order-sensitive arrays whose current contents are:

```js
// :101-104
"unity-debugger-pure.refreshTargets",
"unity-debugger-pure.openLogs",
"unity-debugger-pure.copyDiagnostics",
"unity-debugger-pure.attachUnityDebugger",
// :110-113
"Unity Debugger Pure: Refresh Local Editors",
"Unity Debugger Pure: Open Sanitized Diagnostics",
"Unity Debugger Pure: Copy Sanitized Diagnostics",
"Attach Unity Debugger",          // <- the bare title this task renames
```

- **`:113` is the bare `Attach Unity Debugger` that your rename must change**, which is why this task and the rename are the same change: leaving it would fail the array, and changing the manifest without it would fail the assertion.
- **These arrays are order-sensitive**, so append `showUnityApiReference` in the position it occupies in your manifest rather than sorting them — and change the fourth id's title entry in place, do not move it.
- **Do not weaken either assertion** to a length or containment check. They are the only place the command *titles* are pinned anywhere in the repository, and the user has just made a decision about exactly those strings.

Task 8 makes the same two edits for its own command. Whichever of the two lands second will rebase its array onto the other's; do not rewrite the other task's line, just add yours.

**On the title string — the user has decided this, and it inverts an earlier revision of this brief.** The reference's two commands carry bare titles (`Attach Unity Debugger`, `Unity API Reference`) because it routes them through `package.nls*.json` and declares no `category`. Our three existing commands instead read `Unity Debugger Pure: <Action>`, and an earlier draft of this task had the two new ones bare to match the reference verbatim — which would have left our Command Palette mixing both styles.

**The user chose consistency: all five commands take the `Unity Debugger Pure: ` prefix.** So this task contributes its command as **`Unity Debugger Pure: Unity API Reference`**, and it also **renames Task 8's command** from the bare `Attach Unity Debugger` to **`Unity Debugger Pure: Attach Unity Debugger`** — the same two files, so it belongs here rather than in a follow-up. The rationale is coexistence: both extensions may be installed, and two identically-titled palette entries (`Attach Unity Debugger` from each) would be indistinguishable, which is the same reasoning behind the three approved coexistence names. Record both titles in Task 12 as a **divergence with the user's approval**, not as a gap.

- The four extension-side paths this touches are `package.json` (both `contributes.commands` titles), `extension.ts` if it surfaces either, `productIdentity.ts` if a title constant lives there, and `tests/build/scaffold.test.mjs`, whose command-title array is hardcoded and will fail on either rename. Fix the array, do not weaken it.

**Interfaces:**
- Produces: `buildUnityReferenceUrl(selectedText: string | undefined): string`. Non-empty, non-whitespace, **single-line** text → `https://docs.unity3d.com/ScriptReference/30_search.html?q=<encodeURIComponent(text)>`; otherwise the bare index `https://docs.unity3d.com/ScriptReference`.

  The reference's handler was read out of `out/extension.js` verbatim, so this is the observed behaviour rather than an inference. It registers `vstuc.showUnityAPIReference`, takes `window.activeTextEditor` — and if there is **no active editor it does nothing at all**, with no message — reads the selection, falls back to `document.getWordRangeAtPosition(selection.start)` when the selection is empty, and then:

  ```js
  const base = "https://docs.unity3d.com/ScriptReference";
  const url = isSingleLineNonEmpty(text) ? `${base}/30_search.html?q=${text}` : base;
  commands.executeCommand("vscode.open", Uri.parse(url));
  ```

  Three consequences worth stating:

  1. It is a **search** URL (`30_search.html?q=`), not a per-symbol page. There is no `<Symbol>.html` anywhere in the handler.
  2. A **multi-line selection yields the index**, not a search — the guard is `(text.match(/\n/g) ?? []).length === 0`.
  3. The reference does **not** URL-encode (`encodeURIComponent` appears zero times in the handler), so a symbol containing `#`, `&`, or a space produces a broken URL. **We will encode anyway.** That is a deliberate, documented improvement: identical output for every plain C# identifier, and correct where the reference is broken. Do not "align" by removing the encoding, and do not treat this as a divergence needing a matrix row — a difference that only appears on input the reference mishandles is not a behaviour we should reproduce.
- Produces: `PRODUCT_IDENTITY.commandIds.showUnityApiReference = "unity-debugger-pure.showUnityApiReference"`. The command **title** must be **`Unity Debugger Pure: Unity API Reference`** — the prefixed form per the user's decision above, not the reference's bare `Unity API Reference`. The lookup it opens is unchanged: `buildUnityReferenceUrl` builds `https://docs.unity3d.com/ScriptReference/30_search.html?q=<encodeURIComponent(text)>` for single-line text and the bare index otherwise. **Do not change the URL, the encoding, or the no-active-editor behaviour** — only the palette label is affected by the naming decision. Note our id keeps our own prefix; the reference's is `vstuc.showUnityAPIReference` — and the id is **not** user-visible, so the title is what matters.

- [ ] **Step 1: Write the failing test**

```ts
it("searches for a selected symbol", () => {
  expect(buildUnityReferenceUrl("GameObject"))
    .toBe("https://docs.unity3d.com/ScriptReference/30_search.html?q=GameObject");
});
it("falls back to the index for a multi-line selection", () => {
  expect(buildUnityReferenceUrl("a\nb"))
    .toBe("https://docs.unity3d.com/ScriptReference");
});
it("falls back to the index for an empty selection", () => {
  expect(buildUnityReferenceUrl(undefined)).toBe("https://docs.unity3d.com/ScriptReference");
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run tests/extension/unityApiReference.test.ts`
Expected: FAIL — the module does not exist.

- [ ] **Step 3: Implement**

Add the module; register the command in the manifest and `extension.ts`, reading the active editor's selection and falling back to the word at the cursor. Add these three cases to the test file alongside the ones above, because they are the reference's actual edge behaviour and two of them are silent:

```ts
it("yields the index when the selection is only whitespace", () => {
  expect(buildUnityReferenceUrl("   ")).toBe("https://docs.unity3d.com/ScriptReference");
});
it("encodes a symbol the reference would break on", () => {
  expect(buildUnityReferenceUrl("Foo&Bar"))
    .toBe("https://docs.unity3d.com/ScriptReference/30_search.html?q=Foo%26Bar");
});
```

and a command-level test that invoking it with **no active editor** performs no `openExternal` call and shows no error.

Open the URL with `vscode.env.openExternal(vscode.Uri.parse(url))`. The reference uses `commands.executeCommand("vscode.open", Uri.parse(url))`, which reaches the same browser for an `https` URI; `openExternal` is the documented API for this and is what we should use. Record that choice in your report rather than treating it as invisible.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run tests/extension/unityApiReference.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/unityApiReference.ts extension/src/productIdentity.ts extension/src/extension.ts package.json tests/extension/unityApiReference.test.ts tests/build/scaffold.test.mjs
git commit -m "feat: add the Unity API reference command"
```

---

### Task 11: Output channel

**Files:**
- Create: *(nothing — Task 9 creates `extension/src/outputChannel.ts`)*
- Modify: `extension/src/extension.ts`, **and `extension/src/outputChannel.ts`** (to add `writeSanitized`; an earlier revision of this list omitted it)
- Test: `tests/extension/outputChannel.test.ts` (extend Task 9's file; the module already exists)

**Task 9 now creates `outputChannel.ts`, because its function-breakpoint warning must go through a `LogOutputChannel` rather than a popup — verified against the reference's shipped bundle.** So this task **consumes** that module instead of authoring it.

- **Do not recreate or re-signature it.** Task 9 builds it to the exact shape named under "Produces" below, with `factory` injected, and the name from `PRODUCT_IDENTITY.displayName`. If what Task 9 landed does not match what you need, **say so and stop** rather than editing it into a second shape — a module authored in one task and reshaped in another is how the two-copies-disagree class starts.
- Your tests still belong here: they exercise the channel through the two commands (`openLogs`, `copyDiagnostics`) and the diagnostics behaviours this task adds, which is consumer-level coverage rather than a duplicate of Task 9's construction tests. Extend `outputChannel.test.ts` rather than creating a parallel file.
- If Task 9's module turns out to be missing something this task needs, that is a finding to report, not a licence to rewrite — the fix belongs in Task 9's file with its own test.

**Interfaces:**
- Produces: `createOutputChannel(factory): LogOutputChannel` where `factory` is injected for testing; the channel name is `PRODUCT_IDENTITY.displayName`.
- Produces: `OutputChannel.writeSanitized(line: string): void` that **refuses a line naming the user's home directory** — defence in depth for the allowlist constraint.

**The scope of that refusal is the home directory, not "any absolute path", and an earlier revision of this line said the latter — the reviewer caught the over-claim and the code is right.** The precise behaviour: a line is refused when the user's home directory appears in it, matched case-insensitively, with either separator, and **with a boundary after it** so `C:\Users\Admin` does not also refuse `C:\Users\Administrator\…`. Paths outside the home — a workspace on another drive, `\\build-server\share`, `/opt/unity/proj` — **pass**, because a channel built from nothing but a factory cannot know which roots to treat as sensitive; the adapter's `PathRedactor` redacts both `<user-home>` and `<workspace>`, and the workspace half remains the producer's job.

- **This is recorded as a deliberate limit rather than a gap, and Task 12 must say so in the matrix.** The reviewer's point was that the *claim* was broader than the code: "refuses any line containing an absolute user path" describes a general boundary, and what exists is one specific one. A future reader who trusted the broad wording would assume workspace paths were covered, which is exactly the path a debug log most naturally carries. → Task 14 item 44 owns the record.
- One consequence worth inheriting: because the matched home has to be known, the guard **cannot** be widened to "looks like an absolute path" without also refusing legitimate lines that mention a Unity install root. If a later task wants the broader guarantee, it needs a defined root list, not a syntactic test.

**The reference's mechanism, read out of its bundle, and a correction to this brief.** The reference activates with:

```js
u = window.createOutputChannel(extension.packageJSON.displayName, { log: true });
context.subscriptions.push(u);
```

So it is a **`LogOutputChannel`** — the `{ log: true }` option is what makes it one — and its logging is **level-based** (`logWarning`, `logError`, `logDebug` exist; `isDebugMode` gates debug output). **`appendLine` appears zero times in the reference bundle**, because a `LogOutputChannel` does not use it.

An earlier revision of this brief asserted on `appendLine` while declaring a `LogOutputChannel`; those two are inconsistent, and with a log channel the tests must assert on the level methods instead. Write the tests against `info`/`warn`/`error`/`debug`.

**Channel name:** the reference's `displayName` is the NLS key `%extension.displayName%`, which resolves to **`Unity`** — the same string in every locale including `zh-cn`. Ours is the literal `Unity Debugger Pure`, so our channel is `Unity Debugger Pure`. **Do not rename it to `Unity`**, for the same reason as the debug-type label: both extensions can be installed at once and two identically-named output channels would be indistinguishable. Drive the name from `PRODUCT_IDENTITY.displayName` rather than a fresh literal, and record the difference in Task 12 alongside the label. `writeSanitized` is our own addition — the reference has no equivalent — and it exists to keep the sanitized-diagnostics invariant enforceable at the channel boundary.

- [ ] **Step 1: Write the failing test**

**The snippets below passed a second argument in an earlier revision, and that would have failed the `tsc` gate — Task 9 caught it, verified against this repo's own `tsconfig.json`.** `createOutputChannel` is declared with **one** parameter above, all three snippets called it with two, and `tsconfig.json`'s `include` covers `tests/extension/**/*.ts` — so `Expected 1 arguments, but got 2` would have broken the build, not merely looked untidy at runtime. The home directory is now **derived inside the module** (`os.homedir()`), which is also the better design: the leak check needs to know what a user path looks like, and that is the module's business rather than its callers'.

```ts
it("drops lines that would leak a user path", () => {
  const channel = createOutputChannel(fakeFactory);
  channel.writeSanitized("attached to C:\\Users\\Admin\\proj");
  expect(fakeChannel.info).not.toHaveBeenCalled();
  expect(fakeChannel.warn).not.toHaveBeenCalled();
});
it("passes through a sanitized line", () => {
  const channel = createOutputChannel(fakeFactory);
  channel.writeSanitized("event=attach");
  expect(fakeChannel.info).toHaveBeenCalledWith("event=attach");
});
it("creates a log channel named after the product", () => {
  createOutputChannel(fakeFactory);
  expect(fakeFactory).toHaveBeenCalledWith("Unity Debugger Pure", { log: true });
});
```

**If you decide you genuinely need the home directory injected anyway, that is a re-signature of a module Task 9 built and Task 11 was told to consume — so report it rather than performing it.** Task 9 deliberately did not pre-add an optional second parameter for this. Note also that `outputChannel.test.ts` has no `vi.mock("vscode")` today: consumer-level tests added here will need the mock pattern `functionBreakpointWarning.test.ts` already demonstrates.

The third test pins the two things the reference's own activation proves: the `{ log: true }` option, without which this is a plain `OutputChannel` and the level methods do not exist, and the name coming from the product identity rather than a loose literal.

**Steps 2, 3 and the staging line below were written when this task authored the module, and Task 9 taking it over did not update them. They contradict the header above, and the header is right.** Corrected this round:

| Was | Actually |
| --- | --- |
| Step 2 expected `FAIL — the module does not exist` | The module **does** exist (`extension/src/outputChannel.ts`, **26 lines when Task 9 delivered it**). RED comes from `writeSanitized` not existing on it, so the failures are `channel.writeSanitized is not a function` — **not** a missing module. If you see a module-resolution error, something is wrong with your working tree, not with your test. |
| Step 3 said "Create the module and register the channel in `context.subscriptions`" | **Do not create it.** Add `writeSanitized` to the existing module, and note that Task 9 **already** registers the channel in `context.subscriptions` from `extension.ts` — so verify that wiring rather than writing it a second time. |
| Staging was `extension/src/outputChannel.ts extension/src/extension.ts tests/extension/outputChannel.test.ts` | Still correct as a *list*, but only because Task 11 modifies `outputChannel.ts` — which the Files block above does not mention. See the next bullet. |

- **The Files block is incomplete: `extension/src/outputChannel.ts` belongs under "Modify".** `writeSanitized` is our own addition and has no home other than the module that owns the channel. Staging it is right; not listing it is what makes the staging line look wrong. Rule 11 covers you either way, but the list should be right on its face.
- **A stray closing code fence sat between the snippet and the paragraph that explains it**, which would have rendered every step after it as code. Removed. Worth naming because a brief whose *formatting* is broken is harder to trust than one whose content is, and a reader who sees Step 2 and Step 3 rendered as a wall of code is likely to skim exactly the part that contradicts the header.

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run tests/extension/outputChannel.test.ts`
Expected: FAIL — `writeSanitized` does not exist yet, so the consumer tests fail on `channel.writeSanitized is not a function`. **The module itself already exists**; a module-resolution failure means your tree is wrong.

- [ ] **Step 3: Implement**

**Add `writeSanitized` to the existing module; do not create it.** Task 9 authored `extension/src/outputChannel.ts` (one parameter, `factory` injected, name from `PRODUCT_IDENTITY.displayName`) and already registers the channel in `context.subscriptions` from `extension.ts` — so confirm that wiring rather than duplicating it. Your edit is the `writeSanitized` method and whatever the two consumer commands (`openLogs`, `copyDiagnostics`) need to route through it.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run tests/extension/outputChannel.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/outputChannel.ts extension/src/extension.ts tests/extension/outputChannel.test.ts
git commit -m "feat: add the extension output channel"
```

---

### Task 11b: The deprecated-extension warning

**Found missing this round, and it is a spec deliverable that no task owned.** The spec's surface table says `` | Deprecated-extension warning | warns when `unity.unity-debug` is active | absent | implement | `` (`:138`), and its authoritative-strings table lists the text as item 11 (`:163`). **The plan contained no occurrence of the word "deprecated" at all**, and neither does the shipped source — so one of the rows Task 12 must record as implemented has nothing behind it. This closes that before Task 12 documents it.

**Files:**
- Create: `extension/src/deprecatedExtension.ts`
- Modify: `extension/src/extension.ts` (call it during activation)
- Test: `tests/extension/deprecatedExtension.test.ts`

**The reference's mechanism, read out of its bundle, verbatim:**

```js
async function(e){
  var t = b.extensions.getExtension("unity.unity-debug");
  if (void 0 === t || !t.isActive) return;
  const n = b.Uri.parse(`command:extension.open?["${t.id}"]`),
        r = new b.MarkdownString(`[${t.packageJSON.displayName}](${n})`);
  b.window.showWarningMessage(E.extensionStrings.deprecatedExtension(r.value, e.extension.packageJSON.displayName))
}
```

So: look up `unity.unity-debug`; **return if it is absent or not active**; otherwise warn with the deprecated extension's `displayName` linked to `command:extension.open?["<id>"]`, and our own `displayName` as the second argument.

**Interfaces:**
- Produces: the verbatim string `The {0} extension is deprecated and incompatible with the {1} extension. Please uninstall or disable it.` — confirmed character-for-character in the reference's `out/nls.bundle.json` this round. **Do not paraphrase it.**
- Produces: `warnAboutDeprecatedExtensions(deps): void` with the extension registry and the warning sink injected, so the absent / inactive / active branches are testable without a host.

**One more divergence is baked into the message, and it is the approved one rather than a new choice.** `{1}` is the extension's own `displayName`, and the reference's resolves to **`Unity`** (its manifest declares `%extension.displayName%` and `package.nls.json` resolves it to `Unity`) while ours is **`Unity Debugger Pure`**. So the same template renders *"…incompatible with the **Unity** extension"* there and *"…incompatible with the **Unity Debugger Pure** extension"* here.

- That follows directly from the coexistence decision — the two extensions can be installed together, so a message naming only `Unity` would be ambiguous about which one to keep — and it needs **no separate approval**. Record it in the same matrix row as the link decision and cite the prefix decision it descends from, so a later reader does not treat it as a second unapproved divergence.
- The template itself is unchanged, which is why this is a divergence in the *arguments* rather than in the string: anyone diffing the two bundles finds identical text and a different substitution.

**Two things to get right, both of which the reference gets wrong and we should not silently copy:**

- **The reference passes `r.value` — the markdown string — into the format, so the link renders literally as `[Name](command:extension.open?["unity.unity-debug"])` rather than as a clickable link.** That is a defect in the reference, not behaviour to preserve. Either pass `{0}` as the display name in plain text, or pass a `MarkdownString` as the whole *message* so the link works. **Record whichever you choose as a divergence with its reasoning** — do not reproduce a broken link merely because it is what ships.
- **`unity.unity-debug` is a different extension from `local.vstuc`, the reference this work item aligns to.** Coexistence with `local.vstuc` is approved and expected; this warning targets a third, genuinely deprecated extension. Do not conflate the two, and note that `docs/release-checklist.md:70` names a *third* distinct id for uninstall during migration.

**Constraints:**
- **This must not add a dependency.** `extensionDependencies`/`extensionPack`/`dependencies` stay `[]`, and no `ms-dotnettools` or `com.unity.ide.*` reference is introduced. `vscode.extensions.getExtension` creates no dependency; declaring one would break the invariant.
- **Check whether `activationEvents` needs anything** rather than assuming — the warning runs at activation, and Task 10 had to add an event for its command. If nothing needs to change, say so explicitly.

**Rule 11 files — expect these to break, and fix them in the same commit.** Reading `vscode.extensions.getExtension(...)` at activation means every double that runs `activate()` must now provide an `extensions` registry. **I checked, and none of them does**, so this is a prediction rather than a warning:

| File | `activate()` calls | Provides `extensions`? |
| --- | --- | --- |
| `tests/extension/outputChannel.test.ts` | 8 | **no** |
| `tests/extension/functionBreakpointWarning.test.ts` | 3 | **no** |
| `tests/build/extension-entry.test.mjs` | 2 | **no** — and this one runs **real** activation, so it will throw rather than silently pass |
| `tests/extension/unityApiReference.test.ts` | 1 | **no** |
| `tests/extension/extensionCommands.test.ts` | 0 | — (activates indirectly; check it) |

- **Add a minimal registry to each**: an object with `getExtension` returning `undefined` is enough for the absent branch, and one test should supply a fake extension object to exercise the active branch. **Give the fake a `packageJSON.displayName`, since the message interpolates it** — an object without one produces `undefined` in the middle of a user-visible string, which is the kind of defect a mocked test will happily accept.
- **This is the fourth time this exact pattern has bitten this work item**, and the first time it has been predicted rather than discovered. Task 9's `tests/build` stub lacked `window.createOutputChannel` and `activate()` threw, taking the suite to 19/20; Task 10 broke `scaffold.test.mjs`'s two hardcoded command arrays; Task 11 broke `unityApiReference.test.ts`'s `vi.fn()` factory that returned `undefined`. **A factory or namespace added to the activation path invalidates every double in the repository, and no brief lists them** — so this note names them, and Step 2's "fails for the right reason" check should now read "fails on the assertion, not on a missing stub member".

- [ ] **Step 1: Write the failing test** — the three branches (absent, present-but-inactive, present-and-active) plus a `toBe` pinning the verbatim string, **split at a different point from the production literal** so a typo in either is caught rather than both drifting together.
- [ ] **Step 2: Run it and confirm it fails for the right reason** — not module resolution alone.
- [ ] **Step 3: Implement**, wiring the call into activation.
- [ ] **Step 4: Run the full gate** — `npx vitest run tests/extension`, `node --test tests/build/*.test.mjs`, `tsc`, plus the three invariants:
  - `extensionDependencies`, `extensionPack` and `dependencies` all still `[]`
  - no `ms-dotnettools`/`csdevkit` and no `com.unity.ide.*` in `extension/`, `adapter/src/` or `package.json`
  - no telemetry identifier in shipped source
- [ ] **Step 5: Commit** — explicit paths only; the six `adapter/vendor/*/packages.lock.json` stay untouched and uncommitted.

**Report the link decision for Task 12**: this adds a matrix row, and the link question is a `divergent` entry needing its own justification rather than a silent improvement.

---

### Task 12: Documentation — matrix, reference identity, security, amended scope

**Files:**
- Modify: `docs/reference-debugger-compatibility.md`, `AGENTS.md`, `README.md`, `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`, `docs/superpowers/plans/2026-08-06-reference-compatible-mature-debugger-backend.md`, `docs/release-checklist.md`

**Two of these six were added to this list after the fact, and both were being edited while absent from it — which is the failure mode, not a near miss.** `docs/release-checklist.md` was the first (round 126) and the 2026-08-06 backend plan is the second (round 134): the brief's body names it **four times** as a site whose prohibited-acts and authority clauses need amending, the task began editing it, and the Files list still named five paths. **The commit protocol is keyed to this list**, so an edited file that is not in it is a file that gets left unstaged for a later commit to sweep up.

- **The mechanism is worth stating plainly, because this is the third time**: a Files list is written when a task is authored, then the task's *prose* grows to cover more files as reviews append to it, and **only the prose is reread**. `AGENTS.md` makes the consequence explicit — an unapproved or unrecorded difference blocks the completion claim — and an unstaged edit is exactly an unrecorded one.
- **The check that catches it is one question, asked per file: is this file named in the Modify line?** Rounds 126 and 134 both found the answer to be no, and both were found by comparing the list against what the tree showed as modified — **not** by reading either the list or the prose alone.

**`docs/release-checklist.md` was missing from this list until it was checked, and the omission mattered more than it looks.** The body of this brief describes that file's three stale `MyGame` sites, its acceptance list, and two lines to leave alone — so the *work* was specified. **What was missing was the staging instruction**, and the commit protocol for this work item is keyed to this list: an implementer following it stages four files, the fifth edit sits unstaged, and the very next commit sweeps it up under an unrelated message. **A file can be described in a brief's prose and still be absent from its checklist**, and only the checklist is read at commit time.

**Interfaces:**
- Consumes: the spec's two diff tables and the observed `initialize` body.

- [ ] **Step 1: Add the matrix rows**

Add one row per line of the spec's two diff tables to `docs/reference-debugger-compatibility.md`, using the existing `| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |` shape and the existing status vocabulary. Reference the observed `initialize` body, the observed `debuggerTargets` payload, and the REA evidence ids where they apply. Status is `aligned` only for items verified by running both extensions; everything implemented but not yet A/B-run is `not yet verified`.

**The `Evidence` cell has a convention, and matching it is what makes 42 new rows indistinguishable from the 28 old ones.** Every existing cell is a **semicolon-separated list of dated observations**, not a bare source name:

| Row | `Evidence` |
| --- | --- |
| `BP-01` | `2026-08-06 MyGame reference and Pure 0.3.0 runs` |
| `EX-01` | `2026-08-06 and 2026-08-10 MyGame reference/Pure A/B runs; sanitized Adapter logs` |
| `CFG-01` | `2026-08-06 reference manifest; 2026-08-08 Pure installed manifest and bundle inspection` |

So write yours as `2026-10-16 <what was observed>` — for example `2026-10-16 reference 1.3.2 out/nls.bundle.json; REA ev_…` for a bundle read, or `2026-10-16 debuggerTargets payload captured from 10.30.154.73:54997` for the live probe. **The date is the part that carries the weight**: it is what tells a future reader whether an observation predates or postdates a change, and a cell reading only "reference bundle" cannot be aged. Task 12 exists partly because `CFG-01`'s evidence was still describing a build whose configuration surface has since changed — the date is the mechanism that makes that visible next time.

`Decision` is a single sentence of rationale, and the existing rows all answer *why this status is correct* rather than restating the status. Keep that shape; for the `divergent` rows the sentence is where the approval gets cited.

**And the `Scenario/action` cell names a concrete, checkable action rather than a category** — `First hit at \`GameRuntimeBootstrap.Install()\` line 11`, not "breakpoints work". Some cells use inline `<code>` to keep backticked identifiers legible inside the table. Neither is mandatory on its own, but a row whose scenario is a category cannot be re-run, which leaves the evidence column beside it asserting something nobody can check.

**The two diff tables hold 42 data rows between them, and that number is the scope — items 1–19 below detail only the divergent ones.** Counted this round: `Reference capability diff` at spec `:95` has **19** data rows (`| Capability | Reference | Pure | Action |`), and `Reference extension-surface diff` at `:119` has **23** (`| Area | Reference 1.3.2 | Pure 0.3.2 | Action |`). 19 + 23 = **42 new rows** on top of the existing 28.

- **The asymmetry is deliberate and easy to misread.** Items 1–19 enumerate the *divergences* — the rows where our status is `divergent` or a recorded gap — because those are the ones needing a justification and an approval citation. The other 23 are `aligned`, `implement`, `keep`, `identical` or `not applicable` rows from the same two tables, and they need rows too: **a compatibility matrix that lists only the differences is not a compatibility matrix**, and the spec's tables are the record of what was compared, not only of what disagreed.
- **Read the `Action` column of each table rather than inferring status from the row's subject.** The capability table is mostly `aligned` with four `gap` entries, one `divergence — remove`, and one row whose action is literally `aligned — the reference does **not** support hit conditions, so our numeric-equality parsing is a superset` — i.e. a row that is aligned *with an explanation*, which needs both recorded. The surface table mixes `implement`, four `divergent`, two `keep`, one `identical — confirms protocol compatibility`, and one `not applicable (vsdbg/CoreCLR only)`.
- **`not applicable` and `identical` are statuses worth having in the matrix even though nothing was done for them.** `Concord` is genuinely out of scope (CoreCLR-only) and the debugger port being `identical` is positive evidence that the protocol matches; recording the second is how a future reader knows the match was checked rather than assumed.

**Get the status terms exactly right — an earlier revision of this brief said `not verified`, which is a term this file does not use.** The file's own vocabulary today is `aligned` (**25 rows**) and `not yet verified` (**3 rows**), and `AGENTS.md:41` declares the full set as **`reference-verified`, `aligned`, `divergent`, or `not yet verified`**. So `not yet verified` is the correct phrase, three words, and `not verified` would be a third spelling in a document whose entire purpose is precise status.

Two consequences worth knowing before you start:

- **No row currently uses `reference-verified` or `divergent`.** Yours will be the first `divergent` rows. That is expected, not a sign you have misread something.
- **The boundary between `reference-verified` and `aligned` is not defined anywhere I can find**, and the file does not yet disambiguate them. My reading is that `reference-verified` means the reference's behaviour was established from its shipped artifacts (which is what the REA work did) whereas `aligned` means the same behaviour was observed in **both** extensions live. Do not silently pick one: state which term you used for which rows and why, so the distinction is recorded the first time it matters rather than being inferred later from inconsistent usage. If you think my reading is wrong, say so and use yours — a stated disagreement is worth more here than a guessed convention.

Also fix this stale reference the Task 1 review caught: `README.md:51` says "all-exception and uncaught-exception break modes", but the advertised filter id is now `unhandled`. Correct the wording, and make sure the W10 matrix rows exist and follow spec Verification 4/5 — no row marked `aligned` from automated evidence alone, and the conditions divergence recorded as `divergent`.

The reference README was read directly (`C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2\readme.md`, 4474 bytes) so these three README defects are quoted, not guessed:

1. **`README.md:51`** — "all-exception and uncaught-exception break modes"; the id is now `unhandled`. **Use the reference's own label wording, which our adapter already matches exactly:** the two filters are `all` → `All Exceptions` → "Break on all exceptions", and `unhandled` → **`User-Unhandled Exceptions`** → "Break only on unhandled exceptions" (`UnityDebugSession.cs:60-73`; reference body at spec `:74-78`). So the sentence should read as all-exception and **user-unhandled**-exception modes, not "uncaught". The spec's `:111` records why this matters — the old id `uncaught` was a **gap** against the reference's `unhandled`, closed by Task 1 at `:414`; the README simply never caught up, so the stale word is the visible residue of a real alignment fix.
2. **`README.md:81`** — "Currently excluded: remote Player attach, Android/iOS, IL2CPP, WebGL, …". Once Task 7 lands `endPoint`, "remote Player attach" is **no longer excluded** and this line becomes false. Remove that phrase, keep the genuinely excluded items (Android/iOS enumeration, IL2CPP, WebGL).
3. **`README.md:85-90`** — the "## Security and privacy" section states that version `0.3.2` "automatically connects only to a discovered loopback address" and "does not accept … remote debug hosts … so future remote support will require explicit opt-in." After Tasks 2 and 7 this is false. Rewrite it to state the truth plainly: attaching to a non-loopback `endPoint` is supported, and Unity's managed debugger transport is **unauthenticated**, so anyone who can reach the debugger port can attach and the debugger can execute code in the target Editor or player; recommend loopback or a trusted network. Do not imply a guarantee we do not provide.

For wording alignment, the reference's own command descriptions are:

- `` `Attach Unity Debugger`: lists Unity Editors and Players available for debugger attachment. ``
- `` `Unity API Reference`: opens the Unity documentation for the selected code construct. ``

and its player-attach section states that the default configuration attaches to the Editor running the open project, that `Attach Unity Debugger` is how a Player is debugged, and that a known endpoint can be configured explicitly with `"endPoint": "127.0.0.1:56321"`.

**Do not treat the security section as an alignment failure.** A keyword sweep of the reference README for `secur|warn|caution|note|auth|trust|expos|firewall|risk` returns nothing: the reference documents **no security posture whatsoever** for remote attach. Our statement is an intentional addition beyond the reference, and the matrix must record it that way rather than as a divergence to close.

### The two authority clauses to amend, quoted

These were read directly; use the existing wording's register and do not overstate what is permitted.

**1. `AGENTS.md`** is 45 lines and needs its acceptance project and its reference identity updated in **seven line references across three groups** — an earlier revision of this brief said "five places", which counted only the bare `MyGame` references and was wrong about its own enumeration:

- `:6` — `` `D:\Unity\TuanjieHub\Projects\MyGame` for real Unity/Tuanjie runs. `` → the acceptance project is `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`.
- `:7`, `:9`, `:10`, `:17`, `:26` — the bare `MyGame` references that follow from it, including the `DevTools` preference at `:26-27`. Note the new acceptance project has **no `DevTools` fixture**, so that sentence needs rewording rather than a path swap: keep the reachability requirement and drop the claim that a verified fixture exists.
- `:32` — `` - Treat the installed `zlorn.vstuc` debugger as the behavioral reference for `` → `visualstudiotoolsforunity.vstuc` **1.3.2**. Verify against `docs/reference-debugger-compatibility.md:3`, which names `zlorn.vstuc 1.2.1` and must be updated in the same pass, and check for any other `zlorn` or `1.2.1` reference with a repo-wide search rather than assuming these are the only two.

**2. `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`** (303 lines). Three places:

- `:50-51`, under the `:45` heading **"Permanently out of scope:"** — `` - Reusing, separating, decompiling, modifying, or redistributing proprietary binaries from the reference extension. ``
- `:56` — `` The installed `zlorn.vstuc 1.2.1` debugger defines the expected user-visible and DAP-visible behavior. `` → 1.3.2.
- `:78-80`, under the `:76` heading **"Legal and Dependency Boundary"** — `` The reference package contains proprietary Microsoft/SyntaxTree assemblies. Its license does not permit separating, reverse engineering, or redistributing those components. They are neither build inputs nor runtime dependencies. ``

**Two of those three quotes are wrapped across source lines, so do not verify them by searching for the sentence — it will not match, and you will conclude the brief is wrong.** The first is `:50`+`:51` (`` …redistributing proprietary `` / `` binaries from the reference extension. ``) and the second is `:56`+`:57` (`` …expected user-visible `` / `` and DAP-visible behavior. ``). The third, `:78-80`, happens to fall on line boundaries and does match as written. The citations above are correct — the first bullet's quote starts at `:50` and the second's at `:56` — so edit by **line number** rather than by searching for text.

I found this by trying to verify the quotes with a substring match and getting two false misses, which is worth knowing because it is the sixth time this session a check of mine produced a confident wrong answer. **A multi-line quote in Markdown source is not a string, and any verification that treats it as one will report a defect that is not there** — the same shape as the `[regex]::Escape` miss and the character class that crossed table boundaries, one level further from the code.

The recorded decision is that **analysing** the shipped binaries as behavioural evidence is permitted, while copying, linking, packaging, and redistributing remain prohibited. Amend accordingly, and do it carefully:

- Split `:50-51` rather than deleting it. Keep prohibited: reuse, separating, modifying, and redistributing the binaries. Add what is now permitted: **observing the shipped software as a black box** (running the adapter and reading the DAP/JSON-RPC it emits) and **static analysis of its IL for behavioural evidence**, which is how the capability table and the `endPoint` table in this plan's spec were produced.
- **Do not claim the license permits reverse engineering.** `:79` is a statement about the licence and it remains true. The honest framing is that we do not do the prohibited things; we observe behaviour and record what it emits, and no reference bytes reach our build or package. If a sentence would read as a legal opinion, make it narrower, not broader.
- Keep the closing guarantee that they are neither build inputs nor runtime dependencies, and state that no reference artifact is included in the VSIX — the task's packaging audit proves that mechanically.
- Add a pointer to the evidence trail: this plan's spec records the observed `initialize` body, the `debuggerTargets` payload, the attach-failure response, and the `endPoint` table, so a future reader can see exactly what observing the reference produced.

### Complete sweep — the brief's earlier two-file list was incomplete

A repo-wide `git grep` for `zlorn` and `1\.2\.1` found the stale identity in **six tracked documents**, not two. Apply these rules:

**Rule A — live authority statements must name 1.3.2.** These are the ones a reader would take as current, and every one must change:

- `AGENTS.md:32`
- `docs/reference-debugger-compatibility.md:3` (the matrix header) and `:255` (the debug configuration it records)
- `docs/reference-debugger-compatibility.md:4` — **the matrix's own Target line**, which currently reads `Target: D:\Unity\TuanjieHub\Projects\MyGame, Tuanjie 2022.3.62t12.` This one is easy to miss because it is not a reference-identity line at all: it names the *acceptance project*, and the objective moves that to `D:\workspace\sgproj1\sgproj_xiaolonghan_0003` on Unity `2022.3.23f1`. Update it in the same pass as `AGENTS.md:6-10`, and keep the two consistent — a matrix that says the target is MyGame while `AGENTS.md` says otherwise is exactly the kind of split-brain the identity sweep exists to prevent. Note the acceptance project genuinely has **no `ProjectSettings\ProjectVersion.txt`**, so the version must come from `Library\EditorInstance.json` (`2022.3.23f1`), not from the usual file.
- `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md:56` — the "defines the expected user-visible and DAP-visible behavior" sentence
- `docs/superpowers/plans/2026-08-06-reference-compatible-mature-debugger-backend.md:14` — "is the sole authority for observable debugger behavior"

**Rule B — historical observations keep their 1.2.1 attribution.** Do **not** silently rewrite what was observed against 1.2.1 into 1.3.2; that would make the record false. Instead leave the observation and add a short parenthetical or note that the authority was later re-targeted to `visualstudiotoolsforunity.vstuc` 1.3.2. This applies to:

- `docs/reference-debugger-compatibility.md:151` (a decompilation record), `:633`
- `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md:7`, `:13`
- `docs/superpowers/plans/2026-08-06-reference-compatible-mature-debugger-backend.md:5`, `:128`, `:935`

**Rule C — prohibited-acts clauses must be amended for consistency, not deleted.** `docs/superpowers/plans/2026-08-06-reference-compatible-mature-debugger-backend.md:18` currently reads:

> Do not reuse, separate, decompile, modify, link, or redistribute any proprietary binary from `C:\Users\Admin\.vscode\extensions\zlorn.vstuc-1.2.1`.

That forbids decompiling outright, which now contradicts the recorded decision permitting analysis as behavioural evidence. Amend it the same way as the parity spec's `:50-51`: keep reuse, separation, modification, linking, and redistribution prohibited; permit observation and static analysis for behavioural evidence; never claim the licence permits more than it does.

**Rule D — exclude noise.** `package-lock.json` matches `1.2.1`/`1.2.10` dozens of times (`define-properties`, `ieee754`, `source-map-js`, `rolldown`). Those are npm dependency versions and must **not** be touched. Confirm the final state with `git grep -n -E 'zlorn|vstuc 1\.2\.1' -- '*.md'`, which should return only Rule B's annotated historical mentions.



Also add these six `divergent` rows, which are known and must not be silently absorbed. The first four are behaviours; the last two are accepted representation differences in wire shape:

1. **No localization.** The reference ships 14 `package.nls*.json` locales and routes its command titles and property descriptions through them (`%commands.attachUnityDebugger.title%` → "Attach Unity Debugger", etc.). We ship **no** localizations, so every string is English-only even for a `zh-cn` user, who would otherwise see `附加 Unity 调试程序`. This plan does not add localization. **Flag it to the user as a scope decision** rather than treating it as closed: it is a real user-visible difference, and the fix is additive (add `package.nls.json` plus translations and switch the manifest to placeholders) rather than architectural.

2. **Unparseable `endPoint`.** We are deliberately **stricter** than the reference: a non-IP-literal host or a zero/missing port is rejected with an explicit error, where the reference answers `success:true` with no message and attaches to nothing. Evidence and the full table are in the spec's "Observed `endPoint` handling" section. This is a divergence **in our favour** and must never be "aligned" to the silent no-op. (Task 2's implementer flagged that this row was missing from an earlier version of this list — it was present in the spec table but not here, which is exactly the kind of gap that lets a row get skipped.)

3. **Error-response body and sequence numbers.** For the same failing attach, the reference replies `body:{}` with `seq:2` while we emit the vendored error object (`{"error":{"id":2004,"format":"Failed to attach debugger to …"}}`) with `seq:3`. Our `success`, `command` and `message` match the reference character for character, and the body shape is what this adapter emits for **every** error rather than something attach-specific, so it is an accepted representation difference — but record it rather than leaving it unmentioned.

Note — **not a divergent row.** **`logFile` redirection — status `not yet verified`, not `aligned` and not `divergent`.** (Wrong in an earlier revision as `not verified`; the file's term is `not yet verified`.) Task 3 implements it (`b90562d`). An earlier revision of the spec claimed the reference "declares this property and never reads it", citing zero occurrences in `out/extension.js`; that inference was wrong. `UnityDebugAdapter.dll` carries the literal run `.ProjectFileMapper: unable to map .Assets.Packages.Library.pid.projectPath.logFile.endPoint.|`, placing `logFile` alongside `pid`, `projectPath` and `endPoint` — the other attach keys the adapter reads. A name in the literal pool proves the adapter refers to the property, not what it does with it, so neither `aligned` nor `divergent` is supportable from this evidence. Mark it `not verified` and settle it at W13 by setting `logFile` on a real attach and checking whether the reference writes a file. **Do not** repeat the earlier "favourable superset" framing — it was wrong.

4. **Exception filter conditions.** The reference advertises `supportsCondition: true`, so the client offers an exception-condition box and sends `filterOptions[].condition`. Task 1 accepts `filterId` and ignores `condition`, so a typed condition list is not honoured. Reference and ours otherwise agree on the filters themselves.
5. **Player advertisement `debug` gating.** The reference lists an Android player whose advertisement carried `debug: false` as `isValidForAttachment: true`; our `playerAdvertisement.ts` treats `debug === "1"` as `allowDebugging` and Task 5 filters on it, so we would exclude that target. Neither behaviour is changed by this plan.
6. **Connection label engine token.** The reference renders `Port:56548 (VSTU)`; we render `Port:<port> (Mono)`. Same format, different engine name.
Note — **withdrawn; do not add a machine-label row.** An earlier revision of this plan recorded that we could not reproduce the reference's machine label for a network player. That was wrong: the label is the second component inside the advertisement's `[Id]` field, e.g. `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)`, so Tasks 5 and 8 now parse it and match the reference exactly. Recording a divergence here would misreport working behaviour.

7. **`configurationAttributes.attach.properties` declares three properties the reference omits.** Verified this round by reading both manifests: the reference declares exactly `projectPath`, `logFile` and `endPoint`, whereas ours also declares `name`, `type` and `request`. Everything that *is* declared matches the reference exactly — `required: []`, `projectPath` defaulting to `${workspaceFolder}`, the other two defaulting to the empty string, and all three descriptions verbatim from `package.nls.json` (`Project Path`, `Path to the log file for the debugger`, `ip:port`). So this is purely additive: the three extra entries appear in `launch.json` IntelliSense and nowhere else, and they document fields every configuration must carry anyway. Record it as `divergent` on the schema row rather than removing them — deleting the standard DAP fields from our own schema to match a reference that omits them would make our IntelliSense worse for no user-visible gain. If you disagree, say so; the argument for removal is that "aligned" should mean identical, and that is a defensible reading.

8. **`contributes.configurationSnippets` is ours alone, and the rest of the `contributes` diff is scope rather than divergence.** Verified this round by reading both manifests side by side. The reference's `debuggers[0]` keys are `type, languages, label, configurationAttributes, initialConfigurations` — **no `configurationSnippets`** — while ours adds it with one entry. That is purely additive: it enriches the `launch.json` snippet list and removes nothing. Record it on the schema row as `divergent`, together with the two names the reference uses for the same concept (`initialConfigurations[0].name` is `Unity Debugger`; the configuration it builds at runtime is named `Unity`), where ours uses `Attach to Unity Debugger Pure` in both.
    - The wider `contributes` comparison, so you do not have to redo it: the reference also contributes `csharpExtensionLoadPaths`, `jsonValidation`, `languages`, `grammars` and `configuration`, while we contribute only `commands`, `breakpoints` and `debuggers` (plus `configuration` once Task 8 lands). **These are the C# language-service half of that extension, not its debugger half** — `csharpExtensionLoadPaths` is its C# Dev Kit hook, `languages`/`grammars` are C# editing features, and five of its six settings (`refreshOnSave`, `enableMessageCompletion`, `enableCodeModel`, `unityMessageScope`, and the asset-refresh behaviour) control IntelliSense, CodeLens and Unity-message generation. None of that is debugger behaviour, and `csharpExtensionLoadPaths` is exactly the C# Dev Kit dependency this project forbids.
    - **So do not record them as divergences.** A matrix row claiming divergence from a reference feature we are *required* not to have would misrepresent a deliberate scope boundary as a compatibility gap. The one setting that does affect the debugger UI is `displayBackgroundProcesses`, which Task 8 adds; state that ours covers the debugger-relevant subset and name the excluded settings as out of scope, with `csharpExtensionLoadPaths` cited as forbidden rather than missing. If you think any of them do affect debugger behaviour, say which and why.

9. **`unity-debugger-pure.displayBackgroundProcesses` filters nothing, because our discovery has no background classification — record this as `divergent` with the mechanism, not just the outcome.** Task 8 adds the setting with the reference's exact description, default and filter/sort behaviour, and wires `isBackground` onto `EditorCandidate` — but sets it to `false` at both construction sites, because nothing in our discovery knows which processes are children of the main Unity process.

    **The reference's `isBackground` is adapter-supplied, and that is the fact which makes this a real divergence rather than a wiring omission.** Read out of its bundle this round: the process model is constructed with `isBackground = !1` and the extension only ever *reads* it — `t.processes.map(…).filter(e => !e.process.isBackground || setting)` — where `t.processes` arrives from the adapter's `debuggerTargets` **notification**, delivered through `r.onNotification(S, …)`. So the classification is the adapter's job in the reference, and our adapter emits no equivalent notification. Both halves belong in the row, or a later reader will try to "fix" the wiring.

    - The comparison deserves one sentence in `Decision`: the filter and sort match the reference exactly and would take effect the moment an adapter-side classification existed, so this is a **missing data source, not a missing feature**. That distinction is what keeps it out of the "unimplemented" bucket in the handoff.
    - **Do not mark it `aligned` on the strength of the identical filter expression.** The expression is identical and the outcome is not, and keeping those two things apart is the matrix's entire purpose.

10. **The three coexistence names — approved divergences, and you must cite the approval.** `Unity Debugger Pure` (debugger label), `Attach to Unity Debugger Pure` (`initialConfigurations[0].name`) and `Unity Debugger Pure` (output channel) all differ from the reference's `Unity` / `Unity Debugger` / `Unity`, **with the user's explicit approval**, because both extensions can be installed at once and indistinguishable names would be a defect rather than alignment. State the reason and the approval in `Decision`; do **not** list these as gaps, and do not "fix" them toward the reference. Note while you are there that the reference is itself inconsistent — `initialConfigurations` says `Unity Debugger` while the configuration it builds at runtime is named `Unity` — so ours agreeing with itself is a small point in our favour.

11. **The prefixed command titles — approved, and decided this round.** All five of our commands read `Unity Debugger Pure: <Action>`, including the two whose underlying action matches the reference (`Unity Debugger Pure: Attach Unity Debugger`, `Unity Debugger Pure: Unity API Reference`). The reference's two are bare. The user chose consistency over verbatim titles, for the same coexistence reason as item 10. Record both, cite the approval, and note the four unchanged ones (`Refresh Local Editors`, `Open Sanitized Diagnostics`, `Copy Sanitized Diagnostics`) are ours alone with no reference counterpart.

12. **The picker's population model — a genuine behavioural difference, and not one to fix.** The reference opens its QuickPick inside the adapter's `debuggerTargets` notification handler (`r.onNotification(S, t => { e.items = … })`), so the list fills as targets arrive and its placeholder reads `Detecting... press any key to filter` for that window. We collect all candidates first and open afterwards, so that phase does not exist for us and we deliberately do not fake its placeholder. Record it as `divergent` with that mechanism, and note this is why our empty-state message is the one visible string of the pair. Do not mark it `aligned` merely because the *items* end up equivalent — the timing is user-visible when discovery is slow. Task 8 left a comment at `attachTargetPicker.ts:68-69` recording the decision; cite it.

13. **Non-canonical IP literals are rejected — a deliberate tightening, distinct from item 2's unparseable-host rule.** The reference accepts any `endPoint` string and fails later; we require a canonical IPv4 literal, so `1.2.3`, `010.1.1.1` and `192.168.001.001` are **rejected** rather than quietly reinterpreted. This is a second divergence and needs its own row: the reason is that `.NET`'s `IPAddress.Parse` accepts those forms and normalises them to *different addresses* than the user typed (`1.2.3` → `1.2.0.3`, `010.1.1.1` → `8.1.1.1`), so a permissive parse can connect to a machine the user did not name. `::FFFF:127.0.0.1` is likewise rejected — loudly, rather than silently rewritten. Record the tightening and the reason; a reviewer seeing only "stricter validation" will assume it is arbitrary.

14. **The reference ships telemetry and we ship none — a divergence in our favour, and the strongest evidence for the no-telemetry invariant.** Its bundle contains an Application Insights client (`AppInsights`, 7 occurrences) and the attach path calls `telemetryService?.logDebuggerLaunch…`; our source contains none, and `@opentelemetry/api` is absent from `package-lock.json` entirely. Record it plainly, and make sure the README section named in Step 3 says the reference reports usage while we do not — that is a user-visible difference and it deserves to be stated rather than implied. This is the one row where the reference's behaviour is the thing being declined.

15. **The reference has a second engine type (`coreclr`) that we do not implement — scope, not divergence.** Its attach path branches on `debuggerEngine`: `coreclr` sends `type: coreclr` with a `processId` and no `endPoint`, while the `vstuc` (Mono) path sends `endPoint` and no `processId`. We implement only the Mono path, which is the one this work item targets. Record it as an out-of-scope boundary with that mechanism, so a reader comparing `type` values does not conclude we are missing a feature we never claimed.

16. **The reference emits four empty arrays in `initialize` that we omit — accepted representation differences, per the spec's Verification 2.** `completionTriggerCharacters`, `additionalModuleColumns`, `supportedChecksumAlgorithms` and `breakpointModes` come from the VS protocol library's serializer defaults; our vendored `VSCodeDebug` layer does not emit them. An empty array and an absent property are equivalent to a DAP client, so record all four by name as accepted representation differences rather than ignoring them — the spec's Verification 2 requires the enumeration, and "we did not notice" and "we decided it does not matter" look identical in a matrix that omits them.

17. **You must also RE-VALIDATE existing rows whose subject matter this work changed — adding rows is not enough, and one existing row is now factually false.** An earlier revision of this brief said only "add" rows; that would leave the matrix asserting things the code no longer does. Found this round by reading all 28 rows (25 `aligned`, 3 `not yet verified`):

    - **`CFG-01` (status `aligned`) now contains a false statement about our own build.** Its Pure column reads *"Installed build `0.3.0+g8c6f030f7256` has no contributed configuration, exposes only `name/type/request` Attach properties…"*. Task 7 added `endPoint` and `projectPath` to `configurationAttributes`, and Task 8 added the `unity-debugger-pure.displayBackgroundProcesses` setting under a new `contributes.configuration` block. So **both halves of that sentence are now wrong**, and the row is marked `aligned` — the matrix currently asserts, as verified-equivalent, a configuration surface that no longer exists.
    - **`EX-01` (status `aligned`) needs the same treatment.** Its subject is caught/unhandled exception filters, and Task 1 renamed the filter id `uncaught` → **`unhandled`** to match the reference (spec `:111` records the old id as a **gap**, closed at `:414`). Its Pure column cites an older build and its wording predates the rename, so its evidence no longer covers the behaviour it claims.
    - **Check the rest for the same condition**, using the rule rather than my list: a row whose *subject* intersects Tasks 1–8 needs its Pure column and evidence re-read against the current build, and either re-validated or downgraded. Rows about stepping, variables, watch and evaluation (`STEP-01`, `VAR-*`, `EVAL-*`, `CTRL-*`, `END-01`, `RLD-01`, `BP-*`) look untouched by this work, but "looks untouched" is exactly the assumption being tested — say which you checked and how.
    - **The bounded version of that check, swept this round so you need not re-derive it: 17 of the 28 rows name a build or candidate older than this branch.** Fourteen carry a concrete identifier and three describe one in prose:

      | Row | Status | Evidence cites |
      | --- | --- | --- |
      | `BP-02`, `BP-03`, `BP-04`, `CTRL-02b`, `EVAL-05b`, `EVAL-03`, `EVAL-06`, `SET-01`, **`CFG-01`** | `aligned` | `0.3.0+g8c6f030f7256` |
      | **`EX-01`**, `EVAL-04` | `aligned` | `0.3.0+g0ecce4d08bfb` |
      | `CTRL-01`, `END-01` | `aligned` | `0.3.0+g10b1d65475b8` |
      | `CTRL-02a` | `aligned` | `0.3.0+g19772c378d36` |
      | `EVAL-07`, `BP-06` | `not yet verified` | prose ("Installed build/Pure") |
      | `EVAL-08` | `not yet verified` | prose ("previous candidate") |

    - **An older build ID does not by itself make a row stale, and saying so is the point of the exercise.** A row recording that *build X did Y* stays true of build X forever. What has to be re-read is whether the **behaviour it describes still holds in what ships now** — so for each of the 17, decide which of three things is true: the behaviour is unchanged and the row stands (state that you checked), the behaviour moved and the row needs re-validation against the current build, or the row's subject no longer exists. `CFG-01` and `EX-01` are the two I could confirm are in the second category from the diff alone; the other fifteen need your reading, and **eleven rows cite no build at all**, which is not automatically safer — it may just mean their evidence is vaguer.
    - **The eleven no-build rows, read this round so you can verify rather than re-derive:** `EVAL-09`, `ATT-01`, `RLD-01`, `BP-01`, `EVAL-01`, `EVAL-02`, `STEP-01`, `VAR-01`, `VAR-02`, `VAR-03`, `EVAL-05a`. Their subjects are evaluation/thread suspension, attach-in-Edit-Mode, domain reload, a breakpoint hit, hover/watch, stepping, and variable expansion. **None of those subjects is touched by Tasks 1–8**, whose user-visible surfaces were the capability block, the version gate, `logFile`, dead-code removal, Player targets, discovery robustness, the attach configuration and the picker/command/setting. So my reading is that all eleven stand.
      - **Two deserve a second look and are the reason this is guidance rather than a verdict.** `RLD-01`'s scenario is "Ordinary Play/Domain Reload with **default exception filters**", and Task 1 changed the filter ids (`uncaught` → `unhandled`). It stands only because **both filters default to `false`** (`UnityDebugSession.cs:60-73`), so "default" means none active and the rename does not alter the scenario — a fact worth stating rather than assuming. And `ATT-01` is about attaching, which is the surface Task 7 rebuilt; its scenario is Edit Mode → Play with a discovered Editor, which neither `endPoint` nor `projectPath` nor the version gate changes, but the reasoning should be written down rather than left as "attach rows are fine".
      - If you disagree with either, say so — I read the *scenarios*, not the full row bodies, and a row whose scenario looks untouched can still carry a stale Pure column.
    - **Do not silently rewrite the older build IDs.** Rows citing `0.3.0+g…` or `0.3.1+g…` are evidence of what was observed *at that time*; the honest edit is to record the re-validation and its new build alongside, or to mark the row as needing re-verification. Rewriting the identifier would erase the fact that the claim was once true of a different artifact — the same reasoning as Rules A/B for the identity sites.
    - Note that `CFG-01`'s falsity is not a regression in the product: adding `endPoint`, `projectPath` and a setting is the **intended** work of this plan. What is wrong is the matrix, and it is wrong in the one direction that matters most — `aligned` is a claim of verified equivalence, so a stale `aligned` row is worse than a missing one.

18. **Our function-breakpoint predicate is stricter than the reference for an empty function name — a deliberate divergence to record, not to fix.** Task 9's reviewer found it and I confirmed the reference side from the bundle: the reference's condition is exactly `s.debug.breakpoints.find(e => e instanceof s.FunctionBreakpoint)` with **no name check at all**, while ours requires `typeof functionName === "string" && functionName.length > 0` (`functionBreakpointWarning.ts:25`).

    - **The only input where they differ** is a `FunctionBreakpoint` whose name is the empty string. That is not reachable through VS Code's own function-breakpoint prompt, so it needs either a corrupted stored entry or another extension calling the API — meaning the divergence is real, narrow, and benign in direction: **we emit strictly fewer, non-interrupting log lines than the reference would.**
    - Record it as `divergent` with that reachability stated, and **do not mark it `aligned` on the strength of automated evidence.** The two behaviours agree on every input a user can produce through the UI, which is exactly the situation where an automated test passing tells you nothing about the difference — and `AGENTS.md` requires every known difference to be called out regardless of how narrow it is.
    - Worth one sentence in `Decision`: the guard is brief-mandated, so this is not a defect to undo but a boundary to state. **A matrix that recorded only the reachable-through-the-UI comparison would be technically true and materially misleading**, which is the failure mode this row exists to prevent.

19. **Record the mirrored `getText(undefined)` quirk as `aligned` with the oddity documented — do not let it look like a bug, because a future reader will "fix" it.** Task 10 mirrored the reference's selection logic exactly, and I verified both sides this round:

    - **The reference**: `` t=e.document.getText(e.selection); if(!t){ let n=e.selection.start, r=e.document.getWordRangeAtPosition(n); t=e.document.getText(r) } ``
    - **Ours** (`unityApiReference.ts:99-109`): selection text if non-empty, otherwise `getText(getWordRangeAtPosition(selection.start))`.
    - The consequence is that **when the cursor is not on a word, `getWordRangeAtPosition` returns `undefined` and VS Code's `getText(undefined)` returns the entire document** — so on a single-line file we search for the whole line rather than doing nothing. That is genuinely odd behaviour, it is the reference's behaviour, and we reproduce it deliberately. The code says so in a comment at `:104-106`.

    Record it as `aligned` — we match — and put the quirk itself in `Decision`. **This is the row most likely to be edited away by someone reading the matrix as a to-do list**, since it is the only entry whose "aligned" status means "we faithfully reproduce something surprising". Saying that explicitly is the only thing that protects it.
20. **Two more differences to record, both found by verifying Task 10's report rather than its diff.** Neither changes what a user can do, and both belong in the matrix for the same reason as the item above:

    - **Our command opens the URL through `vscode.env.openExternal(Uri.parse(url))`; the reference calls `commands.executeCommand("vscode.open", Uri.parse(r))`.** Functionally equivalent for a single URL, different API surface, and ours is injected as a dependency so the command is testable without a host. Record as `divergent` on mechanism, noting the user-visible outcome is the same.
    - **Our setting uses `description` where the reference uses `markdownDescription`.** Both render the same string for plain text — and ours is byte-identical to the reference's resolved value, `Display background processes created by the main Unity process`, which I confirmed lives in the reference's **`package.nls.json`** under `configuration.displayBackgroundProcesses.description` (not in `out/nls.bundle.json`, and not under a `description` property, both of which are where a reader would first look and find nothing). Worth one line because the next person comparing manifests will hit the same three dead ends.

21. **The output channel writes a path-free refusal marker the reference never writes — a NEW user-visible divergence, and the first this work item has added rather than inherited.** `CHANNEL_REFUSED_LINE = "event=diagnostics.channel.refused"` (`extension/src/diagnostics.ts:31-40`) is emitted at `info` whenever `writeSanitized` refuses a line that names the user's home directory. The reference's `Unity` channel has no such line and no such guard.
    - **The user approved keeping it, at `info` level**, when asked directly in round 116, because it is a user-visible difference with no reference equivalent and `AGENTS.md` requires the question to be asked before the difference is preserved. **Record the row as `divergent` with the approval as its justification**, citing that the alternative was a silent drop.
    - The reasoning worth carrying into the matrix: the marker closes an observability gap — an over-broad match would otherwise eat a diagnostic with nothing to notice — and it is path-free by construction, so it cannot leak the thing the guard exists to hide. `info` rather than `warn` keeps it in the same stream as the event it replaced and leaves the function-breakpoint warning's level untouched.
22. **The guard is home-scoped, not workspace-scoped — also approved as a deliberate limit, and not the same claim as "refuses absolute paths".** `writeSanitized` refuses a line naming the **user's home directory**; workspace paths on another drive, `\\build-server\share`, `/opt/unity/proj` all pass, and the adapter's `PathRedactor` remains responsible for redacting them.
    - **The user approved keeping the split** in round 116, in preference to widening the guard to a syntactic "looks like an absolute path" test — which would refuse legitimate lines naming a Unity install root and start eating diagnostics that are safe to log. **Record the row as `divergent` with the approval, and state the limit explicitly in the row text**, because a reader who believed the broader claim would assume the half a debug log most naturally carries was covered.
    - Two sub-limits belong in the same entry rather than in separate rows: an unreadable `os.homedir()` **fails open** (writes the line — stated in code as a strictness-for-availability trade), and the ASCII continuation class omits non-ASCII letters, so `C:\Users\Adminé` is refused as though it were a sibling. Both are unreachable with today's callers.
23. **The deprecated-extension warning fires at a different moment from the reference's — approved, and fix round 1 closes it by adding an activation event.** The reference activates on `workspaceContains:**/Packages/manifest.json`, so its warning appears **when a Unity project is opened**; ours activated only on `onDebugResolve:unity-debugger-pure` and the five `onCommand:*` entries, so it appeared when you started debugging or ran a command.
    - **The user approved adding `workspaceContains:**/Packages/manifest.json`** when asked in round 118, in preference to `onStartupFinished` (which activates in **every** workspace, Unity or not) and to leaving the gap. **Record the row as `divergent`/resolved with the approval**, and note that the event is Unity-scoped so no non-Unity workspace pays.
    - **State the affected user rather than the event list**, because that is the actual cost and it is easy to miss: someone who installs both extensions and **never starts a debug session** saw no warning for the entire session. A reader comparing `activationEvents` arrays will see a timing difference; only the row text will say who it hurt.
    - **One correction that must reach the row, because the code's own comment had it wrong.** The implementer justified dropping the reference's broken link as avoiding a `MarkdownString` with `isTrusted: true` — a trusted, command-capable document interpolating a third-party extension's `displayName`. **That is not possible on this surface**: all four `showWarningMessage` overloads take `message: string`, and `MessageOptions` has only `modal` and `detail`, so **a notification has no markdown-rendering path at all**. The reference's link could never have rendered as a link, and the brief's "pass a whole `MarkdownString` as the message" option was never implementable. **The decision is unchanged; only the reason was wrong** — and the accurate statement is *"this surface is plain text"*, not *"clickable affordances are unsafe here."* Do not carry the invented risk into the matrix: a future reader deciding whether to add a click-through would find a security objection that does not apply.

In `AGENTS.md`, replace `zlorn.vstuc` with `VisualStudioToolsForUnity.vstuc` 1.3.2 and give the installed path `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2` together with its provenance (`D:\Unity\vstuc-mirror\build_local_vsix.py`: packaging metadata only). Replace the MyGame acceptance path with `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`, and record that it is Unity 2022.3.23f1, not the Tuanjie baseline.

- [ ] **Step 3: Update `README.md`**

Rewrite "Security and privacy" for non-loopback `endPoint` (including the plain statement that Unity's managed debug transport has no authentication, so attaching to a remote endpoint trusts whoever controls it). Remove "remote Player attach" from "Currently excluded". Add the new features and correct the domain-reload wording at `README.md:52` to say the mature session owns rebinding.

**`README.md` is not the only shipped file that is now wrong — two others ship in the VSIX, are user-visible, and no step in this task mentioned them. Both were found this round by asking which files `.vscodeignore` lets through:**

**(a) `SECURITY.md` contains a directly false sentence, and it is the more serious of the two.** Its final line reads:

> Version `0.3.2` supports loopback Editor attach only and does not provide remote-transport authentication.

**"Loopback Editor attach only" is exactly what this work removes.** Non-loopback `endPoint` and Player attach are the headline deliverables, so that sentence becomes false the moment Task 2 lands — and `SECURITY.md` is the canonical place a security-conscious user looks. Update it to match the README rewrite, in the same terms and with the same plain statement that Unity's managed debug transport has no authentication. **The two files must not disagree**: a README saying "remote attach is supported, unauthenticated, opt-in by explicit `endPoint`" next to a security policy saying "loopback only" is worse than either alone, because a reader cannot tell which one is current.

**(b) `CHANGELOG.md` has no entry for any of this.** Its `## 0.3.2 - 2026-09-24` section lists four backend fixes and nothing about Player targets, `endPoint`, the two new commands, the picker, the `displayBackgroundProcesses` setting, the `endPoint`/`projectPath` attach properties, the function-breakpoint warning, or the output channel — all of which are user-visible additions. Add an entry describing them.

- **Do not bump the version, and the reason is much larger than it first appeared.** An earlier revision of this brief said `0.3.2` was hardcoded in "four places outside `package.json`". **That count was wrong** — I checked it this round by grepping the whole tree, and the string appears in **27 tracked files** outside `superpowers/` and `vendor/`. The ones that would actually break:

  | Area | Files |
  | --- | --- |
  | Scripts | `scripts/stage-adapter.mjs:6`, `scripts/verify-vsix.mjs` (3×), `scripts/verify-release-artifact.mjs` (2×), `package.json` (4×) |
  | CI | `.github/workflows/publish-open-vsx.yml` (6×, including the `v0.3.2` release-tag input), `.github/workflows/release.yml` (2×) |
  | Adapter | `adapter/src/UnityDebugger.Adapter/Diagnostics/BuildIdentity.cs`, `.../Dap/AttachArguments.cs` |
  | Docs | `README.md` (3×), `SECURITY.md`, `CHANGELOG.md`, `docs/release-checklist.md` (6× including the never-reuse list and the Cursor version check), the matrix (15×) |
  | Tests | `tests/build/release-artifact.test.mjs` (5×), `tests/package/vsix.test.mjs` (4×), `tests/build/release-workflows.test.mjs`, `tests/build/repository-boundary.test.mjs`, `tests/build/scaffold.test.mjs`, `tests/extension/productIdentity.test.ts`, `tests/adapter/.../BuildIdentityTests.cs` (5×) |

  So a version bump is not a rename — it is a coordinated change across build scripts, CI inputs, the adapter's build identity, four shipped documents, and six test files that assert the current value. **That is a release decision well outside this work item**, which is why the entry goes under the existing version heading or a clearly-labelled `Unreleased` one, and the task says which it chose.
- **The miscount is worth leaving visible rather than quietly fixing, because it is the same defect class as the brief errors Task 9 disputed.** "Four places" was a number I produced from memory of the two npm scripts I had just read, presented as though it were a count. The actual answer took one `git grep -c`. **A brief that states a quantity has made a claim, and an unchecked quantity is the cheapest kind of wrong claim to ship** — the instruction it supports (do not bump) was right, and the justification under it was not.
- **How these two were missed is worth recording, because it is a repeat.** Both files are in `verify-vsix.mjs`'s required list, so the packaging machinery has been treating them as first-class shipped artefacts all along, and both are excluded from nothing in `.vscodeignore`. The task's Step 3 was written by listing files I was already thinking about — README, AGENTS.md, the matrix — rather than by asking **which shipped files this change makes false**. That is the same enumeration-versus-derivation mistake as the seven missing divergences in round 65, and the check that found it is the same one: read what actually ships, not what you remember shipping.

**The enumeration is now closed, so you do not have to re-derive it — and here is the whole of it.** I listed all **33 entries** in the shipped `dist/unity-debugger-pure-0.3.2.vsix` and classified each. **Four of them are documents this work makes false, and Step 3 now covers all four:**

| Shipped entry | Status |
| --- | --- |
| `extension/readme.md` | **false** — security section, exclusions list, domain-reload wording |
| `extension/SECURITY.md` | **false** — "loopback Editor attach only" |
| `extension/changelog.md` | **silent** — no entry for any of this work |
| `extension/package.json` | **stale** — commands, settings, configuration attributes |

The other 29 are unaffected and need no edit: 19 adapter binaries plus `build-info.json` and `UnityDebuggerPure.exe.config` (all regenerated by the build), `dist/extension.cjs` (compiled), `images/icon.png`, `LICENSE.txt`, the three MIT licence texts under `third-party/licenses/`, `third-party/runtime-assemblies.json`, `THIRD_PARTY_NOTICES.md`, and the two generated VSIX manifests. **`THIRD_PARTY_NOTICES.md` in particular does not change**, because this work adds no third-party code — `dependencies` is empty and `verify-third-party` exits 0.

- **One packaging detail worth knowing when you look for your own edits inside the artifact:** `vsce` **lowercases** the readme and changelog — the VSIX contains `extension/readme.md` and `extension/changelog.md` — but leaves `SECURITY.md` and `THIRD_PARTY_NOTICES.md` uppercase. `verify-vsix.mjs` is indifferent because it keys and compares every path through `.toLowerCase()` (`:30`, `:68`), so this is a note for a human reading the archive, not a check you need to satisfy.
- **Treat this table as the answer to "what else does this contradict?" rather than as one more entry in a list.** Rounds 78 to 80 each found one more file by looking one step further, which is the signature of a method that is enumerating rather than deriving; the table above is the derivation, produced by asking what ships and testing each entry against the change.

**One more file, found by applying the same question to the repository instead of the artifact: `docs/release-checklist.md` names the acceptance project in three places and is now stale.** It is the only markdown file under `docs/` besides the matrix, it is not shipped (so Round 81's artifact sweep could not have found it), and it is where the release procedure actually lives.

| Line | Text | Why it is now wrong |
| --- | --- | --- |
| `:14` | "Confirm the checksum matches the exact VSIX accepted in **MyGame**" | The acceptance project is `sgproj_xiaolonghan_0003` by your decision |
| `:68` | "## 8. Installed extension migration and **MyGame** acceptance" | Same |
| `:72` | "Migrate only the old debugger entry in **MyGame's** `.vscode/launch.json`" | Same |

- **Update those three to the new acceptance project, using the same path Task 12 uses in `AGENTS.md:32`.** Leaving them would put the release checklist and the agent instructions in direct conflict about which project acceptance means — **the identical failure shape as `SECURITY.md` contradicting the README**, and worse here, because a release checklist is followed literally by whoever cuts the release and there is no obvious reason to doubt one of its two disagreeing sources.
- **`:73-74`'s acceptance list also needs the new features added.** It currently reads "Complete the reachable-breakpoint, stepping, pause, inspection, Domain Reload, continue, and disconnect acceptance checklist" — every item of which predates this work. Add the user-visible surfaces this change introduces: the `Unity Debugger Pure: Attach Unity Debugger` command and its picker, Player/`endPoint` attach, and the `Unity Debugger Pure: Unity API Reference` command. **Task 13 owns performing the acceptance; this task owns the checklist that describes it**, and a checklist that omits three of the release's headline features is a checklist that will pass without testing them.
- **Two lines deliberately left alone.** `:78`'s "Never reuse or overwrite accepted `0.1.0`, `0.1.1`, `0.2.0`, `0.3.0`, or `0.3.2`" is consistent with the no-version-bump instruction above and should not be edited. `:70`'s reference to uninstalling `unity-debugger-community.unity-debugger-vscode` is a *different* extension from the `local.vstuc` reference this work coexists with, so the coexistence decision does not affect it — do not delete it on the assumption that it is about the reference.

**Two further stale spots I found this round while reading the sections you are rewriting — they are not in the list above because I had not read the whole page before.** Both are the same defect as the three you are already fixing: a sentence that was true when written and is now false.

- **`:74` says `- local Editor attach;`** — no longer the whole story. This work adds Player targets and a non-loopback `endPoint`, so "local" is now a restriction the product does not have. Say what is actually supported: a discovered local Editor, a discovered Player via its advertisement, and an explicit `host:port` endpoint.
- **`:81-83`'s excluded list says `Android/iOS`** — and that one needs a decision rather than a deletion. Task 5 makes a debug-enabled Player advertisement discoverable *including an Android one* (the live probe recorded `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)`), so the **mechanism** now exists; what does not exist is any verification that an Android attach works end to end. So do not simply delete the entry, and do not leave it reading as unsupported either. State it the way this project states everything else: the path exists and is **unverified**. `IL2CPP`, `WebGL`, `Tuanjie Mini Game DebugProxy`, `native C++`, `multiple simultaneous Editors` and `non-Windows platforms` all remain genuinely excluded and should stay.
- The rest of the page holds up: `:75` (`Mono managed C# debugging`) is still accurate, `:77-79` remains correct that 2022.3 is the formal baseline and Unity 6 connects unverified — though note Task 7 added a *second*, target-neutral warning for the case where the version cannot be determined at all, so "shows one warning for versions outside the formal baseline" is now one of two messages rather than the only one. And `:92`'s "The extension has no telemetry" is true and should stay true — it is the sentence that makes the reference's Application Insights client a deliberate omission rather than an oversight.

- [ ] **Step 4: Amend the superseded scope clause**

In `2026-08-06-reference-debugger-engine-parity-design.md`, replace the out-of-scope bullet at `:50-51` and the paragraph at `:76-80` with the lifted boundary: reference binaries may be inspected as behavioural evidence; no reference code is copied, linked, packaged, or redistributed; no reference binary becomes a build input or runtime dependency. Cite the 2026-10-16 user decision and note that prior practice (`docs/reference-debugger-compatibility.md:151`) already decompiled one of these assemblies.

- [ ] **Step 5: Commit**

```bash
git add AGENTS.md README.md \
        docs/reference-debugger-compatibility.md \
        docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md \
        docs/superpowers/plans/2026-08-06-reference-compatible-mature-debugger-backend.md
git commit -m "docs: record VSTU alignment matrix, reference identity, and scope amendment"
```

**Do not stage the `docs` directory, and read this before you stage anything.** An earlier revision of this step was `git add docs AGENTS.md README.md`. That is not merely untidy here — it is a live contamination risk in this specific repository, for two reasons:

1. `docs/` holds this plan and this spec, which **the controller is editing continuously between tasks**. A directory-wide stage at the wrong moment commits the controller's in-flight edits under your message, producing a commit whose message describes work it does not contain — the same failure mode as the Task 1 incident recorded in the executor notes, where an implementer's `--amend` folded a test hunk into the controller's docs commit and hid those lines from the commit-scoped review package.
2. `docs/` also holds frozen historical plans that must not be touched at all. This task edits exactly two of them, and only at the lines Rules B and C name.

Name the five paths above explicitly. Then run `git status --short` and confirm the only entries remaining are the six `adapter/vendor/*/packages.lock.json` files plus whatever the controller is currently editing — if you see a plan or spec file you did not intend to change staged, unstage it rather than committing it.

---

### Task 13: Package verification and real-Editor acceptance

**Files:**
- Modify: `docs/reference-debugger-compatibility.md` (evidence log only)

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Run the full automated gate**

Run: `npm run build && npm run typecheck && npm test`
Expected: PASS — build tests, extension tests, adapter tests, integration tests. Record the exact counts.

- [ ] **Step 2: Prove no reference binary entered the package**

Run: `npm run package`
Expected: PASS — the VSIX audit, runtime inventory, and third-party provenance checks all succeed, and the audit reports no `SyntaxTree.*` or `Microsoft.VisualStudio.*` entry.

**Which script enforces which part, verified this round — the sentence above names the right outcome but the wrong enforcer, and knowing the difference is what makes a PASS meaningful.** `scripts/verify-vsix.mjs` (279 lines) checks **structure**: ZIP path safety, duplicate entries, every file under the allowlisted VSIX root, five forbidden *path* patterns (`tests/`, `.git`, `src`, `adapter/vendor/`, anything matching `testadapter`), and an eleven-path required list. It contains **no** assembly-name check at all.

- The forbidden **assembly** guarantee comes from **`npm run inventory:runtime` → `scripts/generate-runtime-inventory.mjs --verify`**, which compares the staged adapter against the pinned `third-party/runtime-assemblies.json` (4171 bytes) and throws ``No audited runtime origin for ${name}`` (`:88`) for anything not in that list. An assembly from either family would fail **there**, not in the VSIX audit.
- So when you record the evidence, cite the runtime-inventory output for the assembly claim and the audit output for the path claim. Writing "the VSIX audit reports no `SyntaxTree.*` entry" would be a true-sounding statement about a check that does not exist, and a later reader trying to reproduce it would find nothing to run.
- Two facts that make Step 2's chain work at all, both confirmed: `npm run package` is `build && stage:adapter && inventory:runtime && vsce package --out dist/unity-debugger-pure-0.3.2.vsix && verify:vsix && test:package`, and `verify-vsix.mjs` **requires** `extension/third-party/runtime-assemblies.json` to be present in the artifact — so a missing or stale inventory fails the audit rather than passing silently.

**A stale artifact is sitting at exactly the path Step 2 audits, and it produces a false PASS — check this before you trust any audit output.** `dist/unity-debugger-pure-0.3.2.vsix` currently exists and is dated **2026-09-24 18:45**, roughly three weeks before this branch's work. Both `scripts/verify-vsix.mjs` and `tests/package/vsix.test.mjs:9` read that path, and `test:package` is **not** in `npm test`'s chain (`build && extension && adapter && integration`), so **nothing in Step 1 touches it**.

- **The failure mode is silent and specific:** running `npm run verify:vsix`, or `npm run test:package`, *before* `npm run package`, validates the September artifact and reports success. Every assertion would be about an extension that predates Tasks 1–8 — including `CFG-01`'s claim that we contribute no setting and no `endPoint` properties, which that artifact genuinely does not. A PASS from the stale file is therefore not merely uninformative; it would actively confirm the **superseded** state.
- **So Step 1 of this brief is "run `npm run package"`, and any audit evidence you record must be from the artifact that command just wrote.** Confirm the timestamp moved (`dist/unity-debugger-pure-0.3.2.vsix` should be minutes old, not weeks) and, better, record the artifact's own SHA-256 alongside its `--out` path. If you ever need to re-run `verify:vsix` by hand after that, it is safe; running it cold at the start of the task is the trap.
- Worth knowing while diagnosing: `dist/` is **gitignored**, so this artifact is invisible to `git status` and to every Files-vs-staging check in this plan. That is precisely why it was able to sit there unchanged through eight tasks — **a stale input that no diff, no test and no review would surface**, because everything that reads it is designed to read a file that `package` is supposed to have just created.
- The other files in `dist/` are historical candidates (`0.3.0-superseded-*`, `*-candidate.vsix`, an `mcp` package) and none is read by any script — **39 files at the time of writing**, an earlier revision of this brief said "~30", so treat the directory list as the authority. Leave them; do not "tidy" them as part of this task, since they are evidence of earlier work and deleting them is not in scope.

**Do not run `npm run verify:release-artifact` as part of this task, and if you do, do not read its failure as a defect — it will fail for a legitimate reason.** That script is a *release* gate: `scripts/verify-release-artifact.mjs` re-hashes `dist/unity-debugger-pure-0.3.2.vsix` and compares it against the sibling `.sha256`, then checks the packaged `extension/package.json` identity against the version argument.

- **`npm run package` does not regenerate the checksum file.** Its chain is `build && stage:adapter && inventory:runtime && vsce package --out … && verify:vsix && test:package` — no hashing step. So the `.sha256` next to the artifact is the September one, and after you run `package` the pair is genuinely inconsistent: a **new** VSIX against an **old** checksum. `verify:release-artifact` would correctly report `Release artifact SHA-256 mismatch.` (`:41`) and that is evidence the gate works, not that the package is broken.
- **If you want that evidence anyway, regenerate the checksum first** and record both the artifact and the checksum you produced, so the pair you validated is the pair you built. Otherwise leave the gate alone and cite `verify:vsix` plus `test:package`, which is what `npm run package` actually runs and what Step 2 asked for.
- `package.json`'s version is **`0.3.2`**, matching the artifact name, so the identity check in that script has nothing to complain about once the hash is fresh. This is the only version-drift question in the packaging path, and it currently resolves correctly.

**Two packaging facts verified this round, so a Step 2 failure can be diagnosed rather than guessed at.** All eleven paths in `verify-vsix.mjs`'s required list are satisfiable from this tree — but not all by the same mechanism, and the odd one out is worth knowing:

- **`extension/LICENSE.txt` is the only required path that no script produces.** The repo has `LICENSE` (no extension); `vsce` includes it and **renames it to `LICENSE.txt`** while packaging. The September artifact confirms it — it contains `extension/LICENSE.txt` and **no** root-level `LICENSE` — and `.vscodeignore` does not exclude it. `package.json`'s `license` is `MIT`. So if Step 2 ever fails on `Required VSIX file is missing: extension/LICENSE.txt` (`verify-vsix.mjs:69`), the cause is a `vsce` behaviour change (3.9.2 here), not our staging, and the fix is to add the file explicitly rather than to hunt through `scripts/`.
- The other ten are produced by machinery you can point at: `dist/extension.cjs` by `esbuild.mjs`, the two `adapter/win32-x64/` files by `scripts/stage-adapter.mjs` (which throws `Release output has no UnityDebuggerPure.exe.` if the build is missing), `third-party/runtime-assemblies.json` via the `.vscodeignore` re-include, and `README.md`, `CHANGELOG.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`, `images/icon.png` and `package.json` as ordinary repo files that `.vscodeignore` does not exclude.
- **`.vscodeignore` also tells you what Task 12's changes do and do not affect.** `AGENTS.md` and `docs/**` are excluded, so the repository's agent instructions and the compatibility matrix **never ship**; `README.md` is not excluded, so its rewritten security section **does**. Worth stating because it settles a question the acceptance record would otherwise have to hedge: the security statement is a user-visible claim in the artifact, and the matrix is internal evidence for it.

**`adapter/win32-x64/` is also gitignored, also stale-looking, and also fine — do not raise it as a defect.** Its files are dated **2026-09-24 18:37** and its `build-info.json` names commit **`e7d74f1dcda7`**, a September commit, which together look exactly like the stale-VSIX problem one directory over. They are not, and the difference is worth knowing before you look:

- **`scripts/stage-adapter.mjs` deletes and recreates that directory on every run** (`fs.rm(..., { recursive: true, force: true })` then `mkdir` at `:56-57`), copying from `adapter/src/UnityDebugger.Adapter/bin/Release/net48/` — the output `npm run build` just produced. It then computes `git rev-parse HEAD` and writes a **fresh** `build-info.json` (`:64`, `:78`). So `npm run package` regenerates both the binaries and the commit stamp; the September timestamp only means nobody has packaged since.
- **The path is worth the paragraph because a stale-looking build directory next to a genuinely stale artifact is the most misleading thing in this task.** I raised it as a potential critical finding and disproved it by reading the script's first sixty lines — which is the right order, and the outcome to expect when a scary signal sits near a real one. If you find yourself wanting to "clean" `adapter/win32-x64/` first, do not: `stage:adapter` owns that directory and deletes it anyway.
- **The inventory check is what makes the regeneration trustworthy rather than merely plausible.** `generate-runtime-inventory.mjs --verify` compares the freshly staged directory against the pinned `third-party/runtime-assemblies.json` (`generatedFrom: "adapter/win32-x64"`), so a new build that introduced or lost an assembly **fails** rather than shipping quietly — which is the same interlock described above, applied to the adapter instead of the archive.

- [ ] **Step 3: Get the Editor running on the acceptance project**

Ask the user to open `D:\workspace\sgproj1\sgproj_xiaolonghan_0003` in the Editor. Confirm discovery succeeds afterwards (the project currently has no `ProjectSettings\ProjectVersion.txt`); if the Editor does not create one, that path is exactly what Task 6 makes survivable, so attach must still work.

Verified facts about this project, gathered before starting so they need not be re-derived:

- Unity version is **2022.3.23f1**, not the Tuanjie baseline, so the version-compatibility row is `compatible-unverified`, not `aligned`.
- `com.unity.ide.visualstudio` is **2.0.22**, which clears the reference's own stated requirement of `>= 2.0.20`. **A/B against the reference is therefore possible in this project** — if this were below 2.0.20 the reference could not attach and Step 5 would be impossible rather than merely unverified.
- Its `.vscode/launch.json` currently contains exactly one configuration: `{"name": "Attach to Unity", "type": "vstuc", "request": "attach"}`.
- No `Library\EditorInstance.json` process is alive (the recorded pid is dead), so nothing is discovered until the Editor is started.
- **That file carries no port, so discovery derives one — and knowing the number is what lets you tell a working derivation from a silent fallback.** Verified this round: the `EditorInstance.json` files on this machine contain **only** `process_id`, `version`, `app_path` and `app_contents_path`. There is no `address` and no `debuggerPort`, even though the filename suggests an endpoint record.
  - `editorDiscovery.ts:164` therefore calls `defaultEditorPort(instance.processId)` and labels the candidate `source: "derived-port"` (`:171`), a path with its own test coverage. The formula is `56000 + pid % 1000`.
  - **The acceptance Editor is now running** — the user opened it, and the values as of that moment are pid **91740**, alive, with `Temp\UnityLockfile` **held** by it. **The derived port is therefore 56740.** Look for `Port:56740 (Mono)` in the picker label; **a different port means the derivation did not fire and something else supplied the endpoint.**
  - **Re-check the pid before you use that number**, which is why the value is dated rather than stated as a fact: it comes from a file the Editor rewrites on every start. An earlier reading of this same project recorded pid 42260 and derived 56260; the number changed because the Editor restarted. **A hardcoded port in an acceptance checklist is wrong the first time Unity restarts.**
  - Note also that a **second** Editor is running on `sgproj` (pid 54548, port 56548). Two instances are expected — `AGENTS.md`'s `MyGame` instruction is superseded here by the user naming this project as the acceptance target — so the picker may legitimately show more than one row. **The row you want is 56740**; if `sgproj` also appears, that is correct behaviour rather than a discovery bug, and it is a useful check that the label's project name distinguishes them.

  **Two environment facts re-verified at round 52, both of which affect how you ask the user to do this:**

  - **The acceptance project has a stale `Temp\UnityLockfile`** — zero bytes, dated **2026-09-22**, from an Editor that died. It is **not held** by any process (checked by attempting an exclusive open), so it is harmless: Unity will open the project normally. But if the Editor shows a "project is already open" or lockfile warning on startup, *that* is why, it is expected, and it is not a fault in this work. Do not delete the lockfile to "fix" it unless Unity actually refuses to open — deleting a lockfile for a project an Editor really does hold is a good way to lose work.
  - **The Unity Editor currently running on this machine has a different project open.** Process `54548`, started 2026-10-15 20:21, window title `sgproj - GameEntry - Android - Unity 2022.3.23f1 <DX11>`, and its held lockfile is `D:\workspace\sgproj\Temp\UnityLockfile`. So when you ask the user to open `sgproj_xiaolonghan_0003`, be explicit that it means a **second** Editor instance unless they choose to close the first. Both are Unity **2022.3.23f1**, the same version, so either arrangement works — but say which you expect rather than leaving them to guess, and do not close the running Editor on their behalf.
  - The running Editor being on the *same Unity version* is mildly useful — if `sgproj` were ever a more convenient A/B target it has an identical Editor build — though the acceptance project remains the agreed one, and its `com.unity.ide.visualstudio 2.0.22` is what makes the reference side of the A/B possible at all.
  - Also in the acceptance project's `Packages\manifest.json` and not previously recorded: `com.boxqkrtm.ide.cursor`, a **local `file:` package** pointing at `../../../sgproj/FilePackages/com.unity.ide.cursor`, plus `com.unity.ide.rider 3.0.28` and `com.unity.ide.vscode 1.2.5`. None block anything — the reference's requirement is on `com.unity.ide.visualstudio`, which is satisfied — but the `file:` package is worth knowing about, because a local package whose path lies outside the project can fail to resolve if that sibling directory moves, and a package resolution failure would present as a discovery problem.
- The project is outside this repository. Any edit you make under it (for example the launch configuration in Step 5) is **not** part of this repo's history — never `git add` it here.

  **One check to run here, added after Task 7, because it only surfaces on a real project.** Tasks 6 and 7 interact on the version field, and the interaction is silent: Task 6 falls back to `version` in `Library\EditorInstance.json` when `ProjectVersion.txt` is absent, and Task 7 guards that fallback so a version the policy cannot use degrades to `""` (unknown, attachable) rather than reaching `classifyVersion` as malformed (blocking). So after the Editor is running, **confirm which version string actually reaches the gate and that it lands on `compatible-unverified` rather than `unsupported`**:

  1. Read `Library\EditorInstance.json`'s `version` verbatim and state it. The recorded value is `2022.3.23f1`, with **no revision suffix** — a suffixed value of the `2022.3.62t11 (b1a0a2c)` shape is the case the guard exists for.
  2. Confirm whether the Editor created `ProjectSettings\ProjectVersion.txt` when it started. Either answer is informative: if it did, the primary path is exercised and the fallback is not; if it did not, the fallback is the live path and this is exactly the case the whole work item was about.
  3. Confirm attach **proceeds**. A block here with a message like `Malformed or unsupported Editor version:` means the guard is not doing its job — a real finding to report rather than a step to work around.

  Record all three in the evidence log. This is the one place the version-policy change can be observed end to end, and it cannot be reproduced in a unit test against a synthetic `EditorInstance.json` — the value Unity actually writes is the input that matters.

- [ ] **Step 4: Identify and prove a reachable breakpoint target**

Pick a method in the project that is provably reached (an `Awake`/`Start` on an object in the active scene, or a bootstrap path), and record the evidence that it executes. Do not use an unproven candidate, and do not read a missed breakpoint as a debugger failure.

**Two of the three `not yet verified` rows need a specific type, and I traced one to a concrete call site — including its reachability, which is what this step exists to demand.**

`EVAL-07` and `EVAL-08` both concern `FightUnitAttrUtils.GetAttrCurrValue`. The acceptance project contains every identifier those rows name (`FightUnitAttrUtils`, `EFightUnitAttr`, `GetAttrCurrValue`) and the `SGBattleLogic` namespace. **The call site is:**

```
D:\workspace\sgproj1\sgproj_xiaolonghan_0003\Assets\Scripts\sgcode\Scripts\Common\CharacterDebug.cs:383
    var value = FightUnitAttrUtils.GetAttrCurrValue(LogicEntity, attr);
```

inside `public void UpdateAttrInfo()` (declared at `:366`). **The reachability chain, traced rather than assumed:** `UpdateAttrInfo` is called only by `SafeUpdateAttrInfo` (`:1611-1613`), which has three callers — `CharacterDebug.cs:612` **guarded by `if (AttrInfoDebug)`**, `CharacterDebug.cs:1361`, and `CharacterDebugSnapshotReader.cs:575`/`:639` via **reflection** (`InvokeNoArgMethod(characterDebug, "SafeUpdateAttrInfo")`).

- **So there are two practical triggers and the run needs one of them**: set `AttrInfoDebug` and let the debug panel refresh, or drive `SafeUpdateAttrInfo` through the snapshot reader. **If a breakpoint at `:383` never fires, check which trigger was used before concluding anything** — the guard at `:612` means the method can simply not be called, and `AGENTS.md` is explicit that an unreachable path proves nothing about the debugger. This is the same "is the candidate actually reached?" question the paragraph above asks, answered for one specific candidate so the run does not stall on it.
- **One breakpoint and two Watch expressions cover both evaluation rows.** `EVAL-07`'s expression is `FightUnitAttrUtils.GetAttrCurrValue(LogicEntity, (EFightUnitAttr)1033)`; `EVAL-08` is about passing the **integer** `1033` to an enum parameter, so run the cast form **and** the form without it, since the difference between them is the whole subject of `EVAL-07`.
- **`BP-06` needs no particular method** — it concerns a bound breakpoint's **filename** after a status change and a source saved **outside the attached workspace** — but it does need a saveable source file, so run it **last**, after the evaluation rows, in case the saved-file state perturbs the session.
- **What these three rows still cannot settle, so do not over-record them:** every one compares our behaviour against a **user report** of the reference rather than an observation you can re-run. The 2026-09-22 screenshots are the only direct reference evidence for `EVAL-08` and `BP-06`, so a passing run makes them `aligned` on our side while the reference side stays as-reported — say so in the row rather than implying both were observed together.

- [ ] **Step 5: Run the A/B acceptance and record it**

**First add the configuration this step needs.** The project ships only a `vstuc` configuration, so there is currently no way to start our debugger from it. Add a second entry to that project's `.vscode/launch.json` pointing at Unity Debugger Pure — `{"name": "Attach to Unity (Pure)", "type": "unity-debugger-pure", "request": "attach"}` — keeping the existing `vstuc` entry so both sides of every A/B run use the same action sequence from the same file. That edit lives in the acceptance project, not this repo.

Also exercise the headline `endPoint`/player path explicitly, not only the discovered-Editor path: a non-loopback `endPoint` attach (Task 7) and the `Attach Unity Debugger` command (Task 8) have no equivalent in the default configuration, so a run that only uses the default config leaves them `not verified`.

For each item implemented by Tasks 1, 2, 3, 5, 6, 7, 8, 9, 10, and 11, run the same action sequence with the reference extension (configured as `"type": "vstuc"`) and with Unity Debugger Pure, then set the row's status. Mark anything not exercised `not verified` with the reason.

- [ ] **Step 6: Commit the evidence**

```bash
git add docs/reference-debugger-compatibility.md
git commit -m "test: record VSTU alignment acceptance evidence"
```

- [ ] **Step 7: Report**

State the versions, build ids, VSIX and adapter SHA-256 values, the exact automated test counts, the Editor version actually used, and every matrix row still `divergent` or `not verified`. Claim "reference-compatible" only when no row is in either state.

---

### Task 14: Consolidated minor pass

Findings accumulated from the reviews of Tasks 1, 2 and 3. They were deliberately **not** fixed per task: each fix round costs a full `dotnet test` cycle against whichever task is currently building, and two of these sit in code that a later task has not yet touched. This pass runs once, against a static tree, before the final whole-branch review.

**Two of these are defects, not coverage nits, and are marked as such.** Do not downgrade them to "nice to have" because they are listed beside test-tidiness items — the list is grouped by area, not by severity.

### Splitting this task into two, and they can run in parallel

**The list holds 48 items — numbered 1 through 47 plus `11b` — and an earlier revision of this section said 36. That was wrong, and the way it was wrong is worth keeping.** I counted with `^\d+\. \*\*`, which matches only items whose text opens with a bold marker. Items **3–9, 11, 11b, 12, 13 and 14** are written `3. Pin that …` **without** the marker, so a bold-anchored count skipped twelve real items and I then wrote the conclusion into this plan and into two agents' dispatches.

| Regex | Matches | Meaning |
| --- | --- | --- |
| `^\d+[a-z]?\.\s` | **48** | **the real count** |
| `^\d+[a-z]?\.\s\*\*` | 36 | what this section, and the two dispatches, quoted |

**Twelve items were never assigned, and the completion claim would have rested on "nothing is unassigned".** The twelve are 3–9 (DiagnosticLog and AttachArguments coverage gaps), 11 (the `AttachArgumentsTests.cs` move), 11b (the `ReferenceCapabilityTests.cs:54-126` body key-set pin), 12 (the `ExceptionFilterIds` input-shape matrix), 13 (the integration-test `DapClient.child.pid`), and 14 (a fixture hoist). **Item 6 is the one to prioritise**: the untested `logFile.redirectFailed` fallback is what carries the claim that a bad `logFile` cannot cost the user their session.

**They partition by file and by toolchain.** Grouped by the file each item **targets** — not the files it mentions, a distinction that misfiled one item and left two more unclaimed:

| Items | Area | Toolchain to verify |
| --- | --- | --- |
| 1, 2, 10, 15, 16, 17, 22, 27 | `adapter/src/**`, `tests/adapter/**` (C#) | `dotnet test` |
| 13 | `tests/integration/**` (TypeScript) | `vitest run tests/integration` |
| 3–9, 11, 11b, 12, 14 | `adapter/**`, `tests/adapter/**` (C#) — **deferred until 14a frees the tree** | `dotnet test` |
| 18, 19, 20, 21, 23–26, 28–42, 46, 47 | `extension/src/**`, `tests/extension/**`, `tests/build/**` (TS) | `vitest`, `node --test`, `tsc` |
| 43, 44, 45 | Records — no code | none |

**So split it as:**

- **Task 14a — the adapter side.** Items **1, 2, 10, 15, 16, 17, 22, 27**. Verifies with `dotnet test` (`Failed 0, Passed 207, Skipped 5, Total 212` is the baseline to hold).
- **Task 14b — the extension side.** Items **18, 19, 20, 21, 23–26, 28–42, 43–47**, with **47 last**. Verifies with `npx vitest run tests/extension`, `node --test tests/build/*.test.mjs`, `npx tsc --noEmit`.
- **Task 14c — the twelve deferred items**, items **3–9, 11, 11b, 12, 13, 14**. **They must wait for Task 14a**, because every one of them targets `adapter/**` or `tests/adapter/**` — the tree 14a is editing. **Dispatching them in parallel is the one collision this partition cannot absorb.**
- **Items 43–45 are records, not fixes.** 43 and 44 are Task 14b's; **45 is recorded as awaiting acceptance** rather than claimed, because "no real VS Code host has exercised the in-place decoration" can only stop being true when someone runs the extension in a host.
- **Item 47 must be last within 14b**, because it re-derives line citations in `extension.ts` and that file is edited by items 28–32 and 35. Every other item in the list can be done in any order.
- **Items 3–9 and 11b are in a file whose hardcoded assertions must be read before it is changed**, and item 27's target — `tests/adapter/…/Dap/UnityDebugSessionLifecycleTests.cs` — was originally filed under the extension half because its row *mentions* `extension.ts`. **The file an item targets decides its half; the files it mentions do not.**

**Files:**
- Modify (**adapter, Task 14a**): `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs`, `adapter/src/UnityDebugger.Adapter/Program.cs`, `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs`, `adapter/src/UnityDebugger.Adapter/Backend/UnavailableDebuggerBackend.cs`, and under `tests/adapter/UnityDebugger.Adapter.Tests/`: `Dap/ReferenceCapabilityTests.cs`, `Dap/AttachArgumentsTests.cs`, `Dap/UnityDebugSessionLifecycleTests.cs`, `Diagnostics/DiagnosticLogTests.cs`, `Diagnostics/BuildIdentityTests.cs`, `Backend/MonoDebuggingBackendLifecycleTests.cs`
- Modify (**extension, Task 14b**): `extension/src/extension.ts`, `extension/src/debugConfigurationProvider.ts`, `extension/src/diagnostics.ts`, `extension/src/editorDiscovery.ts`, `extension/src/functionBreakpointWarning.ts`, `extension/src/outputChannel.ts`, `extension/src/playerAdvertisement.ts`, `extension/src/productIdentity.ts`, `extension/src/unityApiReference.ts`, and under `tests/`: `build/scaffold.test.mjs`, `extension/debugConfigurationProvider.test.ts`, `extension/editorDiscovery.test.ts`, `extension/extensionCommands.test.ts`, `extension/functionBreakpointWarning.test.ts`, `extension/productIdentity.test.ts`, `extension/unityApiReference.test.ts`, `integration/adapter.integration.test.ts`, `integration/dapClient.ts`

**This list was six files and is now twenty-eight, and the reason is the defect this whole work item keeps finding in its own briefs.** The original list named the files the **first three items** touch — the ones written when Task 14 was created — and never grew as items 15 through 47 were appended from later reviews. Every subsequent item named its own file in prose and nobody re-read the list.

- **Twenty-two files were being modified by a task whose checklist named six of them**, which is the same failure as round 126's `release-checklist.md` and Tasks 8 and 9's staging lists — **fourth instance, and by far the largest**. The commit protocol is keyed to this list, so the practical effect would have been a commit staging six paths while twenty-two files sat modified in the tree.
- **The fix is grouped by the two halves of the split**, because the two halves are what can be dispatched independently and each needs its own list. A single flat list would have re-created the problem the split solves.
- **And the general lesson is about how these lists decay rather than how they are written.** A Files list is accurate when the task is authored and drifts as the task grows — which is exactly the same mechanism as a **line citation** drifting as a file grows (round 112) and a **test count** drifting as a suite grows (round 124). **Anything a brief states in the present tense about a thing that changes will be wrong later, and the lists are no exception.**

- [ ] **Step 1: Fix the two real defects**

1. **`DiagnosticLog.cs:193` — lock ordering regression.** `FormatLine` now runs *outside* the `lock`, so the timestamp is sampled before lock acquisition and concurrent writers can emit non-monotonic timestamps in both files. The pre-Task-3 code formatted and wrote under the lock. Move `var line = FormatLine(...)` inside the `lock` so formatting and both writes are one critical section.
2. **`DiagnosticLog.cs:219` → `:230` — an unhandled shutdown exception.** `Dispose` can throw from `previous.Dispose()` when a user-supplied path has gone away (network share dropped, disk full). `log.Dispose()` in `Program.cs:87` sits **outside** `SafeWrite`, so it escapes `Main`'s `finally` as an unhandled exception. Task 3 added a catch for the *open* path precisely so a bad `logFile` cannot cost the user their session; the dispose path needs the same treatment or that rule is only half-enforced. Swallow the secondary-dispose failure.

- [ ] **Step 2: Close the coverage gaps**

3. Pin that `0.0.0.0` is accepted — `[InlineData("0.0.0.0")]` in the acceptance theory. It is currently verified by inspection only, and it is the literal an earlier rejection theory used to cover.
4. Pin a canonical IPv6 literal (`::1`), and one whitespace-padded host (`" 127.0.0.1"`), so the branch that rejects each is frozen. Keep the `::FFFF:127.0.0.1` casing behaviour as-is — rejecting it is deliberate.
5. Pin the **replacement contract** for `OpenSecondary` with a *different* path: it must replace the sink (dispose and flush the previous writer) rather than leak it or add a second one. The idempotency test currently takes only the early-return branch, so `CloseSecondary()` inside `OpenSecondary` is never executed by a test.
6. Cover the untested `logFile.redirectFailed` fallback with an unwritable path, asserting the attach still **succeeds** and the event lands in the primary log. That fallback carries the claim that a bad `logFile` cannot cost the user their session, so it should not be the one path with no test.
7. Tighten the secondary-sink bypass test to assert the secondary's **line count**. Today it checks for absence of the secret value, so a value-less leak (a line like `expressionText=` with no value) would still pass.
8. Assert the primary and secondary carry the **same** line, pinning the "written to both" contract rather than only spot-checking the primary for `event=attach`.
9. Cover post-`Dispose` use (`ObjectDisposedException`), an empty path (`ArgumentException`), and a blank or non-string `__logFile`.

- [ ] **Step 3: Close the test-structure gaps**

10. **Already done — verify and skip.** An earlier version of this item asked to pin the non-IP rejection message with `Assert.Equal` instead of `Assert.Contains`, at `AttachArgumentsTests.cs:71-74`. **Task 2's fix round already did it**: the loose assertion is gone from the tree, and `:77-80` now reads `Assert.Equal("__host must be an IP address literal, such as " + "192.168.1.50.", error.Message)`. Confirm it is still there and move on; do not re-do it. Recorded because an item that is silently already done is indistinguishable from an item that was skipped.
11. `AttachArgumentsTests.cs:6,8` now imports the Backend namespaces and constructs `MonoDebuggingBackend`, so a backend-level assertion lives in the DAP parsing test file while `MonoDebuggingBackendLifecycleTests.cs` already owns the fake and `AttachTarget` plumbing. Move it.
11b. `ReferenceCapabilityTests.cs:54-126` pins the **filters'** exact key set but not the **body's**, so an accidentally added capability field would pass silently. Assert the body's exact property-name set, which also documents the four accepted absences and `supportsDelayedStackTraceLoading`. Replace the hard `(JArray)` cast at `:77` with `Assert.IsType<JArray>` so a missing property fails with a clear message rather than a null-reference on `.Count`.**(This was mis-numbered `11` as well, so "item 11" was ambiguous — say `11b` when reporting it.)**
12. `ExceptionFilterIds` input-shape matrix: add `filterOptions` with `filters` absent entirely, and a mixed request carrying ids in both shapes (precedence `all` > `unhandled`/`uncaught`). The mixed case is what a client sends with stale UI state. **Find it by symbol — it moved to `UnityDebugSession.cs:1162`** (an earlier citation of `:1133-1151` was correct when written and was invalidated by Task 3's additions and Task 4's deletions; the call site is at `:428`).
13. Close the `pid` gap: the committed test compares against the **test host's** PID because `DapTestProtocol.Run` drives an in-process session. Add a `child.pid` accessor to `DapClient` and assert against it in `tests/integration/adapter.integration.test.ts`, which already spawns the real adapter.
14. Test tidiness: `DiagnosticLogTests.cs:182-199` is a near-verbatim copy of the fixture at `BuildIdentityTests.cs:52-69`. Hoist it into shared test support — graded Minor because the project already carries two per-file copies and no production logic is duplicated.
15. **`Continued` — decide its fate, do not sweep it.** Verified: `UnityDebugSession` subscribes to exactly five backend events (`Stopped`, `ThreadChanged`, `ModuleChanged`, `Output`, `Terminated`), so `Continued` is production-dead — it appears in `adapter/src` only as the declaration at `UnavailableDebuggerBackend.cs:11`, with no raiser and no subscriber. Task 4 correctly left it in place; the spec's cleanup sentence has been amended to stop grouping it with the reload events, because it is an execution-control concept and, decisively, a test **names it as its subject**: `ExecutionControlTests.Synchronous_backend_continued_event_is_not_forwarded` sets `FakeDebuggerBackend.RaiseContinuedSynchronously` and asserts the session does not forward the event.
    **The question to settle first:** is that test vacuous? It appears to be — nothing subscribes, so it would pass identically if the event never fired. Determine this before changing anything, and report the finding even if you change nothing. If it is vacuous, the honest options are to remove the event, the `RaiseContinuedSynchronously` scaffolding and the test together (all-or-nothing, since removing one breaks the others), **or** to keep them and record plainly that the test guards an intent rather than current behaviour. Either is acceptable; leaving a test whose name asserts unimplemented behaviour without recording why is not. Do not delete the event on its own — that breaks the build.
16. **Report whether `InternalDebuggerLog.AllowedEventNames` should shed its writer-less entries.** Task 4 left `status.pending.suppressed` (whose writer it deleted) alongside the pre-existing `reload.complete.bound`/`.pending`, and noted `MonoDebuggerLogger.LogInternalEvent` has no in-tree caller. All are zero-runtime-effect vocabulary. Judgement call: if they are a deliberate allowlist of every event name ever emitted, keep them and say so; if they are meant to match the writers, they are drifting. Report your reading rather than editing silently. Task 4's reviewer agreed with this reading and added the sharper point that unlike the pre-existing `reload.complete.*` pair, **`status.pending.suppressed` became dead in that very commit**, so it is a leftover from this work rather than unrelated debris — which is an argument for dropping that one line.
17. **Optional: the second-`Bind`-on-a-still-pending-entry path lost its only assertion.** Task 4's reviewer observed that the deleted test's neighbour covers "pending→verified" only through a *bound-status event*; the path where a **second `Bind` runs against an entry that is still pending** — reachable by setting `FakeDebuggerBackend.RejectBreakpoint`, binding, then changing the condition with rejection off — is no longer asserted, and it differs from `Changed_condition_removes_and_rebinds_same_logical_id` (`:137-153`) in that `RemoveBackendBreakpoint` takes the null branch (`BreakpointManager.cs:364-370`). Graded low value because the deleted test's own subject is gone, so this is "coverage could be broader" rather than lost coverage. Add it if it is cheap; if you judge it not worth the cycles, say so and leave it — a recorded "not worth it" is a fine outcome, a silent skip is not.
18. **RESOLVED BY EVIDENCE — do not change this, and do not "fix" the fixtures.** Task 5's reviewer raised a residual phantom-self-target risk: consumption is by exact project-name match (`editorDiscovery.ts:122,138-139`), the dedupe key is `LAN-IP:port` and cannot collide with the Editor's own `127.0.0.1:port`, so an Editor advertising a name other than its folder's basename would reappear as an attachable-looking `player-advertisement` at the host's LAN address.
    **The premise does not hold, and I verified it rather than reasoning about it.** A Unity **Editor** advertises its project **folder name**; a **Player** advertises the **productName**. Both halves are observed, not inferred:
    - `D:\workspace\sgproj` has `productName: 头号禁区` while its folder is `sgproj` — the two genuinely differ — and the reference's live Editor label read `Unity Editor, sgproj, xiaolonghan, Port:56548 (VSTU)`, i.e. the **folder** name.
    - The captured live **player** advertisement from the same machine carried `[ProjectName] 头号禁区`, i.e. the **productName**.

    So an Editor's advertisement always matches `path.basename(workspaceRoot)` and is always consumed, while a Player's correctly does not match and is correctly surfaced. Our name-based match is the right discriminator for the data Unity actually sends, and `[Guid]`/`[EditorId]` identity would be more machinery for a case that does not arise.
    **Consequence for the fixtures, which is the only real residue:** Task 5's `H:\fixture` vs `[ProjectName] FixtureProject` mismatch is a **fixture artifact** — the test author picked a name unrelated to the fixture root — not a modelled real scenario. It is why four pre-existing Editor expectations needed scoping. Leave the behaviour alone; if you touch the fixtures, make the advertised name match the root so they model reality, and say so in your report. Do **not** weaken the name match, and do not add identity-based exclusion.
    **Bonus, and directly relevant to Task 13:** the acceptance project `sgproj_xiaolonghan_0003` also carries `productName: 头号禁区`, so it is itself a mismatch case — and its Editor advertisement will still be consumed correctly, because the Editor sends the folder name. That removes a plausible late surprise from the real-Editor acceptance run.
19. **Two cheap Task 5 assertion tightenings, both recorded by its reviewer.** `editorDiscovery.test.ts:279-284` asserts only a count of 2 for the two-players-same-port case; a bug mapping `host` from the wrong field would still yield 2, so assert the host values. And the Player candidate's `processId: 0` / `projectVersion: ""` (`editorDiscovery.ts:270-277`) are implemented but asserted nowhere — add them to the first player test's `objectContaining`. Both are low impact because the host value is pinned elsewhere.
20. **`tests/build/scaffold.test.mjs:54-80` — `assert.deepEqual` does not pin the empty-string defaults.** `0 == ""` under loose equality, so `default: 0` or `default: false` would satisfy assertions written against `""`. **Use `assert.deepStrictEqual`** for the manifest-property assertions. This is a vacuous-assertion defect rather than a style preference: the whole point of those lines is to freeze three verbatim strings and three defaults, and as written they freeze the strings but not the defaults. Found by Task 7's reviewer.
21. **`debugConfigurationProvider.test.ts` "emits no log field when logFile is %s" — the `["absent", undefined]` row exercises nothing new.** Spreading `{...attach, logFile: undefined}` still creates the key, so the "absent" row takes the same path as the "empty" row. Either drop the key explicitly (`const { logFile: _omit, ...rest } = attach`) or delete the row. The mutation proof still discriminates both, so this is coverage-shaped rather than wrong.
22. **Two mutation-evidence gaps Task 7's reviewer identified, both inferable but not demonstrated.** The `__port 65536` row (`AttachArgumentsTests.cs:118`) has no mutation proving it discriminates — the batch's 5-failure total implies `> 65535` survived, but that is arithmetic, not evidence. And nothing reverts `__host: target.host` (`debugConfigurationProvider.ts:246`) to the literal `"127.0.0.1"`; the Player-host test failed at RED for the *gate* reason, so its host half is unproven by RED — though `toMatchObject({__host: "192.168.1.77"})` cannot pass with the literal, which is why this is Minor and not Important. Prove both by mutation, or record them as inspection-only in the same terms Task 6's report uses.
23. **`debugConfigurationProvider.ts:282` — `length > 0` lets a whitespace-only `logFile` onto the wire**, where the adapter then drops it (`AttachArguments.cs:129`, `IsNullOrWhiteSpace`). Harmless today because the adapter is the stricter of the two, but `value.trim().length > 0` would keep both layers' notion of "present" identical — which is the same two-copies-agree principle this plan keeps relearning.
24. **`debugConfigurationProvider.ts:263-272` — a relative or `${workspaceFolder:name}`-style `projectPath` is passed through unqualified.** Discovery then reports "Unity not running for the current project" rather than a bad-path message, so the user is told the wrong thing about a config they just wrote. Either validate the expanded path or surface a distinct message; decide and say which, because silently reporting "Unity is not running" for a malformed path is exactly the misleading-message class this work item exists to remove.
25. **No cross-layer contract test exists.** The defect Task 7 closed was a *two-copies-disagree* class — the TypeScript gate and the C# gate disagreed about `""`. Add a golden test that feeds a provider-resolved configuration through `AttachArguments.Parse`, asserting the emitted key set and that both layers accept what the other produces. That is the regression net for the whole class, and it is the one thing that would have caught the original defect directly rather than by inspection.
26. **`NO_UNITY_ENDPOINT_MESSAGE` is exported but never shown — Task 6 handed it to Task 8 and Task 8 dropped it.** Verified this round: `diagnostics.ts:22` declares `"No Unity endPoint to connect to"`, the spec's placement table (`:156`) assigns it to "no endpoint could be resolved", Task 6's brief `:41` and report (`:23`, `:150`) both say explicitly that Task 8's picker shows it — and Task 8's report never mentions the constant, with no consumer anywhere in `extension/src`. So one of the reference's five authoritative strings is currently dead code.

    **It has a real home in our architecture, and it is the portless-Player case.** `playerAdvertisement.ts:41` reads `debuggerPort` as `Number(fields.get("debuggerport") ?? 0)`, `:43-45` accept `0` as valid, and `editorDiscovery.ts:312` copies it onto the candidate — so a Player advertisement carrying no debugger port produces a candidate with **`port: 0`**, whose endpoint genuinely cannot be resolved. That is precisely the reference's condition (`type === vstuc && endPoint === undefined` → `throw noUnityEndPointError`, read out of `out/extension.js` in `resolveDebugConfiguration`), and the live Android advertisement recorded earlier (`[Debug] 0`, no debugger port) is exactly such a Player.

    Today that case reaches Task 7's `endPoint` validation and surfaces a **raw `ip:port` validation error** instead of the reference's sentence. Fix: when a resolved target's endpoint cannot be formed — port not a positive integer, or host empty — report `NO_UNITY_ENDPOINT_MESSAGE`, in the same place the config flow reports `UNITY_NOT_RUNNING_MESSAGE` (`debugConfigurationProvider.ts:178`, `:185`). Keep them distinct: "no Editor for this project" and "an Editor/Player was found but has no debugger port" are different facts and the reference uses different sentences for them. Add a test for a zero-port candidate specifically, since that is the input that produces the case.
27. **The known-version warning is pinned only by two `Contains`, so a lost space survives every test.** `UnityDebugSessionLifecycleTests.cs:113-122` asserts `Contains("Editor 6000.0.50f1 is unverified")` and `Contains(<url>)`; anything changed *between* those two fragments — a dropped space, a reworded baseline sentence, a missing separator — passes. Add one full-string `Assert.Equal` for the known-version branch, mirroring the precision the unknown-version test already has. Worth recording why this is not merely tidiness: **the defect Task 8 fixed was a double space**, so the guard against its recurrence currently does not cover the half of the message where it occurred.
28. **`extension.ts` reads `"displayBackgroundProcesses"` as a bare literal that no test ties to `settingIds`.** The reader at `extension.ts:40` and `productIdentity.ts`'s `settingIds.displayBackgroundProcesses` are asserted nowhere against each other, and no test calls `activate()`. So renaming one alone makes the setting **silently always-`false`** — the filter would simply always hide background processes, with no error and no failing test. Derive the key from the identity value (strip the `<extensionName>.` prefix) or add an assertion in `productIdentity.test.ts` tying the literal to the id. Task 8's 30-mutation battery could not reach this because it mutates the injected dependency, not the reader.
29. **`extension.ts:41-42` duplicates `debugConfigurationProvider.ts:83` verbatim.** Task 8 disclosed it, and the two strings are byte-identical today — which is precisely the "two copies agree until they don't" condition this plan keeps closing elsewhere. One exported constant, imported by both.
30. **`extension.ts:86` discards `startDebugging`'s `false` and `runAttachUnityDebugger` has no `catch`.** A failed start, or a rejection from `discover`, leaves the user with **no message at all** — the command simply does nothing. Low likelihood, since `EditorDiscovery.discover` swallows its own errors, and cheap to close: one error line on the falsy branch and one `catch`. This is the same class as the three unreachable-user cases this work item has already fixed, so it should not be left as the one place a failure is silent.
31. **`extension.ts:154` casts `folder as unknown as vscode.WorkspaceFolder | undefined`.** It exists because `AttachCommandDependencies` deliberately keeps `vscode` out via `WorkspaceFolderLike`, which is a reasonable testing trade — but the double cast is the one place a real signature drift could hide undetected. Narrow the parameter type or comment why the cast is safe.
32. **The config-flow picker's placeholder now under-describes its own list.** `extension.ts:254` still reads `Select a local Unity or Tuanjie Editor`, while a Player row can appear beneath it now that Task 5 made Players discoverable. Task 8 disclosed this as out of scope, which was reasonable for its brief — but it is user-visible copy that is now inaccurate, which is exactly what the rest of this work item exists to fix. Change it to cover both, or state in the handoff why it stays.
33. **`extensionCommands.test.ts`'s `expect(labels(calls[0][0])[1]).not.toContain("PID 0")` is subsumed** by the `toEqual` immediately above it. Harmless redundancy; remove it or leave it, but do not count it as coverage.
34. **Task 9's report overreaches on what its mutation battery proves, and one tenth mutation closes it.** §3 claims "M1–M8 also serve as per-assertion discrimination proofs for those", but the **positive** branches are green under every one of M0–M8: `functionBreakpointWarning.test.ts:107-115`'s second `expect` and `:122-128` both pass when M5 makes the predicate always-true, and the descriptor guard at `:157` never executes in a failing run because M0/M3 fail earlier in the same test. Nothing shipped is wrong — **it is the evidence claim that overreaches** — and `breakpoints.some` → `breakpoints.every` at `functionBreakpointWarning.ts:22` should fail exactly `warns when one of several breakpoints is a function breakpoint`. Run it and record the result, or correct the sentence. This is the same defect class as Task 6's vacuous assertions, one level up: a proof that covers the negative half and is described as covering both.
35. **The function-breakpoint log call shares the `try` whose `catch` aborts the launch.** `extension.ts:143-145` sits inside the `try` opened at `:127`, whose handler returns `undefined` — i.e. no adapter. So if `warn` ever threw (a disposed channel, a host fault), **a logging call would turn into a failed debug session with an error popup.** Impact is low and the position is brief-mandated (it must come after the launcher), so this is hardening rather than a bug: emit after `:146-150`, or wrap the emission in its own `try`, if the ordering is ever revisited. Worth recording because it is the one place where the diagnostic path can break the thing it is diagnosing.
36. **`FUNCTION_BREAKPOINT_WARNING`'s golden test sits under the predicate's `describe`.** `functionBreakpointWarning.test.ts:135` asserts the constant while living inside `describe("shouldWarnAboutFunctionBreakpoints")`, so a failure of it reports the wrong name. A separate `describe` would keep the failure message meaningful — cosmetic, but this is the assertion that pins a verbatim reference string, so its failure name is worth being accurate.
37. **Record the union-parameter alternative for the call-site assertion, so it is not re-derived.** `readonly (vscode.Breakpoint | { functionName?: string })[]` with a type-only VS Code import **is** assignable from `readonly Breakpoint[]` — `Breakpoint` satisfies a union member, so the weak-type rule never fires — and the tests' `{ functionName: "Foo" }` / `{}` inputs still type-check. The tradeoff is a VS Code type dependency inside a deliberately host-free module, and it would re-signature the module Task 11's brief pins, so **keeping the single documented cast is the right call for Task 9.** Record the alternative in a code comment or here; do not perform the change. The reviewer's own verdict on the decomposition stands: `outputChannel.ts` is small enough to look like a smell, and stays justified because the injected factory is what makes the contract testable without a VS Code host and because Task 11 needs it as the home of `writeSanitized`.
38. **One assertion in `unityApiReference.test.ts` is weaker than its siblings and the implementer disclosed it.** Its empty-selection test asserts two inputs, `undefined` and `""`. The mutation `guard → text === undefined` leaves the `undefined` half **green** and fails only on the `""` half — so **the test's discriminating power lives entirely in its second line**, and the first is decorative. The implementer reported this rather than letting the mutation list imply both halves were covered, which is the same honesty Task 9's reviewer had to add a tenth mutation to recover. Either split it into two tests so each can fail alone, or leave it with a comment — but do not count it as two cases.
39. **`runShowUnityApiReference` does nothing and shows no message when there is no active editor, which mirrors the reference and is brief-conformant — but record it in the matrix rather than only in code.** The reference's handler returns without a message, so a user who runs the command from the palette with no editor open gets silence. That is a real usability oddity and it is the reference's, so it stays; it belongs alongside item 19's `getText(undefined)` quirk as an `aligned` row whose justification is "the reference does this too". Both are the kind of entry a reader would otherwise propose fixing.
40. **`unityApiReference.ts:44`'s comment overstates one of its three examples.** It names `#`, `&` and "a space" as inputs the reference would break on. `#` truncates the query into a fragment and `&` produces a second parameter — both provably "look something else up" — but a **raw space is normally percent-encoded by the browser**, so the reference would likely still search `Foo Bar` correctly. Not a behaviour conflict, just an example that cannot be demonstrated the way the other two can. Drop "or a space" or soften the verb to "can produce".
41. **Nothing pins the C# forms where our encoding legitimately differs from the reference.** `List<int>` → `%3Cint%3E`, `int?` → `%3F`, `int[]` → `%5B%5D`, and — the one that matters most — **`a.b` produces output byte-identical to the reference**, because `.` is unreserved and `encodeURIComponent` is the identity on it. That identity is the property the brief's own rationale relies on ("no change for a plain C# identifier"), and no assertion pins it. Add one for `a.b` and one for `List<int>`; the first protects the rationale and the second protects the encoding from being removed.
42. **`unityApiReference.ts:93` awaits `openExternal` with no `try`/`catch` and discards its boolean.** A rejection surfaces as an unhandled rejection from the command handler, and a `false` result is silent. **This matches the reference's fire-and-forget `executeCommand("vscode.open", …)`, so leaving it is defensible** — adding user feedback would be a new divergence needing approval under `AGENTS.md`. Record the choice rather than changing it.
43. **`writeSanitized` is a boundary, not a guarantee, and nothing structural stops a producer from bypassing it.** Task 11 disclosed this itself: only the two consumer commands route through `writeSanitized`, and any future code calling `channel.info(...)`/`channel.warn(...)` directly writes whatever it likes. The guard is enforced by **review and two assertions**, not by the type system — `SanitizedOutputChannel extends vscode.LogOutputChannel`, so the raw level methods remain reachable on the same object.
    - **Judge whether that is acceptable or whether it should be closed**, and if it stays open, record it where a future contributor will see it — the matrix row for the output channel, or a comment on the interface. A defence that depends on everyone remembering to use it is the *sanitized-diagnostics allowlist* invariant's weakest link, and the invariant is one of the three the goal names.
    - One option that would close it without a redesign: make `writeSanitized` the only exported way to write, and have it be the consumer of a narrow internal channel. **Do not do that here** — Task 11's design is approved and re-signaturing it in a cleanup task is exactly the "two copies disagree" class. Record the gap and the option; decide later.
44. **The guard is home-scoped, not workspace-scoped, and that is a deliberate limit rather than an oversight.** Task 11 documented it in code and in a test: a line naming any path *outside* the user's home — a workspace path on another drive, a build server path, a colleague's machine — passes the guard. That is consistent with the brief, which asked for "an absolute user path", and the producer (the adapter's `PathRedactor`) remains responsible for the rest. **Say so in the matrix or the code comment so the limit is not mistaken for coverage**, because "refuses absolute paths" and "refuses the user's home directory" are different claims and only the second is implemented.
45. **No real VS Code host has exercised the in-place decoration.** `Object.assign(channel, …)` mutates the object VS Code actually returned, which the implementer chose over wrapping so that `dispose()` and the `context.subscriptions` entry keep reaching the right object — mutation B proves a wrap would fail the identity test. The residual risk, stated by the implementer, is that VS Code's channel is a proxy whose member behaviour is not ours to assume: it returns a plain unfrozen instance today, so the risk is low, but **it is unit-tested against doubles only**. Task 13's acceptance is where a real host would confirm it, and if acceptance cannot reach the output channel, record that as an explicit gap rather than assuming the doubles generalise.
46. **Two of the three invariants the goal names are enforced by nothing, and the third only partly — add the tests.** Found this round by grepping for where each would fail:

    | Invariant | Current enforcement |
    | --- | --- |
    | No C# Dev Kit | `scaffold.test.mjs:37` asserts `manifest.extensionDependencies === undefined` — **the whole of it** |
    | — `extensionPack` | **no test** |
    | — `dependencies` | **no test** (only `packages.lock.json` matched, which is unrelated) |
    | — no `ms-dotnettools` / `csdevkit` | **no test** |
    | No Unity-side package (`com.unity.ide.*`) | **no test** |
    | No telemetry | **no test** |

    **Add three cheap assertions**, in `tests/build/scaffold.test.mjs` beside the existing one, since that file already parses the manifest and is where the current single check lives:

    - `extensionPack` is `undefined`, and `dependencies` is either absent or `{}` — the same shape as the existing `extensionDependencies` assertion.
    - No `ms-dotnettools`/`csdevkit` string anywhere in `package.json`, and no `com.unity.ide.*` in the shipped source. **A whole-tree grep over `extension/src`, `adapter/src` and `package.json` is enough**, and `scripts/verify-third-party.mjs` already demonstrates the pattern for walking files.
    - No `telemetry`/`appInsights`/`applicationinsights` identifier in the shipped source.

    - **Why these matter more than their size, and why they belong in CI rather than in a checklist.** All three are *negative* claims — the absence of a dependency — which is exactly the kind that cannot be noticed by reading code and cannot be preserved by good intentions. Every verification of them this session was a manual grep I ran by hand, and a manual grep is a check that stops existing the moment nobody remembers to run it. **Two of the three invariants the goal names currently rest on my memory of this conversation.**
    - **The sanitized-diagnostics guard has the same defect and for the same reason** — item 43 records that it survives on review rather than structure. Putting these three in the suite does not fix item 43, but it does establish the pattern that an invariant worth naming in a goal is worth an assertion, and it removes the largest remaining block of "verified by remembering" from the branch.
    - **Do not add these to the adapter's xunit project or to a new file.** They are manifest and source-tree facts, `tests/build` already owns both, and a second home for the same claim is the "two copies disagree" class this plan keeps closing.
47. **Re-derive this task's `extension.ts` citations — six are already stale, before Task 11b touches the file.** The items above cite roughly fifty `file:line` locations, and I checked the six that point into `extension/src/extension.ts`. **Every one is wrong**, drifted by 13 to 55 lines as Tasks 9, 10 and 11 edited that file:

    | Item | Cited | **Actual** | What it is |
    | --- | --- | --- | --- |
    | 28 | `:40` | **`:53`** | `const DISPLAY_BACKGROUND_PROCESSES_SETTING = "displayBackgroundProcesses";` |
    | 29 | `:41-42` | **`:53`** | The same literal, duplicated with `debugConfigurationProvider.ts:83` (that half of the citation is correct) |
    | 30 | `:86` | **`:99`** | `await dependencies.startDebugging(folder, {` — the discarded boolean |
    | 31 | `:154` | **`:185`** | `folder as unknown as vscode.WorkspaceFolder &#124; undefined` |
    | 32 | `:254` | **`:309`** | `placeHolder: "Select a local Unity or Tuanjie Editor"` |
    | 35 | `:143-145`, `:127` | **`:152`**, **`:135`** | The `warn` call, and the `try` whose `catch` returns `undefined` at `:163` |

    - **Task 11b will move them again**, since it inserts an activation-time call into this file. So the numbers above are correct **as of this revision and not afterwards** — which is the point of the item rather than a caveat on it.
    - **Every one of the six is a location you are meant to edit, which makes a stale line the difference between finding the code and not.** The instruction reads "fix the literal at `extension.ts:40`"; if line 40 now holds an unrelated `import type` and the literal has moved to `:53`, the failure mode is not a confusing error — it is **editing the wrong thing**, because a plausible line at the cited position is exactly what a careless reader accepts as confirmation. I made this concrete by checking, and found `:40` holding `import type { UnityDebuggerPureApiV1 } …`, `:86` holding a `showError` call, and `:254` holding an unrelated comment.
    - **Re-derive rather than offset.** Do not add Task 11b's line count to each number: the insertion point will sit above some and below others, and a mechanical offset repeats the original mistake with more confidence. **Grep for the code each item describes** — the string literal, the discarded call, the double cast, the placeholder text — and cite where it is now. The items describe their subjects precisely enough for that to work, which is why the descriptions and not the numbers are the durable half.
    - **This item exists because round 108 found three citations in Task 5's section stale by fifty lines, guarding a rule that was correct.** That paragraph's evidence was wrong while its instruction was right; here the *targets* are wrong, which is strictly worse — a stale rule gets discounted, but a stale target gets edited.


- [ ] **Step 4: Verify and commit**

Run: `dotnet test UnityDebugger.sln -c Release --no-restore` and `npm test` (or the brief's documented equivalents). Report before/after counts for both suites. The adapter suite should **gain** tests and lose none; if any existing test fails, the defect fix was wrong — restore and report rather than adjusting the test to match.

Stage explicit paths only; the six `adapter/vendor/*/packages.lock.json` files stay uncommitted.

```bash
git commit -m "fix: close the review findings from tasks 1-3"
```

- [ ] **Step 5: Report**

State: what changed for each numbered item, or why an item was deliberately left; the exact before/after counts for both suites; and any item you believe is wrong or not worth fixing, with your reasoning. A finding you disagree with is worth more reported than silently skipped — say so and leave it, rather than making a change you do not believe in.

---

## Notes for the executor

- **Commit protocol — new commits only.** An implementer must never `--amend`, `rebase`, `reset`, `cherry-pick`, or otherwise rewrite history, and must never rewrite a commit it did not create. During Task 1 an implementer finished while the controller's own docs commit happened to be at the tip and amended *that* commit, which mislabelled it as docs-only and folded a test hunk into it, hiding those lines from the commit-scoped review package. The mistake was self-repaired, but the rule exists so it cannot recur: if the tip commit is not yours, commit on top of it. Stage explicit paths only — never `git add -A`, `git add .`, or `git commit -a`.
- The working tree carries six pre-existing modifications under `adapter/vendor/*/packages.lock.json`. `dotnet restore --locked-mode` reports them consistent with the projects. Do not revert or commit them as part of these tasks unless a task explicitly requires it. They have no content diff (`git diff` is empty for them); the status is a CRLF-vs-LF artifact, so they never appear in a diff-based review package.
- The REA reference evidence is retained in this session only. If a claim in the spec needs re-checking, the reference files are at `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2`; the adapter can be probed directly with a framed DAP `initialize` on stdio, which is how the capability table above was produced. Three further probes are recorded in the spec and are cheap to repeat: the attach probe's `debuggerTargets` notification (run `bin\UnityAttachProbe.dll` with a live Editor's `app_contents_path`), the attach-failure response for an unreachable endpoint, and a listen on the Unity multicast group `225.0.0.222` for player advertisements.
- **`dotnet test UnityDebugger.sln` does NOT build `tests/integration/UnityDebugger.TestAdapter`.** Verified by Task 4, which touched that project: it has `OutputType=Exe` and no test framework, so the solution's test run compiles nothing in it and **an edit there can pass the brief's command while being broken**. If your task changes that project, build it explicitly (`dotnet build tests/integration/UnityDebugger.TestAdapter -c Release --no-restore`) and report the result separately. Do not read a green `dotnet test` as covering it.
- `npm run test:adapter` skips the 21 real-Mono cases unless `UNITY_DEBUGGER_TEST_MONO` points at a `mono.exe`. Skipped is not coverage; say so in any report.
