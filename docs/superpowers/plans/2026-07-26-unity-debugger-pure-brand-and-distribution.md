# Unity Debugger Pure Brand and Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the unreleased debugger to the final `Unity Debugger Pure`
identity, produce one audited `0.1.0` VSIX, and prepare controlled distribution
through the Visual Studio Marketplace, Open VSX, and Cursor.

**Architecture:** A single TypeScript product-identity module supplies all
extension-host constants while the manifest remains the registry contract and
is checked against those constants. The C# Adapter and packaging pipeline use
the same final executable and diagnostics identities. GitHub Release owns the
one audited VSIX; separate, manually approved Marketplace and Open VSX
workflows download and hash-verify that artifact without rebuilding it.

**Tech Stack:** VS Code Extension API, TypeScript 7, Vitest 4, Node.js 26.5,
ESM build scripts, .NET SDK 10, .NET Framework 4.8 x64, xUnit, GitHub Actions,
`@vscode/vsce` 3.9.2, `ovsx` 1.0.2.

## Global Constraints

- Display name: `Unity Debugger Pure`.
- Publisher and extension ID: `kpk.unity-debugger-pure`.
- Debug type: `unity-debugger-pure`; do not register a `unity-community`
  compatibility alias.
- Default launch name: `Attach to Unity Debugger Pure`.
- Adapter executable: `UnityDebuggerPure.exe`.
- Diagnostics directory:
  `%LOCALAPPDATA%\unity-debugger-pure\logs`.
- VSIX and version: `unity-debugger-pure-0.1.0.vsix`, version `0.1.0`.
- Public repository:
  `https://github.com/kpkhxlgy0/unity-debugger-vscode`; branch `master`.
- Keep the local/repository name `unity-debugger-vscode`, solution
  `UnityDebugger.sln`, and C# namespaces `UnityDebugger.*`.
- Windows x64 only; Adapter target stays `net48`; build SDK stays .NET 10.
- Formal Editor baseline stays Tuanjie `2022.3.62t11`; debugger behavior and
  support matrix must not change.
- Build one VSIX once. Both registries publish the exact GitHub Release asset
  after SHA-256 verification; publishing workflows must not package from
  source.
- `VSCE_PAT` and `OVSX_PAT` remain separate GitHub Environment secrets and
  must never appear in source, logs, commands captured in history, or chat.
- Actual GitHub repository creation, push, tag, Marketplace publication, Open
  VSX ownership request/publication, and Cursor request require separate
  action-time confirmation.
- Real Editor acceptance uses only
  `H:\workspace\Unity\Tuanjie\Projects\MyGame` and its existing VS Code window.
  Do not create or launch another project or VS Code profile.
- Do not modify MyGame source. Update only the migrated debugger entry in
  MyGame's `.vscode\launch.json`.
- Before every Computer Use session, obtain explicit user confirmation and
  close Computer Use immediately after the required interaction.
- Never treat a missed breakpoint as a debugger failure without first proving
  that the selected MyGame code path is currently reachable.

## File Map

### New files

- `extension/src/productIdentity.ts` — one typed source for extension-host
  product constants.
- `tests/extension/productIdentity.test.ts` — exact product-identity contract.
- `tests/build/branding.test.mjs` — active-product legacy-token and manifest
  consistency checks.
- `scripts/verify-release-artifact.mjs` — verifies release checksum, filename,
  tag, publisher, package name, and manifest version before publication.
- `tests/build/release-artifact.test.mjs` — valid and tampered release-asset
  tests.
- `tests/build/release-workflows.test.mjs` — static guarantees that both
  publishing workflows consume the prebuilt asset and use separate controls.
- `.github/workflows/publish-open-vsx.yml` — separately approved Open VSX
  publication.
- `docs/release-checklist.md` — exact public release, ownership, and Cursor
  verification checklist.

### Modified product files

- `package.json`, `package-lock.json` — registry identity, source links,
  scripts, and pinned Open VSX CLI.
- `extension/src/adapterLauncher.ts`,
  `extension/src/debugConfigurationProvider.ts`,
  `extension/src/diagnostics.ts`, `extension/src/extension.ts`,
  `extension/src/model.ts` — consume the final identity constants.
- `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj` — final
  Adapter assembly name.
- `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs` — final support
  URL.
- `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs` — final
  diagnostics directory.
- `adapter/src/UnityDebugger.Adapter/packages.lock.json`,
  `tests/adapter/UnityDebugger.Adapter.Tests/packages.lock.json`,
  `tests/integration/UnityDebugger.TestAdapter/packages.lock.json` — restored
  project identity.
- `scripts/stage-adapter.mjs`,
  `scripts/generate-runtime-inventory.mjs`, `scripts/verify-vsix.mjs` — final
  executable and package paths.
- `third-party/runtime-assemblies.json` — regenerated Adapter path and hash.
- `README.md`, `CHANGELOG.md`, `LICENSE` — public identity and distribution
  documentation.
- `.github/workflows/ci.yml`, `.github/workflows/release.yml`,
  `.github/workflows/publish-marketplace.yml` — final artifact names and
  immutable publishing.

### Modified tests

- `tests/build/scaffold.test.mjs`
- `tests/extension/adapterLauncher.test.ts`
- `tests/extension/debugConfigurationProvider.test.ts`
- `tests/extension/diagnostics.test.ts`
- `tests/adapter/UnityDebugger.Adapter.Tests/Build/AdapterAssemblyTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/DiagnosticLogTests.cs`
- `tests/integration/adapter.integration.test.ts`
- `tests/package/vsix.test.mjs`

### Acceptance-only external file

- `H:\workspace\Unity\Tuanjie\Projects\MyGame\.vscode\launch.json` — migrate
  only the `unity-community` entry; leave the unrelated `vstuc` entry intact.

---

### Task 1: Establish the final identity and manifest contract

**Files:**

- Create: `extension/src/productIdentity.ts`
- Create: `tests/extension/productIdentity.test.ts`
- Modify: `tests/build/scaffold.test.mjs`
- Modify: `package.json`
- Modify: `package-lock.json`

