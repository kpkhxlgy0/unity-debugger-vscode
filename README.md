# Unity Debugger Pure

An independent, community-maintained VS Code debugger for managed C# code in
local Unity 2022 and Tuanjie Editors. This project is not affiliated with,
endorsed by, or supported by Unity Technologies or Unity China.

It provides Unity Editor attach without requiring C# Dev Kit or Microsoft's
Unity extension. ReSharper for VS Code can remain installed for code analysis,
navigation, and refactoring; this extension owns only the
`unity-debugger-pure` debug type. Its Marketplace extension ID is
`kpk.unity-debugger-pure`.

## Install

Install **Unity Debugger Pure** from one of these sources:

- [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure)
  for VS Code;
- [Open VSX](https://open-vsx.org/extension/kpk/unity-debugger-pure) for
  Open VSX-compatible editors;
- Cursor's Extensions view, which obtains this extension from Open VSX.

For sideloading, download `unity-debugger-pure-0.2.0.vsix` from the
[GitHub Releases page](https://github.com/kpkhxlgy0/unity-debugger-vscode/releases),
run **Extensions: Install from VSIX...**, and select that exact file.

Before attaching, set the Editor's **Code Optimization** mode to **Debug** and
open the Unity/Tuanjie project root in VS Code.

Use **Run and Debug: Add Configuration...** and select the provided attach
configuration:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Start debugging with F5. If more than one supported local Editor is running,
VS Code asks which instance to attach to.

## Features

- local Windows x64 Editor discovery and attach;
- source and conditional breakpoints;
- step in, step over, step out, pause, and continue;
- threads, call stacks, locals, expandable variables, watch, and REPL
  evaluation;
- all-exception and uncaught-exception break modes;
- breakpoint recovery across Domain Reload;
- sanitized local diagnostics with no telemetry.

Version 0.2.0 exposes a versioned local extension API for trusted local
clients. API targets are opaque, restricted to the current workspace, and do
not change normal interactive debugging.

## Implicit evaluation

`unityDebuggerPure.enableImplicitEvaluation` defaults to `true`. It allows
Hover, Locals, and variable expansion to invoke property getters and target
`ToString()` while the Editor is paused. These calls execute code in the debug
target and can cause side effects or delays.

Disable it in User settings to change the default for every project, or in
Workspace / Workspace Folder settings to override a specific project:

```json
{
  "unityDebuggerPure.enableImplicitEvaluation": false
}
```

Workspace Folder values override Workspace values, which override User values.
Changes apply on the next Attach. Watch and Debug Console evaluation remain
explicit regardless of this setting.

## Support policy

<a id="support-policy"></a>

Formally supported:

- Windows 10/11 x64;
- Tuanjie `2022.3.62t11`;
- local Editor attach;
- Mono managed C# debugging.

Unity `2022.3` LTS versions are accepted for compatibility testing. Unity 6
is allowed to connect but is unverified and is not guaranteed. The Adapter
shows one warning for versions outside the formal baseline.

Currently excluded: remote Player attach, Android/iOS, IL2CPP, WebGL,
Tuanjie Mini Game DebugProxy, native C++, multiple simultaneous Editors, and
non-Windows platforms.

## Security and privacy

Version `0.2.0` automatically connects only to a discovered loopback address.
It does not accept arbitrary executables or remote debug hosts from
`launch.json`. Unity's managed debug transport has no strong authentication,
so future remote support will require explicit opt-in.

The extension has no telemetry. Diagnostics are written under
`%LOCALAPPDATA%\unity-debugger-pure\logs`, retain five files, redact user
profile/workspace paths, and never record source text, expressions, evaluation
results, or variable values.

Use:

- **Unity Debugger Pure: Open Sanitized Diagnostics**
- **Unity Debugger Pure: Copy Sanitized Diagnostics**

When reporting an issue, include only the sanitized diagnostics plus Editor,
extension, Windows, and reproduction information. Do not paste project source
or raw variable data.

## Build from source

Requirements: Windows x64, Node.js `26.5.0`, .NET SDK `10.0.x`, and npm.

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm test
npm run package
```

The package pipeline builds the Adapter from audited source, stages production
assemblies only, verifies the committed runtime inventory, audits VSIX paths
and hashes, and emits a SHA-256 file.

## License

Project code is MIT licensed. The bundled `THIRD_PARTY_NOTICES.md` contains
pinned upstream sources and runtime dependency notices.
