# Community Debugger for Unity 2022 & Tuanjie Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and publicly package a standalone VS Code debug adapter that attaches to managed C# code in a local Tuanjie 2022.3.62t11 Editor without C# Dev Kit or Microsoft's Unity extension.

**Architecture:** A TypeScript VS Code extension resolves the open Unity/Tuanjie projects, discovers local Editors, lets the user select exactly one target, applies the version policy, and launches a Windows x64 C# Adapter over DAP stdin/stdout. The .NET Framework 4.8 Adapter validates the loopback-only attach target and translates DAP requests to an audited, source-built Mono Soft Debugger backend. Pure interfaces isolate Editor discovery, DAP session state, Mono transport, breakpoints, source mapping, and diagnostics so each can be tested without a live Editor.

**Tech Stack:** Node.js 26.5.0; TypeScript 7.0.2; esbuild 0.28.1; Vitest 4.1.10; VS Code API 1.95; `@vscode/vsce` 3.9.2; .NET SDK 10.0.302; .NET Framework 4.8/x64; xUnit 2.9.3; Newtonsoft.Json 13.0.4; audited MIT sources from `vscode-mono-debug`, `debugger-libs`, and `NRefactory`.

## Global Constraints

- Work on branch `master`; it must remain buildable after every task.
- Repository name and extension manifest name are `unity-debugger-vscode`.
- Display name is `Community Debugger for Unity 2022 & Tuanjie`.
- Debug type is `unity-community`; the only supported request is `attach`.
- Initial extension version is `0.1.0`; repository license is MIT.
- Formally support Windows 10/11 x64 and local Tuanjie `2022.3.62t11`.
- Build the production Adapter for .NET Framework 4.8 with `PlatformTarget=x64`.
- Do not depend on C# Dev Kit, `ms-dotnettools.csharp`, Microsoft's Unity extension, ReSharper APIs, or Microsoft proprietary debugger packages.
- ReSharper for VS Code must be able to remain enabled; this extension owns only `unity-community`.
- Permit other Unity 2022.3 and Unity 6 versions with one
  `compatible-unverified` warning; reject malformed versions, other Editor
  version lines, and incompatible handshake data.
- Never expose or accept a configurable remote host in version 0.1.0. The Adapter must reject every non-loopback address even if a user edits `launch.json`.
- Do not execute workspace commands, builds, Unity menu items, or user scripts during attach.
- Do not collect telemetry. Normal diagnostics must exclude source text, expression/variable values, and full user-home paths.
- Do not copy opaque upstream binaries into source control or a VSIX. Build the Adapter and vendored debugger libraries from recorded source revisions.
- Pin npm through `package-lock.json`, NuGet through `packages.lock.json`, the .NET SDK through `global.json`, and every vendored source through `third-party/sources.json`.
- Use the real installed `Tuanjie 2022.3.62t11` Editor as the mandatory pre-release acceptance baseline.
- Keep remote Players, mobile devices, IL2CPP, WebGL, Mini Game DebugProxy, .NET 8 scripting, native C++, consoles, macOS, Linux, and multi-target sessions out of version 0.1.0.

## Planned File Map

### Build and repository

- `.editorconfig` — shared text and C#/TypeScript formatting.
- `.gitignore` — generated Node, .NET, VSIX, fixture `Library`, and diagnostic output.
- `.node-version` — Node `26.5.0`.
- `global.json` — .NET SDK `10.0.302`, latest-patch roll forward.
- `Directory.Build.props` — deterministic .NET build, nullable, language version, x64 defaults.
- `Directory.Packages.props` — central, exact NuGet versions.
- `package.json` / `package-lock.json` — extension manifest, scripts, and pinned npm graph.
- `tsconfig.json`, `vitest.config.ts`, `esbuild.mjs` — TypeScript build/test/bundle configuration.
- `UnityDebugger.sln` — production Adapter, adapter tests, and test-only Adapter.

### TypeScript extension

- `extension/src/model.ts` — shared `EditorCandidate`, version, and attach configuration types.
- `extension/src/projectVersion.ts` — parse `ProjectSettings/ProjectVersion.txt`.
- `extension/src/versionPolicy.ts` — supported/unverified/unsupported classification.
- `extension/src/playerAdvertisement.ts` — parse the Unity/Tuanjie multicast record format.
- `extension/src/editorInstance.ts` — parse and validate `Library/EditorInstance.json`.
- `extension/src/editorDiscovery.ts` — collect local advertisements, derive/probe fallback port, return candidates.
- `extension/src/debugConfigurationProvider.ts` — resolve an attach configuration and Quick Pick.
- `extension/src/adapterLauncher.ts` — resolve the packaged Adapter and provide a `DebugAdapterExecutable`.
- `extension/src/diagnostics.ts` — sanitized extension-side messages and diagnostic path.
- `extension/src/extension.ts` — VS Code registrations only.

### C# Adapter

- `adapter/src/UnityDebugger.Adapter/Program.cs` — production stdin/stdout entry point.
- `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs` — DAP request/event coordinator.
- `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs` — strict attach argument parsing.
- `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs` — testable debugger boundary.
- `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs` — lifecycle and object conversion.
- `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs` — only class that directly wraps `Mono.Debugging.Soft`.
- `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs` — logical/bound breakpoint state and reload rebinding.
- `adapter/src/UnityDebugger.Adapter/Source/SourceMapper.cs` — runtime/workspace path mapping.
- `adapter/src/UnityDebugger.Adapter/State/HandleTable.cs` — stable frame/variable references per stop.
- `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs` — redacted file logging.
- `adapter/src/UnityDebugger.Adapter/Diagnostics/PathRedactor.cs` — replace user/workspace paths.

### Audited upstream source

- `adapter/vendor/vscode-mono-debug/` — two MIT DAP framing/model files from commit `d233b366b0c67ae4d61488f7e974e2a5b9da2e3b`, plus project-owned trace/format helpers.
- `adapter/vendor/debugger-libs/` — `Mono.Debugger.Soft`, `Mono.Debugging`, and `Mono.Debugging.Soft` from commit `cd005e941d18c92ddf0c50084c59ddaae6bf4c5d`.
- `adapter/vendor/nrefactory/` — the two NRefactory projects required by `Mono.Debugging` from commit `0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5`.
- `third-party/sources.json` — source URL, exact revision, copied paths, license, notice, and local modifications.
- `third-party/licenses/` and `THIRD_PARTY_NOTICES.md` — retained notices.
- `scripts/import-upstream.ps1` — reproducible source refresh/import; never used during normal build.
- `scripts/verify-third-party.mjs` — fail CI for missing provenance or notices.

### Tests and release

- `tests/extension/*.test.ts` — pure TypeScript unit tests.
- `tests/adapter/UnityDebugger.Adapter.Tests/` — xUnit unit tests with fake facades/backends.
- `tests/integration/UnityDebugger.TestAdapter/` — test-only Adapter hosting `ScenarioDebuggerBackend`.
- `tests/integration/dapClient.ts` and `adapter.integration.test.ts` — process-level DAP tests.
- `tests/fixtures/TuanjieProject/` — minimal real-Editor fixture without generated `Library`.
- `docs/testing/tuanjie-2022.3.62t11.md` — exact manual acceptance procedure.
- `scripts/verify-real-editor-environment.ps1` — inspect version, process, project, and EditorInstance state.
- `scripts/verify-vsix.mjs` — allowlist packaged files and runtime assemblies.
- `.github/workflows/ci.yml` — Windows build, tests, audit, and VSIX artifact.
- `.github/workflows/release.yml` — tagged GitHub release candidate and checksum.
- `.github/workflows/publish-marketplace.yml` — manually dispatched, environment-approved Marketplace publication.

---

### Task 1: Reproducible extension and .NET build scaffold

**Files:**
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `.node-version`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `vitest.config.ts`
- Create: `esbuild.mjs`
- Create: `extension/src/extension.ts`
- Create: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Create: `adapter/src/UnityDebugger.Adapter/Program.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj`
- Create: `tests/build/scaffold.test.mjs`
- Create: `UnityDebugger.sln`
- Generate: `package-lock.json`
- Generate: `adapter/src/UnityDebugger.Adapter/packages.lock.json`
- Generate: `tests/adapter/UnityDebugger.Adapter.Tests/packages.lock.json`

**Interfaces:**
- Consumes: no product interfaces.
- Produces: npm scripts `build`, `test`, `test:extension`, `test:adapter`, `package`; `UnityDebugger.sln`; production entry point `UnityDebugger.Adapter.Program.Main(string[] args)`.

- [x] **Step 1: Write the scaffold contract test**

Create `tests/build/scaffold.test.mjs`:

```js
import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

test("manifest defines an independent attach-only debugger", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  assert.equal(manifest.name, "unity-debugger-vscode");
  assert.equal(manifest.displayName, "Community Debugger for Unity 2022 & Tuanjie");
  assert.equal(manifest.version, "0.1.0");
  assert.equal(manifest.extensionDependencies, undefined);
  assert.deepEqual(manifest.extensionKind, ["workspace"]);
  assert.deepEqual(manifest.os, ["win32"]);

  const debuggerContribution = manifest.contributes.debuggers.find(
    (entry) => entry.type === "unity-community",
  );
  assert.ok(debuggerContribution);
  assert.ok(debuggerContribution.configurationAttributes.attach);
  assert.equal(debuggerContribution.configurationAttributes.launch, undefined);
});

test("adapter project targets net48 x64", () => {
  const project = fs.readFileSync(
    "adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj",
    "utf8",
  );
  assert.match(project, /<TargetFramework>net48<\/TargetFramework>/);
  assert.match(project, /<PlatformTarget>x64<\/PlatformTarget>/);
});
```

- [x] **Step 2: Run the contract test and verify the empty repository fails**

Run:

```powershell
node --test tests/build/scaffold.test.mjs
```

Expected: FAIL because `package.json` and the Adapter project do not exist.

- [x] **Step 3: Create the root toolchain and extension manifest**

Use these exact manifest constraints in `package.json`; keep `publisher` as the explicit development value until the reviewed release-preparation commit verifies Marketplace ownership:

```json
{
  "name": "unity-debugger-vscode",
  "displayName": "Community Debugger for Unity 2022 & Tuanjie",
  "publisher": "unity-debugger-community",
  "version": "0.1.0",
  "license": "MIT",
  "engines": { "vscode": "^1.95.0", "node": ">=26.5.0" },
  "extensionKind": ["workspace"],
  "os": ["win32"],
  "categories": ["Debuggers"],
  "main": "./dist/extension.js",
  "activationEvents": ["onDebugResolve:unity-community"],
  "contributes": {
    "breakpoints": [{ "language": "csharp" }],
    "debuggers": [{
      "type": "unity-community",
      "label": "Community Debugger for Unity 2022 & Tuanjie",
      "languages": ["csharp"],
      "configurationAttributes": {
        "attach": {
          "required": ["name", "type", "request"],
          "properties": {
            "name": { "type": "string" },
            "type": { "enum": ["unity-community"] },
            "request": { "enum": ["attach"] }
          }
        }
      },
      "configurationSnippets": [{
        "label": "Attach Tuanjie / Unity 2022 Editor",
        "body": {
          "name": "Attach Tuanjie / Unity 2022 Editor",
          "type": "unity-community",
          "request": "attach"
        }
      }]
    }]
  },
  "scripts": {
    "build:extension": "node esbuild.mjs",
    "build:adapter": "dotnet restore UnityDebugger.sln --locked-mode && dotnet build UnityDebugger.sln -c Release --no-restore",
    "build": "npm run build:extension && npm run build:adapter",
    "test:build": "node --test tests/build/*.test.mjs",
    "test:extension": "vitest run tests/extension",
    "test:adapter": "dotnet test UnityDebugger.sln -c Release --no-restore",
    "test": "npm run test:build && npm run test:extension && npm run test:adapter",
    "package": "npm run build && vsce package --out dist/unity-debugger-vscode-0.1.0.vsix"
  },
  "devDependencies": {
    "@types/node": "26.1.1",
    "@types/vscode": "1.95.0",
    "@vscode/vsce": "3.9.2",
    "esbuild": "0.28.1",
    "typescript": "7.0.2",
    "vitest": "4.1.10"
  }
}
```

Set `.node-version` to `26.5.0`. Set `global.json` to:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Set `Directory.Build.props` to:

```xml
<Project>
  <PropertyGroup>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <PathMap>$(MSBuildProjectDirectory)=/_/src/$(MSBuildProjectName)</PathMap>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

Set central package versions in `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

- [x] **Step 4: Create the minimal extension and Adapter projects**

`extension/src/extension.ts` must contain only a buildable activation shell:

```ts
import type * as vscode from "vscode";

export function activate(_context: vscode.ExtensionContext): void {}
export function deactivate(): void {}
```

`adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <AssemblyName>UnityCommunityDebug</AssemblyName>
    <RootNamespace>UnityDebugger.Adapter</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" PrivateAssets="all" />
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```

`adapter/src/UnityDebugger.Adapter/Program.cs`:

```csharp
namespace UnityDebugger.Adapter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return 0;
        }
    }
}
```

Create the xUnit project targeting `net48`, referencing the Adapter project and the centrally pinned test packages. Add both projects to `UnityDebugger.sln`.

- [x] **Step 5: Install, restore, build, and generate lockfiles**

Run:

```powershell
npm install
dotnet restore UnityDebugger.sln --force-evaluate
npm run build
```

Expected: npm creates `package-lock.json`; .NET creates both `packages.lock.json` files; TypeScript bundles to `dist/extension.js`; `UnityCommunityDebug.exe` builds for `net48`.

- [x] **Step 6: Verify the scaffold contract and clean working build**

Run:

```powershell
node --test tests/build/scaffold.test.mjs
npm test
git diff --check
```

Expected: all tests PASS and `git diff --check` emits no output.

- [x] **Step 7: Commit the scaffold**

```powershell
git add -- .editorconfig .gitignore .node-version global.json Directory.Build.props Directory.Packages.props package.json package-lock.json tsconfig.json vitest.config.ts esbuild.mjs extension adapter tests/build UnityDebugger.sln
git commit -m "build: scaffold extension and adapter"
```

### Task 2: Audited, reproducible upstream source import

**Files:**
- Create: `scripts/import-upstream.ps1`
- Create: `scripts/verify-third-party.mjs`
- Create: `third-party/sources.json`
- Create: `third-party/licenses/vscode-mono-debug-MIT.txt`
- Create: `third-party/licenses/debugger-libs-MIT.txt`
- Create: `third-party/licenses/nrefactory-MIT.txt`
- Create: `THIRD_PARTY_NOTICES.md`
- Create: `tests/build/third-party.test.mjs`
- Create: `adapter/vendor/Directory.Build.props`
- Create: `adapter/vendor/Directory.Packages.props`
- Create: `adapter/vendor/vscode-mono-debug/ProtocolTrace.cs`
- Create: `adapter/vendor/vscode-mono-debug/ProtocolUtilities.cs`
- Import: `adapter/vendor/vscode-mono-debug/`
- Import: `adapter/vendor/debugger-libs/`
- Import: `adapter/vendor/nrefactory/`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify: `UnityDebugger.sln`
- Modify: `package.json`

**Interfaces:**
- Consumes: Task 1 build and lockfile infrastructure.
- Produces: source-built `VSCodeDebug`, `Mono.Debugger.Soft`, `Mono.Debugging`, and `Mono.Debugging.Soft` assemblies; `npm run verify:third-party`; exact provenance manifest schema `{name, repository, revision, license, notice, paths, modifications}`.

- [x] **Step 1: Write the provenance contract test**

Create `tests/build/third-party.test.mjs`:

```js
import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const expected = new Map([
  ["vscode-mono-debug", "d233b366b0c67ae4d61488f7e974e2a5b9da2e3b"],
  ["debugger-libs", "cd005e941d18c92ddf0c50084c59ddaae6bf4c5d"],
  ["nrefactory", "0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5"],
]);

test("every vendored source has exact provenance and a retained notice", () => {
  const manifest = JSON.parse(fs.readFileSync("third-party/sources.json", "utf8"));
  assert.equal(manifest.sources.length, expected.size);

  for (const source of manifest.sources) {
    assert.equal(source.revision, expected.get(source.name));
    assert.equal(source.license, "MIT");
    assert.ok(fs.existsSync(source.notice), source.notice);
    assert.ok(source.paths.length > 0);
    source.paths.forEach((path) => assert.ok(fs.existsSync(path), path));
    assert.ok(source.modifications.length > 0);
  }
});
```

- [x] **Step 2: Run the provenance test and verify it fails**

Run:

```powershell
node --test tests/build/third-party.test.mjs
```

Expected: FAIL because `third-party/sources.json` does not exist.

- [x] **Step 3: Implement the pinned source importer**

`scripts/import-upstream.ps1` must use three immutable revisions, reject non-empty destination directories, clone into a GUID-named directory under `[System.IO.Path]::GetTempPath()`, copy only these paths, and validate the temporary delete target before cleanup:

```powershell
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ("unity-debugger-import-" + [guid]::NewGuid().ToString('N'))

$sources = @(
    @{
        Name = 'vscode-mono-debug'
        Repository = 'https://github.com/Unity-Technologies/vscode-mono-debug.git'
        Revision = 'd233b366b0c67ae4d61488f7e974e2a5b9da2e3b'
        Paths = @('src/DebugSession.cs', 'src/Protocol.cs', 'LICENSE.txt', 'ThirdPartyNotices.txt')
    },
    @{
        Name = 'debugger-libs'
        Repository = 'https://github.com/Unity-Technologies/debugger-libs.git'
        Revision = 'cd005e941d18c92ddf0c50084c59ddaae6bf4c5d'
        Paths = @('Mono.Debugger.Soft', 'Mono.Debugging', 'Mono.Debugging.Soft', 'Mono.Debugging.settings', 'LICENSE')
    },
    @{
        Name = 'nrefactory'
        Repository = 'https://github.com/icsharpcode/NRefactory.git'
        Revision = '0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5'
        Paths = @('ICSharpCode.NRefactory', 'ICSharpCode.NRefactory.CSharp', 'ICSharpCode.NRefactory.snk', 'doc/license.txt')
    }
)

New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    foreach ($source in $sources) {
        $checkout = Join-Path $tempRoot $source.Name
        git clone --no-checkout $source.Repository $checkout
        git -C $checkout fetch --no-tags origin $source.Revision
        git -C $checkout checkout FETCH_HEAD -- $source.Paths

        $destination = Join-Path $repoRoot ("adapter/vendor/" + $source.Name)
        if ((Test-Path -LiteralPath $destination) -and
            (Get-ChildItem -LiteralPath $destination -Force | Select-Object -First 1)) {
            throw "Destination is not empty: $destination"
        }
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        foreach ($path in $source.Paths) {
            $item = Join-Path $checkout $path
            Copy-Item -LiteralPath $item -Destination $destination -Recurse -Force
        }
    }
}
finally {
    $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTemp.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedTemp"
    }
    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
}
```

The import script copies `vscode-mono-debug/src/*.cs` directly into its vendor
root. Copy the retained license texts into `third-party/licenses/` without
editing their wording.

- [x] **Step 4: Record provenance and local modifications**

Create `third-party/sources.json` with the exact values tested above. The modifications arrays must be explicit:

```json
{
  "sources": [
    {
      "name": "vscode-mono-debug",
      "repository": "https://github.com/Unity-Technologies/vscode-mono-debug",
      "revision": "d233b366b0c67ae4d61488f7e974e2a5b9da2e3b",
      "license": "MIT",
      "notice": "third-party/licenses/vscode-mono-debug-MIT.txt",
      "paths": ["adapter/vendor/vscode-mono-debug"],
      "modifications": ["Only Protocol.cs and DebugSession.cs are imported and built as a source project; payload/path/stack logging is removed, command-name-only ProtocolTrace callbacks and a project-owned error formatter are added, and duplicate outer DAP response emission is removed."]
    },
    {
      "name": "debugger-libs",
      "repository": "https://github.com/Unity-Technologies/debugger-libs",
      "revision": "cd005e941d18c92ddf0c50084c59ddaae6bf4c5d",
      "license": "MIT",
      "notice": "third-party/licenses/debugger-libs-MIT.txt",
      "paths": ["adapter/vendor/debugger-libs"],
      "modifications": ["Built from source as dependencies of the net48 x64 Adapter; NRefactory project-reference paths are adjusted for the recorded vendor layout, reference-assembly restore is added for hosted CI, and a payload-free AssemblyUnloaded lifecycle event is exposed for Domain Reload coordination. No upstream binaries are shipped."]
    },
    {
      "name": "nrefactory",
      "repository": "https://github.com/icsharpcode/NRefactory",
      "revision": "0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5",
      "license": "MIT",
      "notice": "third-party/licenses/nrefactory-MIT.txt",
      "paths": ["adapter/vendor/nrefactory"],
      "modifications": ["Only the core and CSharp projects required by Mono.Debugging are imported; reference-assembly restore is added for hosted CI."]
    }
  ]
}
```

`THIRD_PARTY_NOTICES.md` must name these three sources plus restored transitive packages and link each retained license file.

- [x] **Step 5: Wire source projects into the Adapter build**

Create `adapter/vendor/vscode-mono-debug/ProtocolTrace.cs`:

```csharp
namespace VSCodeDebug
{
    public static class ProtocolTrace
    {
        public static System.Action<string> Sink { get; set; } = _ => { };
        public static void CommandReceived(string command) => Sink(command);
    }
}
```

Create `ProtocolUtilities.cs` with only the error-format expansion needed by
`DebugSession.SendErrorResponse`. It may replace `{_name}` tokens from
project-owned anonymous objects using reflection; unknown/null values become a
fixed `<unavailable>` marker. Do not import upstream `Utilities.cs`,
`Handles.cs`, or `MonoDebug.cs`: they add unused process/path helpers and a
console/file logger. Patch `DebugSession.cs` to call
`ProtocolUtilities.ExpandVariables`.

Patch the imported `Protocol.cs` to remove `using UnityDebug`, replace the two
unconditional `Log.Write` calls with
`ProtocolTrace.CommandReceived(request.command)`, and change conditional
request/response/event tracing to log only command/event names. Remove the
outer `SendMessage(response)` after `DispatchRequest`; concrete DAP handlers
already send exactly one success or error response. Patch `DebugSession.cs` so
its catch response contains the request name and exception type only, never
`e.StackTrace` or a raw exception message, and replace the malformed-client-path
log with a fixed string that does not include the path. The vendored protocol
must never serialize `request.arguments`, a response body, or an event body.
Retain every upstream copyright header.

Create a small SDK-style `adapter/vendor/vscode-mono-debug/VSCodeDebug.csproj`
targeting `net48` and compiling the two imported `.cs` files plus
`ProtocolTrace.cs` and `ProtocolUtilities.cs`. Because vendor central package
management is disabled, pin its package references directly:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies"
                    Version="1.0.3"
                    PrivateAssets="all" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
</ItemGroup>
```

Add project references from the production Adapter to:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\vendor\vscode-mono-debug\VSCodeDebug.csproj" />
  <ProjectReference Include="..\..\vendor\debugger-libs\Mono.Debugger.Soft\Mono.Debugger.Soft.csproj" />
  <ProjectReference Include="..\..\vendor\debugger-libs\Mono.Debugging\Mono.Debugging.csproj" />
  <ProjectReference Include="..\..\vendor\debugger-libs\Mono.Debugging.Soft\Mono.Debugging.Soft.csproj" />
</ItemGroup>
```

Create `adapter/vendor/Directory.Build.props` so old audited source builds
deterministically without inheriting project-owned nullable/warning policies:

```xml
<Project>
  <PropertyGroup>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <PathMap>$(MSBuildProjectDirectory)=/_/vendor/$(MSBuildProjectName)</PathMap>
    <Nullable>disable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

Create `adapter/vendor/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

This keeps the exact package versions declared by the audited upstream
projects, while project-owned packages remain centrally pinned. Add
`Microsoft.NETFramework.ReferenceAssemblies` version `1.0.3` with
`PrivateAssets="all"` to each of the five imported legacy project files so
Windows CI does not depend on machine-installed targeting packs. Generate and
commit a `packages.lock.json` for every vendored project that restores a
package.

Update relative NRefactory references so they resolve to `adapter/vendor/nrefactory`, without changing source namespaces or stripping copyright headers. Add every source project used by the Adapter to `UnityDebugger.sln`.

Apply one lifecycle-only patch to the imported debugger libraries:

- add `public event EventHandler AssemblyUnloaded` and a protected
  `OnAssemblyUnloaded()` raiser to
  `Mono.Debugging.Client.DebuggerSession`;
- call the raiser from
  `SoftDebuggerSession.HandleAssemblyUnloadEvents` without passing an assembly
  path, name, or object.

This payload-free hook is required because the audited revision exposes
`AssemblyLoaded` but not Domain/assembly unload. Retain copyright headers and
record the exact changed files in `third-party/sources.json`.

- [x] **Step 6: Implement and run third-party verification**

`scripts/verify-third-party.mjs` must perform the same checks as the test, reject a moving branch name in `revision`, and reject a missing `THIRD_PARTY_NOTICES.md` entry. Add:

```json
"verify:third-party": "node scripts/verify-third-party.mjs"
```

The verifier must also scan vendored `Protocol.cs` and fail if it contains
`SerializeObject(request.arguments)`, `SerializeObject(e.body)`, or
`SerializeObject(message,`. It must scan `DebugSession.cs` and fail on
`e.StackTrace` or the raw-path log format `path not well formed: '{0}'` so
future upstream refreshes cannot reintroduce DAP payload/path logging.
It must verify the two `AssemblyUnloaded` hook markers are present and that the
event type is plain `EventHandler`, preventing assembly paths from entering the
reload signal.
For the VSCodeDebug vendor directory, allowlist exactly `Protocol.cs`,
`DebugSession.cs`, `ProtocolTrace.cs`, and `ProtocolUtilities.cs`; fail if the
unused upstream process, logger, or handle helpers are imported.

