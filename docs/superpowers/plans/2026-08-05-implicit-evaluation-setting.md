# Implicit Evaluation Setting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [x]) syntax for tracking.

**Goal:** Enable implicit evaluation by default for Hover, Locals, and variable expansion while exposing a resource-scoped Cursor setting that restores safe automatic inspection.

**Architecture:** The TypeScript extension resolves "unityDebuggerPure.enableImplicitEvaluation" for the attached workspace resource and sends a private boolean Attach argument. The Adapter validates and stores that value per session, then passes an explicit BackendEvaluationMode into Hover, scope creation, and variable expansion; Watch and REPL remain Explicit. The setting is session-static and takes effect on the next Attach.

**Tech Stack:** VS Code/Cursor extension manifest, TypeScript 7, Vitest, Node test runner, C#/.NET Framework 4.8, xUnit, Debug Adapter Protocol, Mono.Debugging.

## Global Constraints

- Setting key: "unityDebuggerPure.enableImplicitEvaluation".
- Setting type: boolean; default: true; scope: resource.
- Effective precedence remains Workspace Folder, Workspace, User, then default.
- Setting changes take effect only on the next Attach session.
- Enabled applies invoking evaluation to Hover, Locals, and variable expansion.
- Disabled restores Safe evaluation for Hover, Locals, and variable expansion.
- Watch and Debug Console remain Explicit regardless of the setting.
- The setting description warns that target code may execute and cause side effects or delays.
- Omitted private Attach arguments default to true; malformed values are rejected.
- Do not log expressions, values, or workspace paths.
- Do not edit vendored debugger sources.
- Preserve the existing uncommitted breakpoint-hit changes and keep their tests passing.

---

### Task 0: Preserve the validated breakpoint-hit baseline

**Files:**
- Existing modified Adapter, test, and runtime-inventory files shown by git status

**Interfaces:**
- Consumes: the validated hitBreakpointIds implementation.
- Produces: a committed baseline so the new feature is reviewable independently.

- [x] **Step 1: Inspect the baseline**

Run:

~~~powershell
git status --short
git diff --check
git diff --stat
~~~

Expected: only the known breakpoint-hit source/tests and third-party/runtime-assemblies.json are modified.

- [x] **Step 2: Re-run directly relevant tests**

Run:

~~~powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ExecutionControlTests|FullyQualifiedName~DomainReloadTests"
npm run test:integration
~~~

Expected: zero failures.

- [x] **Step 3: Commit the known baseline only**

Stage the paths printed in Step 1 that belong to hitBreakpointIds and the reviewed runtime hash, run "git diff --cached --check", then commit:

~~~powershell
git commit -m "fix: report breakpoint hits to editor"
~~~

Do not stage the plan file or any unrelated path.

---

### Task 1: Contribute and resolve the Cursor setting

**Files:**
- Modify: package.json
- Modify: extension/src/model.ts
- Modify: extension/src/debugConfigurationProvider.ts
- Modify: extension/src/extension.ts
- Modify: tests/build/scaffold.test.mjs
- Modify: tests/extension/debugConfigurationProvider.test.ts

**Interfaces:**
- Consumes: Cursor resource-scoped configuration resolution.
- Produces: UnityAttachConfiguration.__enableImplicitEvaluation: boolean and an optional provider dependency "(workspaceRoot: string) => boolean".

- [x] **Step 1: Write failing manifest and provider tests**

Add this boundary assertion to tests/build/scaffold.test.mjs:

~~~javascript
const implicitEvaluation =
  manifest.contributes.configuration.properties[
    "unityDebuggerPure.enableImplicitEvaluation"
  ];
assert.equal(implicitEvaluation.type, "boolean");
assert.equal(implicitEvaluation.default, true);
assert.equal(implicitEvaluation.scope, "resource");
assert.match(implicitEvaluation.description, /execute.*target code/i);
~~~

Add provider tests whose literal expected results are true for the default reader and false for an injected resource reader:

~~~typescript
expect(defaultResolved?.__enableImplicitEvaluation).toBe(true);
expect(disabledResolved?.__enableImplicitEvaluation).toBe(false);
~~~

The disabled reader must assert that its workspaceRoot argument is "H:\\fixture".

- [x] **Step 2: Run tests and verify RED**

Run:

~~~powershell
node --test tests/build/scaffold.test.mjs
npx vitest run tests/extension/debugConfigurationProvider.test.ts
~~~

Expected: failures because the manifest setting and private Attach field do not exist.

- [x] **Step 3: Add the manifest setting and typed field**

Add under contributes:

~~~json
"configuration": {
  "title": "Unity Debugger Pure",
  "properties": {
    "unityDebuggerPure.enableImplicitEvaluation": {
      "type": "boolean",
      "default": true,
      "scope": "resource",
      "description": "Allow Hover, Locals, and variable expansion to execute target code for property getters and ToString(). This can cause side effects or delays. Changes apply on the next Attach."
    }
  }
}
~~~

Add to UnityAttachConfiguration:

~~~typescript
readonly __enableImplicitEvaluation: boolean;
~~~

- [x] **Step 4: Resolve the effective setting once per Attach**

Append this dependency to DebugConfigurationProvider's constructor:

~~~typescript
private readonly readImplicitEvaluation: (
  workspaceRoot: string,
) => boolean = () => true,
~~~

Read it using folder.uri.fsPath and pass the result through both ordinary discovery and trusted API attach flows. Add this exact field to the returned trusted configuration:

~~~typescript
__enableImplicitEvaluation: enableImplicitEvaluation,
~~~

Inject the real reader from extension.ts:

~~~typescript
(workspaceRoot) =>
  vscode.workspace
    .getConfiguration(
      "unityDebuggerPure",
      vscode.Uri.file(workspaceRoot),
    )
    .get<boolean>("enableImplicitEvaluation", true),
~~~

- [x] **Step 5: Verify GREEN**

Run:

~~~powershell
node --test tests/build/scaffold.test.mjs
npx vitest run tests/extension/debugConfigurationProvider.test.ts tests/extension/publicApi.test.ts
npm run typecheck
~~~

Expected: all pass with no TypeScript errors.

- [x] **Step 6: Commit**

Stage only the six Task 1 files, run "git diff --cached --check", and commit:

~~~powershell
git commit -m "feat: add implicit evaluation setting"
~~~

---

### Task 2: Apply the per-session policy in the Adapter

**Files:**
- Modify: adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs
- Modify: adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs
- Modify: adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs
- Modify: adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs
- Modify: adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs
- Modify: adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs
- Modify: adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggerBackendLifecycleTests.cs

**Interfaces:**
- Consumes: private Attach boolean "__enableImplicitEvaluation".
- Produces: AttachTarget.EnableImplicitEvaluation and mode-bearing GetScopes/GetVariables methods.

- [x] **Step 1: Write failing Attach parser tests**

Add tests for omitted/default true, explicit true/false, and string "true" rejection:

~~~csharp
Assert.True(
    AttachArguments.Parse(ValidArguments())
        .EnableImplicitEvaluation);

json["__enableImplicitEvaluation"] = false;
Assert.False(
    AttachArguments.Parse(json)
        .EnableImplicitEvaluation);

json["__enableImplicitEvaluation"] = "true";
Assert.Throws<AttachArgumentException>(
    () => AttachArguments.Parse(json));
~~~

- [x] **Step 2: Write failing DAP routing tests**

Extend FakeDebuggerBackend with LastScopesMode and LastVariablesMode. The default Attach test must send scopes, variables, and Hover, then assert all three modes are Explicit. A disabled Attach test must assert all three are Safe, then send Watch and assert LastEvaluationMode becomes Explicit.

These tests catch hard-coded Safe/Explicit branches and lost settings; they assert real UnityDebugSession request routing rather than fake call existence.

