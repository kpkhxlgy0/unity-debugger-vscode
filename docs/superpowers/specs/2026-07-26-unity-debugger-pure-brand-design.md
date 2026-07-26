# Unity Debugger Pure Brand Migration Design

Date: 2026-07-26

## Objective

Rename the unpublished extension from its community-oriented working identity
to the final public product identity, **Unity Debugger Pure**, before the first
Marketplace release.

The migration must be complete across the Marketplace identity, debugger
configuration, commands, Adapter executable, diagnostics, documentation,
packaging, tests, and the installed MyGame acceptance environment. It must not
change debugger behavior or the supported Editor matrix.

This document supersedes the product-identity decisions in
`2026-07-26-unity-debugger-vscode-design.md`. The earlier design and
implementation plan remain unchanged as historical records.

## Final Identity

| Surface | Final value |
|---|---|
| Display name | `Unity Debugger Pure` |
| Marketplace publisher ID | `kpk` |
| Extension package name | `unity-debugger-pure` |
| Marketplace item ID | `kpk.unity-debugger-pure` |
| Debug type | `unity-debugger-pure` |
| Default configuration name | `Attach to Unity Debugger Pure` |
| Command prefix | `unity-debugger-pure` |
| Adapter executable | `UnityDebuggerPure.exe` |
| Diagnostics directory | `%LOCALAPPDATA%\unity-debugger-pure\logs` |
| VSIX filename | `unity-debugger-pure-0.1.0.vsix` |
| Version | `0.1.0` |

The Marketplace description is:

> Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, without
> C# Dev Kit or Microsoft's Unity extension.

The Git repository and local directory remain named `unity-debugger-vscode`.
The C# namespace remains `UnityDebugger.*` because it is product-neutral and
changing it would provide no user value.

## Manifest and Extension Host

`package.json` will use the final publisher, package name, display name, and
description. The debugger contribution will register only
`unity-debugger-pure`, with this attach snippet:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Activation events, configuration validation, session filtering, model types,
and extension tests will use the new debug type.

The commands will be renamed consistently:

- `unity-debugger-pure.refreshTargets`
- `unity-debugger-pure.openLogs`
- `unity-debugger-pure.copyDiagnostics`

Their visible titles will begin with `Unity Debugger Pure:`.

No compatibility alias for `unity-community` will be registered. The extension
has not been publicly released, so supporting both identities would add
permanent complexity without protecting an installed public user base.

## Adapter and Diagnostics

The project-built Adapter executable, staging path references, launcher,
runtime inventory, package allowlist, and hash checks will use
`UnityDebuggerPure.exe`.

The managed Adapter support-policy URL will point to:

```text
https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure#support-policy
```

The support-URL build test will continue deriving the expected Marketplace item
ID from `package.json`, preventing a future publisher or package rename from
leaving a stale Adapter URL.

Sanitized diagnostics will move from
`%LOCALAPPDATA%\unity-debugger-vscode\logs` to
`%LOCALAPPDATA%\unity-debugger-pure\logs`. Existing development logs will not
be migrated or deleted. Diagnostic privacy, redaction, retention, and
no-telemetry behavior remain unchanged.

## Documentation and Release Automation

Current public-facing materials will use the final product identity:

- `README.md`
- `CHANGELOG.md`
- manifest command titles and descriptions
- diagnostics user messages where a product name is shown
- CI artifact names
- Marketplace and release workflows
- package and VSIX verification scripts
- build, extension, package, and integration tests

Historical specifications and implementation plans retain their original
identifiers because they describe decisions and code at the time they were
written. This new design is the authoritative identity specification.

The repository directory, solution name, source namespaces, third-party
notices, and support matrix are not renamed.

## Installed Extension Migration

Changing both publisher and package name creates a new VS Code extension ID.
Installing `kpk.unity-debugger-pure` will not replace
`unity-debugger-community.unity-debugger-vscode`.

Final installation therefore follows this order:

1. Build and audit the new VSIX.
2. Ask the user to stop the active MyGame debug session.
3. Uninstall the old extension ID.
4. Install the new VSIX.
5. Compare the staged and installed `UnityDebuggerPure.exe` SHA-256 values.
6. Reload only the VS Code window opened from MyGame.

No separate VS Code profile or Unity/Tuanjie project will be created.

The existing MyGame launch configuration, if present, will update only the
debugger entry being migrated:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Unrelated MyGame configuration and all MyGame source files remain untouched.

## Error Handling

- Packaging must fail if any old executable or VSIX path remains in the
  production allowlist.
- Build tests must fail if the manifest identity, contributed debug type,
  command prefix, support URL, or configuration snippet is inconsistent.
- Runtime inventory verification must fail until the renamed Adapter hash and
  path are committed.
- Marketplace publication remains manually approved and must fail if the
  configured publisher does not exactly equal `kpk`.
- If the old extension cannot be uninstalled or the new installed Adapter hash
  differs, real-Editor acceptance stops before attaching.
- No Marketplace publishing, Git push, or tag is authorized by this migration.

## Test Strategy

Implementation uses test-driven development:

1. Update identity and packaging contract tests first and observe failure
   against the old brand.
2. Update extension tests for the new debug type, command IDs, default
   configuration, and diagnostics directory.
3. Rename production identity and Adapter artifacts until focused tests pass.
4. Run the full build, extension, Adapter, process-integration, and VSIX
   package suites.
5. Audit the VSIX file list and runtime inventory.
6. Install the exact new VSIX and hash-verify the installed Adapter.

Real acceptance uses only the existing
`H:\workspace\Unity\Tuanjie\Projects\MyGame` Editor and its VS Code window. The
user will verify:

- the Run and Debug configuration is named
  `Attach to Unity Debugger Pure`;
- attach and the already-proven reachable DevTools breakpoint still work;
- Pause does not open an invalid source;
- Hover, Watch, and Debug Console evaluation still work;
- a script reimport preserves the breakpoint through Domain Reload;
- Continue and disconnect complete normally.

## Out of Scope

- Renaming the Git repository or its local directory
- Changing the supported Tuanjie/Unity versions
- Adding remote Player, mobile, IL2CPP, or Unity 6 guarantees
- Adding aliases for the old extension or debug type
- Publishing to Marketplace, pushing Git, or creating a release tag