Run:

```powershell
npm run verify:third-party
dotnet restore UnityDebugger.sln --force-evaluate
dotnet build UnityDebugger.sln -c Release --no-restore
node --test tests/build/third-party.test.mjs
```

Expected: provenance checks PASS and every debugger dependency builds from source.

- [x] **Step 7: Commit the audited source baseline**

```powershell
git add -- adapter/vendor adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj UnityDebugger.sln scripts/import-upstream.ps1 scripts/verify-third-party.mjs third-party THIRD_PARTY_NOTICES.md tests/build/third-party.test.mjs package.json package-lock.json
git commit -m "build: import audited debugger sources"
```

### Task 3: Project version parser and compatibility policy

**Files:**
- Create: `extension/src/model.ts`
- Create: `extension/src/projectVersion.ts`
- Create: `extension/src/versionPolicy.ts`
- Create: `tests/extension/projectVersion.test.ts`
- Create: `tests/extension/versionPolicy.test.ts`

**Interfaces:**
- Consumes: Node file reads supplied by callers; no VS Code API.
- Produces: `parseProjectVersion(contents: string): string`; `readProjectVersion(workspaceRoot: string): Promise<string>`; `classifyVersion(projectVersion: string): VersionDecision`; types `SupportLevel`, `VersionDecision`, and `EditorCandidate`.

- [x] **Step 1: Write failing version tests**

`tests/extension/projectVersion.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { parseProjectVersion } from "../../extension/src/projectVersion.js";

describe("parseProjectVersion", () => {
  it("reads the exact Tuanjie baseline", () => {
    expect(parseProjectVersion("m_EditorVersion: 2022.3.62t11\r\n")).toBe(
      "2022.3.62t11",
    );
  });

  it("rejects a missing editor version", () => {
    expect(() => parseProjectVersion("m_EditorVersionWithRevision: x"))
      .toThrow("ProjectVersion.txt does not contain m_EditorVersion");
  });
});
```

`tests/extension/versionPolicy.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { classifyVersion } from "../../extension/src/versionPolicy.js";

describe("classifyVersion", () => {
  it("formally supports only the baseline", () => {
    expect(classifyVersion("2022.3.62t11")).toEqual({
      level: "supported",
      version: "2022.3.62t11",
      warning: undefined,
    });
  });

  it.each(["2022.3.61f1", "6000.0.50f1", "6000.1.4f1"])(
    "allows valid but unverified version %s",
    (version) => expect(classifyVersion(version).level).toBe("compatible-unverified"),
  );

  it.each(["2021.3.45f1", "2023.2.20f1"])(
    "rejects version outside the 0.1.0 compatibility policy: %s",
    (version) => expect(classifyVersion(version).level).toBe("unsupported"),
  );

  it("rejects malformed version metadata", () => {
    expect(classifyVersion("latest").level).toBe("unsupported");
  });
});
```

- [x] **Step 2: Run the tests and verify missing modules fail**

Run:

```powershell
npm run test:extension -- projectVersion versionPolicy
```

Expected: FAIL because the parser and policy modules do not exist.

- [x] **Step 3: Implement exact version types and parsing**

`extension/src/model.ts`:

```ts
export type SupportLevel =
  | "supported"
  | "compatible-unverified"
  | "unsupported";

export interface VersionDecision {
  readonly level: SupportLevel;
  readonly version: string;
  readonly warning: string | undefined;
}

export interface EditorCandidate {
  readonly processId: number;
  readonly projectName: string;
  readonly workspaceRoot: string;
  readonly host: "127.0.0.1";
  readonly port: number;
  readonly projectVersion: string;
  readonly source: "advertisement" | "derived-port";
}
```

Implement the parser and policy:

```ts
const VERSION_PATTERN = /^(?:\d{4}\.\d+\.\d+[abfpxt]\d+|\d{4}\.\d+\.\d+)$/;
const SUPPORTED_VERSION = "2022.3.62t11";

export function parseProjectVersion(contents: string): string {
  const match = /^m_EditorVersion:\s*(\S+)\s*$/m.exec(contents);
  if (!match) {
    throw new Error("ProjectVersion.txt does not contain m_EditorVersion");
  }
  return match[1];
}

export function classifyVersion(projectVersion: string): VersionDecision {
  if (!VERSION_PATTERN.test(projectVersion)) {
    return {
      level: "unsupported",
      version: projectVersion,
      warning: `Malformed or unsupported Editor version: ${projectVersion}`,
    };
  }
  if (projectVersion === SUPPORTED_VERSION) {
    return { level: "supported", version: projectVersion, warning: undefined };
  }
  if (
    !projectVersion.startsWith("2022.3.") &&
    !projectVersion.startsWith("6000.")
  ) {
    return {
      level: "unsupported",
      version: projectVersion,
      warning: `Editor ${projectVersion} is outside the version 0.1.0 compatibility policy.`,
    };
  }
  return {
    level: "compatible-unverified",
    version: projectVersion,
    warning: `Editor ${projectVersion} is unverified; the supported baseline is ${SUPPORTED_VERSION}.`,
  };
}
```

`readProjectVersion` must read exactly
`<workspaceRoot>/ProjectSettings/ProjectVersion.txt` as UTF-8 and delegate to
`parseProjectVersion`.

- [x] **Step 4: Run the focused and full extension tests**

Run:

```powershell
npm run test:extension -- projectVersion versionPolicy
npm run test:extension
```

Expected: all tests PASS.

- [x] **Step 5: Commit version policy**

```powershell
git add -- extension/src/model.ts extension/src/projectVersion.ts extension/src/versionPolicy.ts tests/extension/projectVersion.test.ts tests/extension/versionPolicy.test.ts
git commit -m "feat: add editor version policy"
```

### Task 4: Loopback-only local Editor discovery

**Files:**
- Create: `extension/src/playerAdvertisement.ts`
- Create: `extension/src/editorInstance.ts`
- Create: `extension/src/editorDiscovery.ts`
- Create: `tests/extension/playerAdvertisement.test.ts`
- Create: `tests/extension/editorInstance.test.ts`
- Create: `tests/extension/editorDiscovery.test.ts`

**Interfaces:**
- Consumes: `EditorCandidate`, `readProjectVersion`; injected filesystem/process/socket dependencies.
- Produces: `parsePlayerAdvertisement(record: string): PlayerAdvertisement`; `parseEditorInstance(json: string): EditorInstance`; `defaultEditorPort(processId: number): number`; class `EditorDiscovery.discover(workspaceRoots: readonly string[], timeoutMs?: number): Promise<readonly EditorCandidate[]>`; `EditorDiscovery.dispose(): void`.

- [x] **Step 1: Write failing packet, instance, and discovery tests**

Use this representative packet from the MIT `PlayerConnection` key/value format:

```ts
const packet =
  "[IP] 192.168.1.20 [Port] 55000 [Flags] 3 [Guid] 1234 " +
  "[EditorId] 9876 [Version] 1048832 [Id] WindowsEditor(Tuanjie) " +
  "[Debug] 1 [DebuggerPort] 56234 [ProjectName] FixtureProject";
```

Assert:

```ts
expect(parsePlayerAdvertisement(packet)).toMatchObject({
  ip: "192.168.1.20",
  debuggerPort: 56234,
  projectName: "FixtureProject",
  allowDebugging: true,
});

expect(parseEditorInstance('{"process_id": 1234}')).toEqual({ processId: 1234 });
expect(defaultEditorPort(1234)).toBe(56234);
```

For `EditorDiscovery`, inject:

```ts
const dependencies = {
  readFile: async () => '{"process_id":1234}',
  readProjectVersion: async () => "2022.3.62t11",
  isProcessAlive: async (pid: number) => pid === 1234,
  collectAdvertisements: async () => [parsePlayerAdvertisement(packet)],
  probeLoopbackPort: async (port: number) => port === 56234,
};
```

Call discovery with `["H:\\fixture"]` and verify one candidate is returned with
`workspaceRoot: "H:\\fixture"` and host exactly `127.0.0.1`. Also test that a
dead PID, `debug=0`, invalid port, malformed JSON, or failed loopback probe
produces no candidate. A two-root test must return two candidates with distinct
workspace roots, and duplicate roots must be de-duplicated.

- [x] **Step 2: Run the discovery tests and verify missing modules fail**

Run:

```powershell
npm run test:extension -- playerAdvertisement editorInstance editorDiscovery
```

Expected: FAIL because the discovery modules do not exist.

- [x] **Step 3: Implement strict advertisement and EditorInstance parsing**

Parse bracketed fields without `eval` or permissive object spreading:

```ts
const REQUIRED = ["ip", "port", "guid", "editorid", "version", "id", "debug"];

export function parsePlayerAdvertisement(record: string): PlayerAdvertisement {
  const fields = new Map<string, string>();
  for (const match of record.trimEnd().matchAll(/\[([^\]]+)\]\s*([^\[]*)/g)) {
    fields.set(match[1].trim().toLowerCase(), match[2].trim().replace(/\0+$/, ""));
  }
  for (const key of REQUIRED) {
    if (!fields.has(key)) throw new Error(`Advertisement is missing ${key}`);
  }
  const debuggerPort = Number(fields.get("debuggerport") ?? 0);
  if (!Number.isInteger(debuggerPort) || debuggerPort < 0 || debuggerPort > 65535) {
    throw new Error("Advertisement has an invalid debugger port");
  }
  return {
    ip: fields.get("ip")!,
    debuggerPort,
    projectName: fields.get("projectname") ?? "",
    id: fields.get("id")!,
    allowDebugging: fields.get("debug") === "1",
  };
}
```

`parseEditorInstance` must accept only an object with integer `process_id > 0`.
`defaultEditorPort` is:

```ts
export function defaultEditorPort(processId: number): number {
  if (!Number.isInteger(processId) || processId <= 0) {
    throw new Error("Editor process ID must be a positive integer");
  }
  return 56000 + (processId % 1000);
}
```

- [x] **Step 4: Implement disposable multicast collection and local discovery**

The production collector must:

- use UDP4 with `reuseAddr: true`;
- bind ports `54997`, `34997`, `57997`, and `58997`;
- join multicast group `225.0.0.222`;
- retain only successfully parsed records;
- close every socket in `finally` and in `dispose()`;
- use the advertised debugger port only after a matching project name and successful loopback probe;
- otherwise probe `56000 + processId % 1000`;
- always return `host: "127.0.0.1"` and never the advertised LAN IP.

Implement a private `discoverOne(workspaceRoot, advertisements)` helper with
this exact order:

```ts
const normalizedRoot = path.resolve(workspaceRoot);
const projectName = path.basename(normalizedRoot);
const editorInstancePath = path.join(
  normalizedRoot,
  "Library",
  "EditorInstance.json",
);
const instance = parseEditorInstance(
  await dependencies.readFile(editorInstancePath, "utf8"),
);
if (!(await dependencies.isProcessAlive(instance.processId))) return [];

const projectVersion = await dependencies.readProjectVersion(normalizedRoot);
const advertisements = await dependencies.collectAdvertisements(timeoutMs);
const candidatePorts = unique([
  ...advertisements
    .filter((item) => item.allowDebugging && item.projectName === projectName)
    .map((item) => item.debuggerPort)
    .filter((port) => port > 0),
  defaultEditorPort(instance.processId),
]);

for (const port of candidatePorts) {
  if (await dependencies.probeLoopbackPort(port)) {
    return [{
      processId: instance.processId,
      projectName,
      workspaceRoot: normalizedRoot,
      host: "127.0.0.1",
      port,
      projectVersion,
      source: advertisements.some((item) => item.debuggerPort === port)
        ? "advertisement"
        : "derived-port",
    }];
  }
}
return [];
```

`discover(workspaceRoots, timeoutMs)` must normalize and de-duplicate roots,
collect advertisements once, call `discoverOne` for each root, flatten the
results, and de-duplicate candidates by process ID and port. This allows a
multi-root VS Code window to offer every running local Editor while preserving
the same loopback-only boundary.

- [x] **Step 5: Verify discovery behavior and socket cleanup**

Run:

```powershell
npm run test:extension -- playerAdvertisement editorInstance editorDiscovery
npm run test:extension
```

Expected: malformed packets are ignored, remote IPs are never returned, fallback port calculation works, and all tests PASS.

- [x] **Step 6: Commit local discovery**

```powershell
git add -- extension/src/playerAdvertisement.ts extension/src/editorInstance.ts extension/src/editorDiscovery.ts tests/extension/playerAdvertisement.test.ts tests/extension/editorInstance.test.ts tests/extension/editorDiscovery.test.ts
git commit -m "feat: discover local unity editors"
```

### Task 5: Attach configuration, target selection, and Adapter descriptor