**Interfaces:**

- Produces:
  `PRODUCT_IDENTITY: Readonly<{ publisher: "kpk"; extensionName:
  "unity-debugger-pure"; displayName: "Unity Debugger Pure"; description:
  string; debugType: "unity-debugger-pure"; defaultConfigurationName:
  "Attach to Unity Debugger Pure"; commandIds: Readonly<Record<string,
  string>>; adapterExecutable: "UnityDebuggerPure.exe";
  diagnosticsDirectoryName: "unity-debugger-pure"; vsixFileName:
  "unity-debugger-pure-0.1.0.vsix" }>` for Tasks 2 and 4.
- Consumes: no new interfaces.

- [ ] **Step 1: Write the failing product-identity tests**

Create `tests/extension/productIdentity.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";

describe("PRODUCT_IDENTITY", () => {
  it("defines the reviewed public identity", () => {
    expect(PRODUCT_IDENTITY).toEqual({
      publisher: "kpk",
      extensionName: "unity-debugger-pure",
      displayName: "Unity Debugger Pure",
      description:
        "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, " +
        "without C# Dev Kit or Microsoft's Unity extension.",
      debugType: "unity-debugger-pure",
      defaultConfigurationName: "Attach to Unity Debugger Pure",
      commandIds: {
        refreshTargets: "unity-debugger-pure.refreshTargets",
        openLogs: "unity-debugger-pure.openLogs",
        copyDiagnostics: "unity-debugger-pure.copyDiagnostics",
      },
      adapterExecutable: "UnityDebuggerPure.exe",
      diagnosticsDirectoryName: "unity-debugger-pure",
      vsixFileName: "unity-debugger-pure-0.1.0.vsix",
    });
  });
});
```

In `tests/build/scaffold.test.mjs`, replace the old name/display/debug-type
assertions and add:

```js
assert.equal(manifest.publisher, "kpk");
assert.equal(manifest.name, "unity-debugger-pure");
assert.equal(manifest.displayName, "Unity Debugger Pure");
assert.equal(
  manifest.description,
  "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, " +
    "without C# Dev Kit or Microsoft's Unity extension.",
);
assert.deepEqual(manifest.repository, {
  type: "git",
  url: "https://github.com/kpkhxlgy0/unity-debugger-vscode.git",
});
assert.deepEqual(manifest.bugs, {
  url: "https://github.com/kpkhxlgy0/unity-debugger-vscode/issues",
});
assert.equal(
  manifest.homepage,
  "https://github.com/kpkhxlgy0/unity-debugger-vscode#readme",
);
const debuggerContribution = manifest.contributes.debuggers.find(
  (entry) => entry.type === "unity-debugger-pure",
);
assert.ok(debuggerContribution);
assert.equal(
  debuggerContribution.configurationSnippets[0].body.name,
  "Attach to Unity Debugger Pure",
);
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
npx vitest run tests/extension/productIdentity.test.ts
npm run test:build
```

Expected: the first command fails because `productIdentity.ts` is absent; the
second fails on the old manifest publisher, package name, and debug type.

- [ ] **Step 3: Create the typed identity module**

Create `extension/src/productIdentity.ts`:

```ts
export const PRODUCT_IDENTITY = {
  publisher: "kpk",
  extensionName: "unity-debugger-pure",
  displayName: "Unity Debugger Pure",
  description:
    "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, " +
    "without C# Dev Kit or Microsoft's Unity extension.",
  debugType: "unity-debugger-pure",
  defaultConfigurationName: "Attach to Unity Debugger Pure",
  commandIds: {
    refreshTargets: "unity-debugger-pure.refreshTargets",
    openLogs: "unity-debugger-pure.openLogs",
    copyDiagnostics: "unity-debugger-pure.copyDiagnostics",
  },
  adapterExecutable: "UnityDebuggerPure.exe",
  diagnosticsDirectoryName: "unity-debugger-pure",
  vsixFileName: "unity-debugger-pure-0.1.0.vsix",
} as const;
```

- [ ] **Step 4: Apply the exact final manifest identity**

Update `package.json` to include:

```json
{
  "name": "unity-debugger-pure",
  "displayName": "Unity Debugger Pure",
  "description": "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, without C# Dev Kit or Microsoft's Unity extension.",
  "publisher": "kpk",
  "version": "0.1.0",
  "repository": {
    "type": "git",
    "url": "https://github.com/kpkhxlgy0/unity-debugger-vscode.git"
  },
  "bugs": {
    "url": "https://github.com/kpkhxlgy0/unity-debugger-vscode/issues"
  },
  "homepage": "https://github.com/kpkhxlgy0/unity-debugger-vscode#readme"
}
```

Replace every manifest activation event, command ID, debugger type, debugger
label, enum value, and snippet with the final values. The sole snippet body is:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Keep version `0.1.0`, `private: true`, `extensionKind: ["workspace"]`,
`os: ["win32"]`, and the existing engine floors.

- [ ] **Step 5: Refresh the npm lockfile**

Run:

```powershell
npm install --package-lock-only --ignore-scripts
```

Verify both root `name` entries in `package-lock.json` equal
`unity-debugger-pure`.

- [ ] **Step 6: Run the focused tests and commit**

Run:

```powershell
npx vitest run tests/extension/productIdentity.test.ts
node --test --test-name-pattern "manifest defines an independent attach-only debugger" tests/build/scaffold.test.mjs
git diff --check
```

Expected: the product-identity and focused manifest contract pass. The full
build suite is deferred until Task 3 migrates the Adapter support URL.

Commit:

```powershell
git add package.json package-lock.json extension/src/productIdentity.ts tests/extension/productIdentity.test.ts tests/build/scaffold.test.mjs
git commit -m "feat: define Unity Debugger Pure identity"
```

---

### Task 2: Migrate the extension host to the final identity

**Files:**

