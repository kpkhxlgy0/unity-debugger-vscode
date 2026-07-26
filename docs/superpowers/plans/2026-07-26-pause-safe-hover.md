# Pause Source Handling and Safe Hover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Pause safe when the selected managed frame has no workspace source and add non-invoking Hover evaluation for locals and fields.

**Architecture:** Replace the vendored stack-frame response with a project-owned DAP model whose optional `source` is omitted when unavailable. Carry an explicit `BackendEvaluationMode` from the DAP evaluation context through the backend boundary to a tested evaluation-options policy, so Hover uses non-invoking options while Watch and REPL retain explicit evaluation.

**Tech Stack:** C# / .NET Framework 4.8 x64; Newtonsoft.Json 13.0.4; Mono.Debugging.Soft; xUnit 2.9.3; TypeScript 7.0.2; Vitest 4.1.10; Debug Adapter Protocol; VS Code 1.95+.

## Global Constraints

- Work on branch `master`; do not create a feature branch or worktree.
- Use test-driven development: add each regression test and observe its expected failure before changing production code.
- Preserve Windows 10/11 x64 and local Tuanjie `2022.3.62t11` as the formal support baseline.
- Do not add C# Dev Kit, Microsoft's Unity extension, ReSharper APIs, telemetry, remote hosts, or opaque binaries.
- A stack frame without a mapped workspace file remains visible but must omit the optional DAP `source` property.
- Hover supports safe locals, parameters, `this`, and fields while disabling property getters, methods, target invocation, and target `ToString()`.
- Watch and REPL keep their existing explicit evaluation behavior.
- Never log expressions, values, source contents, or full workspace/user paths.
- Real-Editor verification uses only `H:\workspace\Unity\Tuanjie\Projects\MyGame` and the VS Code window opened from that project.
- Do not edit MyGame or create another Unity/Tuanjie project or VS Code profile.
- The user performs the UI verification. Computer Use requires separate explicit confirmation and is not needed by this plan.

---

### Task 1: Omit unavailable stack-frame sources

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Dap/DapStackFrameModels.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:361-415`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:920-936`
- Test: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs`

**Interfaces:**
- Consumes: `MappedSource`, `DapSource`, `VSCodeDebug.ResponseBody`, and the existing `BackendStackFrame`.
- Produces: `DapStackFrame(int id, string name, DapSource? source, int line, int column, string presentationHint)` and `DapStackTraceResponseBody(IEnumerable<DapStackFrame> frames, int totalFrames)`.

- [ ] **Step 1: Read the test-design rules**

Read `superpowers:test-driven-development/writing-good-tests.md` completely before changing the test file. Name the production behavior that makes the regression fail: `UnityDebugSession.StackTrace` currently serializes a synthetic `source` object for an unavailable runtime path.

- [ ] **Step 2: Write the failing unavailable-source regression**

Add this test to `InspectionRequestTests.cs`:

```csharp
[Fact]
public void Stack_trace_omits_source_for_unavailable_frame()
{
    var fixture = Fixture();
    fixture.Backend.Frames.Insert(
        0,
        new BackendStackFrame(
            3000,
            42,
            "UnityEngine.PlayerLoop",
            string.Empty,
            0,
            1));

    var messages = Run(
        fixture.Session,
        Initialize(),
        Attach(fixture.Workspace),
        Request("threads", new { }),
        Request(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 }));

    var frames = (JArray)Response(messages, "stackTrace")
        .SelectToken("body.stackFrames")!;
    var unavailable = (JObject)frames[0]!;
    var mapped = (JObject)frames[1]!;

    Assert.Equal(
        "UnityEngine.PlayerLoop",
        Required<string>(unavailable["name"]));
    Assert.Equal(
        "deemphasize",
        Required<string>(unavailable["presentationHint"]));
    Assert.Null(unavailable.Property("source"));
    Assert.Equal(
        Path.GetFullPath(fixture.SourcePath),
        Path.GetFullPath(Required<string>(
            mapped.SelectToken("source.path"))),
        ignoreCase: true);
}
```

- [ ] **Step 3: Run the focused test and verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj --no-restore --filter FullyQualifiedName~InspectionRequestTests.Stack_trace_omits_source_for_unavailable_frame
```