- [x] **Step 3: Verify RED**

Run:

~~~powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AttachArgumentsTests|FullyQualifiedName~InspectionRequestTests"
~~~

Expected: compile/test failure because the policy and new method signatures do not exist.

- [x] **Step 4: Parse and store the session policy**

Extend AttachTarget:

~~~csharp
public AttachTarget(
    int processId,
    IPAddress address,
    int port,
    string workspaceRoot,
    string projectVersion,
    bool enableImplicitEvaluation = true)
{
    ProcessId = processId;
    Address = address;
    Port = port;
    WorkspaceRoot = workspaceRoot;
    ProjectVersion = projectVersion;
    EnableImplicitEvaluation = enableImplicitEvaluation;
}

public bool EnableImplicitEvaluation { get; }
~~~

AttachArguments.Parse accepts a present token only when its JTokenType is Boolean and otherwise defaults to true.

In UnityDebugSession add:

~~~csharp
private BackendEvaluationMode automaticEvaluationMode =
    BackendEvaluationMode.Explicit;
~~~

After parsing Attach arguments assign:

~~~csharp
automaticEvaluationMode = target.EnableImplicitEvaluation
    ? BackendEvaluationMode.Explicit
    : BackendEvaluationMode.Safe;
~~~

- [x] **Step 5: Carry mode through automatic inspection**

Change both backend interfaces and every implementation to:

~~~csharp
IReadOnlyList<BackendScope> GetScopes(
    long frameId,
    BackendEvaluationMode mode);
IReadOnlyList<BackendVariable> GetVariables(
    long variablesReference,
    BackendEvaluationMode mode);
~~~

MonoDebuggerBackend forwards mode unchanged. SoftDebuggerSessionFacade creates one options instance per request with:

~~~csharp
var options = EvaluationOptionsPolicy.Create(
    session.EvaluationOptions,
    mode);
~~~

Use that instance for value resolution and child enumeration. UnityDebugSession passes automaticEvaluationMode to every scope and variable request, including children created after Evaluate.

- [x] **Step 6: Select Hover mode while preserving explicit contexts**

Replace the hard-coded Hover Safe assignment with:

~~~csharp
mode = automaticEvaluationMode;
~~~

Keep Watch and REPL hard-coded Explicit and keep unknown contexts rejected.

- [x] **Step 7: Update fakes and facade lifecycle coverage**

Update all IDebuggerBackend and ISoftDebuggerSessionFacade implementations. Fakes record the received mode. Extend MonoDebuggerBackendLifecycleTests to assert Safe and Explicit are forwarded unchanged for scopes and variables.

- [x] **Step 8: Verify GREEN and regressions**

Run:

~~~powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AttachArgumentsTests|FullyQualifiedName~InspectionRequestTests|FullyQualifiedName~MonoDebuggerBackendLifecycleTests"
dotnet test UnityDebugger.sln -c Release --no-restore
~~~

Expected: zero failures. Only the existing vendored CS0168 warning is allowed.

- [x] **Step 9: Commit**

Stage only Task 2 files, run "git diff --cached --check", and commit:

~~~powershell
git commit -m "feat: apply implicit evaluation policy"
~~~

---

### Task 3: Audit process and packaged behavior

**Files:**
- Modify: tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs
- Modify: tests/integration/adapter.integration.test.ts
- Modify: scripts/verify-vsix.mjs
- Modify: tests/package/vsix.test.mjs
- Modify: README.md

**Interfaces:**
- Consumes: mode-bearing backend inspection and packaged manifest setting.
- Produces: process-level default/disabled evidence and a VSIX verifier that rejects a changed setting contract.

- [x] **Step 1: Write failing process tests**

Make ScenarioDebuggerBackend return mode-dependent literals from Hover and variables:

~~~csharp
var displayValue =
    mode == BackendEvaluationMode.Explicit
        ? "implicit-enabled"
        : "implicit-disabled";
~~~