- Modify: `extension/src/adapterLauncher.ts`
- Modify: `extension/src/debugConfigurationProvider.ts`
- Modify: `extension/src/diagnostics.ts`
- Modify: `extension/src/extension.ts`
- Modify: `extension/src/model.ts`
- Modify: `tests/extension/adapterLauncher.test.ts`
- Modify: `tests/extension/debugConfigurationProvider.test.ts`
- Modify: `tests/extension/diagnostics.test.ts`

**Interfaces:**

- Consumes: `PRODUCT_IDENTITY` from Task 1.
- Produces: `UnityAttachConfiguration.type` as
  `typeof PRODUCT_IDENTITY.debugType`; existing exported diagnostics command
  constants remain available under their current TypeScript names.

- [ ] **Step 1: Change extension tests to the final runtime identity**

Update `tests/extension/adapterLauncher.test.ts` to expect:

```ts
"UnityDebuggerPure.exe"
```

Update all debug types and errors in
`tests/extension/debugConfigurationProvider.test.ts`:

```ts
type: "unity-debugger-pure"
```

```ts
"unity-debugger-pure supports only request: attach."
```

Add to `tests/extension/diagnostics.test.ts`:

```ts
import {
  COPY_DIAGNOSTICS_COMMAND,
  OPEN_LOGS_COMMAND,
  REFRESH_TARGETS_COMMAND,
  resolveDiagnosticsDirectory,
} from "../../extension/src/diagnostics.js";

it("uses the final commands and diagnostics directory", () => {
  expect(REFRESH_TARGETS_COMMAND).toBe(
    "unity-debugger-pure.refreshTargets",
  );
  expect(OPEN_LOGS_COMMAND).toBe("unity-debugger-pure.openLogs");
  expect(COPY_DIAGNOSTICS_COMMAND).toBe(
    "unity-debugger-pure.copyDiagnostics",
  );
  expect(resolveDiagnosticsDirectory("C:\\Local")).toBe(
    path.resolve("C:\\Local", "unity-debugger-pure", "logs"),
  );
});
```

Import `node:path` in that test.

- [ ] **Step 2: Run extension tests and verify identity failures**

Run:

```powershell
npm run test:extension
```

Expected: failures show the old debug type, executable, command IDs, and
diagnostics directory.

- [ ] **Step 3: Consume `PRODUCT_IDENTITY` in production modules**

Use these exact imports and assignments:

```ts
import { PRODUCT_IDENTITY } from "./productIdentity.js";
```

In `adapterLauncher.ts`, build the executable path with:

```ts
PRODUCT_IDENTITY.adapterExecutable
```

In `diagnostics.ts`, preserve existing exported names:

```ts
export const REFRESH_TARGETS_COMMAND =
  PRODUCT_IDENTITY.commandIds.refreshTargets;
export const OPEN_LOGS_COMMAND = PRODUCT_IDENTITY.commandIds.openLogs;
export const COPY_DIAGNOSTICS_COMMAND =
  PRODUCT_IDENTITY.commandIds.copyDiagnostics;
```

Build the diagnostics path with:

```ts
return path.resolve(
  localApplicationData,
  PRODUCT_IDENTITY.diagnosticsDirectoryName,
  "logs",
);
```

In `debugConfigurationProvider.ts`, validate and return
`PRODUCT_IDENTITY.debugType`, use
`PRODUCT_IDENTITY.defaultConfigurationName`, and produce this error:

```ts
`${PRODUCT_IDENTITY.debugType} supports only request: attach.`
```

In `extension.ts`, use `PRODUCT_IDENTITY.debugType` for both registrations and
the terminated-session filter.

In `model.ts`, type the field as:

```ts
readonly type: typeof PRODUCT_IDENTITY.debugType;
```

- [ ] **Step 4: Run extension and build-entry tests**

Run:

```powershell
npm run test:extension
node --test tests/build/extension-entry.test.mjs
```

Expected: all extension tests and the CommonJS extension-entry test pass.

- [ ] **Step 5: Commit the extension-host migration**

```powershell
git add extension/src tests/extension
git commit -m "refactor: migrate extension host identity"
```

---

### Task 3: Rename the Adapter, support URL, and diagnostics directory

**Files:**

- Modify:
  `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify:
  `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Modify:
  `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs`
- Modify:
  `tests/adapter/UnityDebugger.Adapter.Tests/Build/AdapterAssemblyTests.cs`
- Modify:
  `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/DiagnosticLogTests.cs`
- Modify: `tests/integration/adapter.integration.test.ts`
- Modify:
  `adapter/src/UnityDebugger.Adapter/packages.lock.json`
- Modify:
  `tests/adapter/UnityDebugger.Adapter.Tests/packages.lock.json`
- Modify:
  `tests/integration/UnityDebugger.TestAdapter/packages.lock.json`

**Interfaces:**

- Produces: `UnityDebuggerPure.exe` with managed assembly name
  `UnityDebuggerPure`; internal
  `DiagnosticLog.ProductDirectoryName == "unity-debugger-pure"`.
- Consumes: final manifest identity from Task 1.

- [ ] **Step 1: Change Adapter tests before production code**

In `AdapterAssemblyTests.cs`, expect:

```csharp
var executable = Path.Combine(
    AppContext.BaseDirectory,
    "UnityDebuggerPure.exe");
Assert.Equal("UnityDebuggerPure", assembly.GetName().Name);
```

In `DiagnosticLogTests.cs`, add:

```csharp
[Fact]
public void Product_directory_uses_final_identity()
{
    Assert.Equal(
        "unity-debugger-pure",
        DiagnosticLog.ProductDirectoryName);
}
```

In `tests/integration/adapter.integration.test.ts`, change both initialize
requests to:

```ts
adapterID: "unity-debugger-pure"
```

- [ ] **Step 2: Run Adapter tests and verify the rename fails**

Run:

```powershell
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: compilation fails because `ProductDirectoryName` is absent or the
assembly-name test cannot find `UnityDebuggerPure.exe`.

- [ ] **Step 3: Apply the Adapter identity**

Set in `UnityDebugger.Adapter.csproj`:

```xml
<AssemblyName>UnityDebuggerPure</AssemblyName>
```

Set in `UnityDebugSession.cs`:

```csharp
private const string SupportPolicyUrl =
    "https://marketplace.visualstudio.com/items?itemName=" +
    "kpk.unity-debugger-pure" +
    "#support-policy";