Expected: FAIL because `source` is present and the unavailable frame has presentation hint `normal`.

- [ ] **Step 4: Add project-owned stack-frame response models**

Create `DapStackFrameModels.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using VSCodeDebug;

namespace UnityDebugger.Adapter.Dap
{
    internal sealed class DapStackFrame
    {
        public DapStackFrame(
            int id,
            string name,
            DapSource? source,
            int line,
            int column,
            string presentationHint)
        {
            this.id = id;
            this.name = name;
            this.source = source;
            this.line = System.Math.Max(0, line);
            this.column = System.Math.Max(0, column);
            this.presentationHint = presentationHint;
        }

        public int id { get; }
        public string name { get; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DapSource? source { get; }

        public int line { get; }
        public int column { get; }
        public string presentationHint { get; }
    }

    internal sealed class DapStackTraceResponseBody : ResponseBody
    {
        public DapStackTraceResponseBody(
            IEnumerable<DapStackFrame> frames,
            int totalFrames)
        {
            stackFrames = frames.ToArray();
            this.totalFrames = totalFrames;
        }

        public DapStackFrame[] stackFrames { get; }
        public int totalFrames { get; }
    }
}
```

Keep the model in project-owned code so no audited vendored file changes.

- [ ] **Step 5: Emit an optional source from `UnityDebugSession.StackTrace`**

Change the stack-trace response construction to:

```csharp
var dapFrames = new List<DapStackFrame>();
foreach (var frame in frames)
{
    var mapped = sourceMapper?.ToClientPath(frame.SourcePath);
    var source = ToDapSource(mapped);
    dapFrames.Add(
        new DapStackFrame(
            frameHandles.Create(frame),
            frame.Name,
            source,
            frame.Line,
            Math.Max(1, frame.Column),
            source == null ? "deemphasize" : "normal"));
}
SendResponse(
    response,
    new DapStackTraceResponseBody(
        dapFrames,
        dapFrames.Count));
```

Replace `ToDapSource` with:

```csharp
private DapSource? ToDapSource(MappedSource? mapped)
{
    if (mapped == null || !mapped.Available)
        return null;
    return new DapSource(
        mapped.Name,
        ConvertDebuggerPathToClient(mapped.Path) ?? mapped.Path,
        mapped.SourceReference);
}
```

- [ ] **Step 6: Run focused and neighboring inspection tests**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj --no-restore --filter FullyQualifiedName~InspectionRequestTests
```

Expected: all `InspectionRequestTests` PASS. The mapped-frame assertions remain unchanged.

- [ ] **Step 7: Commit the source-handling fix**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Dap/DapStackFrameModels.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs
git commit -m "fix: omit unavailable stack frame sources"
```

---

### Task 2: Route Hover through safe evaluation

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Backend/EvaluationOptionsPolicy.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs:104-119`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs:166-172`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs:141-149`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs:310-352`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs:500-515`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:46-68`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:542-607`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggerBackendLifecycleTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/EvaluationOptionsPolicyTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/UnityDebugSessionLifecycleTests.cs`
- Modify: `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs`

**Interfaces:**
- Consumes: stopped DAP frame handles and `Mono.Debugging.Client.EvaluationOptions`.
- Produces: `BackendEvaluationMode.Safe`, `BackendEvaluationMode.Explicit`, `Evaluate(long frameId, string expression, BackendEvaluationMode mode)`, and `EvaluationOptionsPolicy.Create(EvaluationOptions baseline, BackendEvaluationMode mode)`.

- [ ] **Step 1: Write failing DAP routing tests**

Change the lifecycle assertion to:

```csharp
Assert.True(Required<bool>(response.SelectToken(
    "body.supportsEvaluateForHovers")));
```

