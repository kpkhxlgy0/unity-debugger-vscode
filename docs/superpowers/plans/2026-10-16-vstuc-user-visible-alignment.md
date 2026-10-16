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
  `No processes to attach to`.
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
- Produces: `AttachArguments.LogFile` (nullable `string`), read from the `__logFile` attach key.

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
- Modify: `extension/src/model.ts:18`, `:29`, `extension/src/editorDiscovery.ts:110-126`, `extension/src/playerAdvertisement.ts`
- Test: `tests/extension/editorDiscovery.test.ts`, `tests/extension/playerAdvertisement.test.ts`

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

In `playerAdvertisement.ts`, parse `machine` from `[Id]` per the rule in Interfaces. The rule was verified against the live record: `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)` yields `samsung_SM-S9260@10.30.154.73`, exactly the reference's `machine`, and an `[Id]` with no parentheses correctly fails to match and falls back to the advertised `ip`. Widen `host`/`__host` to `string` in `model.ts` and add `machine: string` to `EditorCandidate`. In `discover`, set `machine: os.hostname()` (import `node:os`) on Editor candidates — **use `os.hostname()`, which returns `xiaolonghan` here and matches the reference's label exactly; do not use `process.env.COMPUTERNAME`, which is upper-cased to `XIAOLONGHAN` and would not match** — then append one candidate per remaining advertisement (those not consumed as the current Editor's port source) that has `allowDebugging === true` and a positive `debuggerPort`, with `source: "player-advertisement"`, `machine` from the advertisement, `workspaceRoot` set to the first root, `processId: 0`, and `projectVersion: ""`. Adding a required field to `EditorCandidate` breaks every existing literal. Update the fixtures in `tests/extension/editorDiscovery.test.ts`, `tests/extension/debugConfigurationProvider.test.ts`, `tests/extension/publicApi.test.ts`, and any other construction site before running the suite. Change the dedupe key from `${processId}:${port}` to `${host}:${port}`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension`
Expected: PASS, including the pre-existing `editorDiscovery` and `debugConfigurationProvider` suites.

- [ ] **Step 5: Commit**

```bash
git add extension/src/model.ts extension/src/editorDiscovery.ts tests/extension/editorDiscovery.test.ts
git commit -m "feat: surface Unity player targets from discovery"
```

---

### Task 6: Discovery robustness and the aligned failure messages

**Files:**
- Modify: `extension/src/projectVersion.ts:13-23`, `extension/src/editorDiscovery.ts:89-145`, `extension/src/debugConfigurationProvider.ts:97-113`, `extension/src/diagnostics.ts`
- Test: `tests/extension/projectVersion.test.ts`, `tests/extension/debugConfigurationProvider.test.ts`

**Interfaces:**
- Produces: `readProjectVersion` returns `""` when the file is missing or has no `m_EditorVersion` line, instead of throwing.
- Produces: exported constants `UNITY_NOT_RUNNING_MESSAGE = "Unity is not running for the current project"`, `NO_UNITY_ENDPOINT_MESSAGE = "No Unity endPoint to connect to"`, `NO_PROCESSES_MESSAGE = "No processes to attach to"`.

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
git add extension/src/projectVersion.ts extension/src/editorDiscovery.ts extension/src/debugConfigurationProvider.ts extension/src/diagnostics.ts tests/extension
git commit -m "fix: tolerate missing project metadata and align failure messages"
```

---

### Task 7: Attach configuration surface

**Files:**
- Modify: `package.json:66-106`, `extension/src/debugConfigurationProvider.ts:39-127`
- Test: `tests/extension/debugConfigurationProvider.test.ts`, `tests/build/scaffold.test.mjs`

**Interfaces:**
- Consumes: `EditorCandidate.host: string` (Task 5).
- Produces: the provider accepts `endPoint` (`"host:port"`) and `projectPath` (string, `${workspaceFolder}` expanded) and emits `__host`/`__port` from `endPoint` when present.
- Produces: the provider emits `__logFile` whenever the `logFile` property is present and non-empty, so Task 3's adapter-side `AttachArguments.LogFile` is actually reachable. This is the only producer of that field.

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

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx vitest run tests/extension && node --test tests/build/scaffold.test.mjs`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add package.json extension/src/debugConfigurationProvider.ts tests/extension tests/build
git commit -m "feat: add endPoint and projectPath attach properties"
```

---

### Task 8: The attach command, its picker, and the background-process setting

**Files:**
- Create: `extension/src/attachTargetPicker.ts`
- Modify: `extension/src/productIdentity.ts`, `extension/src/extension.ts`, `package.json`
- Test: `tests/extension/attachTargetPicker.test.ts`, `tests/build/scaffold.test.mjs`

**Interfaces:**
- Produces: `formatTargetLabel(candidate: EditorCandidate): string` returning `Unity <type>, <projectName>, <machine>, <connection>` with empty parts omitted, joined by `", "`.
  - `<type>` is `Editor` when `source` is `"advertisement"` or `"derived-port"`, and `Player` when `source` is `"player-advertisement"` (the reference's `displayType` values, observed as `"Editor"` and `"Player"`).
  - `<machine>` is `candidate.machine`, produced by Task 5 (`os.hostname()` for Editors, the `[Id]` component for Players). The label function reads the field rather than touching the host, so it stays testable.
  - `<connection>` is `` `Port:${port} (Mono)` ``, matching the reference's observed format `Port:<debuggerPort> (<debuggerEngine>)`, e.g. `Port:56548 (VSTU)` for its local Editor. Our engine token differs; Task 12 records that as an accepted cosmetic divergence.
- Produces: `selectTarget(candidates, options): Promise<EditorCandidate | undefined>` where `options` carries `displayBackgroundProcesses` and the QuickPick factory, so the picker is testable without VS Code.
- Produces: the QuickPick **title** is `Select Unity Instance`, verbatim from the reference's English bundle (`out\nls.bundle.json`). The reference's placeholder is `Detecting... press any key to filter`, which reveals that it opens the picker **before** discovery finishes and populates it as results arrive; we collect first and open afterwards, so we have no equivalent placeholder. **Use the reference's title; do not fake the placeholder.** Record the streaming-population difference as a divergence in Task 12 rather than restructuring the picker here — it is a real perceived-responsiveness difference, but turning the picker into a streaming UI is well beyond this task and needs its own design.
- Produces: `PRODUCT_IDENTITY.commandIds.attachUnityDebugger = "unity-debugger-pure.attachUnityDebugger"` and `PRODUCT_IDENTITY.settingIds.displayBackgroundProcesses = "unity-debugger-pure.displayBackgroundProcesses"`. The command **title** is `Attach Unity Debugger` and the setting **description** is `Display background processes created by the main Unity process`, both verbatim from the reference's bundle. Our command ids keep our own `unity-debugger-pure.` prefix because the id is not user-visible; the reference's are `vstuc.`-prefixed.

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
git add extension/src/attachTargetPicker.ts extension/src/model.ts extension/src/productIdentity.ts extension/src/extension.ts package.json tests
git commit -m "feat: add the attach Unity debugger command and picker"
```

---

### Task 9: Function-breakpoint performance warning

**Files:**
- Create: `extension/src/functionBreakpointWarning.ts`
- Modify: `extension/src/extension.ts:45-67`
- Test: `tests/extension/functionBreakpointWarning.test.ts`

**Interfaces:**
- Produces: `shouldWarnAboutFunctionBreakpoints(breakpoints: readonly { type?: string }[]): boolean` and `FUNCTION_BREAKPOINT_WARNING` holding the reference text verbatim: `You are using function breakpoints. Even if they are fully supported, they have a major negative impact on debugging performance. Use them only when necessary.`

- [ ] **Step 1: Write the failing test**

```ts
it("warns only when a function breakpoint exists", () => {
  expect(shouldWarnAboutFunctionBreakpoints([{ type: "source" }])).toBe(false);
  expect(shouldWarnAboutFunctionBreakpoints([{ type: "function" }])).toBe(true);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run tests/extension/functionBreakpointWarning.test.ts`
Expected: FAIL — the module does not exist.

- [ ] **Step 3: Implement**

Add the module, then call it from `createDebugAdapterDescriptor` in `extension.ts` after the launcher succeeds, showing a warning message when it returns true.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run tests/extension/functionBreakpointWarning.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/functionBreakpointWarning.ts extension/src/extension.ts tests/extension/functionBreakpointWarning.test.ts
git commit -m "feat: warn about function breakpoint performance"
```

---

### Task 10: Unity API Reference command

**Files:**
- Create: `extension/src/unityApiReference.ts`
- Modify: `extension/src/productIdentity.ts`, `extension/src/extension.ts`, `package.json`
- Test: `tests/extension/unityApiReference.test.ts`

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
- Produces: `PRODUCT_IDENTITY.commandIds.showUnityApiReference = "unity-debugger-pure.showUnityApiReference"`. The command **title** must be `Unity API Reference`, verbatim from the reference's bundle. Note our id keeps our own prefix; the reference's is `vstuc.showUnityAPIReference` — and the id is **not** user-visible, so the title is what matters.

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
git add extension/src/unityApiReference.ts extension/src/productIdentity.ts extension/src/extension.ts package.json tests/extension/unityApiReference.test.ts
git commit -m "feat: add the Unity API reference command"
```

---

### Task 11: Output channel

**Files:**
- Create: `extension/src/outputChannel.ts`
- Modify: `extension/src/extension.ts`
- Test: `tests/extension/outputChannel.test.ts`

**Interfaces:**
- Produces: `createOutputChannel(factory): LogOutputChannel` where `factory` is injected for testing; the channel name is `PRODUCT_IDENTITY.displayName`.
- Produces: `OutputChannel.writeSanitized(line: string): void` that refuses any line containing an absolute user path (defence in depth for the allowlist constraint).

**The reference's mechanism, read out of its bundle, and a correction to this brief.** The reference activates with:

```js
u = window.createOutputChannel(extension.packageJSON.displayName, { log: true });
context.subscriptions.push(u);
```

So it is a **`LogOutputChannel`** — the `{ log: true }` option is what makes it one — and its logging is **level-based** (`logWarning`, `logError`, `logDebug` exist; `isDebugMode` gates debug output). **`appendLine` appears zero times in the reference bundle**, because a `LogOutputChannel` does not use it.

An earlier revision of this brief asserted on `appendLine` while declaring a `LogOutputChannel`; those two are inconsistent, and with a log channel the tests must assert on the level methods instead. Write the tests against `info`/`warn`/`error`/`debug`.

**Channel name:** the reference's `displayName` is the NLS key `%extension.displayName%`, which resolves to **`Unity`** — the same string in every locale including `zh-cn`. Ours is the literal `Unity Debugger Pure`, so our channel is `Unity Debugger Pure`. **Do not rename it to `Unity`**, for the same reason as the debug-type label: both extensions can be installed at once and two identically-named output channels would be indistinguishable. Drive the name from `PRODUCT_IDENTITY.displayName` rather than a fresh literal, and record the difference in Task 12 alongside the label. `writeSanitized` is our own addition — the reference has no equivalent — and it exists to keep the sanitized-diagnostics invariant enforceable at the channel boundary.

- [ ] **Step 1: Write the failing test**

```ts
it("drops lines that would leak a user path", () => {
  const channel = createOutputChannel(fakeFactory, "C:\\Users\\Admin");
  channel.writeSanitized("attached to C:\\Users\\Admin\\proj");
  expect(fakeChannel.info).not.toHaveBeenCalled();
  expect(fakeChannel.warn).not.toHaveBeenCalled();
});
it("passes through a sanitized line", () => {
  const channel = createOutputChannel(fakeFactory, "C:\\Users\\Admin");
  channel.writeSanitized("event=attach");
  expect(fakeChannel.info).toHaveBeenCalledWith("event=attach");
});
it("creates a log channel named after the product", () => {
  createOutputChannel(fakeFactory, "C:\\Users\\Admin");
  expect(fakeFactory).toHaveBeenCalledWith("Unity Debugger Pure", { log: true });
});
```

The third test pins the two things the reference's own activation proves: the `{ log: true }` option, without which this is a plain `OutputChannel` and the level methods do not exist, and the name coming from the product identity rather than a loose literal.

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run tests/extension/outputChannel.test.ts`
Expected: FAIL — the module does not exist.

- [ ] **Step 3: Implement**

Create the module and register the channel in `context.subscriptions`.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run tests/extension/outputChannel.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add extension/src/outputChannel.ts extension/src/extension.ts tests/extension/outputChannel.test.ts
git commit -m "feat: add the extension output channel"
```

---

### Task 12: Documentation — matrix, reference identity, security, amended scope

**Files:**
- Modify: `docs/reference-debugger-compatibility.md`, `AGENTS.md`, `README.md`, `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`

**Interfaces:**
- Consumes: the spec's two diff tables and the observed `initialize` body.

- [ ] **Step 1: Add the matrix rows**

Add one row per line of the spec's two diff tables to `docs/reference-debugger-compatibility.md`, using the existing `| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |` shape and the existing status vocabulary. Reference the observed `initialize` body, the observed `debuggerTargets` payload, and the REA evidence ids where they apply. Status is `aligned` only for items verified by running both extensions; everything implemented but not yet A/B-run is `not verified`.

Also fix this stale reference the Task 1 review caught: `README.md:51` says "all-exception and uncaught-exception break modes", but the advertised filter id is now `unhandled`. Correct the wording, and make sure the W10 matrix rows exist and follow spec Verification 4/5 — no row marked `aligned` from automated evidence alone, and the conditions divergence recorded as `divergent`.

The reference README was read directly (`C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2\readme.md`, 4474 bytes) so these three README defects are quoted, not guessed:

1. **`README.md:51`** — "all-exception and uncaught-exception break modes"; the id is now `unhandled`.
2. **`README.md:81`** — "Currently excluded: remote Player attach, Android/iOS, IL2CPP, WebGL, …". Once Task 7 lands `endPoint`, "remote Player attach" is **no longer excluded** and this line becomes false. Remove that phrase, keep the genuinely excluded items (Android/iOS enumeration, IL2CPP, WebGL).
3. **`README.md:85-90`** — the "## Security and privacy" section states that version `0.3.2` "automatically connects only to a discovered loopback address" and "does not accept … remote debug hosts … so future remote support will require explicit opt-in." After Tasks 2 and 7 this is false. Rewrite it to state the truth plainly: attaching to a non-loopback `endPoint` is supported, and Unity's managed debugger transport is **unauthenticated**, so anyone who can reach the debugger port can attach and the debugger can execute code in the target Editor or player; recommend loopback or a trusted network. Do not imply a guarantee we do not provide.

For wording alignment, the reference's own command descriptions are:

- `` `Attach Unity Debugger`: lists Unity Editors and Players available for debugger attachment. ``
- `` `Unity API Reference`: opens the Unity documentation for the selected code construct. ``

and its player-attach section states that the default configuration attaches to the Editor running the open project, that `Attach Unity Debugger` is how a Player is debugged, and that a known endpoint can be configured explicitly with `"endPoint": "127.0.0.1:56321"`.

**Do not treat the security section as an alignment failure.** A keyword sweep of the reference README for `secur|warn|caution|note|auth|trust|expos|firewall|risk` returns nothing: the reference documents **no security posture whatsoever** for remote attach. Our statement is an intentional addition beyond the reference, and the matrix must record it that way rather than as a divergence to close.

### The two authority clauses to amend, quoted

These were read directly; use the existing wording's register and do not overstate what is permitted.

**1. `AGENTS.md`** is 45 lines and needs its acceptance project and its reference identity updated in five places:

- `:6` — `` `D:\Unity\TuanjieHub\Projects\MyGame` for real Unity/Tuanjie runs. `` → the acceptance project is `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`.
- `:7`, `:9`, `:10`, `:17`, `:26` — the bare `MyGame` references that follow from it, including the `DevTools` preference at `:26-27`. Note the new acceptance project has **no `DevTools` fixture**, so that sentence needs rewording rather than a path swap: keep the reachability requirement and drop the claim that a verified fixture exists.
- `:32` — `` - Treat the installed `zlorn.vstuc` debugger as the behavioral reference for `` → `visualstudiotoolsforunity.vstuc` **1.3.2**. Verify against `docs/reference-debugger-compatibility.md:3`, which names `zlorn.vstuc 1.2.1` and must be updated in the same pass, and check for any other `zlorn` or `1.2.1` reference with a repo-wide search rather than assuming these are the only two.

**2. `docs/superpowers/specs/2026-08-06-reference-debugger-engine-parity-design.md`** (303 lines). Three places:

- `:50-51`, under the `:45` heading **"Permanently out of scope:"** — `` - Reusing, separating, decompiling, modifying, or redistributing proprietary binaries from the reference extension. ``
- `:56` — `` The installed `zlorn.vstuc 1.2.1` debugger defines the expected user-visible and DAP-visible behavior. `` → 1.3.2.
- `:78-80`, under the `:76` heading **"Legal and Dependency Boundary"** — `` The reference package contains proprietary Microsoft/SyntaxTree assemblies. Its license does not permit separating, reverse engineering, or redistributing those components. They are neither build inputs nor runtime dependencies. ``

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

-1. **No localization.** The reference ships 14 `package.nls*.json` locales and routes its command titles and property descriptions through them (`%commands.attachUnityDebugger.title%` → "Attach Unity Debugger", etc.). We ship **no** localizations, so every string is English-only even for a `zh-cn` user, who would otherwise see `附加 Unity 调试程序`. This plan does not add localization. **Flag it to the user as a scope decision** rather than treating it as closed: it is a real user-visible difference, and the fix is additive (add `package.nls.json` plus translations and switch the manifest to placeholders) rather than architectural.

-2. **Unparseable `endPoint`.** We are deliberately **stricter** than the reference: a non-IP-literal host or a zero/missing port is rejected with an explicit error, where the reference answers `success:true` with no message and attaches to nothing. Evidence and the full table are in the spec's "Observed `endPoint` handling" section. This is a divergence **in our favour** and must never be "aligned" to the silent no-op. (Task 2's implementer flagged that this row was missing from an earlier version of this list — it was present in the spec table but not here, which is exactly the kind of gap that lets a row get skipped.)