```

Set in `DiagnosticLog.cs`:

```csharp
internal const string ProductDirectoryName = "unity-debugger-pure";
```

Use `ProductDirectoryName` in the existing `Path.Combine` call. Do not change
retention, redaction, file naming, or logged fields.

- [ ] **Step 4: Refresh and verify NuGet lockfiles**

Run:

```powershell
dotnet restore UnityDebugger.sln --force-evaluate
dotnet restore UnityDebugger.sln --locked-mode
```

Verify the two dependent lockfiles contain the project key
`UnityDebuggerPure`, and none contains `UnityCommunityDebug`.

- [ ] **Step 5: Run Adapter, integration, and support-URL tests**

Run:

```powershell
dotnet build UnityDebugger.sln -c Release --no-restore
dotnet test UnityDebugger.sln -c Release --no-build --no-restore
npm run test:integration
npm run test:build
```

Expected: Adapter and integration suites pass; the support URL now matches
`kpk.unity-debugger-pure`.

- [ ] **Step 6: Commit the Adapter migration**

```powershell
git add adapter/src/UnityDebugger.Adapter tests/adapter tests/integration
git commit -m "refactor: rename Unity debug adapter"
```

---

### Task 4: Migrate packaging, runtime inventory, and active-brand safeguards

**Files:**

- Create: `tests/build/branding.test.mjs`
- Modify: `scripts/stage-adapter.mjs`
- Modify: `scripts/generate-runtime-inventory.mjs`
- Modify: `scripts/verify-vsix.mjs`
- Modify: `tests/package/vsix.test.mjs`
- Modify: `tests/build/scaffold.test.mjs`
- Modify: `third-party/runtime-assemblies.json`
- Modify: `package.json`
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/workflows/release.yml`

**Interfaces:**

- Consumes: `UnityDebuggerPure.exe` from Task 3 and
  `unity-debugger-pure-0.1.0.vsix` from the Task 1 identity.
- Produces: staged Adapter tree, committed runtime inventory, VSIX, and
  `.sha256` file under the final names.

- [ ] **Step 1: Write the failing active-brand guard**

Create `tests/build/branding.test.mjs`:

```js
import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const activeFiles = [
  "package.json",
  "extension/src/adapterLauncher.ts",
  "extension/src/debugConfigurationProvider.ts",
  "extension/src/diagnostics.ts",
  "extension/src/extension.ts",
  "extension/src/model.ts",
  "adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj",
  "adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs",
  "adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs",
  "scripts/stage-adapter.mjs",
  "scripts/generate-runtime-inventory.mjs",
  "scripts/verify-vsix.mjs",
];

test("active product files contain no retired identity", () => {
  const retired = [
    "unity-community",
    "UnityCommunityDebug",
    "unity-debugger-community",
    "Community Debugger for Unity 2022 & Tuanjie",
  ];
  for (const file of activeFiles) {
    const text = fs.readFileSync(file, "utf8");
    for (const token of retired) {
      assert.equal(
        text.includes(token),
        false,
        `${file} contains retired identity ${token}`,
      );
    }
  }
});
```

Historical files under `docs/superpowers` and third-party upstream notices are
intentionally absent from this list.

Update `tests/package/vsix.test.mjs` to invoke:

```js
"dist/unity-debugger-pure-0.1.0.vsix"
```

- [ ] **Step 2: Run the guard and verify packaging identity failures**

Run:

```powershell
node --test tests/build/branding.test.mjs
node --test tests/package/vsix.test.mjs
```

Expected: branding fails on packaging scripts; package test fails because the
final VSIX does not exist.

- [ ] **Step 3: Rename every Adapter artifact reference**

Use `UnityDebuggerPure.exe` in:

- the required release output in `scripts/stage-adapter.mjs`;
- the project-origin entry in `scripts/generate-runtime-inventory.mjs`;
- the required path, lookup key, and AMD64 verification target in
  `scripts/verify-vsix.mjs`.

Update package scripts to:

```json
{
  "verify:vsix": "node scripts/verify-vsix.mjs dist/unity-debugger-pure-0.1.0.vsix",
  "package": "npm run build && npm run stage:adapter && npm run inventory:runtime && vsce package --out dist/unity-debugger-pure-0.1.0.vsix && npm run verify:vsix && npm run test:package"
}
```

Remove `--allow-missing-repository` because Task 1 declares the public
repository.

In `.github/workflows/ci.yml`, name the uploaded artifact:

```yaml
name: unity-debugger-pure-vsix
```

In `.github/workflows/release.yml`, attach:

```powershell
dist/unity-debugger-pure-0.1.0.vsix `
dist/unity-debugger-pure-0.1.0.vsix.sha256 `
```

- [ ] **Step 4: Rebuild and explicitly regenerate the reviewed inventory**

Run:

```powershell
npm run build
npm run stage:adapter
npm run inventory:runtime:write
```

Verify:

```powershell
Get-ChildItem adapter\win32-x64 -Filter 'Unity*Debug*.exe'
Select-String -Path third-party\runtime-assemblies.json -Pattern 'UnityDebuggerPure.exe'
```

Expected: exactly `UnityDebuggerPure.exe` is staged, and the inventory records
its final path and newly built SHA-256.

- [ ] **Step 5: Build and audit the final package**

Run:

```powershell
npm run package
node --test tests/build/branding.test.mjs
```

Expected: `dist\unity-debugger-pure-0.1.0.vsix` and its checksum exist; the
VSIX verifier, package test, runtime inventory, and active-brand guard pass.

- [ ] **Step 6: Prove retired package artifacts are absent**

Run:

```powershell
rg -n "unity-community|UnityCommunityDebug|unity-debugger-community|Community Debugger for Unity 2022 & Tuanjie" package.json extension/src adapter/src scripts .github/workflows tests/extension tests/adapter tests/integration tests/package third-party/runtime-assemblies.json
Get-ChildItem adapter\win32-x64,dist -Recurse -File | Where-Object Name -Match 'UnityCommunityDebug|unity-debugger-vscode-0\.1\.0'
```

Expected: both commands produce no retired active identity or artifact.

- [ ] **Step 7: Commit the packaging migration**

```powershell
git add package.json scripts tests/build/branding.test.mjs tests/build/scaffold.test.mjs tests/package/vsix.test.mjs third-party/runtime-assemblies.json .github/workflows/ci.yml .github/workflows/release.yml
git commit -m "build: package Unity Debugger Pure"
```

---

### Task 5: Update public documentation and the release checklist

**Files:**

- Create: `docs/release-checklist.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `LICENSE`
- Modify: `tests/build/branding.test.mjs`

**Interfaces:**

- Produces: exact installation URLs and a human release gate used by Task 9.
- Consumes: final identity and distribution order from Tasks 1 and 4.

- [ ] **Step 1: Extend the documentation test before editing prose**

Add this test to `tests/build/branding.test.mjs`:

```js
activeFiles.push("README.md", "CHANGELOG.md");

test("README documents every supported distribution channel", () => {
  const readme = fs.readFileSync("README.md", "utf8");
  for (const required of [
    "Unity Debugger Pure",
    "kpk.unity-debugger-pure",
    "Attach to Unity Debugger Pure",
    "https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure",
    "https://open-vsx.org/extension/kpk/unity-debugger-pure",
    "%LOCALAPPDATA%\\unity-debugger-pure\\logs",
    "Cursor",
  ]) {
    assert.ok(readme.includes(required), `README is missing ${required}`);
  }
});
```

- [ ] **Step 2: Run the documentation contract and verify it fails**

Run:

```powershell
node --test tests/build/branding.test.mjs
```

Expected: README assertions fail on the final name, IDs, URLs, and diagnostics
path.

- [ ] **Step 3: Rewrite current public identity and installation guidance**

Update `README.md` to:

- title the project `Unity Debugger Pure`;
- retain the non-affiliation notice;
- describe the owned debug type as `unity-debugger-pure`;
- show the final attach JSON;
- link the Visual Studio Marketplace and Open VSX listings;
- explain that Cursor obtains the extension from Open VSX;
- explain release-VSIX sideloading;
- show the final diagnostics directory and command titles;
- keep the support policy and formal Editor matrix unchanged.

The attach JSON must be:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

The installation links must be:

```text
https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure
https://open-vsx.org/extension/kpk/unity-debugger-pure
https://github.com/kpkhxlgy0/unity-debugger-vscode/releases
```

Change the changelog line to:

```markdown
- Initial Windows x64 release of Unity Debugger Pure.
```

Change the license copyright line to:

```text
Copyright (c) 2026 Unity Debugger Pure contributors
```

- [ ] **Step 4: Write the exact manual release checklist**

Create `docs/release-checklist.md` with these ordered gates:

1. clean `master`, full tests, exact VSIX and checksum;
2. public repository and source metadata verification;
3. GitHub Environments `vscode-marketplace` and `open-vsx`;
4. Visual Studio Marketplace publication;
5. Open VSX `kpk` Option 1 ownership request;
6. Open VSX publication of the same GitHub Release asset;
7. Open VSX and Cursor discovery checks;
8. removal of the old extension ID and MyGame acceptance;
9. rollback rule: never reuse version `0.1.0`; fix and publish a higher patch.

List these non-secret environment values:

```text
VSCE_PUBLISHER=kpk
MARKETPLACE_OWNERSHIP_CONFIRMED=true
OVSX_NAMESPACE=kpk
OPEN_VSX_OWNERSHIP_CONFIRMED=true
```

List `VSCE_PAT` and `OVSX_PAT` only as secret names, with instructions that the
user enters them directly in GitHub and never sends their values through chat.

- [ ] **Step 5: Run documentation and package checks**

Run:

```powershell
node --test tests/build/branding.test.mjs
npm run package
git diff --check
```

Expected: documentation contract passes and the rebuilt VSIX contains the
final README, changelog, and license.

- [ ] **Step 6: Commit documentation**

```powershell
git add README.md CHANGELOG.md LICENSE docs/release-checklist.md tests/build/branding.test.mjs
git commit -m "docs: document Unity Debugger Pure distribution"
```

---

### Task 6: Verify immutable GitHub Release assets

**Files:**

- Create: `scripts/verify-release-artifact.mjs`
- Create: `tests/build/release-artifact.test.mjs`
- Modify: `package.json`

**Interfaces:**

- Produces:
  `verifyReleaseArtifact({ vsixPath, checksumPath, expectedTag }):
  Promise<{ sha256: string; publisher: "kpk"; name:
  "unity-debugger-pure"; version: "0.1.0" }>` and a three-argument CLI.
- Consumes: VSIX layout from `scripts/verify-vsix.mjs`.

- [ ] **Step 1: Write valid and tampered release-asset tests**

Create `tests/build/release-artifact.test.mjs`. Build a temporary VSIX with
`AdmZip` containing:

```json
{
  "publisher": "kpk",
  "name": "unity-debugger-pure",
  "version": "0.1.0"
}
```

Add tests with these exact outcomes:

```js
const verified = await verifyReleaseArtifact({
  vsixPath,
  checksumPath,
  expectedTag: "v0.1.0",
});
assert.equal(verified.publisher, "kpk");
assert.equal(verified.name, "unity-debugger-pure");
assert.equal(verified.version, "0.1.0");
```

```js
await assert.rejects(
  verifyReleaseArtifact({
    vsixPath: tamperedVsixPath,
    checksumPath,
    expectedTag: "v0.1.0",
  }),
  /Release artifact SHA-256 mismatch/,
);
```

```js
await assert.rejects(
  verifyReleaseArtifact({
    vsixPath,
    checksumPath,
    expectedTag: "v0.1.1",
  }),
  /Release tag does not match manifest version/,
);
```

- [ ] **Step 2: Run the test and verify the module is absent**

Run:

```powershell
node --test tests/build/release-artifact.test.mjs
```

Expected: FAIL because `scripts/verify-release-artifact.mjs` does not exist.

- [ ] **Step 3: Implement checksum and manifest verification**

Create `scripts/verify-release-artifact.mjs` with these checks in order:

```js
const expectedFileName = "unity-debugger-pure-0.1.0.vsix";
const expectedPublisher = "kpk";
const expectedName = "unity-debugger-pure";
const expectedVersion = "0.1.0";
```

1. Resolve both supplied paths and require the VSIX basename to equal
   `expectedFileName`.
2. Parse exactly
   `64-lowercase-hex + two spaces + expectedFileName + newline`.
3. Compute SHA-256 over the downloaded VSIX and compare it with the checksum.
4. Read `extension/package.json` from `AdmZip`.
5. Require publisher, name, and version to match the constants.
6. Require `expectedTag === "v" + manifest.version`.
7. Return the verified metadata and print only the filename and SHA-256, never
   environment values.

Expose this CLI:

```powershell
node scripts/verify-release-artifact.mjs dist/unity-debugger-pure-0.1.0.vsix dist/unity-debugger-pure-0.1.0.vsix.sha256 v0.1.0
```

Add:

```json
{
  "verify:release-artifact": "node scripts/verify-release-artifact.mjs dist/unity-debugger-pure-0.1.0.vsix dist/unity-debugger-pure-0.1.0.vsix.sha256 v0.1.0"
}
```

- [ ] **Step 4: Run focused and real-artifact verification**

Run:

```powershell
node --test tests/build/release-artifact.test.mjs
npm run verify:release-artifact
```

Expected: temporary valid/tampered cases behave as specified, and the real
audited VSIX passes.

- [ ] **Step 5: Commit immutable-asset verification**

```powershell
git add scripts/verify-release-artifact.mjs tests/build/release-artifact.test.mjs package.json
git commit -m "build: verify immutable release assets"
```

---

### Task 7: Make both registry workflows consume the same release asset

**Files:**

- Create: `tests/build/release-workflows.test.mjs`
- Create: `.github/workflows/publish-open-vsx.yml`
- Modify: `.github/workflows/publish-marketplace.yml`
- Modify: `package.json`
- Modify: `package-lock.json`

**Interfaces:**

- Consumes: the verifier CLI from Task 6 and GitHub Release assets generated
  by `.github/workflows/release.yml`.
- Produces: separate `vscode-marketplace` and `open-vsx` approved workflows.

- [ ] **Step 1: Write workflow policy tests**

Create `tests/build/release-workflows.test.mjs` to assert:

```js
const marketplace = fs.readFileSync(
  ".github/workflows/publish-marketplace.yml",
  "utf8",
);
const openVsx = fs.readFileSync(
  ".github/workflows/publish-open-vsx.yml",
  "utf8",
);

