# Community Debugger for Unity 2022 & Tuanjie — Design Specification

**Status:** Approved design  
**Date:** 2026-07-26  
**Repository:** `unity-debugger-vscode`  
**License:** MIT

## 1. Purpose

Build a small, independently installable VS Code debugger extension for managed
C# code running inside the local Tuanjie or Unity Editor. It must work without
Microsoft's Unity extension or C# Dev Kit and coexist with ReSharper for VS
Code.

The first public release is intentionally narrow: Windows x64, local Editor
attach, and the Mono managed debugger used by the Tuanjie/Unity 2022.3 line.
The installed `Tuanjie 2022.3.62t11` Editor is the first required real-world
test baseline.

## 2. Product Identity

- Repository name: `unity-debugger-vscode`
- Extension manifest name: `unity-debugger-vscode`
- Display name: `Community Debugger for Unity 2022 & Tuanjie`
- Debug type: `unity-community`
- Initial version: `0.1.0`
- License: MIT

The Marketplace description and README must state that the project is a
community product and is not affiliated with or endorsed by Unity
Technologies or Unity China. Unity and Tuanjie names are used only to describe
compatibility.

The Marketplace publisher identifier is a deployment credential rather than a
product name. It must be verified against the maintainer's Marketplace account
before the first public publication and must not change the manifest name,
display name, or debug type defined above.

## 3. Support Policy

### 3.1 Formally supported in version 0.1.0

- Windows 10 and Windows 11, x64
- Tuanjie `2022.3.62t11`
- A local Editor process
- Mono managed script debugging
- Attach only
- Standard and conditional breakpoints
- Step In, Step Over, and Step Out
- Threads, Call Stack, Locals, Watch, and expression evaluation
- Exception breakpoints
- Detach and reattach
- Breakpoint rebinding after script recompilation or Domain Reload
- Coexistence with ReSharper for VS Code
- Operation without C# Dev Kit and without Microsoft's Unity extension

### 3.2 Compatible but not guaranteed

- Other Unity 2022.3 LTS patch releases
- Unity 6

The extension may allow these Editors to connect. An unverified version
receives a single warning in the Debug Console but is not blocked. A version
becomes formally verified only after the same real-Editor acceptance suite used
for `2022.3.62t11` passes.

### 3.3 Outside version 0.1.0

- Remote or LAN Players
- Android and iOS devices
- IL2CPP debugging
- WebGL
- Tuanjie Mini Game DebugProxy
- Tuanjie .NET 8 scripting backend
- Native C++ debugging
- Consoles and other special target platforms
- Attaching to more than one Editor in the same VS Code debug session
- macOS and Linux hosts

These targets are not represented by dormant options or incomplete code in the
first release. Later releases add them only through separate, tested designs.

## 4. Architecture

The extension has two processes with a narrow boundary:

```text
VS Code
  |
  | VS Code Extension API
  v
TypeScript extension host
  |
  | Debug Adapter Protocol over stdin/stdout
  v
C# Debug Adapter (.NET Framework 4.8, Windows x64)
  |
  | TCP / Mono Soft Debugger protocol
  v
Local Tuanjie or Unity Editor
```

The TypeScript layer owns VS Code integration, discovery UX, configuration,
version policy, process launch, and diagnostic presentation. The C# Adapter
owns DAP state and Mono Soft Debugger communication. ReSharper is not called,
wrapped, or modified; coexistence follows from registering a separate
`unity-community` debug type.

The Adapter targets .NET Framework 4.8 and Windows x64 for version 0.1.0.
Cross-platform or modern .NET migration is explicitly deferred until a
cross-platform release is designed.

## 5. Repository Structure

```text
unity-debugger-vscode/
├─ extension/                  # TypeScript VS Code integration
├─ adapter/                    # C# DAP-to-Mono bridge
├─ tests/
│  ├─ extension/              # TypeScript unit tests
│  ├─ adapter/                # C# unit tests
│  └─ integration/            # Simulated endpoint and DAP integration tests
├─ third-party/                # Notices, source revisions, licenses, modifications
├─ docs/
│  └─ superpowers/
│     ├─ specs/
│     └─ plans/
└─ .github/
   ├─ ISSUE_TEMPLATE/
   └─ workflows/
```

Files should be split by responsibility. Discovery parsing, version policy,
Adapter launch, DAP session state, breakpoint state, and source mapping remain
independently testable units rather than a single extension or session class.

## 6. Components and Interfaces

### 6.1 TypeScript extension layer

`EditorDiscovery`

- Listens for the Unity/Tuanjie discovery traffic used by the supported Editor.
- Parses project name, Editor or process identifier, host, managed debug port,
  and available version metadata.
- Accepts only loopback or verified local-process candidates in version 0.1.0.
- Returns typed candidates; it does not show UI or launch the Adapter.

`VersionPolicy`

