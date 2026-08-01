# Initial Debug Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make VS Code generate the existing minimal Unity Debugger Pure attach configuration when a user first creates `launch.json`.

**Architecture:** Add a declarative `initialConfigurations` entry beside the existing debugger snippet and require both templates to remain identical. Extend the source-manifest test and packaged-VSIX verifier; do not change extension or Adapter runtime code.

**Tech Stack:** VS Code extension manifest JSON, Node.js built-in test runner, VSIX ZIP audit.

## Global Constraints

- The generated configuration is exactly `{ "name": "Attach to Unity Debugger Pure", "type": "unity-debugger-pure", "request": "attach" }`.
- Keep interactive target discovery; do not add host, port, target, executable, or MCP fields.
- Keep version `0.2.0` and all runtime behavior unchanged.
- Do not commit, push, tag, or publish without separate user authorization.

---

### Task 1: Add and audit the initial attach configuration

**Files:**
- Modify: `tests/build/scaffold.test.mjs`
- Modify: `package.json`
- Modify: `scripts/verify-vsix.mjs`
- Modify: `tests/package/vsix.test.mjs`

**Interfaces:**
- Consumes: `contributes.debuggers[0].configurationSnippets[0].body`.
- Produces: `contributes.debuggers[0].initialConfigurations`, an array with one configuration equal to the existing snippet body.

- [ ] **Step 1: Add the failing source-manifest contract**

Define the expected configuration as a hand-written literal, then require both
the snippet body and the initial configuration to equal it:

```js
const expectedAttachConfiguration = {
  name: "Attach to Unity Debugger Pure",
  type: "unity-debugger-pure",
  request: "attach",
};
assert.deepEqual(
  debuggerContribution.configurationSnippets[0].body,
  expectedAttachConfiguration,
);
assert.deepEqual(debuggerContribution.initialConfigurations, [
  expectedAttachConfiguration,
]);
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
node --test tests/build/scaffold.test.mjs
```

Expected: FAIL because `initialConfigurations` is `undefined`.

- [ ] **Step 3: Add the minimal manifest contribution**

Add this sibling of `configurationSnippets` in `package.json`:

```json
"initialConfigurations": [
  {
    "name": "Attach to Unity Debugger Pure",
    "type": "unity-debugger-pure",
    "request": "attach"
  }
]
```

Do not change the existing `configurationSnippets` or
`configurationAttributes`.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run:

```powershell
node --test tests/build/scaffold.test.mjs
```

Expected: PASS.

- [ ] **Step 5: Add a failing packaged-VSIX contract**

In `tests/package/vsix.test.mjs`, copy the real VSIX to a temporary fixture,
remove `initialConfigurations` from its packaged manifest, and verify the
production audit rejects the altered archive. Run the focused test and confirm
RED because the existing verifier accepts the altered package.

- [ ] **Step 6: Extend the production VSIX manifest audit**

In `scripts/verify-vsix.mjs`, after resolving `debuggerContribution`, compare
the packaged snippet and initial configuration to the same exact literal:

```js
const expectedAttachConfiguration = {
  name: "Attach to Unity Debugger Pure",
  type: "unity-debugger-pure",
  request: "attach",
};
if (
  JSON.stringify(debuggerContribution.configurationSnippets?.[0]?.body) !==
    JSON.stringify(expectedAttachConfiguration) ||
  JSON.stringify(debuggerContribution.initialConfigurations) !==
    JSON.stringify([expectedAttachConfiguration])
) {
  throw new Error(
    "Packaged manifest has the wrong initial debug configuration.",
  );
}
```

- [ ] **Step 7: Run complete validation and packaging**

Use the repository-pinned Node `26.5.0` runtime and run:

```powershell
$nodeDir = 'C:\Users\Admin\scoop\apps\nodejs\26.5.0'
$node260 = Join-Path $nodeDir 'node.exe'
$npm260 = Join-Path $nodeDir 'node_modules\npm\bin\npm-cli.js'
$env:PATH = "$nodeDir;$env:PATH"

& $node260 $npm260 ci
dotnet restore UnityDebugger.sln --locked-mode
& $node260 $npm260 run typecheck
& $node260 $npm260 test
& $node260 $npm260 run package
& $node260 $npm260 run verify:vsix
git diff --check
git status --short
```

Expected: all commands exit `0`; the audited `0.2.0` VSIX is rebuilt; only
the spec, plan, manifest, source-manifest test, package test, and verifier are
modified.

- [ ] **Step 8: Request review and commit authorization**

Review the complete diff for exact template equality and package drift. Do not
run `git commit`, `git push`, create a tag, or publish until the user explicitly
authorizes each requested Git/release action.