**Files:**
- Create: `extension/src/debugConfigurationProvider.ts`
- Create: `extension/src/adapterLauncher.ts`
- Create: `extension/src/diagnostics.ts`
- Create: `tests/extension/debugConfigurationProvider.test.ts`
- Create: `tests/extension/adapterLauncher.test.ts`
- Modify: `extension/src/model.ts`
- Modify: `extension/src/extension.ts`
- Modify: `package.json`

**Interfaces:**
- Consumes: `EditorDiscovery.discover`, `classifyVersion`, `EditorCandidate`.
- Produces: `UnityAttachConfiguration`; `DebugConfigurationProvider.resolveDebugConfiguration(folder, configuration)`; `AdapterLauncher.createDescriptor(extensionPath)`; commands `unity-community.refreshTargets`, `unity-community.openLogs`, and `unity-community.copyDiagnostics`.

- [x] **Step 1: Write failing configuration and launcher tests**

Extend `model.ts` in the test imports with this required shape:

```ts
export interface UnityAttachConfiguration {
  readonly name: string;
  readonly type: "unity-community";
  readonly request: "attach";
  readonly __processId: number;
  readonly __host: "127.0.0.1";
  readonly __port: number;
  readonly __workspaceRoot: string;
  readonly __projectVersion: string;
}
```

Test a single target:

```ts
const resolved = await provider.resolveDebugConfiguration(
  { uri: { fsPath: "H:\\fixture" } },
  { name: "Attach", type: "unity-community", request: "attach" },
);

expect(resolved).toMatchObject({
  type: "unity-community",
  request: "attach",
  __processId: 1234,
  __host: "127.0.0.1",
  __port: 56234,
  __projectVersion: "2022.3.62t11",
});
```

Add cases for:

- no workspace folder;
- request other than `attach`;
- no discovered Editor shows a `Refresh` action;
- accepting `Refresh` performs exactly one additional discovery attempt;
- a second empty result stops without recursion;
- two candidates invoke the injected selector exactly once;
- selector cancellation returns `undefined`;
- `compatible-unverified` resolves successfully and preserves the version for
  the Adapter's one Debug Console warning;
- `unsupported` returns `undefined` and reports an error.

For `AdapterLauncher`, assert the descriptor points to:

```text
<extensionPath>/adapter/win32-x64/UnityCommunityDebug.exe
```

and fails with an actionable error when the executable is absent.

- [x] **Step 2: Run focused tests and verify missing implementations fail**

Run:

```powershell
npm run test:extension -- debugConfigurationProvider adapterLauncher
```

Expected: FAIL because both modules and `UnityAttachConfiguration` are absent.

- [x] **Step 3: Implement pure configuration resolution**

Keep VS Code UI behind an injected boundary so Vitest does not need an Extension Host:

```ts
export interface ConfigurationUi {
  selectCandidate(
    candidates: readonly EditorCandidate[],
  ): Promise<EditorCandidate | undefined>;
  showNoEditor(message: string): Promise<"refresh" | undefined>;
  showError(message: string): Promise<void>;
}

export class DebugConfigurationProvider {
  public constructor(
    private readonly discovery: EditorDiscovery,
    private readonly ui: ConfigurationUi,
    private readonly listWorkspaceRoots: (
      currentFolder: string,
    ) => readonly string[],
  ) {}

  public async resolveDebugConfiguration(
    folder: { readonly uri: { readonly fsPath: string } } | undefined,
    configuration: Readonly<Record<string, unknown>>,
  ): Promise<UnityAttachConfiguration | undefined> {
    if (!folder) {
      await this.ui.showError("Open a Unity or Tuanjie project folder before attaching.");
      return undefined;
    }
    if (
      configuration.type !== "unity-community" ||
      configuration.request !== "attach"
    ) {
      await this.ui.showError("unity-community supports only request: attach.");
      return undefined;
    }

    let candidates = await this.discovery.discover(
      this.listWorkspaceRoots(folder.uri.fsPath),
    );
    if (candidates.length === 0) {
      const action = await this.ui.showNoEditor(
        "No local debug-enabled Tuanjie or Unity Editor was found. Switch Code Optimization to Debug, then refresh.",
      );
      if (action !== "refresh") return undefined;
      candidates = await this.discovery.discover(
        this.listWorkspaceRoots(folder.uri.fsPath),
      );
      if (candidates.length === 0) {
        await this.ui.showError(
          "No local debug-enabled Editor was found after refresh.",
        );
        return undefined;
      }
    }
    const candidate = candidates.length === 1
      ? candidates[0]
      : await this.ui.selectCandidate(candidates);
    if (!candidate) return undefined;

    const decision = classifyVersion(candidate.projectVersion);
    if (decision.level === "unsupported") {
      await this.ui.showError(decision.warning!);
      return undefined;
    }
    return {
      name: String(configuration.name ?? "Attach Tuanjie / Unity 2022 Editor"),
      type: "unity-community",
      request: "attach",
      __processId: candidate.processId,
      __host: "127.0.0.1",
      __port: candidate.port,
      __workspaceRoot: candidate.workspaceRoot,
      __projectVersion: candidate.projectVersion,
    };
  }
}
```

The production `listWorkspaceRoots` implementation must put the current folder
first, append every `vscode.workspace.workspaceFolders` path, and de-duplicate
case-insensitively on Windows. Do not copy unknown user fields into the
resolved object. Implement `showNoEditor` with a VS Code message action labelled
`Refresh`; do not retry more than once in a single configuration resolution.

- [x] **Step 4: Implement descriptor resolution and extension registration**

`AdapterLauncher` must validate `process.platform === "win32"` and
`process.arch === "x64"`, verify the executable exists, and return a descriptor
model:

```ts
export interface AdapterExecutableDescriptor {
  readonly command: string;
  readonly args: readonly string[];
  readonly options: { readonly cwd: string };
}

export class AdapterLauncher {
  public createDescriptor(extensionPath: string): AdapterExecutableDescriptor {
    const command = path.join(
      extensionPath,
      "adapter",
      "win32-x64",
      "UnityCommunityDebug.exe",
    );
    if (!this.fileExists(command)) {
      throw new Error(
        "The packaged Unity debug adapter is missing. Reinstall the extension.",
      );
    }
    return { command, args: [], options: { cwd: path.dirname(command) } };
  }
}
```

In `extension.ts`, adapt that model to
`new vscode.DebugAdapterExecutable(command, [...args], options)` and register:

```ts
vscode.debug.registerDebugConfigurationProvider(
  "unity-community",
  provider,
);
vscode.debug.registerDebugAdapterDescriptorFactory(
  "unity-community",
  descriptorFactory,
);
```

The Quick Pick label must include project name, PID, version, and port. Register
refresh/open-log/copy-diagnostic commands without activating on C# files or
calling ReSharper.

- [x] **Step 5: Verify extension behavior**

Run:

```powershell
npm run test:extension -- debugConfigurationProvider adapterLauncher
npm run build:extension
npm run test:extension
```

Expected: all tests PASS and `dist/extension.js` builds without a runtime dependency on C# Dev Kit.

- [x] **Step 6: Commit configuration and launcher**

```powershell
git add -- extension/src extension/src/model.ts tests/extension/debugConfigurationProvider.test.ts tests/extension/adapterLauncher.test.ts package.json package-lock.json
git commit -m "feat: resolve unity attach sessions"
```

### Task 6: DAP entry point, strict attach arguments, and lifecycle boundary

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/UnavailableDebuggerBackend.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/UnityDebugSessionLifecycleTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj`

**Interfaces:**
- Consumes: vendored `VSCodeDebug.DebugSession`; Newtonsoft.Json.
- Produces: `AttachTarget`; `IDebuggerBackend`; complete backend model names used by Tasks 7–11; `UnityDebugSession` supporting DAP `initialize`, `attach`, and `disconnect`, plus explicit attach-only responses for every other abstract DAP method.

- [x] **Step 1: Write strict attach argument tests**

`AttachArgumentsTests.cs` must cover:

```csharp
[Fact]
public void Parse_accepts_loopback_target()
{
    var json = JObject.Parse(@"{
      '__processId': 1234,
      '__host': '127.0.0.1',
      '__port': 56234,
      '__workspaceRoot': 'H:\\fixture',
      '__projectVersion': '2022.3.62t11'
    }");

    var target = AttachArguments.Parse(json);

    Assert.Equal(1234, target.ProcessId);
    Assert.Equal(IPAddress.Loopback, target.Address);
    Assert.Equal(56234, target.Port);
}

[Theory]
[InlineData("192.168.1.10")]
[InlineData("8.8.8.8")]
[InlineData("0.0.0.0")]
public void Parse_rejects_non_loopback_hosts(string host)
{
    var json = ValidArguments();
    json["__host"] = host;
    var error = Assert.Throws<AttachArgumentException>(
        () => AttachArguments.Parse(json));
    Assert.Contains("loopback", error.Message, StringComparison.OrdinalIgnoreCase);
}
```

Also reject PID `0`, ports `0`/`65536`, empty workspace root, and a malformed
project version. Accept other `2022.3.*` and `6000.*` versions as unverified,
and reject otherwise valid `2021.*`/`2023.*` versions as outside the 0.1.0
compatibility policy.

- [x] **Step 2: Define the backend boundary once**

Create these exact members in `IDebuggerBackend`; later tasks implement them
without renaming:

```csharp
internal interface IDebuggerBackend : IDisposable
{
    event EventHandler<BackendStoppedEventArgs> Stopped;
    event EventHandler Continued;
    event EventHandler<BackendThreadEventArgs> ThreadChanged;
    event EventHandler<BackendBreakpointChangedEventArgs> BreakpointChanged;
    event EventHandler ReloadStarted;
    event EventHandler ReloadCompleted;
    event EventHandler Terminated;

    bool IsAttached { get; }
    void Attach(AttachTarget target);
    void Disconnect();
    IReadOnlyList<BackendThread> GetThreads();
    IReadOnlyList<BackendStackFrame> GetStackTrace(long threadId, int startFrame, int levels);
    IReadOnlyList<BackendScope> GetScopes(long frameId);
    IReadOnlyList<BackendVariable> GetVariables(long variablesReference);
    BackendEvaluationResult Evaluate(long frameId, string expression);
    BackendBoundBreakpoint BindBreakpoint(LogicalBreakpoint breakpoint);
    void RemoveBreakpoint(long backendBreakpointId);
    void Continue(long threadId);
    void Pause(long threadId);
    void StepIn(long threadId);
    void StepOver(long threadId);
    void StepOut(long threadId);
    void ConfigureExceptions(ExceptionBreakMode mode);
}
```

Define concrete immutable classes in `BackendModels.cs` for `AttachTarget`,
`BackendThread`, `BackendStackFrame`, `BackendScope`, `BackendVariable`,
`BackendEvaluationResult`, `BackendBoundBreakpoint`, `LogicalBreakpoint`,
`BackendStoppedEventArgs`, `BackendThreadEventArgs`,
`BackendBreakpointChangedEventArgs`, enum `BackendStopReason`, and enum
`ExceptionBreakMode`. Use constructors and get-only properties so
`net48` needs no `IsExternalInit`.

Create `FakeDebuggerBackend` as the shared test implementation. It must expose
mutable test setup lists for threads/frames/scopes/variables, call counters for
every control method, `BindAsPending`, the last exception mode, and explicit
`RaiseStopped`, `RaiseReloadStarted`, `RaiseReloadCompleted`, and
`RaiseBreakpointChanged`, and `RaiseTerminated` helpers. Every interface method
must either return configured data or record one call; no test may rely on a
product backend or network.

Add:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="UnityDebugger.Adapter.Tests" />
  <InternalsVisibleTo Include="UnityDebugger.TestAdapter" />
</ItemGroup>
```

to the production SDK project so the unit and process-level test assemblies can
exercise internal boundaries without making them public API.

- [x] **Step 3: Run tests and verify missing types fail**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "AttachArguments|UnityDebugSessionLifecycle"
```

Expected: FAIL because the types and session do not exist.

- [x] **Step 4: Implement strict parsing and a lifecycle-only backend**

`AttachArguments.Parse(JObject)` must:

```csharp
var address = IPAddress.Parse(RequiredString(args, "__host"));
if (!IPAddress.IsLoopback(address))
    throw new AttachArgumentException("Only loopback Editor targets are allowed.");
