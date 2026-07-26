# Pause Source Handling and Safe Hover Evaluation

**Status:** Approved design

**Date:** 2026-07-26
**Scope:** Version 0.1.0 real-Editor acceptance fixes

## 1. Purpose

Close two gaps found while testing the packaged debugger against the supported
Tuanjie `2022.3.62t11` Editor:

1. Pausing execution can stop in a Unity or framework frame whose source file
   is not present in the workspace. The Adapter currently emits a synthetic
   DAP `Source` with neither a path nor a source reference. VS Code then tries
   to open that source and its editor fails.
2. Locals, Watch, and Debug Console evaluation work, but data-tip Hover is
   disabled by the Adapter even for ordinary local variables and fields.

The change must preserve the existing loopback-only attach boundary,
diagnostic redaction, and explicit Watch/REPL evaluation behavior.

## 2. Confirmed Evidence

- The supported Editor process and `UnityCommunityDebug` process remained
  alive after the Pause failure.
- The VS Code renderer recorded a text-model creation exception at the time
  the Pause result tried to open an unavailable frame.
- Breakpoint stops open MyGame source correctly because their selected frame
  has a workspace path.
- The Adapter advertises `supportsEvaluateForHovers = false` and explicitly
  rejects `evaluate` requests whose context is `hover`.
- Watch and REPL evaluation already succeed in the same stopped frame.

## 3. Behavior

### 3.1 Frames without workspace source

The Adapter keeps every managed frame in the Call Stack, including Unity and
framework frames whose source is unavailable. A frame with no mapped workspace
file:

- includes its frame ID, name, line, column, and a deemphasized presentation
  hint;
- omits the optional DAP `source` property completely;
- is visible but is not presented as an openable source location.

A frame whose source maps to an existing file under the workspace continues to
include its normal `source` name and path and remains openable.

The Adapter does not filter internal frames, download source, create virtual
documents, or manufacture a positive `sourceReference` in this change.

### 3.2 Safe Hover

The Adapter advertises `supportsEvaluateForHovers = true`.

Evaluation contexts map to backend modes as follows:

| DAP context | Backend mode | Target invocation |
|---|---|---|
| `hover` | Safe | Disabled |
| `watch` | Explicit | Enabled |
| `repl` | Explicit | Enabled |
| anything else | Rejected | Not called |

Safe Hover supports expressions that the Mono evaluator can resolve without
executing target code, including local variables, parameters, ordinary fields,
`this`, and field access such as `this._isVisible`.

Safe Hover disables:

- property getter invocation;
- method evaluation;
- target invocation;
- implicit target `ToString()` calls.

When an expression requires disabled behavior, Hover returns no value through
the normal sanitized evaluation-error path. The error does not include the
expression, source text, or a project path.

Watch and REPL retain their current explicit behavior. This change does not
weaken their existing user-initiated evaluation semantics.

## 4. Components and Interfaces

### 4.1 DAP stack-frame model

Add a project-owned stack-frame response model whose `source` member is
nullable and omitted during JSON serialization when unavailable. This avoids
changing the audited vendored DAP source files.

`UnityDebugSession.StackTrace` constructs:

- the project-owned model with a normal `DapSource` for mapped workspace
  files;
- the same model with no `source` for unavailable files.

### 4.2 Evaluation intent

Add a backend evaluation-mode value with exactly two states:

- `Safe`
- `Explicit`

Pass the mode through `IDebuggerBackend.Evaluate` and the Mono session facade.
The facade selects:

- its existing safe evaluation options for Hover;
- its existing explicit evaluation options for Watch and REPL.

No expression parser or allowlist is added to the DAP layer. Safety is enforced
by the debugger library's evaluation options at the point where target code
could be invoked.

## 5. Data Flow

### Pause

1. VS Code sends `pause`.
2. The Adapter pauses the selected managed thread and sends a `stopped` event.
3. VS Code requests threads and stack frames.
4. Each runtime source path is checked against the workspace.
5. Workspace frames include `source`; unavailable frames omit it.
6. VS Code displays the Call Stack without attempting to open an invalid
   editor input.

### Hover

1. The user pauses on a managed frame and points at an expression.
2. VS Code sends `evaluate` with context `hover`.
3. The Adapter resolves the DAP frame handle and requests backend evaluation
   in `Safe` mode.
4. The Mono facade evaluates with target invocation, methods, and `ToString()`
   disabled.
5. A safe value is returned using the existing bounded variable-handle path;
   an unsafe or invalid expression returns a sanitized failure.

## 6. Error Handling and Privacy

- Hover remains unavailable while running, during Domain Reload, or after its
  frame handle becomes stale.
- Missing source is not an Adapter error; it is represented by the absence of
  a DAP `source`.
- Evaluation failures use the existing generic message and never echo the
  submitted expression.
- Normal logs do not record expressions, values, source contents, or full
  workspace paths.
- No telemetry, remote connection, project command, or Editor automation is
  added.

## 7. Testing

### Automated

1. Initialization advertises Hover support.
2. `hover` calls the backend once with `Safe` mode.
3. `watch` and `repl` call the backend with `Explicit` mode.
4. Unknown evaluation contexts are rejected without a backend call.
5. Evaluation failures remain sanitized.
6. A mapped stack frame serializes a normal `source`.
7. An unavailable stack frame serializes with no `source` property.
8. A Pause scenario can return an unavailable top frame plus a mapped project
   frame, after which Continue still succeeds.
9. All existing build, Adapter, extension, integration, and VSIX audits pass.

Each production behavior is introduced through a failing regression test
before its implementation.

### Supported real Editor

Using only the existing MyGame Editor and its VS Code window:

1. Attach while MyGame is in Play Mode and click Pause.
2. Confirm that VS Code no longer shows an editor-open failure.
3. Confirm that internal frames remain visible but are not openable.
4. Select a MyGame frame and confirm that its source opens.
5. Continue execution successfully.
6. Stop at the previously verified reachable `DevGmTestPanel` path.
7. Hover `_isVisible` and `this._isVisible` and confirm their values appear.
8. Confirm Watch and Debug Console evaluation still work.

Computer Use is not required; the user performs these UI steps manually.

## 8. Non-Goals

- Evaluating properties or methods from Hover
- Setting variables
- Source download or source-server support
- Virtual source documents for Unity/framework frames
- Filtering or hiding internal managed frames
- Changing Watch or REPL safety policy
- Expanding platform, Editor-version, or remote-debug support

## 9. Acceptance Criteria

- Pause never supplies a pathless, zero-reference synthetic source to VS Code.
- Pause exposes a usable Call Stack and Continue remains functional.
- `_isVisible` and equivalent safe field expressions display through Hover.
- Hover cannot invoke getters, methods, target code, or target `ToString()`.
- Existing explicit evaluation, stepping, breakpoints, and Domain Reload
  behavior remain passing.
- Automated and supported real-Editor verification both pass.