Replace both `Explicit_evaluation_contexts_are_allowed` and the old Hover
rejection test with:

```csharp
[Theory]
[InlineData("hover", BackendEvaluationMode.Safe)]
[InlineData("watch", BackendEvaluationMode.Explicit)]
[InlineData("repl", BackendEvaluationMode.Explicit)]
public void Evaluation_context_selects_backend_mode(
    string context,
    BackendEvaluationMode expectedMode)
{
    var fixture = Fixture();
    var messages = Run(
        fixture.Session,
        Initialize(),
        Attach(fixture.Workspace),
        Request("threads", new { }),
        Request(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 }),
        Request(
            "evaluate",
            new
            {
                frameId = 1,
                expression = "_isVisible",
                context,
            }));

    Assert.True(Required<bool>(
        Response(messages, "evaluate")["success"]));
    Assert.Equal(1, fixture.Backend.EvaluateCount);
    Assert.Equal(expectedMode, fixture.Backend.LastEvaluationMode);
}
```

Add a replacement unknown-context privacy test:

```csharp
[Fact]
public void Unknown_evaluation_context_is_rejected_without_backend_call()
{
    var fixture = Fixture();
    var messages = Run(
        fixture.Session,
        Initialize(),
        Attach(fixture.Workspace),
        Request("threads", new { }),
        Request(
            "stackTrace",
            new { threadId = 1, startFrame = 0, levels = 20 }),
        Request(
            "evaluate",
            new
            {
                frameId = 1,
                expression = "SECRET_EXPRESSION",
                context = "clipboard",
            }));

    var response = Response(messages, "evaluate");
    Assert.False(Required<bool>(response["success"]));
    Assert.DoesNotContain(
        "SECRET_EXPRESSION",
        Required<string>(response["message"]));
    Assert.Equal(0, fixture.Backend.EvaluateCount);
}
```

- [ ] **Step 2: Write failing evaluation-policy tests**

Create `EvaluationOptionsPolicyTests.cs`:

```csharp
using System;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class EvaluationOptionsPolicyTests
    {
        [Fact]
        public void Safe_mode_disables_every_target_invocation_path()
        {
            var baseline = EvaluationOptions.DefaultOptions.Clone();
            baseline.AllowTargetInvoke = true;
            baseline.AllowMethodEvaluation = true;
            baseline.AllowToStringCalls = true;

            var result = EvaluationOptionsPolicy.Create(
                baseline,
                BackendEvaluationMode.Safe);

            Assert.False(result.AllowTargetInvoke);
            Assert.False(result.AllowMethodEvaluation);
            Assert.False(result.AllowToStringCalls);
        }

        [Fact]
        public void Explicit_mode_enables_user_requested_evaluation()
        {
            var baseline = EvaluationOptions.DefaultOptions.Clone();
            baseline.AllowTargetInvoke = false;
            baseline.AllowMethodEvaluation = false;
            baseline.AllowToStringCalls = false;

            var result = EvaluationOptionsPolicy.Create(
                baseline,
                BackendEvaluationMode.Explicit);

            Assert.True(result.AllowTargetInvoke);
            Assert.True(result.AllowMethodEvaluation);
            Assert.True(result.AllowToStringCalls);
        }

        [Fact]
        public void Unknown_mode_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EvaluationOptionsPolicy.Create(
                    EvaluationOptions.DefaultOptions,
                    (BackendEvaluationMode)99));
        }
    }
}
```