```

It must require every internal property, normalize the workspace path with
`Path.GetFullPath`, and validate both the grammar and allowed version families
with the same policy used by TypeScript.

`UnavailableDebuggerBackend` implements the full interface but throws
`DebuggerBackendException("Mono debugger backend is not initialized.")` from
`Attach`; all inspection/control methods throw `InvalidOperationException`;
`Disconnect` and `Dispose` are idempotent.

- [x] **Step 5: Implement initialize/attach/disconnect DAP routing**

`UnityDebugSession` must accept `Func<IDebuggerBackend>` and report only the
capabilities implemented at this point:

```csharp
SendResponse(response, new Capabilities
{
    supportsConfigurationDoneRequest = false,
    supportsFunctionBreakpoints = false,
    supportsConditionalBreakpoints = false,
    supportsEvaluateForHovers = false,
    supportsExceptionOptions = false,
    supportsSetVariable = false
});
SendEvent(new InitializedEvent());
```

`Attach` parses arguments, creates one backend, subscribes to events, calls
`Attach`, and maps `AttachArgumentException` and `DebuggerBackendException` to
actionable DAP errors. `Disconnect` unsubscribes, disconnects, disposes once,
sends the response, and sends one terminated event.

After a successful attach to any valid version other than `2022.3.62t11`, send
exactly one DAP `output` event in the Debug Console:

```text
Editor <version> is unverified; the supported baseline is 2022.3.62t11. Support policy: https://marketplace.visualstudio.com/items?itemName=unity-debugger-community.unity-debugger-vscode#support-policy
```

Do not show a second extension-host notification for the same classification.
Lifecycle tests must assert zero warnings for the baseline and exactly one for
an unverified version.

The vendored `DebugSession` is abstract. Implement every required override in
this task so the Adapter compiles before later feature tasks fill in their
behavior. `Launch` must return an actionable `This debugger supports attach
only.` error. `SetFunctionBreakpoints`, `SetVariable`, and `Source` must send
one successful empty/unsupported response matching the advertised false
capabilities. The remaining breakpoint, inspection, and execution handlers
must send exactly one deterministic `Not implemented yet.` error until Tasks
8–10 replace them. Add lifecycle tests that dispatch each temporary handler
and assert exactly one DAP response per request.

Production `Program.Main` must use binary stdin/stdout without writing ordinary
text to stdout:

```csharp
private static int Main(string[] args)
{
    try
    {
        var session = new UnityDebugSession(() => new UnavailableDebuggerBackend());
        session.Start(Console.OpenStandardInput(), Console.OpenStandardOutput())
            .GetAwaiter().GetResult();
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.GetType().Name);
        return 1;
    }
}
```

- [x] **Step 6: Verify lifecycle and stdout discipline**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "AttachArguments|UnityDebugSessionLifecycle"
dotnet restore UnityDebugger.sln --locked-mode
dotnet build UnityDebugger.sln -c Release --no-restore
```

Expected: parsing/lifecycle tests PASS; the built process writes no startup text to DAP stdout.

- [x] **Step 7: Commit DAP lifecycle**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests
git commit -m "feat: add debug adapter lifecycle"
```

### Task 7: Mono Soft Debugger attach and disconnect backend

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/UnitySoftDebuggerSession.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/FakeSoftDebuggerSessionFacade.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/MonoDebuggerBackendLifecycleTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/MonoDebuggerLoggerTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs`

**Interfaces:**
- Consumes: `AttachTarget`, `IDebuggerBackend`, project-owned `UnitySoftDebuggerSession`, vendored `SoftDebuggerConnectArgs`, `SoftDebuggerStartInfo`, and `DebuggerSessionOptions`.
- Produces: production `MonoDebuggerBackend`; `ISoftDebuggerSessionFacade` used by Tasks 8–11.

- [x] **Step 1: Write failing backend lifecycle tests**

The fake facade records call order. Test:

```csharp
[Fact]
public void Attach_uses_loopback_port_and_expected_retry_policy()
{
    var facade = new FakeSoftDebuggerSessionFacade();
    using var backend = new MonoDebuggerBackend(() => facade);

    backend.Attach(Target(port: 56234));

    Assert.True(backend.IsAttached);
    Assert.Equal(IPAddress.Loopback, facade.ConnectAddress);
    Assert.Equal(56234, facade.ConnectPort);
    Assert.Equal(10, facade.MaxConnectionAttempts);
    Assert.Equal(500, facade.ConnectionAttemptIntervalMilliseconds);
}

[Fact]
public void Disconnect_detaches_and_disposes_exactly_once()
{
    var facade = new FakeSoftDebuggerSessionFacade();
    using var backend = new MonoDebuggerBackend(() => facade);
    backend.Attach(Target());
    backend.Disconnect();
    backend.Disconnect();

    Assert.Equal(1, facade.DetachCount);
    Assert.Equal(1, facade.DisposeCount);
}
```

Also assert attach cannot run twice and a target-exit event raises
`IDebuggerBackend.Terminated` once. Configure the fake facade to throw
`VMMismatchException` from `ConnectAsync` and assert the backend returns the
actionable error `Editor uses an incompatible Mono Soft Debugger protocol.`
without exposing a stack trace. Configure it to throw a connection exception
and assert the stable error names only `127.0.0.1:<port>` and recommends
checking that the Editor process is alive, Code Optimization is set to Debug,
and the local firewall is not blocking the port.

`MonoDebuggerLoggerTests` must call every `ICustomLogger` method with seeded
secret messages, arguments, and exception text. Assert the injected sink
receives only a fixed event name and exception type, `GetNewDebuggerLogFilename`
returns `null`, and nothing is written to `Console.Out` or `Console.Error`.

- [x] **Step 2: Run lifecycle tests and verify missing backend fails**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "MonoDebuggerBackendLifecycle|MonoDebuggerLogger"
```

Expected: FAIL because the facade and backend do not exist.

- [x] **Step 3: Define the Soft Debugger facade**

The initial facade boundary is:

```csharp
internal interface ISoftDebuggerSessionFacade : IDisposable
{
    event EventHandler TargetReady;
    event EventHandler TargetExited;
    event EventHandler<BackendStoppedEventArgs> TargetStopped;
    event EventHandler<BackendThreadEventArgs> ThreadChanged;
    event EventHandler AssemblyUnloaded;
    event EventHandler AssemblyLoaded;

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
}
```

Add inspection, breakpoint, stepping, and exception members only in their
respective tasks, keeping this initial lifecycle small.

- [x] **Step 4: Wrap the vendored Unity Soft Debugger**

Create the project-owned Unity-specific session without importing the
unlicensed `MonoDevelop.Debugger.Soft.Unity` submodule:

```csharp
internal sealed class UnitySoftDebuggerSession : SoftDebuggerSession
{
    protected override void OnExit()
    {
        Detach();
    }
}
```

`SoftDebuggerSessionFacade.ConnectAsync` must create:

```csharp
var connectArgs = new SoftDebuggerConnectArgs(string.Empty, address, port)
{
    MaxConnectionAttempts = maxConnectionAttempts,
    TimeBetweenConnectionAttempts = connectionAttemptIntervalMilliseconds
};
var evaluationOptions = EvaluationOptions.DefaultOptions.Clone();
evaluationOptions.AllowTargetInvoke = false;
evaluationOptions.AllowMethodEvaluation = false;
evaluationOptions.AllowToStringCalls = false;
var options = new DebuggerSessionOptions
{
    EvaluationOptions = evaluationOptions
};
session.Run(new SoftDebuggerStartInfo(connectArgs), options);
```

Forward `TargetReady`, `TargetExited`, stopped, thread, and the payload-free
assembly loaded/unloaded lifecycle events without exposing Mono classes across
the interface. Before `Run`, call `session.SetOutputOptions(new OutputOptions())`
so upstream module/thread/exception text containing paths or values is not
produced; user-visible stops and errors come through typed events instead.
`Detach` must
continue a suspended session before detaching, dispose the session adaptor, and
remain idempotent. `ConnectAsync` must complete only on `TargetReady`; configure
the session's `ExceptionHandler` to complete the same
`TaskCompletionSource` with the original exception, and honor cancellation.
Unsubscribe all temporary completion handlers in `finally`. This prevents the
DAP attach response from claiming success while the background Mono connection
is still retrying.

Implement `MonoDebuggerLogger : ICustomLogger` as the mandatory process-wide
bridge for `DebuggerLoggingService.CustomLogger`. It may forward only a fixed
event name and `exception.GetType().Name`; discard upstream messages, format
arguments, and stack traces, and return `null` for its log filename. Install it
before constructing any `SoftDebuggerSession` and clear it in `Program.Main`'s
`finally`. This is required from the first Mono-backed build because the
upstream fallback writes exceptions to `Console.Out`, which is the DAP channel.
Task 11 connects the bridge to the structured diagnostic log.

- [x] **Step 5: Implement `MonoDebuggerBackend` lifecycle**

Use constants:

```csharp
private const int MaxConnectionAttempts = 10;
private const int ConnectionAttemptIntervalMilliseconds = 500;
```

Validate `IPAddress.IsLoopback(target.Address)` again in the backend. Create the
facade once per attach. Forward all lifecycle events. Block the synchronous
backend boundary on `ConnectAsync(...).GetAwaiter().GetResult()` and set
`IsAttached` only after `TargetReady`; clear it before detach/dispose so event
callbacks cannot re-enter a live state. Catch `VMMismatchException`, retain
only its protocol-version fields when available, and throw a
`DebuggerBackendException` with the stable actionable message asserted above.
Map timeout/socket failures to the tested loopback-only connection message;
never include an exception message or stack trace from the transport.

Replace `UnavailableDebuggerBackend` in `Program.Main`:

```csharp
var session = new UnityDebugSession(
    () => new MonoDebuggerBackend(() => new SoftDebuggerSessionFacade()));
```

- [x] **Step 6: Verify source-built Mono lifecycle**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "MonoDebuggerBackendLifecycle|MonoDebuggerLogger"
dotnet restore UnityDebugger.sln --locked-mode
dotnet build UnityDebugger.sln -c Release --no-restore
npm run verify:third-party
```

Expected: tests PASS; production Adapter links only restored packages and
source-built vendored projects.

- [x] **Step 7: Commit the Mono attach backend**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests
git commit -m "feat: attach mono debugger backend"
```

### Task 8: Breakpoint state, conditional binding, and source mapping

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Source/SourceMapper.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Dap/DapBreakpointModels.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Breakpoints/BreakpointManagerTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Source/SourceMapperTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`

**Interfaces:**
- Consumes: `LogicalBreakpoint`, `BackendBoundBreakpoint`, workspace root.
- Produces: `BreakpointManager.ReplaceForSource(sourcePath, requested)`; `BreakpointManager.MarkAllPending(reason)`; `BreakpointManager.RebindAll()`; `BreakpointManager.RebindPending()`; `BreakpointManager.Changed`; `SourceMapper.ToClientPath(runtimePath)`; DAP `setBreakpoints` and breakpoint-changed events with conditional support.

- [ ] **Step 1: Write failing breakpoint and source tests**

Required breakpoint behavior:

```csharp
[Fact]
public void ReplaceForSource_preserves_logical_breakpoint_and_condition()
{
    var backend = new FakeDebuggerBackend();
    var manager = new BreakpointManager(backend);

    var result = manager.ReplaceForSource(
        @"H:\fixture\Assets\Player.cs",
        new[] { new RequestedBreakpoint(12, "health <= 0") });

    Assert.Single(result);
    Assert.True(result[0].Verified);
    Assert.Equal("health <= 0", backend.Bound.Single().Condition);
}

[Fact]
public void Missing_symbols_leave_breakpoint_pending()
{
    var backend = new FakeDebuggerBackend { BindAsPending = true };
    var manager = new BreakpointManager(backend);
    var result = manager.ReplaceForSource(
        @"H:\fixture\Assets\Player.cs",
        new[] { new RequestedBreakpoint(12, null) });

    Assert.False(result[0].Verified);
    Assert.Equal("Symbols are not loaded.", result[0].Message);
}
```

Source mapping tests must cover case-insensitive workspace paths, slash
normalization, runtime paths outside the workspace, missing files, and
redaction-safe display names.

