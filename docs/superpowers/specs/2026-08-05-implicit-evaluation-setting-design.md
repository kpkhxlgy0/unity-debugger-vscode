# Implicit Evaluation Setting

**Status:** Approved design

**Date:** 2026-08-05
**Scope:** Hover, Locals, and variable expansion evaluation policy

## 1. Purpose

Make implicit evaluation enabled by default so property getters and target
`ToString()` calls can produce values in Hover, Locals, and expanded variables.
Provide a standard Cursor/VS Code setting that can restore the current safe,
non-invoking behavior.

Watch and Debug Console evaluation remain explicit and are not controlled by
this setting.

## 2. User Setting

Add the boolean setting:

`unityDebuggerPure.enableImplicitEvaluation`

- Default: `true`.
- Scope: `resource`, so Cursor supports User, Workspace, and Workspace Folder
  values through its normal configuration system.
- Effective precedence: Workspace Folder, Workspace, User, then the extension
  default.
- Changes take effect on the next Attach session.

The setting description must warn that implicit evaluation can execute code in
the debug target and may cause side effects or delays.

## 3. Behavior

When the effective setting is `true`:

- Hover uses invoking evaluation options.
- Locals and variable expansion use invoking evaluation options.
- Property getters, method evaluation required by the debugger library, target
  invocation, and target `ToString()` calls are enabled.

When the effective setting is `false`:

- Hover, Locals, and variable expansion use the existing Safe evaluation mode.
- Property getters, method evaluation, target invocation, and target
  `ToString()` calls are disabled.

Watch and Debug Console always use Explicit evaluation mode in either case.

## 4. Data Flow

1. The debug configuration provider reads the effective setting for the folder
   being attached.
2. It adds the resolved boolean as a private Attach argument and does not copy
   arbitrary launch configuration fields.
3. The Adapter validates the private argument. If an older client omits it,
   the Adapter uses the new default of `true`.
4. The debug session stores the resolved policy for the lifetime of that Attach
   session.
5. Hover, Locals, and variable expansion select Explicit or Safe backend
   evaluation mode from the stored policy.

Changing the setting does not mutate a running Adapter session. Reattaching
creates a session with the new effective value.

## 5. Components

- `package.json` contributes the setting with a `resource` scope and default
  `true`.
- The TypeScript extension reads the resource-scoped setting and emits a
  private, typed Attach argument.
- `AttachArguments` validates the optional boolean and defaults it to `true`.
- `UnityDebugSession` applies the selected mode to Hover, Locals, and variable
  expansion.
- The backend and Mono facade accept the evaluation mode for scope and variable
  inspection without introducing global mutable evaluation options.

## 6. Safety and Errors

Implicit evaluation is an intentional opt-in-by-default behavior requested for
this extension. Because Mono exposes the required behavior through a coarse set
of evaluation flags, enabling getters and `ToString()` also permits target and
method invocation required by those evaluations.

Existing evaluation timeouts and sanitized DAP failures remain unchanged.
Malformed private Attach values are rejected. No expression or evaluated value
is added to logs.

## 7. Testing

Automated tests cover:

1. The manifest setting name, boolean type, `resource` scope, default `true`,
   and risk description.
2. User/workspace-effective values being passed by the configuration provider.
3. The Adapter defaulting an omitted private argument to `true`, accepting both
   boolean values, and rejecting malformed values.
4. Hover, Locals, and variable expansion selecting Explicit mode when enabled
   and Safe mode when disabled.
5. Watch and Debug Console remaining Explicit when the setting is disabled.
6. Existing build, extension, Adapter, integration, and VSIX tests continuing
   to pass.

Production behavior is introduced through failing tests before implementation.

## 8. Acceptance Criteria

- A clean installation implicitly evaluates Hover, Locals, and expanded
  variables without extra configuration.
- User, Workspace, and Workspace Folder settings are supported with Cursor's
  standard precedence.
- Setting the value to `false` and reattaching restores safe automatic
  inspection.
- Watch and Debug Console behavior does not regress.
- The rebuilt VSIX passes repository verification and is installed into Cursor
  with an installed Adapter hash matching the packaged build.