- [ ] **Step 3: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj --no-restore --filter "FullyQualifiedName~EvaluationOptionsPolicyTests|FullyQualifiedName~InspectionRequestTests.Evaluation_context_selects_backend_mode|FullyQualifiedName~UnityDebugSessionLifecycleTests.Initialize_reports_only_current_capabilities"
```

Expected: build/test failure because `BackendEvaluationMode` and `EvaluationOptionsPolicy` do not exist and Hover is still advertised as unsupported.

- [ ] **Step 4: Add the evaluation mode and options policy**

Add to `BackendModels.cs`:

```csharp
internal enum BackendEvaluationMode
{
    Safe,
    Explicit,
}
```

Create `EvaluationOptionsPolicy.cs`:

```csharp
using System;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Backend
{
    internal static class EvaluationOptionsPolicy
    {
        public static EvaluationOptions Create(
            EvaluationOptions baseline,
            BackendEvaluationMode mode)
        {
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (
                mode != BackendEvaluationMode.Safe &&
                mode != BackendEvaluationMode.Explicit)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            var result = baseline.Clone();
            var allowInvocation =
                mode == BackendEvaluationMode.Explicit;
            result.AllowTargetInvoke = allowInvocation;
            result.AllowMethodEvaluation = allowInvocation;
            result.AllowToStringCalls = allowInvocation;
            return result;
        }
    }
}
```

- [ ] **Step 5: Carry the mode through both backend interfaces**

Change both `IDebuggerBackend.Evaluate` and
`ISoftDebuggerSessionFacade.Evaluate` to:

```csharp
BackendEvaluationResult Evaluate(
    long frameId,
    string expression,
    BackendEvaluationMode mode);
```

Change `MonoDebuggerBackend.Evaluate` to forward the mode:

```csharp
public BackendEvaluationResult Evaluate(
    long frameId,
    string expression,
    BackendEvaluationMode mode)
{
    RequireAttached();
    return facade!.Evaluate(frameId, expression, mode);
}
```

Update every implementation and fake to the same signature. Add
`LastEvaluationMode` to `FakeDebuggerBackend` and
`FakeSoftDebuggerSessionFacade`, setting it before returning their configured
result. Update the lifecycle forwarding assertion to call:

```csharp
backend.Evaluate(5, "health", BackendEvaluationMode.Safe)
```

and assert:

```csharp
Assert.Equal(
    BackendEvaluationMode.Safe,
    facade.LastEvaluationMode);
```

- [ ] **Step 6: Select safe or explicit Mono evaluation options**

Change `SoftDebuggerSessionFacade.Evaluate` to accept the mode and select:

```csharp
var options = EvaluationOptionsPolicy.Create(
    session.EvaluationOptions,
    mode);
```

Replace the duplicated connection setup with:

```csharp
var evaluationOptions = EvaluationOptionsPolicy.Create(
    EvaluationOptions.DefaultOptions,
    BackendEvaluationMode.Safe);
```

Keep the existing `SafeEvaluationOptions()` and
`ExplicitEvaluationOptions()` helpers for inspection call sites, but implement
them through the policy:

```csharp
private EvaluationOptions SafeEvaluationOptions() =>
    EvaluationOptionsPolicy.Create(
        session.EvaluationOptions,
        BackendEvaluationMode.Safe);

private EvaluationOptions ExplicitEvaluationOptions() =>
    EvaluationOptionsPolicy.Create(
        session.EvaluationOptions,
        BackendEvaluationMode.Explicit);
```

- [ ] **Step 7: Enable Hover and map DAP contexts**

Set:

```csharp
supportsEvaluateForHovers = true,
```

In `UnityDebugSession.Evaluate`, map the context before resolving the
expression:

```csharp
BackendEvaluationMode evaluationMode;
if (string.Equals(context, "hover", StringComparison.Ordinal))
{
    evaluationMode = BackendEvaluationMode.Safe;
}
else if (
    string.Equals(context, "watch", StringComparison.Ordinal) ||
    string.Equals(context, "repl", StringComparison.Ordinal))
{
    evaluationMode = BackendEvaluationMode.Explicit;
}
else
{
    SendErrorResponse(
        response,
        2024,
        "Evaluation is available only for hover, watch, or repl.");
    return;
}
```

Call:

```csharp
var result = value.Evaluate(
    frame.Id,
    expression!,
    evaluationMode);
```

Do not add expression logging.

- [ ] **Step 8: Run policy, DAP, and backend tests to verify GREEN**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj --no-restore --filter "FullyQualifiedName~EvaluationOptionsPolicyTests|FullyQualifiedName~InspectionRequestTests|FullyQualifiedName~UnityDebugSessionLifecycleTests|FullyQualifiedName~MonoDebuggerBackendLifecycleTests"
```