for (const workflow of [marketplace, openVsx]) {
  assert.match(workflow, /gh release download/);
  assert.match(workflow, /unity-debugger-pure-0\.1\.0\.vsix/);
  assert.match(workflow, /verify-release-artifact\.mjs/);
  assert.doesNotMatch(workflow, /npm run package/);
  assert.doesNotMatch(workflow, /dotnet (?:restore|build|test)/);
}
assert.match(marketplace, /environment: vscode-marketplace/);
assert.match(marketplace, /secrets\.VSCE_PAT/);
assert.match(marketplace, /vars\.VSCE_PUBLISHER/);
assert.match(openVsx, /environment: open-vsx/);
assert.match(openVsx, /secrets\.OVSX_PAT/);
assert.match(openVsx, /vars\.OVSX_NAMESPACE/);
```

Also assert:

```js
const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
assert.equal(manifest.devDependencies["@vscode/vsce"], "3.9.2");
assert.equal(manifest.devDependencies.ovsx, "1.0.2");
```

- [ ] **Step 2: Run the workflow test and verify it fails**

Run:

```powershell
node --test tests/build/release-workflows.test.mjs
```

Expected: FAIL because the Open VSX workflow and pinned `ovsx` dependency are
absent, and the Marketplace workflow still rebuilds.

- [ ] **Step 3: Pin the Open VSX CLI**

Add to `package.json`:

```json
{
  "devDependencies": {
    "ovsx": "1.0.2"
  }
}
```

Run:

```powershell
npm install --package-lock-only --ignore-scripts
```

- [ ] **Step 4: Replace Marketplace rebuilding with release download**

Keep `workflow_dispatch.tag`, set:

```yaml
permissions:
  contents: read

jobs:
  publish:
    runs-on: windows-latest
    environment: vscode-marketplace
```

Checkout the selected tag, set up Node 26.5.0, run `npm ci`, and download:

```powershell
New-Item -ItemType Directory -Force dist | Out-Null
gh release download "${{ inputs.tag }}" `
  --dir dist `
  --pattern "unity-debugger-pure-0.1.0.vsix" `
  --pattern "unity-debugger-pure-0.1.0.vsix.sha256"
node scripts/verify-release-artifact.mjs `
  dist/unity-debugger-pure-0.1.0.vsix `
  dist/unity-debugger-pure-0.1.0.vsix.sha256 `
  "${{ inputs.tag }}"
```

Provide `GH_TOKEN: ${{ github.token }}` only to that download step.

Validate:

```powershell
if ($env:VSCE_PUBLISHER -ne "kpk") {
  throw "VSCE_PUBLISHER must equal kpk"
}
if ($env:OWNERSHIP_CONFIRMED -ne "true") {
  throw "Marketplace publisher ownership is not confirmed"
}
if ([string]::IsNullOrWhiteSpace($env:VSCE_PAT)) {
  throw "VSCE_PAT is required"
}
```

Publish only:

```powershell
npx --no-install vsce publish `
  --packagePath dist/unity-debugger-pure-0.1.0.vsix `
  -p $env:VSCE_PAT