- Reads `ProjectSettings/ProjectVersion.txt`.
- Combines the project version with discovery or handshake metadata.
- Returns one of `supported`, `compatible-unverified`, or `unsupported`.
- Treats `2022.3.62t11` as supported and permits other reachable versions with
  the unverified warning. A structurally incompatible handshake is unsupported.

`DebugConfigurationProvider`

- Supplies an attach configuration with `type: "unity-community"` and
  `request: "attach"`.
- Resolves the local Editor candidate for the current workspace.
- If multiple local Editors are running, presents a Quick Pick and attaches to
  exactly one selected Editor.
- Presents a refresh action if no Editor is found.
- Does not expose remote host or arbitrary command settings in version 0.1.0.

`AdapterLauncher`

- Starts the packaged Windows x64 Adapter.
- Uses stdin/stdout exclusively for DAP.
- Captures exit code and a sanitized diagnostic log.
- Terminates the child process and closes pipes when the debug session ends.

### 6.2 C# Adapter layer

`UnityDebugSession`

- Implements DAP initialization, attach, disconnect, thread, stack trace,
  scope, variables, evaluation, stepping, and exception-breakpoint requests.
- Converts Mono debugger events into DAP stopped, continued, thread, output,
  breakpoint, and terminated events.

`BreakpointManager`

- Stores logical breakpoints independently from the current loaded assembly.
- Reports pending breakpoints as unverified with a reason.
- Rebinds them after assemblies load following a Domain Reload.
- Does not discard a logical breakpoint merely because symbols are temporarily
  unavailable.

`SourceMapper`

- Maps runtime assemblies, documents, and sequence points to workspace `.cs`
  paths.
- Normalizes Windows paths without emitting full user paths into normal logs.
- Returns an explicit missing-symbol result instead of silently guessing.

`MonoConnection`

- Owns the TCP connection and Mono Soft Debugger session lifecycle.
- Exposes connection, reload, and termination events to `UnityDebugSession`.
- Has no VS Code-specific UI behavior.

## 7. Attach Data Flow

1. The user selects **Attach Tuanjie / Unity 2022 Editor** or starts its attach
   configuration.
2. `DebugConfigurationProvider` checks that the workspace contains
   `ProjectSettings/ProjectVersion.txt`.
3. `EditorDiscovery` finds a local Editor candidate and verifies it is local.
4. `VersionPolicy` classifies the candidate and emits at most one unverified
   version warning.
5. VS Code starts the packaged Adapter through `AdapterLauncher`.
6. VS Code and the Adapter exchange DAP messages over stdin/stdout.
7. `MonoConnection` connects to the Editor's managed debug port over TCP.
8. The Adapter binds logical breakpoints when matching assemblies and symbols
   become available.
9. On a stop event, the Adapter returns threads, frames, scopes, variables, and
   evaluation results on demand.
10. During Domain Reload, logical breakpoints stay pending and are rebound once
    assemblies are available again.
11. Disconnect closes the Mono connection, DAP streams, and Adapter process.

No project command, Unity menu command, build, or user script is executed as
part of attach.

## 8. Error Handling

All expected failures must map to a specific user action:

- **No Editor found:** explain that a supported local Editor was not detected,
  suggest checking the Editor's debug/code-optimization mode, and offer refresh.
- **Editor not in debug mode:** point to Code Optimization → Debug.
- **Port unreachable:** include the sanitized local address and port plus a
  firewall/process-state suggestion.
- **Unverified version:** continue, but write one warning containing the version
  and support-policy link.
- **Missing PDB or sequence points:** leave the breakpoint visible and
  unverified, with a missing-symbol reason.
- **Domain Reload:** report transient reconnect/rebind status without ending the
  session solely because assemblies are temporarily unavailable.
- **Protocol incompatibility:** stop cleanly and retain only sanitized protocol
  version/handshake diagnostics.
- **Adapter crash:** surface its exit code and commands to open or copy the
  sanitized diagnostic report.

Normal logs must not include variable values, expression results, source code,
or full user-directory paths.

## 9. Security and Privacy

- Automatic discovery and attachment are restricted to loopback addresses or
  candidates verified against a local Editor process.
- Version 0.1.0 has no setting for arbitrary remote endpoints.
- The extension never executes workspace commands or scripts.
- Expression evaluation occurs only after an explicit debugger/UI request.
- Telemetry and analytics are absent.
- Logs exclude variable values, evaluated results, source contents, and full
  user-directory paths by default.
- Release artifacts are built from source in CI; the repository does not accept
  opaque precompiled Adapter binaries as source.
- Future remote support must be opt-in and document that the Editor debug port
  does not provide strong authentication.
- Security reports are handled privately through GitHub Security Advisories;
  compatibility bugs use public issue templates.

## 10. Upstream Reuse and Licensing

