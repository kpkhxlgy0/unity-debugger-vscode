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

For sideloading, download `unity-debugger-pure-0.3.2.vsix` from the
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
  "request": "attach",
  "projectPath": "${workspaceFolder}"
}
```

Start debugging with F5. If more than one supported local Editor is running,
VS Code asks which instance to attach to.

**Unity Debugger Pure: Attach Unity Debugger** lists the Unity Editors and
Players available for debugger attachment; pick one to start a session.

A Player, or an Editor that discovery cannot see, can be reached by naming its
debugger endpoint explicitly:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach",
  "endPoint": "127.0.0.1:56321"
}
```

`projectPath` defaults to the workspace folder and selects the project the
debugger maps sources against. `logFile` redirects this session's sanitized
diagnostics log to a path you choose.

## Features

- local Windows x64 Editor discovery and attach, plus Player attach by an
  explicit `endPoint`;
- **Unity Debugger Pure: Attach Unity Debugger**, which lists the Unity Editors
  and Players available for debugger attachment;
- source and conditional breakpoints, with the reference debugger's warning
  when function breakpoints are in use;
- step in, step over, step out, pause, and continue;
- threads, call stacks, locals, expandable variables, watch, and REPL
  evaluation;
- all-exception and user-unhandled-exception break modes;
- breakpoint rebinding across Domain Reload: the mature debugger session owns
  rebinding, rather than a reload-specific mechanism in the adapter;
- **Unity Debugger Pure: Unity API Reference**, which opens the Unity
  documentation for the selected code construct;
- a `Unity Debugger Pure` output channel, and the
  `unity-debugger-pure.displayBackgroundProcesses` setting — contributed for
  manifest compatibility, and inert until the adapter emits the background
  classification it filters on;
- sanitized local diagnostics with no telemetry.

Version 0.3.2 exposes a versioned local extension API for trusted local
clients. API targets are opaque, restricted to the current workspace, and do
not change normal interactive debugging.

## Implicit evaluation

Hover, Locals, variable expansion, Watch, and Debug Console use the mature
debugger session's built-in evaluation behavior. Property getters and target
`ToString()` calls can execute code in the paused debug target and may have
side effects or delays. There is no separate implicit-evaluation setting.

## Support policy

<a id="support-policy"></a>

Formally supported:

- Windows 10/11 x64;
- Tuanjie `2022.3.62t11`;
- Mono managed C# debugging;
- attaching to a discovered local Editor, and to an explicit `host:port` endpoint —
  including a Player's, since the reference reaches a Player by probing the
  device for its debugger port and no advertisement observed so far carries one.

Unity `2022.3` LTS versions are accepted for compatibility testing. Unity 6
is allowed to connect but is unverified and is not guaranteed. The Adapter warns
for versions outside the formal baseline, and separately — naming no baseline —
when the Editor version cannot be determined at all.

Currently excluded: IL2CPP, WebGL, Tuanjie Mini Game DebugProxy, native C++,
multiple simultaneous Editors, and non-Windows platforms.

Android and iOS are a different case rather than the same one. The path exists —
a debug-enabled Player advertisement, including an Android one, is discoverable —
but attaching to one has never been verified end to end, so no Android or iOS
attach is claimed.

## Security and privacy

Attaching to an endpoint other than a discovered loopback one is supported: an
explicit `endPoint` in `launch.json` may name any `ip:port`, including one on
another machine.

**Unity's managed debug transport has no authentication.** Anyone who can reach
the debugger port can attach to that Editor or player, and an attached debugger
can execute code in the target. An endpoint exposed on a network interface is
therefore reachable by anyone who can route to it. The port is derived
deterministically from the Editor's process id (`56000 + pid % 1000`), so it is
guessable rather than secret. Prefer loopback, and expose a remote endpoint only
on a network you trust and control. The extension adds no authentication, and
this section is not a guarantee that a remote endpoint is safe to expose.

The extension has no telemetry — the reference debugger reports usage, and this
one does not, by design. Diagnostics are written under
`%LOCALAPPDATA%\unity-debugger-pure\logs` (or to the configured `logFile` path),
retain five files, redact user profile and workspace paths, and never record
source text, expressions, evaluation results, or variable values.

Use:

- **Unity Debugger Pure: Open Sanitized Diagnostics**
- **Unity Debugger Pure: Copy Sanitized Diagnostics**

When reporting an issue, include only the sanitized diagnostics plus Editor,
extension, Windows, and reproduction information. Do not paste project source
or raw variable data.

## Build from source

Requirements: Windows x64, Node.js `26.5.0`, .NET SDK `10.0.401` (pinned in `global.json`), and npm.

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm test
npm run package
```

The real Mono argument-evaluation regression tests use a standalone console
fixture and do not open an Editor. Set `UNITY_DEBUGGER_TEST_MONO` to an existing
`mono.exe` (for example, the Editor's `Data/MonoBleedingEdge/bin/mono.exe`) before
`npm run test:adapter` to include them. Without this executable, those tests
are reported as skipped. Their temporary fixture files and processes are
removed after each test.

The package pipeline builds the Adapter from audited source, stages production
assemblies only, verifies the committed runtime inventory, audits VSIX paths
and hashes, and emits a SHA-256 file.

## License

Project code is MIT licensed. The bundled `THIRD_PARTY_NOTICES.md` contains
pinned upstream sources and runtime dependency notices.