```

Remove every restore, build, test, and package step from this publishing
workflow.

- [ ] **Step 5: Create the separately approved Open VSX workflow**

Create `.github/workflows/publish-open-vsx.yml` with:

```yaml
name: Publish to Open VSX

on:
  workflow_dispatch:
    inputs:
      tag:
        description: Audited release tag, exactly v0.1.0
        required: true
        type: string

permissions:
  contents: read

jobs:
  publish:
    runs-on: windows-latest
    environment: open-vsx
```

Use the same checkout, Node, `npm ci`, `gh release download`, and verifier
steps as the Marketplace workflow. Validate:

```powershell
if ($env:OVSX_NAMESPACE -ne "kpk") {
  throw "OVSX_NAMESPACE must equal kpk"
}
if ($env:OWNERSHIP_CONFIRMED -ne "true") {
  throw "Open VSX namespace ownership is not confirmed"
}
if ([string]::IsNullOrWhiteSpace($env:OVSX_PAT)) {
  throw "OVSX_PAT is required"
}
```

Bind:

```yaml
env:
  OVSX_PAT: ${{ secrets.OVSX_PAT }}
  OVSX_NAMESPACE: ${{ vars.OVSX_NAMESPACE }}
  OWNERSHIP_CONFIRMED: ${{ vars.OPEN_VSX_OWNERSHIP_CONFIRMED }}
```

Publish only:

```powershell
npx --no-install ovsx publish `
  dist/unity-debugger-pure-0.1.0.vsix `
  -p $env:OVSX_PAT
```

- [ ] **Step 6: Run workflow and full build-policy tests**

Run:

```powershell
node --test tests/build/release-workflows.test.mjs
npm run test:build
rg -n "npm run package|dotnet restore|dotnet build|dotnet test" .github/workflows/publish-marketplace.yml .github/workflows/publish-open-vsx.yml
```

Expected: tests pass and `rg` returns no rebuilding command in either
publishing workflow.

- [ ] **Step 7: Commit registry workflows**

```powershell
git add .github/workflows/publish-marketplace.yml .github/workflows/publish-open-vsx.yml tests/build/release-workflows.test.mjs package.json package-lock.json
git commit -m "ci: publish one VSIX to both registries"
```

---

### Task 8: Run the complete local release and MyGame acceptance gate

**Files:**

- Verify: all repository files changed in Tasks 1–7
- Modify outside repository:
  `H:\workspace\Unity\Tuanjie\Projects\MyGame\.vscode\launch.json`

**Interfaces:**

- Consumes: final VSIX and checksum from Task 4, release verifier from Task 6.
- Produces: a clean, locally accepted `master` release candidate.

- [ ] **Step 1: Run a clean dependency and full-test pass**

Run:

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm run verify:third-party
npm test
npm run package
npm run verify:release-artifact
git diff --check
```

Expected: build tests, extension tests, Adapter tests, integration tests,
package tests, third-party verification, runtime inventory, VSIX audit, and
release-asset verification all pass.

- [ ] **Step 2: Record the staged and release artifact hashes**

Run:

```powershell
Get-FileHash adapter\win32-x64\UnityDebuggerPure.exe -Algorithm SHA256
Get-FileHash dist\unity-debugger-pure-0.1.0.vsix -Algorithm SHA256
Get-Content dist\unity-debugger-pure-0.1.0.vsix.sha256
```

Expected: the VSIX digest equals the checksum file and the Adapter digest
equals its committed runtime-inventory entry.

- [ ] **Step 3: Stop for the user's MyGame readiness**

Ask the user to:

1. open only `H:\workspace\Unity\Tuanjie\Projects\MyGame` in Tuanjie;
2. open VS Code from MyGame;
3. stop any active debug session.

If either application is not open, remind the user and wait. Do not launch a
different Editor, project, VS Code profile, or test workspace.

- [ ] **Step 4: Migrate only the MyGame debugger entry**

Change only:

```json
{
  "name": "Attach Tuanjie / Unity 2022 Editor",
  "type": "unity-community",
  "request": "attach"
}
```

to:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Leave the existing `vstuc` configuration and every MyGame source file
unchanged.

- [ ] **Step 5: Replace the old installed extension**

Have the user, in the VS Code window opened from MyGame:

1. uninstall `unity-debugger-community.unity-debugger-vscode`;
2. install
   `H:\workspace\Unity\unity-debugger-vscode\dist\unity-debugger-pure-0.1.0.vsix`;
3. reload that same window.

If browser or GUI inspection is needed, ask for Computer Use confirmation
immediately before the interaction and close it as soon as the interaction is
done.

- [ ] **Step 6: Hash-verify the installed Adapter**

Run:

```powershell
$installedRoot = Join-Path $env:USERPROFILE '.vscode\extensions\kpk.unity-debugger-pure-0.1.0'
Get-FileHash (Join-Path $installedRoot 'adapter\win32-x64\UnityDebuggerPure.exe') -Algorithm SHA256
Get-FileHash 'H:\workspace\Unity\unity-debugger-vscode\adapter\win32-x64\UnityDebuggerPure.exe' -Algorithm SHA256
```

Expected: both hashes are identical. Stop before attach if the installed path
is absent or the hashes differ.

- [ ] **Step 7: Prove breakpoint reachability before interpreting results**

Use the existing MyGame DevTools path and verify current reachability through
active-scene/prefab attachment, bootstrap/call-site references, or observed
runtime execution. The previously exercised candidate is:

```text
H:\workspace\Unity\Tuanjie\Projects\MyGame\Assets\Scripts\GamePlay\Runtime\DevTools\DevGmTestPanel.cs:112
```

Do not use line 112 unless the current run supplies concrete reachability
evidence. Do not edit the method to manufacture reachability.

- [ ] **Step 8: Execute the real Editor regression**

With configuration `Attach to Unity Debugger Pure`, verify:

1. local Tuanjie attach;
2. a verified reachable breakpoint becomes solid and hits;
3. a conditional breakpoint hits only under its condition;
4. Step In, Step Over, and Step Out;
5. Pause shows Unity-internal and MyGame frames without opening an invalid
   source;