-3. **Error-response body and sequence numbers.** For the same failing attach, the reference replies `body:{}` with `seq:2` while we emit the vendored error object (`{"error":{"id":2004,"format":"Failed to attach debugger to …"}}`) with `seq:3`. Our `success`, `command` and `message` match the reference character for character, and the body shape is what this adapter emits for **every** error rather than something attach-specific, so it is an accepted representation difference — but record it rather than leaving it unmentioned.

-4. **`logFile` redirection — status `not verified`, not `aligned` and not `divergent`.** Task 3 implements it (`b90562d`). An earlier revision of the spec claimed the reference "declares this property and never reads it", citing zero occurrences in `out/extension.js`; that inference was wrong. `UnityDebugAdapter.dll` carries the literal run `.ProjectFileMapper: unable to map .Assets.Packages.Library.pid.projectPath.logFile.endPoint.|`, placing `logFile` alongside `pid`, `projectPath` and `endPoint` — the other attach keys the adapter reads. A name in the literal pool proves the adapter refers to the property, not what it does with it, so neither `aligned` nor `divergent` is supportable from this evidence. Mark it `not verified` and settle it at W13 by setting `logFile` on a real attach and checking whether the reference writes a file. **Do not** repeat the earlier "favourable superset" framing — it was wrong.

