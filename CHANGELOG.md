# Changelog

## 0.4.0

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

## 0.3.0

- Replace the MonoDevelop debugger backend with a direct Mono soft-debugger
  engine aligned to the installed Unity debugger's stop, step, breakpoint,
  exception, and evaluation behavior.
- Preserve pending breakpoints across Unity Domain Reload and report the first
  stopped location correctly to Cursor.
- Enable implicit Getter and `ToString()` evaluation by default for Hover,
  Locals, and variable expansion, with workspace and user settings support.
- Add a packaged build identity to diagnostics and remove obsolete
  Mono.Debugging and NRefactory runtime dependencies.

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