6. Hover displays `_isVisible`;
7. Locals, Watch, and Debug Console evaluation;
8. script reimport/Domain Reload reports restored breakpoint binding;
9. Continue and disconnect complete without Editor or VS Code failure.

If the Editor restarts or any application exits unexpectedly, stop acceptance,
collect only sanitized diagnostics, and diagnose before retrying.

- [ ] **Step 9: Verify repository cleanliness**

Run:

```powershell
git status --short --branch
git log --oneline -10
```

Expected: repository `master` is clean. The MyGame launch change is external
and must not appear in this repository's commit.

---

### Task 9: Execute the separately confirmed public release sequence

**Files and external state:**

- Create externally:
  `https://github.com/kpkhxlgy0/unity-debugger-vscode`
- Configure externally: GitHub Environments `vscode-marketplace` and
  `open-vsx`
- Publish externally: Visual Studio Marketplace and Open VSX
- Verify externally: Cursor extension discovery

**Interfaces:**

- Consumes: the clean and accepted `master` release candidate from Task 8.
- Produces: public source, GitHub Release, two registry listings, and a Cursor
  discovery result.

- [ ] **Step 1: Obtain explicit confirmation for public repository creation and push**

After confirmation, run:

```powershell
gh repo create kpkhxlgy0/unity-debugger-vscode --public --source . --remote origin
git push -u origin master
```

Verify:

```powershell
git remote -v
gh repo view kpkhxlgy0/unity-debugger-vscode --json nameWithOwner,url,visibility,defaultBranchRef
```

Expected: visibility is `PUBLIC`, URL is the reviewed repository, and default
branch is `master`.

- [ ] **Step 2: Have the user configure publishing environments**

The user enters secrets directly in GitHub Settings; the agent never receives
their values.

Configure `vscode-marketplace`:

```text
Secret: VSCE_PAT
Variable: VSCE_PUBLISHER=kpk
Variable: MARKETPLACE_OWNERSHIP_CONFIRMED=true
```

Configure `open-vsx`:

```text
Secret: OVSX_PAT
Variable: OVSX_NAMESPACE=kpk
Variable: OPEN_VSX_OWNERSHIP_CONFIRMED=false
```

Require a reviewer on each environment so each publication is independently
approved.

- [ ] **Step 3: Obtain explicit confirmation for tag push and GitHub Release**

After confirmation, run:

```powershell
git tag -a v0.1.0 -m "Unity Debugger Pure 0.1.0"
git push origin v0.1.0
```

Wait for `.github/workflows/release.yml` to succeed. Verify the GitHub Release
contains exactly:

```text
unity-debugger-pure-0.1.0.vsix
unity-debugger-pure-0.1.0.vsix.sha256
```

Download them to a new temporary directory and run the Task 6 verifier with
tag `v0.1.0`.

- [ ] **Step 4: Obtain explicit confirmation for Visual Studio Marketplace publication**

In GitHub Actions, manually run `Publish to VS Code Marketplace` with:

```text
tag=v0.1.0
```

Approve only the `vscode-marketplace` environment. Wait for success and verify:

```text
https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure
```

Confirm the displayed publisher, name, version, repository link, support-policy
anchor, and installation instructions.

- [ ] **Step 5: Submit the Open VSX Option 1 ownership request**

Create the ownership issue with:

```text
Title: Claiming namespace kpk
Namespace: kpk
```

Check namespace availability, account age, and Option 1. The nested validation
boxes are static Markdown and cannot be clicked; put this exact evidence in the
text area:

```markdown
I own the Visual Studio Marketplace publisher `kpk`.

The published extension is:
https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure

The public source repository is owned by this GitHub account:
https://github.com/kpkhxlgy0/unity-debugger-vscode

The requested Open VSX namespace is `kpk`.
```

Submit only after the Marketplace listing and repository links are public.
Obtain explicit confirmation immediately before creating the external GitHub
issue, then wait until the ownership issue is granted and closed.

- [ ] **Step 6: Enable and confirm Open VSX publication**

After ownership is granted, have the user change:

```text
OPEN_VSX_OWNERSHIP_CONFIRMED=true
```

Obtain explicit confirmation, then manually run `Publish to Open VSX` with:

```text
tag=v0.1.0
```

Approve only the `open-vsx` environment. Verify:

```text
https://open-vsx.org/extension/kpk/unity-debugger-pure
```

Confirm version `0.1.0`, identity `kpk.unity-debugger-pure`, repository link,
license, README, and active namespace ownership.

- [ ] **Step 7: Verify Cursor discovery without creating another package**

Have the user search Cursor Extensions for:

```text
@id:kpk.unity-debugger-pure
```

If it appears, verify that installation resolves to version `0.1.0` from the
Open VSX listing.

If the Open VSX listing is active but Cursor does not discover it, create a
Cursor Community Forum sync/review request with:

```markdown
Unity Debugger Pure is active on Open VSX but is not discoverable in Cursor.

Extension ID: `kpk.unity-debugger-pure`
Open VSX: https://open-vsx.org/extension/kpk/unity-debugger-pure
VS Code Marketplace: https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure
Repository: https://github.com/kpkhxlgy0/unity-debugger-vscode

Please review its Cursor marketplace sync and security status.
```

Obtain explicit confirmation before posting this external forum request, or
have the user submit it directly.

Do not upload a different VSIX to Cursor. A Cursor verification badge may
require a custom-domain website; treat that as a separate future decision and
do not invent or register a domain during this release.

- [ ] **Step 8: Record the completed public release**

Obtain explicit confirmation before editing the public GitHub Release notes.
Add the final listing URLs and release verification date, then confirm the
repository tag, GitHub Release checksum, Marketplace version, and Open VSX
version all identify `0.1.0`.

Do not republish `0.1.0` after any registry accepts it. If a defect is found,
fix it on `master`, increment to `0.1.1`, rebuild one new audited VSIX, and use
the same gated sequence.