Expected: all selected tests PASS, including Hover Safe and Watch/REPL
Explicit routing.

- [ ] **Step 9: Commit safe Hover**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs adapter/src/UnityDebugger.Adapter/Backend/EvaluationOptionsPolicy.cs adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend/EvaluationOptionsPolicyTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggerBackendLifecycleTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/UnityDebugSessionLifecycleTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs
git commit -m "feat: add safe hover evaluation"
```

---

### Task 3: Cover Pause and Hover through the Adapter process

**Files:**
- Modify: `tests/integration/UnityDebugger.TestAdapter/Program.cs`
- Modify: `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs`
- Modify: `tests/integration/adapter.integration.test.ts`

**Interfaces:**
- Consumes: the Task 1 optional-source DAP model and Task 2 evaluation mode.
- Produces: a deterministic `pause-source` process scenario proving Pause response ordering, missing-source serialization, mapped-frame selection, safe Hover, Continue, and clean disconnect.

- [ ] **Step 1: Write the failing process-level scenario test**

Add to `adapter.integration.test.ts`:

```typescript
it("pauses on an unavailable frame and safely evaluates a project field", async () => {
  const client = await start("pause-source");
  await initialize(client);
  await attach(client);

  const threads = await client.request("threads", {});
  const threadId = threads.body.threads[0].id;
  await client.request("pause", { threadId });
  const stopped = await client.waitForEvent("stopped");
  expect(stopped.body).toMatchObject({
    reason: "pause",
    threadId,
    allThreadsStopped: true,
  });

  const stack = await client.request("stackTrace", {
    threadId,
    startFrame: 0,
    levels: 20,
  });
  expect(stack.body.stackFrames[0]).toMatchObject({
    name: "UnityEngine.PlayerLoop",
    presentationHint: "deemphasize",
  });
  expect(stack.body.stackFrames[0]).not.toHaveProperty("source");
  expect(stack.body.stackFrames[1].source.path).toBe(fixtureSource);

  const hover = await client.request("evaluate", {
    expression: "_isVisible",
    frameId: stack.body.stackFrames[1].id,
    context: "hover",
  });
  expect(hover.body.result).toBe("false");

  await client.request("continue", { threadId });
  await client.request("disconnect", {});
  await client.expectCleanExit(0);
});
```

- [ ] **Step 2: Run integration tests and verify RED**

Run:

```powershell
npm run test:integration
```

Expected: FAIL because `pause-source` is not an accepted test-adapter
scenario.

- [ ] **Step 3: Add the deterministic test-adapter behavior**

Add `"pause-source"` to `Program.Scenarios`.

In `ScenarioDebuggerBackend`, add:

```csharp
private int pauseRequested;
```

Set it in `Pause`:

```csharp
public void Pause(long threadId)
{
    if (scenario == "pause-source")
        Interlocked.Exchange(ref pauseRequested, 1);
    ScheduleStop(BackendStopReason.Pause, 10);
}
```

For `pause-source` after Pause, return the unavailable frame before the mapped
fixture frame:

```csharp
if (
    scenario == "pause-source" &&
    Volatile.Read(ref pauseRequested) == 1)
{
    return new[]
    {
        new BackendStackFrame(
            9,
            1,
            "UnityEngine.PlayerLoop",
            string.Empty,
            0,
            1),
        new BackendStackFrame(
            10,
            1,
            "FixtureBehaviour.Update",
            sourcePath,
            12,
            1),
    };
}
```

In its Task 2 `Evaluate` implementation, return the distinguishing safe field
value:

```csharp
if (
    scenario == "pause-source" &&
    mode == BackendEvaluationMode.Safe &&
    string.Equals(expression, "_isVisible", StringComparison.Ordinal))
{
    return new BackendEvaluationResult(
        "false",
        "System.Boolean",
        0);
}
```

Retain the existing `"0"` result for every other test scenario.

- [ ] **Step 4: Run process integration and full automated tests**

Run:

```powershell
npm run test:integration
npm test
git diff --check
```

Expected: integration and the full test suite PASS; `git diff --check` emits
no output.

- [ ] **Step 5: Request code review**

Use `superpowers:requesting-code-review`. The review must explicitly check:

- missing DAP `source` serialization rather than `source: null`;
- no changes to vendored DAP files;
- Hover mode cannot enable any invocation flag;
- Watch/REPL remain explicit;
- expressions and values do not enter diagnostics;
- Pause response precedes the buffered stopped event;
- no new Critical or Important findings.

Address every Critical or Important finding through a new failing regression
test before proceeding.

- [ ] **Step 6: Commit process-level coverage**

```powershell
git add -- tests/integration/UnityDebugger.TestAdapter/Program.cs tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs tests/integration/adapter.integration.test.ts
git commit -m "test: cover pause source and safe hover"
```

---

### Task 4: Package and verify the supported real Editor

**Files:**
- Modify: `third-party/runtime-assemblies.json`
- Generate (ignored): `dist/unity-debugger-vscode-0.1.0.vsix`

**Interfaces:**
- Consumes: the reviewed Task 1-3 commits and the existing MyGame Editor/VS Code session.
- Produces: an audited VSIX, matching installed Adapter hash, supported-Editor acceptance evidence, and a clean `master`.

- [ ] **Step 1: Build the final candidate and refresh the runtime inventory**

Run:

```powershell
npm run build
npm run stage:adapter
npm run inventory:runtime:write
npm run package
git diff --check
```

Expected:

- .NET build succeeds with zero warnings and errors;
- 18 production Adapter files are staged;
- 17 runtime inventory entries are written and verified;
- VSIX contains only the audited allowlist;
- package contract test passes.

- [ ] **Step 2: Stop the old debug session before installing**

Ask the user to press `Shift+F5` in the VS Code window opened from MyGame.
Wait for explicit confirmation. Do not close or launch the Editor and do not
start another VS Code instance.

- [ ] **Step 3: Install and hash-verify the exact VSIX**

Run:

```powershell
code --install-extension H:\workspace\Unity\unity-debugger-vscode\dist\unity-debugger-vscode-0.1.0.vsix --force
```

Resolve the installed extension directory and compare SHA-256 for:

```text
adapter\win32-x64\UnityCommunityDebug.exe
```

between the repository staging directory and the installed extension.
Expected: hashes are identical.

- [ ] **Step 4: Run user-operated MyGame acceptance**

Ask the user to:

1. run `Developer: Reload Window`;
2. press `F5` to attach;
3. enter Play Mode;
4. click Pause while the game is running;
5. confirm no “editor could not be opened” page appears;
6. confirm the internal top frame is visible and not openable;
7. select the next MyGame frame and confirm its source opens;
8. hover `_isVisible` and `this._isVisible` on the previously verified
   `DevGmTestPanel` stop and confirm both values appear;
9. confirm Watch and Debug Console still evaluate `_isVisible`;
10. Continue and stop the debug session.

If any step fails, retain the current logs, return to systematic debugging,
and do not commit the runtime inventory.

- [ ] **Step 5: Verify final logs and repository state**

Read the recent VS Code renderer log and confirm no new text-model creation
exception occurred at the Pause timestamp. Confirm Tuanjie stayed running.
Then run:

```powershell
npm test
node scripts/verify-vsix.mjs dist/unity-debugger-vscode-0.1.0.vsix
git diff --check
git status --short
```

Expected: all automated tests and VSIX audit PASS; only
`third-party/runtime-assemblies.json` remains for the packaging commit.

- [ ] **Step 6: Commit the audited runtime inventory**

```powershell
git add -- third-party/runtime-assemblies.json
git commit -m "build: refresh pause and hover runtime inventory"
git status --short
```

Expected: `master` is clean. Do not push, tag, or publish without a separate
user request.