0. **Exception filter conditions.** The reference advertises `supportsCondition: true`, so the client offers an exception-condition box and sends `filterOptions[].condition`. Task 1 accepts `filterId` and ignores `condition`, so a typed condition list is not honoured. Reference and ours otherwise agree on the filters themselves.
1. **Player advertisement `debug` gating.** The reference lists an Android player whose advertisement carried `debug: false` as `isValidForAttachment: true`; our `playerAdvertisement.ts` treats `debug === "1"` as `allowDebugging` and Task 5 filters on it, so we would exclude that target. Neither behaviour is changed by this plan.
2. **Connection label engine token.** The reference renders `Port:56548 (VSTU)`; we render `Port:<port> (Mono)`. Same format, different engine name.
3. **Withdrawn — do not add a machine-label row.** An earlier revision of this plan recorded that we could not reproduce the reference's machine label for a network player. That was wrong: the label is the second component inside the advertisement's `[Id]` field, e.g. `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)`, so Tasks 5 and 8 now parse it and match the reference exactly. Recording a divergence here would misreport working behaviour.

- [ ] **Step 2: Update the reference identity**

In `AGENTS.md`, replace `zlorn.vstuc` with `VisualStudioToolsForUnity.vstuc` 1.3.2 and give the installed path `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2` together with its provenance (`D:\Unity\vstuc-mirror\build_local_vsix.py`: packaging metadata only). Replace the MyGame acceptance path with `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`, and record that it is Unity 2022.3.23f1, not the Tuanjie baseline.