- [ ] **Step 2: Run focused tests and verify missing managers fail**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "BreakpointManager|SourceMapper"
```

Expected: FAIL because both units are absent.

- [ ] **Step 3: Implement logical breakpoint ownership**

`BreakpointManager` owns monotonically increasing logical IDs and a dictionary
keyed by canonical source path plus line. `ReplaceForSource` must:

1. remove backend bindings not present in the new request;
2. update a changed condition by removing and rebinding;
3. preserve an unchanged logical ID;
4. call `IDebuggerBackend.BindBreakpoint`;
5. return verified/pending state with a specific message.

Use these public result types:

```csharp
internal sealed class ManagedBreakpoint
{
    public long Id { get; }
    public string SourcePath { get; }
    public int Line { get; }
    public string? Condition { get; }
    public long? BackendId { get; }
    public bool Verified { get; }
    public string? Message { get; }
}
```

Never report a breakpoint verified until the backend returns a concrete
sequence point. `RebindPending` must attempt only logical breakpoints without a
verified backend binding; `RebindAll` first removes any surviving backend
binding and then attempts every logical breakpoint. Both paths preserve the
logical ID and condition.

- [ ] **Step 4: Implement source mapping**

`SourceMapper` constructor receives the normalized workspace root and an
injected `Func<string, bool> fileExists`. `ToClientPath` must:

- use `Path.GetFullPath`;
- compare with `StringComparison.OrdinalIgnoreCase`;
- return a normal source only if the path is inside the workspace and exists;
- otherwise return a missing-source result with `SourceReference=0`, no
  fabricated path, and message `Source file is unavailable in this workspace.`;
- never search the entire disk by filename.

- [ ] **Step 5: Add real backend binding and DAP translation**

Extend the facade:

```csharp
event EventHandler<BackendBreakpointChangedEventArgs> BreakpointChanged;
BackendBoundBreakpoint BindBreakpoint(LogicalBreakpoint breakpoint);
void RemoveBreakpoint(long backendBreakpointId);
```

The real facade calls `session.Breakpoints.Add(path, line)`, assigns
`ConditionExpression`, and returns the actual bound line/sequence point when
available. Retain a private mapping from the returned `BreakEvent` to the
numeric backend ID and translate `BreakpointStore.BreakEventStatusChanged`
into `BackendBreakpointChangedEventArgs`; never expose the upstream object.
`MonoDebuggerBackend` forwards this through `IDebuggerBackend`.
Map missing symbols/assemblies to a pending result rather than a DAP request
failure.

In `UnityDebugSession.SetBreakpoints`, reject non-`.cs` sources, convert DAP
source breakpoints to `RequestedBreakpoint`, and return each breakpoint with
stable logical ID, line, verified state, and message. Set
`supportsConditionalBreakpoints = true`. `BreakpointManager` must subscribe to
backend status changes, update the matching logical breakpoint, and raise its
own changed event. Translate that event to DAP
`new Event("breakpoint", new { reason = "changed", breakpoint = ... })`.

Do not use the vendored `VSCodeDebug.Breakpoint` response model because its
constructor cannot set the DAP breakpoint ID. Define project-owned
`DapBreakpoint` and `DapSetBreakpointsResponseBody : ResponseBody` with exact
serialized fields `id`, `verified`, `message`, `source`, `line`, and `column`;
use the same `DapBreakpoint` object in changed events. Add a serialization test
that asserts the stable logical ID is present and no private backend ID is
serialized.

- [ ] **Step 6: Verify breakpoint replacement and mapping**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "BreakpointManager|SourceMapper"
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: tests PASS, including pending symbols and condition replacement.

- [ ] **Step 7: Commit breakpoints and source mapping**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests
git commit -m "feat: bind managed source breakpoints"
```

### Task 9: Threads, stack frames, scopes, variables, and explicit evaluation

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/State/HandleTable.cs`
- Create: `adapter/src/UnityDebugger.Adapter/State/ThreadIdMap.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/State/HandleTableTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/State/ThreadIdMapTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/InspectionRequestTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`

**Interfaces:**
- Consumes: backend models from Task 6 and `SourceMapper`.
- Produces: `HandleTable<T>.Create`, `TryGet`, and `Reset`; `ThreadIdMap.GetOrCreate`, `TryGetBackendId`, `Remove`, and `Reset`; backend inspection methods; DAP `threads`, `stackTrace`, `scopes`, `variables`, and `evaluate`.

- [ ] **Step 1: Write failing handle and inspection tests**

`HandleTableTests.cs`:

```csharp
[Fact]
public void Reset_invalidates_every_previous_handle()
{
    var table = new HandleTable<string>();
    var handle = table.Create("value");
    Assert.True(table.TryGet(handle, out var value));
    Assert.Equal("value", value);

    table.Reset();

    Assert.False(table.TryGet(handle, out _));
}
```

`InspectionRequestTests.cs` must initialize a DAP session with a
`FakeDebuggerBackend` containing two threads, two frames, one locals scope,
primitive and expandable variables, and an evaluation result. Assert:

- `threads` returns stable positive DAP IDs and preserves names, including when
  a backend ID exceeds `Int32.MaxValue`;
- `stackTrace` returns normalized client source and exact line;
- `scopes` returns valid non-zero variable references;
- `variables` caps one response at 100 items and appends one `...` marker;
- an unknown frame/variable reference returns an actionable DAP error;
- evaluation is accepted for `watch` and `repl`;
- evaluation with context `hover` is rejected and never reaches the backend.

`ThreadIdMapTests` must map backend `long` IDs to positive DAP `int` IDs,
return the same DAP ID for repeated backend IDs, reverse-map controls, remove a
thread on exit, reset on reconnect/disconnect, and throw before integer
overflow.

- [ ] **Step 2: Run focused tests and verify missing inspection support fails**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "HandleTable|ThreadIdMap|InspectionRequest"
```

Expected: FAIL because the handle table and DAP handlers are absent.

- [ ] **Step 3: Implement stop-scoped handle tables**

`HandleTable<T>` must start at handle `1`, use a dictionary, throw on integer
overflow, and clear/reset the counter on every stopped event, continued event,
reload start, and disconnect:

```csharp
internal sealed class HandleTable<T>
{
    private readonly Dictionary<int, T> values = new Dictionary<int, T>();
    private int next = 1;

    public int Create(T value)
    {
        if (next == int.MaxValue)
            throw new InvalidOperationException("DAP handle space is exhausted.");
        var handle = next++;
        values.Add(handle, value);
        return handle;
    }

    public bool TryGet(int handle, out T value)
    {
        T found;
        if (values.TryGetValue(handle, out found))
        {
            value = found;
            return true;
        }

        value = default(T);
        return false;
    }

    public void Reset()
    {
        values.Clear();
        next = 1;
    }
}
```

`ThreadIdMap` uses forward and reverse dictionaries with the same positive,
overflow-checked counter. It is session-scoped rather than stop-scoped:
preserve mappings across ordinary stops, remove them on backend thread-exit
events, and reset on disconnect, transport replacement, or Domain Reload.

- [ ] **Step 4: Extend the facade and backend inspection methods**

Add exact facade methods matching `IDebuggerBackend` inspection signatures.
`SoftDebuggerSessionFacade` must:

- get threads from the active `ProcessInfo`;
- map a Mono `Backtrace` to backend frames;
- obtain `this`, parameters, and local variables for a frame;
- wait for `ObjectValue.WaitHandle` before reading a value;
- retain backend object references privately and expose numeric IDs only;
- clone the safe session evaluation options and set `AllowTargetInvoke`,
  `AllowMethodEvaluation`, and `AllowToStringCalls` to `true` only for explicit
  `watch`/`repl` requests made through `Evaluate`;
- never write `DisplayValue` to diagnostics.

Use `MaxChildren = 100`. The 101st client item is a synthetic marker and does
not receive a child reference.

- [ ] **Step 5: Implement DAP inspection handlers**

`UnityDebugSession` must keep:

```csharp
private readonly ThreadIdMap threadIds = new ThreadIdMap();
private readonly HandleTable<BackendStackFrame> frameHandles =
    new HandleTable<BackendStackFrame>();
private readonly HandleTable<BackendVariable[]> variableHandles =
    new HandleTable<BackendVariable[]>();
```

Reset both handle tables whenever execution resumes or a new stop occurs. Map
source paths through `SourceMapper`. Map every backend thread ID through
`threadIds` before sending threads/stopped/thread events, and reverse-map every
incoming DAP thread ID before stack/control calls. Unknown DAP thread IDs fail
without calling the backend. Set:

```csharp
supportsEvaluateForHovers = false
```

and reject an evaluate request whose `context` is not `watch` or `repl`.
Return evaluation errors as failed responses without logging the expression or
result.

- [ ] **Step 6: Verify all inspection requests**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "HandleTable|ThreadIdMap|InspectionRequest"
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: tests PASS and stale handles fail after resume/reload.

- [ ] **Step 7: Commit inspection support**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests
git commit -m "feat: inspect managed execution state"
```

### Task 10: Continue, pause, stepping, and exception break modes

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/State/ExecutionState.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/State/ExecutionStateTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ExecutionControlTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ExceptionBreakpointTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/ISoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/SoftDebuggerSessionFacade.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`

**Interfaces:**
- Consumes: `IDebuggerBackend` control methods and stop events.
- Produces: `ExecutionState` transitions; DAP `continue`, `pause`, `next`, `stepIn`, `stepOut`, and `setExceptionBreakpoints`; exception filter IDs `all` and `uncaught`.

- [ ] **Step 1: Write failing state, control, and exception tests**

State tests must allow:

```text
Disconnected -> Running -> Stopped -> Running -> Disconnected
```

and reject stepping while running or after disconnect.

Control tests verify each DAP request calls exactly one backend method with the
requested thread ID. Exception tests verify:

```csharp
session.SetExceptionFilters(new[] { "all" });
Assert.Equal(ExceptionBreakMode.All, backend.ExceptionBreakMode);

session.SetExceptionFilters(new[] { "uncaught" });
Assert.Equal(ExceptionBreakMode.Uncaught, backend.ExceptionBreakMode);

session.SetExceptionFilters(Array.Empty<string>());
Assert.Equal(ExceptionBreakMode.None, backend.ExceptionBreakMode);
```

Unknown filter IDs must produce a failed response rather than silently enabling
all exceptions.

- [ ] **Step 2: Run focused tests and verify missing control support fails**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "ExecutionState|ExecutionControl|ExceptionBreakpoint"
```

Expected: FAIL because state/control handlers are absent.

- [ ] **Step 3: Implement explicit execution state transitions**

`ExecutionState` exposes:

```csharp
internal enum ExecutionStatus { Disconnected, Running, Stopped }

internal sealed class ExecutionState
{
    public ExecutionStatus Status { get; private set; } = ExecutionStatus.Disconnected;
    public void Attached() => Transition(ExecutionStatus.Disconnected, ExecutionStatus.Running);
    public void Stopped() => Transition(ExecutionStatus.Running, ExecutionStatus.Stopped);
    public void Continued() => Transition(ExecutionStatus.Stopped, ExecutionStatus.Running);
    public void Disconnected() => Status = ExecutionStatus.Disconnected;
    public void RequireStopped(string operation)
    {
        if (Status != ExecutionStatus.Stopped)
            throw new InvalidOperationException($"{operation} requires a stopped target.");
    }
}
```

Duplicate backend events must not emit duplicate DAP stopped/continued events;
log only the event type, not stack or variable data.

- [ ] **Step 4: Wrap Mono control and exception behavior**

Extend the facade with:

```csharp
void Pause();
void StepIn();
void StepOver();
void StepOut();
void ConfigureExceptions(ExceptionBreakMode mode);
```

Map controls to `Stop`, `StepLine`, `NextLine`, and `Finish`. For
`ExceptionBreakMode.All`, add a `System.Exception` catchpoint and forward
thrown/unhandled events. For `Uncaught`, remove the catchpoint and forward only
the backend's unhandled exception event. For `None`, remove catchpoints and do
not generate an exception stop.

- [ ] **Step 5: Implement DAP control and advertised capabilities**

Require stopped state for `next`, `stepIn`, and `stepOut`; require running state
for `pause`; accept `continue` only from stopped. Send the DAP response before
the corresponding continued event can race back from the backend.

For continue and step requests, tentatively transition `ExecutionState` to
running, invoke the backend, send the response, then flush at most one buffered
continued event. If the backend call throws, restore stopped state and send one
failed response. Apply the same response-before-event buffer to synchronous
pause callbacks. Suppress repeated backend stopped, continued, and terminated
callbacks so each state transition produces at most one DAP event.

Advertise:

```csharp
supportsExceptionOptions = true,
exceptionBreakpointFilters = new[]
{
    new ExceptionBreakpointsFilter("all", "All Exceptions", false),
    new ExceptionBreakpointsFilter("uncaught", "Uncaught Exceptions", true)
}
```

Keep function breakpoints, logpoints, and set-variable unsupported.

- [ ] **Step 6: Verify execution control**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "ExecutionState|ExecutionControl|ExceptionBreakpoint"
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: tests PASS; invalid state requests fail without calling the backend.

- [ ] **Step 7: Commit control and exception support**

```powershell
git add -- adapter/src/UnityDebugger.Adapter tests/adapter/UnityDebugger.Adapter.Tests
git commit -m "feat: control execution and exceptions"
```

### Task 11: Domain Reload recovery and redacted diagnostics

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/PathRedactor.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Diagnostics/DiagnosticLog.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/ReconnectController.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Backend/AssemblyReloadCoordinator.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/PathRedactorTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Diagnostics/DiagnosticLogTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/ReconnectControllerTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Backend/AssemblyReloadCoordinatorTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Breakpoints/DomainReloadTests.cs`
- Create: `tests/extension/diagnostics.test.ts`
- Modify: `adapter/src/UnityDebugger.Adapter/Breakpoints/BreakpointManager.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/MonoDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Diagnostics/MonoDebuggerLogger.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/State/ExecutionState.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Program.cs`
- Modify: `extension/src/diagnostics.ts`

**Interfaces:**
- Consumes: backend reload/termination events, `BreakpointManager`, workspace and user-profile paths.
- Produces: `PathRedactor.Redact(string)`; `IDiagnosticLog.Write(eventName, fields)`; `ReconnectController.TryReconnect`; `AssemblyReloadCoordinator`; pending/rebound DAP breakpoint events; sanitized log commands.

- [ ] **Step 1: Write failing redaction and reload tests**

Required redaction test:

