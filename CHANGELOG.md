# Changelog

## Unreleased

User-visible alignment with Microsoft's Visual Studio Tools for Unity for VS Code
(`VisualStudioToolsForUnity.vstuc`) 1.3.2, on the reference-compatibility matrix
in `docs/reference-debugger-compatibility.md`. **No version bump**: the release
identity is a separate, coordinated decision, and the `0.3.2` entry below
describes the artifact that was published and verified, so it is left as it was.

- Add **Unity Debugger Pure: Attach Unity Debugger**, which lists the Unity
  Editors and Players available for debugger attachment and starts a session
  against the one selected.
- Surface Player targets: a debug-enabled Player advertisement becomes a
  selectable candidate instead of only a port source for the open project's
  Editor.
- Add the `endPoint` attach property, so a Player — or an Editor discovery cannot
  see — can be reached by naming `host:port` explicitly, including a non-loopback
  endpoint.
- Add the `projectPath` attach property, defaulting to the workspace folder, and
  honour `logFile` by writing the session's sanitized diagnostics to a chosen
  path.
- Relax `configurationAttributes.attach.required` to `[]`, matching the
  reference; the adapter still validates what it needs at attach time.
- Add **Unity Debugger Pure: Unity API Reference**, which opens the Unity
  documentation for the selected code construct.
- Add the `unity-debugger-pure.displayBackgroundProcesses` setting, and sort the
  attach picker's items non-background-first.
- Warn when function breakpoints are in use, in the reference debugger's words.
- Replace the single discovery-failure message with the reference's three
  distinct ones, and stop returning an empty target list silently when
  `ProjectSettings/ProjectVersion.txt` is missing or
  `Library/EditorInstance.json` records a process that is no longer alive.
- Warn when the deprecated `unity.unity-debug` extension is active.
- Add a `Unity Debugger Pure` output channel for extension-side diagnostics, and
  keep its boundary path-free.
- Align the adapter's advertised capabilities with the reference: drop the legacy
  `supportsExceptionOptions`, add `supportsExceptionFilterOptions` and
  `supportsExceptionConditions`, accept exception filter ids from
  `filterOptions[]`, rename the user-unhandled filter id from `uncaught` to
  `unhandled` while still accepting the old id, add both filters' descriptions,
  and report the adapter's process id.
- Delete the dead domain-reload and reconnect methods and events: rebinding is
  owned by the mature debugger session.
- State the remote-attach security posture plainly — an explicit non-loopback
  `endPoint` is supported, and Unity's managed debug transport has no
  authentication.

## 0.3.2 - 2026-09-24

- Keep unrelated threads suspended while Watch methods, getters, and ToString
  execute, matching the reference debugger's single-threaded invocation mode.

- Accept integral arguments for an unambiguous enum-parameter method when
  ordinary overload matching fails, preserving normal overload priority and
  evaluating each argument only once.
- Resolve Watch and Hover type names through ordinary `using` directives and
  type aliases visible at the stopped source location, using the existing
  Roslyn runtime and mature expression evaluator.
- Preserve the client-requested source path and path format in breakpoint
  status updates, including files outside the attached workspace, so binding
  and reload updates do not erase the breakpoint filename.

## 0.3.0

- Replace the handwritten execution-control and expression-evaluation engines
  with the pinned mature Mono.Debugging, Mono.Debugging.Soft, and NRefactory
  session stack.
- Make Getter and `ToString()` evaluation part of the mature debugger behavior
  and remove the obsolete implicit-evaluation setting and Attach flag.
- Preserve the reference debugger's source/function breakpoint, exception,
  stepping, Step Into Target, Goto, and Set Variable capabilities through the
  mature backend.
- Track reference-plugin compatibility and MyGame acceptance evidence in
  `docs/reference-debugger-compatibility.md`; full parity is not claimed before
  final same-project acceptance.

## 0.2.0

- Expose a versioned local extension API for trusted local clients; public API
  targets are opaque and local only.
- Keep the public API isolated from normal interactive debugging.

## 0.1.1

- Add the Unity Debugger Pure extension icon to Marketplace and Open VSX
  packages.

## 0.1.0

- Initial Windows x64 release of Unity Debugger Pure.
- Local Tuanjie `2022.3.62t11` and Unity 2022.3 Editor attach.
- Managed breakpoints, inspection, evaluation, execution control, and
  exception break modes.
- Domain Reload breakpoint recovery and bounded local reconnect.
- Allowlisted, path-redacted diagnostics with no telemetry.