The integration test attaches once with the private argument omitted and expects "implicit-enabled". It attaches a second client with false and expects "implicit-disabled" from Hover and variables, while Watch still returns "implicit-enabled".

- [x] **Step 2: Write a failing packaged-contract mutation test**

Extend tests/package/vsix.test.mjs to rewrite the packaged setting default to false and assert scripts/verify-vsix.mjs fails with a message matching /implicit evaluation setting/i.

This tests the packaged artifact boundary, not source text.

- [x] **Step 3: Verify RED**

Run:

~~~powershell
npm run test:integration
npm run test:package
~~~

Expected: integration compile/test failures until signatures and outputs are implemented; the mutated VSIX is initially accepted.

- [x] **Step 4: Implement the test Adapter and verifier**

Update ScenarioDebuggerBackend signatures and return mode-dependent values. Let the TypeScript attach helper optionally omit the private argument or send a literal boolean.

In scripts/verify-vsix.mjs, validate the exact setting key, type boolean, default true, scope resource, and a description containing the target-code risk.

- [x] **Step 5: Document usage**

README includes:

~~~json
{
  "unityDebuggerPure.enableImplicitEvaluation": false
}
~~~

Explain User, Workspace, and Workspace Folder precedence, next-Attach activation, and the target-code side-effect risk.

- [x] **Step 6: Verify GREEN**

Run:

~~~powershell
npm run test:integration
node --test tests/build/scaffold.test.mjs
npx vitest run tests/extension/debugConfigurationProvider.test.ts
~~~

Expected: all pass.

- [x] **Step 7: Commit**

Stage only Task 3 files, run "git diff --cached --check", and commit:

~~~powershell
git commit -m "test: audit implicit evaluation setting"
~~~

---

### Task 4: Verify, package, and install in Cursor

**Files:**
- Modify after reviewed build: third-party/runtime-assemblies.json
- Generate ignored artifact: dist/unity-debugger-pure-0.2.0.vsix

**Interfaces:**
- Consumes: completed implementation.
- Produces: verified VSIX installed as kpk.unity-debugger-pure@0.2.0 with matching Adapter SHA256.

- [x] **Step 1: Run complete source verification**

Run:

~~~powershell
npm run typecheck
npm test
npm run build
git diff --check
~~~

Expected: all suites report zero failures and the build reports zero errors. Only the existing vendored CS0168 warning is allowed.

- [x] **Step 2: Refresh and inspect runtime inventory**

Run:

~~~powershell
npm run stage:adapter
npm run inventory:runtime:write
git diff -- third-party/runtime-assemblies.json
~~~

Expected: only the UnityDebuggerPure.exe SHA256 changes.

- [x] **Step 3: Package**

Run:

~~~powershell
npm run package
~~~

Expected: runtime inventory verification, VSIX verification, and package tests pass; dist/unity-debugger-pure-0.2.0.vsix exists.

- [x] **Step 4: Install and verify Cursor registration**

Run:

~~~powershell
cursor --install-extension "D:\Unity\unity-debugger-vscode\dist\unity-debugger-pure-0.2.0.vsix" --force
cursor --list-extensions --show-versions | Select-String -Pattern '^kpk\.unity-debugger-pure@0\.2\.0$'
~~~

Expected: successful installation and exact version listing.

- [x] **Step 5: Verify installed Adapter bytes**

Locate the extension with "cursor --locate-extension kpk.unity-debugger-pure". Compute SHA256 for its adapter/win32-x64/UnityDebuggerPure.exe and the staged executable. Expected: hashes are identical.

- [x] **Step 6: Commit reviewed inventory and check status**

Stage only third-party/runtime-assemblies.json, run cached diff checks, then:

~~~powershell
git commit -m "build: refresh adapter runtime inventory"
git status --short
git log -5 --oneline
~~~

Expected: no uncommitted source changes other than this plan until the plan is intentionally committed. Tell the user to run "Developer: Reload Window", reattach, and test both default true and a workspace override of false.