```csharp
[Fact]
public void Redact_removes_home_workspace_expression_and_value()
{
    var redactor = new PathRedactor(
        @"C:\Users\alice",
        @"H:\secret-project");
    var text = redactor.Redact(
        @"path=C:\Users\alice\a.cs workspace=H:\secret-project\Assets\a.cs");

    Assert.DoesNotContain("alice", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("secret-project", text, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("<user-home>", text);
    Assert.Contains("<workspace>", text);
}
```

`DiagnosticLogTests` must fail any attempt to use field names `expression`,
`result`, `sourceText`, or `variableValue`.

`diagnostics.test.ts` must feed the extension a latest log ending with
`event=adapter.exit exitCode=1` and assert the session-termination handler
returns `The Unity debug adapter exited with code 1. Open sanitized diagnostics
for details.` A normal `exitCode=0` must produce no error notification.

`DomainReloadTests` must:

1. bind a breakpoint;
2. raise reload start;
3. observe an unverified breakpoint with message
   `Waiting for assemblies after Domain Reload.`;
4. raise reload complete;
5. observe one rebind and a verified breakpoint event.

The session test must also begin reload from a stopped target, assert one
continued event is emitted, reject inspection/control while reloading, and
return to running after reload completion.

`ReconnectControllerTests` use an injected attempt function and delay function;
assert at most 40 attempts at 250 ms and immediate cancellation on process
exit/disconnect.

`AssemblyReloadCoordinatorTests` must raise multiple payload-free assembly
unload events and assert one reload-start callback. Raise several assembly-load
events and use an injected delay to assert completion occurs once after a
500 ms quiet window; a later load while waiting restarts the window. Explicit
disconnect cancels completion.

- [ ] **Step 2: Run focused tests and verify recovery/diagnostics fail**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "PathRedactor|DiagnosticLog|ReconnectController|AssemblyReloadCoordinator|DomainReload"
```

Expected: FAIL because diagnostics and reload recovery are absent.

- [ ] **Step 3: Implement structured, allowlisted diagnostics**

`IDiagnosticLog.Write` accepts an event name plus a dictionary but rejects
sensitive field names. Allowed production fields are:

```text
adapterVersion, event, processId, host, port, projectVersion,
protocolVersion, breakpointCount, threadId, exitCode, exceptionType
```

`PathRedactor` replaces normalized workspace and user-profile paths before
writing UTF-8 lines. Logs go to:

```text
%LOCALAPPDATA%\unity-debugger-vscode\logs\adapter-<UTC timestamp>-<PID>.log
```

Retain the latest five files; resolve and verify the log directory is under
`LocalApplicationData` before deleting older files. Never pass a log path from
`launch.json`.

At Adapter startup, connect the payload-free vendored trace hook only to the
allowlisted command event:

```csharp
ProtocolTrace.Sink = command =>
    diagnosticLog.Write("dap.command", new Dictionary<string, object>
    {
        ["event"] = command
    });
```

Clear the sink during shutdown so tests and repeated sessions do not retain the
log instance. Connect `MonoDebuggerLogger` to `DiagnosticLog` in the same
lifetime; record only its fixed event name and `exceptionType`, never the
upstream message or arguments.

`Program.Main` must always write one final `adapter.exit` event with exit code
`0` or `1`, flush/dispose the log, and return the same code. Its catch block
records only `exceptionType`; it must not print an unsanitized exception or
stack trace to stdout/stderr.

- [ ] **Step 4: Implement condition-based reconnect**

`ReconnectController.TryReconnect` signature:

```csharp
public Task<bool> TryReconnect(
    Func<bool> processIsAlive,
    Func<bool> attempt,
    Func<TimeSpan, CancellationToken, Task> delay,
    CancellationToken cancellationToken)
```

Try immediately, then every 250 ms while the process is alive, stopping after
10 seconds. Do not block a DAP reader thread and do not use `Thread.Sleep`.
Return `false` on timeout/process exit and propagate cancellation only for an
explicit disconnect.

`AssemblyReloadCoordinator` consumes the facade's payload-free
`AssemblyUnloaded` and `AssemblyLoaded` signals. The first unload begins a
reload; repeated unloads are coalesced. Each subsequent load resets an
asynchronous 500 ms quiet-window delay. Completion fires once after the quiet
window. Use cancellation tokens and an injected delay; never use
`Thread.Sleep`.

- [ ] **Step 5: Wire reload and reconnect events**

On reload start:

- transition execution to a `Reloading` status; if it was stopped, emit one
  continued event before reload output;
- reset frame/variable handles;
- reset the thread ID map;
- call `BreakpointManager.MarkAllPending`;
- send DAP breakpoint-changed events;
- send one output line `Domain Reload detected; waiting for assemblies.`;

On reload complete:

- transition execution from `Reloading` to running;
- call `BreakpointManager.RebindAll`;
- send changed events for each breakpoint;
- send one output line with the rebound/pending counts.

On each assembly-load signal during the quiet window, call
`BreakpointManager.RebindPending` so breakpoints can verify as soon as their
own assembly arrives. `MonoDebuggerBackend` maps coordinator start/completion
to `IDebuggerBackend.ReloadStarted`/`ReloadCompleted`.

If transport disconnects while the Editor PID remains alive, run the reconnect
controller against the same loopback port and explicitly begin the same reload
coordinator if an unload event was not observed. Recreate only the facade/Mono
session; retain logical breakpoints and exception mode. Feed assembly-load
events from the replacement session into the coordinator. If reconnect fails,
send one terminated event and a sanitized actionable error.

- [ ] **Step 6: Wire extension log commands**

`extension/src/diagnostics.ts` must derive the same fixed log directory, expose
the latest sanitized report, open the directory through
`vscode.env.openExternal(vscode.Uri.file(logDirectory))`, and copy only the
sanitized file contents. It must not scan outside that directory. Register
`vscode.debug.onDidTerminateDebugSession`; for `unity-community`, read the
latest matching log and show the tested non-zero-exit message.

- [ ] **Step 7: Verify recovery and privacy**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --filter "PathRedactor|DiagnosticLog|ReconnectController|AssemblyReloadCoordinator|DomainReload"
npm run test:extension -- diagnostics
npm test
```

Expected: all tests PASS; test logs contain none of the seeded secret values.

- [ ] **Step 8: Commit recovery and diagnostics**

```powershell
git add -- adapter/src/UnityDebugger.Adapter extension/src/diagnostics.ts tests/adapter/UnityDebugger.Adapter.Tests tests/extension/diagnostics.test.ts
git commit -m "feat: recover domain reloads safely"
```

### Task 12: Process-level DAP integration suite

**Files:**
- Create: `tests/integration/UnityDebugger.TestAdapter/UnityDebugger.TestAdapter.csproj`
- Create: `tests/integration/UnityDebugger.TestAdapter/Program.cs`
- Create: `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs`
- Create: `tests/integration/dapClient.ts`
- Create: `tests/integration/adapter.integration.test.ts`
- Modify: `UnityDebugger.sln`
- Modify: `vitest.config.ts`
- Modify: `package.json`

**Interfaces:**
- Consumes: production `UnityDebugSession`, `IDebuggerBackend`, DAP stdin/stdout.
- Produces: test-only executable `UnityDebugger.TestAdapter.exe`; `DapClient.request(command, arguments)`; deterministic scenarios `normal`, `reload`, `protocol-error`, and `backend-crash`.

- [ ] **Step 1: Write the failing process-level integration test**

The normal scenario test must spawn the test Adapter and perform:

```ts
const client = await DapClient.start(testAdapterPath, ["normal"]);
await client.request("initialize", {
  adapterID: "unity-community",
  linesStartAt1: true,
  columnsStartAt1: true,
});
await client.waitForEvent("initialized");
await client.request("attach", validAttachArguments);
await client.request("setBreakpoints", {
  source: { path: fixtureSource },
  breakpoints: [{ line: 12, condition: "health <= 0" }],
  sourceModified: false,
});
const stopped = await client.waitForEvent("stopped");
const threads = await client.request("threads", {});
const stack = await client.request("stackTrace", {
  threadId: stopped.body.threadId,
  startFrame: 0,
  levels: 20,
});
const scopes = await client.request("scopes", {
  frameId: stack.body.stackFrames[0].id,
});
await client.request("variables", {
  variablesReference: scopes.body.scopes[0].variablesReference,
});
await client.request("evaluate", {
  expression: "health",
  frameId: stack.body.stackFrames[0].id,
  context: "watch",
});
await client.request("next", { threadId: stopped.body.threadId });
await client.request("disconnect", { restart: false });
await client.expectCleanExit(0);
```

Add tests for reload/rebind, exception stop, all three step operations,
protocol rejection, backend crash reporting, and disconnect cleanup.

- [ ] **Step 2: Run integration tests and verify the test Adapter is missing**

Run:

```powershell
npm run test:integration
```

Expected: FAIL because the test Adapter and `DapClient` do not exist.

- [ ] **Step 3: Implement test-only Adapter and scripted backend**

`UnityDebugger.TestAdapter` references the production Adapter project, is never
referenced by the production project, and chooses a scenario only from its
command-line argument. `ScenarioDebuggerBackend` implements the full backend
interface with deterministic IDs:

```text
process 1234
thread 1 "Main Thread"
frame 10 "FixtureBehaviour.Update" at Assets/DebuggerFixture.cs:12
scope 20 "Locals"
variable 30 health = 0 (System.Int32)
```

The reload scenario raises reload-start, waits for the next DAP breakpoint
request/event cycle, then raises reload-complete. The crash scenario throws
`DebuggerBackendException("Simulated backend failure.")`; it must not terminate
the test process before DAP reports the error.

- [ ] **Step 4: Implement a framed DAP client**

`DapClient` must buffer stdout bytes, parse ASCII headers through
`\r\n\r\n`, require one valid `Content-Length`, decode exactly that many UTF-8
bytes, correlate response `request_seq`, and queue events by name. Its writer:

```ts
private send(message: object): void {
  const body = Buffer.from(JSON.stringify(message), "utf8");
  const header = Buffer.from(`Content-Length: ${body.length}\r\n\r\n`, "ascii");
  this.child.stdin.write(Buffer.concat([header, body]));
}
```

Capture stderr separately and include it only in assertion failures. Kill a
hung child after a 5-second per-test timeout, then fail the test with pending
request/event names. Treat a response for an unknown or already-completed
`request_seq` as a test failure; this makes the integration suite detect the
duplicate-response defect removed from the vendored protocol. Reject any
non-DAP bytes on stdout.

- [ ] **Step 5: Wire build and test scripts**

Add the test Adapter to `UnityDebugger.sln`, exclude its output from VSIX
staging, and add:

```json
"test:integration": "vitest run tests/integration",
"test": "npm run test:build && npm run test:extension && npm run test:adapter && npm run test:integration"
```

Vitest integration setup must build `UnityDebugger.TestAdapter` once before
tests, not once per case.

- [ ] **Step 6: Verify complete simulated sessions**

Run:

```powershell
npm run test:integration
npm test
```

Expected: normal, reload, exception, protocol-error, and backend-failure
scenarios PASS; every spawned Adapter exits.

- [ ] **Step 7: Commit the integration suite**

```powershell
git add -- tests/integration UnityDebugger.sln vitest.config.ts package.json package-lock.json
git commit -m "test: exercise debug adapter protocol"
```

### Task 13: VSIX staging, artifact audit, documentation, and CI/release gates

**Files:**
- Create: `README.md`
- Create: `CHANGELOG.md`
- Create: `SECURITY.md`
- Create: `.vscodeignore`
- Create: `scripts/stage-adapter.mjs`
- Create: `scripts/generate-runtime-inventory.mjs`
- Create: `scripts/verify-vsix.mjs`
- Create: `third-party/runtime-assemblies.json`
- Create: `tests/package/vsix.test.mjs`
- Create: `.github/ISSUE_TEMPLATE/bug.yml`
- Create: `.github/ISSUE_TEMPLATE/compatibility.yml`
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`
- Create: `.github/workflows/publish-marketplace.yml`
- Modify: `THIRD_PARTY_NOTICES.md`
- Modify: `package.json`
- Modify: `package-lock.json`

**Interfaces:**
- Consumes: release Adapter build, bundled extension, third-party provenance, all tests.
- Produces: `dist/unity-debugger-vscode-0.1.0.vsix`; SHA-256 file; audited runtime inventory; Windows CI artifact; tagged GitHub release candidate; manually approved Marketplace workflow.

- [ ] **Step 1: Write the failing VSIX contract test**

`tests/package/vsix.test.mjs` must invoke the verifier against the packaged
artifact and assert:

- `extension/dist/extension.js` exists;
- `extension/adapter/win32-x64/UnityCommunityDebug.exe` exists;
- `extension/LICENSE`, `extension/README.md`, and
  `extension/THIRD_PARTY_NOTICES.md` exist;
- no path contains `tests/`, `adapter/vendor/`, `.git/`, `src/`, or a test
  Adapter;
- no `extensionDependencies` appear in packaged `package.json`;
- every `.dll` and `.exe` appears in
  `third-party/runtime-assemblies.json`;
- no file is outside the allowlisted extension root.

- [ ] **Step 2: Run the VSIX contract and verify no package exists**

Run:

```powershell
node --test tests/package/vsix.test.mjs
```

Expected: FAIL because the VSIX and verifier do not exist.

- [ ] **Step 3: Implement Adapter staging and runtime inventory**

`scripts/stage-adapter.mjs` must:

1. resolve the repository root and exact Release output;
2. resolve `adapter/win32-x64`, prove it is a direct child of the repository's
   `adapter` directory, clean only that exact staging directory, and recreate
   it while preserving `adapter/src` and `adapter/vendor`;
3. copy the production `.exe`, `.config`, and `.dll` files only; do not ship
   Adapter PDBs containing build-machine source paths;
4. reject filenames containing `Test`, `xunit`, or `testhost`;
5. fail if `UnityCommunityDebug.exe` is absent.

`generate-runtime-inventory.mjs` must hash every staged `.exe`/`.dll` with
SHA-256. Each committed entry records its VSIX path, the lowercase digest
computed by:

```js
const sha256 = createHash("sha256")
  .update(readFileSync(stagedAssembly))
  .digest("hex");