The preferred implementation starts from the archived
`Unity-Technologies/vscode-unity-debug` design and source where licensing
permits. The upstream root is MIT licensed, but its submodules and bundled
assemblies are separate review units.

Before importing any upstream file or dependency:

1. Record its upstream repository, exact revision, and original path.
2. Record the applicable license and retain its required copyright notice.
3. Record local modifications.
4. Replace opaque or obsolete bundled binaries with reproducibly restored or
   built dependencies when possible.
5. Exclude any component whose redistribution terms cannot be established.

The old extension's hard dependency on `ms-dotnettools.csharp` is removed. Its
old .NET Framework 4.7.2/x86 build configuration is not shipped unchanged; the
new Adapter is built and tested as .NET Framework 4.8/Windows x64. No upstream
binary is copied directly into a release.

The repository itself uses the MIT license in `LICENSE`. Imported copyright
notices and dependency notices live under `third-party/`.

## 11. Testing

### 11.1 TypeScript unit tests

- Discovery packet parsing and rejection of malformed packets
- Local-candidate validation
- Project version parsing and policy classification
- Attach configuration resolution
- Sanitization and actionable error-message mapping
- Adapter process cleanup

### 11.2 C# unit tests

- DAP request/response and event translation
- Breakpoint lifecycle: pending, bound, invalid, reload, rebound
- Source path normalization and missing-symbol behavior
- Mono connection lifecycle and disconnect cleanup
- Diagnostic redaction

### 11.3 Integration tests

A deterministic simulated Mono/debug endpoint exercises:

- Initialize and attach
- Breakpoint binding and stop events
- Threads, stack traces, scopes, variables, and evaluation
- Step In, Step Over, and Step Out
- Exception stops
- Domain Reload and breakpoint rebinding
- Graceful disconnect
- Protocol rejection and Adapter failure reporting

### 11.4 Real-Editor acceptance

`Tuanjie 2022.3.62t11` is tested manually on Windows x64 before every public
release. Real-Editor tests are documented and repeatable but are not required
in ordinary hosted CI.

## 12. Version 0.1.0 Acceptance Criteria

1. The VSIX installs and starts without C# Dev Kit or Microsoft's Unity
   extension.
2. It automatically discovers and attaches the local
   `Tuanjie 2022.3.62t11` Editor.
3. Standard and conditional breakpoints bind and stop at the expected source
   line.
4. Step In, Step Over, and Step Out behave correctly.
5. Locals, Watch, Call Stack, threads, and explicit expression evaluation work.
6. Exception breakpoints work.
7. Breakpoints rebind after script recompilation and Domain Reload.
8. Disconnect and Adapter failure leave no Adapter process or open connection.
9. The extension can remain enabled alongside ReSharper for VS Code without
   claiming ReSharper's ordinary .NET debug configurations.
10. Unit and simulated integration suites pass in CI.
11. The VSIX contains no unrecorded binary or dependency and passes a packaged
    contents audit.
12. The real-Editor acceptance checklist passes on Windows x64.

## 13. Build and Release

- GitHub Actions builds on a Windows runner.
- CI restores pinned npm and .NET dependencies, builds the TypeScript extension
  and .NET Framework 4.8 Adapter, and runs all non-Editor tests.
- CI checks third-party notices and inspects the VSIX contents.
- A SemVer Git tag creates a release candidate.
- Marketplace publication is performed manually through the publisher portal;
  tags and GitHub Actions do not publish to the Marketplace.
- Each GitHub Release contains the `.vsix` and its SHA-256 checksum.
- The Marketplace page includes the support matrix, known limitations, privacy
  statement, issue/security routes, and non-affiliation statement.
- `master` remains buildable; release artifacts are produced from tagged commits.

## 14. Maintenance

- Every public release repeats the real-Editor suite on
  `Tuanjie 2022.3.62t11`.
- Other Unity 2022.3 patches enter the verified matrix only after passing that
  suite.
- Unity 6 reports may be investigated, but they do not block version 0.1.x
  releases unless a change also breaks a formally supported target.
- Dependency and upstream-source changes update `third-party/` in the same
  commit.
- Scope expansions require a separate design and acceptance matrix.

## 15. Implementation Sequence

Implementation proceeds in independently testable increments:

1. Establish reproducible TypeScript, .NET Framework 4.8, test, and VSIX builds.
2. Inventory and license-audit the minimum upstream source/dependency set.
3. Implement local Editor discovery and version policy.
4. Establish DAP process launch and a simulated attach lifecycle.
5. Port the minimum Mono connection and session behavior.
6. Add breakpoints, frames, variables, evaluation, stepping, and exceptions.
7. Add Domain Reload recovery and diagnostic redaction.
8. Validate ReSharper coexistence and the real Tuanjie Editor.
9. Add release packaging, checksums, notices, documentation, and a manual
   Marketplace publication procedure.

Each increment must end with passing automated tests and a reviewable commit.