- [ ] **Step 3: Update `README.md`**

Rewrite "Security and privacy" for non-loopback `endPoint` (including the plain statement that Unity's managed debug transport has no authentication, so attaching to a remote endpoint trusts whoever controls it). Remove "remote Player attach" from "Currently excluded". Add the new features and correct the domain-reload wording at `README.md:52` to say the mature session owns rebinding.

- [ ] **Step 4: Amend the superseded scope clause**

In `2026-08-06-reference-debugger-engine-parity-design.md`, replace the out-of-scope bullet at `:50-51` and the paragraph at `:76-80` with the lifted boundary: reference binaries may be inspected as behavioural evidence; no reference code is copied, linked, packaged, or redistributed; no reference binary becomes a build input or runtime dependency. Cite the 2026-10-16 user decision and note that prior practice (`docs/reference-debugger-compatibility.md:151`) already decompiled one of these assemblies.

- [ ] **Step 5: Commit**

```bash
git add docs AGENTS.md README.md
git commit -m "docs: record VSTU alignment matrix, reference identity, and scope amendment"
```

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

- [ ] **Step 3: Get the Editor running on the acceptance project**

Ask the user to open `D:\workspace\sgproj1\sgproj_xiaolonghan_0003` in the Editor. Confirm discovery succeeds afterwards (the project currently has no `ProjectSettings\ProjectVersion.txt`); if the Editor does not create one, that path is exactly what Task 6 makes survivable, so attach must still work.

Verified facts about this project, gathered before starting so they need not be re-derived:

- Unity version is **2022.3.23f1**, not the Tuanjie baseline, so the version-compatibility row is `compatible-unverified`, not `aligned`.
- `com.unity.ide.visualstudio` is **2.0.22**, which clears the reference's own stated requirement of `>= 2.0.20`. **A/B against the reference is therefore possible in this project** — if this were below 2.0.20 the reference could not attach and Step 5 would be impossible rather than merely unverified.
- Its `.vscode/launch.json` currently contains exactly one configuration: `{"name": "Attach to Unity", "type": "vstuc", "request": "attach"}`.
- No `Library\EditorInstance.json` process is alive (the recorded pid is dead), so nothing is discovered until the Editor is started.
- The project is outside this repository. Any edit you make under it (for example the launch configuration in Step 5) is **not** part of this repo's history — never `git add` it here.

- [ ] **Step 4: Identify and prove a reachable breakpoint target**

Pick a method in the project that is provably reached (an `Awake`/`Start` on an object in the active scene, or a bootstrap path), and record the evidence that it executes. Do not use an unproven candidate, and do not read a missed breakpoint as a debugger failure.

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

## Notes for the executor

- **Commit protocol — new commits only.** An implementer must never `--amend`, `rebase`, `reset`, `cherry-pick`, or otherwise rewrite history, and must never rewrite a commit it did not create. During Task 1 an implementer finished while the controller's own docs commit happened to be at the tip and amended *that* commit, which mislabelled it as docs-only and folded a test hunk into it, hiding those lines from the commit-scoped review package. The mistake was self-repaired, but the rule exists so it cannot recur: if the tip commit is not yours, commit on top of it. Stage explicit paths only — never `git add -A`, `git add .`, or `git commit -a`.
- The working tree carries six pre-existing modifications under `adapter/vendor/*/packages.lock.json`. `dotnet restore --locked-mode` reports them consistent with the projects. Do not revert or commit them as part of these tasks unless a task explicitly requires it. They have no content diff (`git diff` is empty for them); the status is a CRLF-vs-LF artifact, so they never appear in a diff-based review package.
- The REA reference evidence is retained in this session only. If a claim in the spec needs re-checking, the reference files are at `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2`; the adapter can be probed directly with a framed DAP `initialize` on stdio, which is how the capability table above was produced. Three further probes are recorded in the spec and are cheap to repeat: the attach probe's `debuggerTargets` notification (run `bin\UnityAttachProbe.dll` with a live Editor's `app_contents_path`), the attach-failure response for an unreachable endpoint, and a listen on the Unity multicast group `225.0.0.222` for player advertisements.
- `npm run test:adapter` skips the 21 real-Mono cases unless `UNITY_DEBUGGER_TEST_MONO` points at a `mono.exe`. Skipped is not coverage; say so in any report.
- The REA reference evidence is retained in this session only. If a claim in the spec needs re-checking, the reference files are at `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2`; the adapter can be probed directly with a framed DAP `initialize` on stdio, which is how the capability table above was produced.