```

and an exact origin such as `source-built:debugger-libs`, with its license.

Use origins `project`, `source-built:<sources.json name>`, or
`nuget:<exact package id>/<locked version>`. Fail if an assembly cannot be
matched to a project output, vendored project, or `packages.lock.json`. Generate
the inventory after a clean Release build, review it, and commit it; subsequent
CI runs verify rather than overwrite it.

Perform the initial write explicitly:

```powershell
npm run build:adapter
npm run stage:adapter
node scripts/generate-runtime-inventory.mjs --write
node scripts/generate-runtime-inventory.mjs --verify
git diff -- third-party/runtime-assemblies.json
```

- [ ] **Step 4: Implement VSIX verification and packaging**

Add `adm-zip` version `0.5.16` as a dev dependency. `verify-vsix.mjs` opens the
VSIX as ZIP, normalizes `/` paths, rejects `..` segments and absolute paths,
enforces the contract above, compares runtime hashes with the committed
inventory, and confirms the Adapter PE architecture is AMD64.

Update scripts:

```json
"stage:adapter": "node scripts/stage-adapter.mjs",
"inventory:runtime:write": "node scripts/generate-runtime-inventory.mjs --write",
"inventory:runtime": "node scripts/generate-runtime-inventory.mjs --verify",
"verify:vsix": "node scripts/verify-vsix.mjs dist/unity-debugger-vscode-0.1.0.vsix",
"test:package": "node --test tests/package/vsix.test.mjs",
"package": "npm run build && npm run stage:adapter && npm run inventory:runtime && vsce package --out dist/unity-debugger-vscode-0.1.0.vsix && npm run verify:vsix && npm run test:package"
```

`.vscodeignore` must exclude source, tests, plans/specs, build intermediates,
lockfiles not required at runtime, `.github`, and the test fixture while keeping
the bundled extension, production Adapter folder, README, LICENSE, CHANGELOG,
and notices. Use ordered adapter rules equivalent to:

```text
adapter/**
!adapter/win32-x64/**
```

and make the VSIX contract fail if the production Adapter was accidentally
excluded or any other adapter subtree was included.

- [ ] **Step 5: Write public documentation and issue routes**

`README.md` must state:

- community/non-affiliation notice in the first screen;
- formally supported matrix and unverified-version policy;
- installation from Marketplace and `.vsix`;
- exact attach configuration;
- no C# Dev Kit/Microsoft Unity extension requirement;
- ReSharper coexistence;
- Code Optimization → Debug prerequisite;
- local-only security boundary and absence of telemetry;
- current exclusions;
- diagnostic commands and sanitized issue information.

Give the support section the stable `support-policy` anchor used by the Adapter
warning. Add a build test that derives the Marketplace item name from manifest
`publisher` + `name` and asserts the warning URL matches it, so a reviewed
publisher change cannot leave a stale link.

`SECURITY.md` directs vulnerabilities to GitHub Security Advisories and tells
users not to paste raw source/variable data. `CHANGELOG.md` starts with
`0.1.0` scope. Issue forms request Editor version, OS, extension version,
reproduction, and sanitized diagnostics; they must not request project source.

- [ ] **Step 6: Add Windows CI**

`.github/workflows/ci.yml` must trigger on pull requests and pushes to
`master`, use:

```yaml
runs-on: windows-latest
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-node@v4
    with:
      node-version: 26.5.0
      cache: npm
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 10.0.x
  - run: npm ci
  - run: dotnet restore UnityDebugger.sln --locked-mode
  - run: npm run verify:third-party
  - run: npm test
  - run: npm run package
  - uses: actions/upload-artifact@v4
    with:
      name: unity-debugger-vscode-vsix
      path: |
        dist/*.vsix
        dist/*.sha256
```

Generate the SHA-256 file before upload. Do not run real Editor tests in hosted
CI.

- [ ] **Step 7: Add gated release workflows**

`release.yml` triggers on `v*` tags, repeats locked build/test/package/audit,
verifies the tag equals `package.json` version, creates the GitHub Release using
`gh release create`, and uploads `.vsix` plus `.sha256`. It does not call
`vsce publish`.

`publish-marketplace.yml` must:

- use `workflow_dispatch`;
- require GitHub environment `marketplace`;
- download or rebuild the exact tagged VSIX;
- require repository variable `VSCE_PUBLISHER` to equal the manifest
  `publisher`;
- require secret `VSCE_PAT`;
- run `vsce publish --packagePath <audited-vsix> -p $env:VSCE_PAT`;
- fail before publication if publisher ownership has not been confirmed in the
  environment.

This preserves manual approval even after a tag.

- [ ] **Step 8: Verify clean packaging and CI contracts**

Run:

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm test
npm run package
npm run test:package
Get-FileHash -Algorithm SHA256 dist/unity-debugger-vscode-0.1.0.vsix
git diff --check
```

Expected: tests and audit PASS; the VSIX contains only allowlisted production
files; the checksum is emitted.

- [ ] **Step 9: Commit release infrastructure**

```powershell
git add -- README.md CHANGELOG.md SECURITY.md .vscodeignore scripts third-party/runtime-assemblies.json tests/package .github THIRD_PARTY_NOTICES.md package.json package-lock.json
git commit -m "build: package audited debugger extension"
```

### Task 14: Tuanjie 2022.3.62t11 fixture and real-Editor acceptance

**Files:**
- Create: `tests/fixtures/TuanjieProject/Assets/Editor/DebuggerFixture.cs`
- Create: `tests/fixtures/TuanjieProject/Assets/Editor/DebuggerFixture.cs.meta`
- Create: `tests/fixtures/TuanjieProject/ProjectSettings/ProjectVersion.txt`
- Create: `tests/fixtures/TuanjieProject/Packages/manifest.json`
- Create: `scripts/verify-real-editor-environment.ps1`
- Create: `docs/testing/tuanjie-2022.3.62t11.md`
- Create: `docs/testing/release-checklist.md`
- Modify: `.gitignore`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: final VSIX, installed `Tuanjie 2022.3.62t11`, VS Code profiles.
- Produces: reproducible fixture and signed-off acceptance record covering all version 0.1.0 real-Editor criteria.

- [ ] **Step 1: Write the fixture behavior**

`DebuggerFixture.cs` must run inside the Editor without a scene or user-created
GameObject:

```csharp
using UnityEditor;

[InitializeOnLoad]
public static class DebuggerFixture
{
    private static int health = 1;

    static DebuggerFixture()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        health = Decrement(health);
        if (health <= 0)
        {
            health = 60;
        }
    }

    private static int Decrement(int value)
    {
        return value - 1;
    }
}
```

The explicit helper gives the acceptance checklist stable user-code targets
for Step In, Step Over, and Step Out without invoking project scripts or scene
objects.

Use this exact metadata:

```yaml
fileFormatVersion: 2
guid: 7e67fb1fef0d4c8499232096dbac8f01
```

Pin `ProjectVersion.txt` to:

```text
m_EditorVersion: 2022.3.62t11
```

Use this package manifest so the fixture adds no IDE/debugging package:

```json
{
  "dependencies": {}
}
```

Ignore the fixture's `Library`, `Logs`, `Temp`, `obj`, and user settings.

- [ ] **Step 2: Implement read-only environment verification**

`verify-real-editor-environment.ps1` must:

- read Windows uninstall registry entries and require display version
  `2022.3.62t11`;
- resolve `Tuanjie.exe` from `DisplayIcon`;
- verify the fixture `ProjectVersion.txt`;
- when the Editor is running, parse `Library/EditorInstance.json`, confirm its
  PID exists, derive the default port, and test a loopback TCP connection;
- print PASS/FAIL rows without changing Editor or project settings;
- return exit code `0` only when all prerequisites needed at that moment pass.

It must accept `-ProjectPath` and default it to the committed fixture. Do not
hard-code this machine's `H:\workspace` or installed Editor path into the public
script.

- [ ] **Step 3: Write the exact manual acceptance procedure**

`docs/testing/tuanjie-2022.3.62t11.md` must include:

1. clean build and VSIX audit;
2. create an isolated VS Code `--user-data-dir` and `--extensions-dir`;
3. install only the built VSIX for the dependency-free run;
4. open the fixture in Tuanjie and switch Code Optimization to Debug;
5. attach and verify standard/conditional breakpoint behavior;
6. verify Step In/Over/Out, threads, Call Stack, Locals, Watch, and Debug
   Console evaluation;
7. verify all/uncaught exception modes using a documented temporary fixture
   edit that is reverted before sign-off;
8. trigger script recompilation and confirm pending/rebound breakpoint state;
9. disconnect, confirm no `UnityCommunityDebug` process remains, and reattach;
10. repeat with ReSharper for VS Code enabled and confirm its ordinary .NET
    configurations remain unchanged;
11. try one unverified Editor only if installed and confirm exactly one warning;
12. inspect the sanitized log and confirm no source, expression, result, or full
    home/workspace path is present.

The acceptance record captures date, Windows version, Tuanjie full version,
VS Code version, VSIX SHA-256, commit SHA, and PASS/FAIL for each criterion.

- [ ] **Step 4: Commit the fixture and acceptance procedure**

Run:

```powershell
git add -- tests/fixtures scripts/verify-real-editor-environment.ps1 docs/testing/tuanjie-2022.3.62t11.md docs/testing/release-checklist.md .gitignore README.md CHANGELOG.md
git commit -m "test: add tuanjie acceptance fixture"
git status --short --branch
```

Expected: branch `master` is clean. This commit is the immutable source revision
recorded by the real-Editor test.

- [ ] **Step 5: Run automated preflight**

Run:

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm test
npm run package
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-real-editor-environment.ps1 -ProjectPath tests/fixtures/TuanjieProject
```

Expected before opening the Editor: build/tests/package PASS; environment
verification confirms installation and fixture, and clearly marks the running
Editor checks as not yet satisfied.

- [ ] **Step 6: Execute and record real-Editor acceptance**

Open the fixture in the installed Tuanjie Editor, enable Debug optimization, run
the verification script again, record the current `git rev-parse HEAD`, and
execute every checklist item. Store the completed record under:

```text
docs/testing/results/2026-07-26-tuanjie-2022.3.62t11.md
```

Expected: all twelve version 0.1.0 acceptance criteria PASS. If any criterion
fails, keep the release unreleased, preserve the sanitized diagnostic report,
and address the failure through a focused debugging task before repeating the
full checklist.

- [ ] **Step 7: Commit the acceptance result**

```powershell
git add -- docs/testing/results/2026-07-26-tuanjie-2022.3.62t11.md
git commit -m "test: validate tuanjie editor debugging"
```

The result file must name the fixture/procedure commit tested in Step 6 and the
audited VSIX checksum. The result commit itself contains documentation only and
is excluded from the VSIX.

- [ ] **Step 8: Run the final verification suite**

Run:

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm run verify:third-party
npm test
node scripts/verify-vsix.mjs dist/unity-debugger-vscode-0.1.0.vsix
Get-FileHash -Algorithm SHA256 dist/unity-debugger-vscode-0.1.0.vsix
git diff --check
git status --short
```

Expected: all automated checks PASS; the already accepted VSIX still passes its
artifact audit and its checksum matches the acceptance record; branch remains
clean. Do not rebuild the VSIX in this step: ZIP timestamps are not assumed to
be byte-for-byte reproducible, and the acceptance-result commit changes
documentation only.

- [ ] **Step 9: Confirm release readiness without publishing**

Run:

```powershell
git status --short --branch
git log --oneline --decorate -15
Get-FileHash -Algorithm SHA256 dist/unity-debugger-vscode-0.1.0.vsix
```

Expected: branch `master` is clean; the acceptance record names the tested
fixture/procedure commit and the current VSIX checksum. Creating a `v0.1.0` tag
or publishing remains a separate, explicit maintainer action.
