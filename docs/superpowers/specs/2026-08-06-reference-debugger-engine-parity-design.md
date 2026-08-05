# Reference Debugger Engine Parity Design

## Goal

Replace UnityDebuggerPure's general-purpose MonoDevelop debugging backend with
a Unity-specific engine built directly on the MIT-licensed
`Mono.Debugger.Soft` protocol layer. The installed Unity debugger is the sole
behavioral reference for debug-session behavior, timing, error handling, and
variable presentation.

The result must make breakpoint stops, rapid stepping, Hover, Locals, Watch,
property getters, `ToString()`, enum expressions, and Domain Reload feel as
close to the reference debugger as the shared Mono Soft Debugger protocol
allows.

## Scope

The project implements debugger functionality only.

In scope:

- Unity discovery and attach.
- Source and function breakpoints, conditional breakpoints, logpoints, and
  exception breakpoints.
- Continue, Pause, Step Into, Step Over, Step Out, Step-in Targets, and Goto.
- Threads, stack traces, scopes, Locals, Watch, REPL evaluation, Hover,
  Set Variable, and expandable variables.
- Getter and `ToString()` invocation.
- Primitive, string, enum/Flags, struct, object, pointer, array, list,
  dictionary, enumerable, debugger-display, debugger-proxy, and Unity-specific
  value presentation.
- Domain Reload, module lifecycle, source mapping, breakpoint unbinding, and
  rebinding.
- User- and workspace-scoped implicit-evaluation control.

Permanently out of scope:

- Code analysis and diagnostics unrelated to an active debug session.
- Completion, Unity Message completion, and project-system features.
- Shader, UXML, USS, or other language services.
- Any attempt to reproduce the reference extension outside its debugger.

## Parity Authority

The installed reference debugger defines observable behavior. When an
implementation choice is ambiguous, capture the reference debugger's DAP and
UI behavior in `D:\workspace\sgproj`, write a failing parity test, and implement
that result. Do not introduce retries, queues, state transitions, timeouts, or
fallbacks merely because they appear locally preferable.

The reference extension's proprietary assemblies are not build or runtime
dependencies and are not redistributed. UnityDebuggerPure uses documented and
MIT-licensed Mono Soft Debugger APIs and implements compatible behavior in
project-owned source.

## Architecture

```text
Cursor
  | DAP
  v
UnityDebugSession
  | DAP model translation only
  v
UnityDebuggerEngine
  +-- UnityConnection
  +-- EventDispatcher
  +-- StepManager
  +-- BreakpointManager
  +-- ExceptionManager
  +-- SourceMapManager
  +-- SuspendedState
  +-- EvaluationService
  +-- Unity property/value model
  |
  v
Mono.Debugger.Soft
  |
  v
Unity Mono Runtime
```

`UnityDebugSession` is a thin DAP adapter. It does not own a second execution
state machine and does not infer whether the target is running.

`UnityDebuggerEngine` owns one connected Mono virtual machine. Its event queue
is the only source of stop, thread, module, and termination truth.

The adapter remains on .NET Framework 4.8 for this rewrite. A runtime migration
is allowed only if a failing parity test proves that the runtime or DAP
dispatcher prevents reference behavior. It is not bundled with the engine
rewrite speculatively.

The compiled adapter and package remove `Mono.Debugging` and
`Mono.Debugging.Soft`. The existing `Mono.Debugger.Soft` source remains the
low-level protocol implementation.

## Component Boundaries

### UnityConnection

`UnityConnection` connects, detaches, and exposes the Mono virtual machine. It
does not translate DAP requests, evaluate expressions, or synthesize debugger
events.

### EventDispatcher

`EventDispatcher` is the sole consumer of Mono event sets. It dispatches
asynchronous thread/log events without suspending the target and processes
breaking events with Mono suspend-count semantics. It preserves the order of
Step, Breakpoint, Exception, UserBreak, type-load, domain, assembly, and VM
events.

Only real Step, Breakpoint, Exception, and UserBreak events create DAP
`stopped` events. A DAP request response never creates an optimistic stop or
running state.

### StepManager

`StepManager` owns at most one enabled Mono `StepEventRequest`. A new step
request cancels the existing request, creates a line-level Into, Over, or Out
request, applies the reference debugger's assembly and debugger-attribute
filters, enables it, and resumes the VM.

Continue cancels the current step request and resumes. Duplicate or late
Resume operations that the reference engine treats as harmless do not produce
user-visible warnings. There is no custom rapid-click queue, command
coalescing, delayed retry, or fixed cancellation wait.

### SuspendedState

`SuspendedState` owns frame, property, code-path, and code-context handles for
the current stop generation. Matching the reference debugger, handles are
reset when the next real stop arrives, not when a Step or Continue request is
accepted.

Missing or stale handles produce the same empty successful DAP response as the
reference debugger. Handle IDs never drive execution state.

### EvaluationService and property/value model

`EvaluationService` parses C# expressions with Roslyn and evaluates syntax
against a stack-frame environment. The environment exposes `this`, arguments,
locals, constants, closure fields, async/iterator state, and supported Unity
main-thread values.

Every Evaluate, Variables, property getter, and property-info operation owns a
request-scoped cancellation token. There is no process-wide
`AsyncOperationManager`, and execution control does not synchronously cancel a
global set of evaluations.

The property/value model provides lazy children and reference-compatible
formatting for:

- primitives, strings, enums, Flags, structs, objects, and pointers;
- arrays with buckets, lists, dictionaries, and enumerable Results View;
- public, non-public, static, base, and Raw View members;
- `DebuggerDisplay` and debugger proxy values;
- Unity Object, Component/GameObject, Scene, GameObject children, and other
  debugger-only Unity presentations supported by the reference engine.

## Attach and Event Flow

Before event consumption begins, Attach creates the engine state, managers,
source map, and event dispatcher. Adapter initialization, Attach responses,
and engine events share an ordered protocol writer.

A breakpoint that arrives immediately during Attach is emitted as a normal
`stopped` event even if the Attach response has not yet been processed by the
client. No independent DAP-side state gate may drop it.

Each real stop:

1. resets `SuspendedState`;
2. maps the real Mono thread;
3. records the stop reason and breakpoint IDs where applicable;
4. emits one DAP `stopped` event with `allThreadsStopped=true`.

Cursor's current-statement marker is driven only by that stopped event.
Late control responses or resume notifications cannot overwrite a newer stop.

## Execution Control

Step Into, Step Over, and Step Out map directly to Mono line-level step
requests. The DAP request returns after the engine accepts the request; it does
not wait for a future StepEvent or for evaluation cleanup.

Rapid Step requests pass through the same ordered control path. A request may
replace the current Mono step request exactly as in the reference engine, but
the adapter does not invent queued future steps.

Continue cancels an active step and resumes. Pause suspends the VM and emits a
Pause stop using real threads. Benign already-running/already-resumed states
are ignored only where the reference engine ignores them.

## Evaluation and Variable Semantics

Scopes registers one lazy Locals root. Variables expands only the requested
property level and registers expandable children in `SuspendedState`.

The default Variables and Evaluate timeout is 10 seconds. `ToString()` waits a
maximum of 2 seconds. Target invocations use
`DisableBreakpoints | SingleThreaded`.

`ToString()` timeout, failure, or an unoverridden object/value implementation
falls back to `{TypeName}`. The fallback is final for that request; it is not a
background placeholder and is not updated asynchronously. A later Hover is a
new request.

Getter exceptions become an error value for the property. They do not fail the
entire Variables response. Variables timeout or an unavailable property
returns an empty variable list. A stale frame produces empty Scopes/Evaluate
responses. Syntax or expression-evaluation failures affect only the current
Evaluate request.

Enum constants preserve their enum type. Enum equality and inequality compare
compatible enum values, so expressions such as
`currentState == ButtonState.Normal` evaluate without falling back to a generic
expression failure. Flags formatting matches the reference debugger.

The setting `unityDebuggerPure.enableImplicitEvaluation` defaults to `true`
and supports user, workspace, and workspace-folder scopes. This is the one
intentional debugger addition beyond the reference's fixed implicit behavior.
When disabled, automatic Hover, Locals, and Variables expansion do not invoke
getters or `ToString()`. Explicit Watch and REPL evaluation remain enabled.

## Breakpoints and Domain Reload

Pending breakpoints outlive AppDomains. Bound breakpoints belong to one loaded
domain and one Mono breakpoint request.

TypeLoad events populate source maps and attempt to bind pending source and
function breakpoints. Domain unload removes that domain's bound breakpoints and
modules without deleting the user's pending breakpoint. Subsequent type loads
bind it again and emit the corresponding breakpoint-change event.

Breakpoint hits are ignored and resumed when they cannot map to debuggable
user code, fail a condition, or are logpoints. Conditional evaluation uses the
same expression engine. A condition error stops and reports the error as the
reference debugger does. Logpoints evaluate, emit output, and resume without a
normal breakpoint stop.

Exception filtering, caught/unhandled behavior, and exception-type conditions
match the reference debugger.

## DAP Capabilities

The adapter advertises the reference debugger's supported capabilities:

- conditional and function breakpoints;
- logpoints;
- Hover evaluation;
- Set Variable;
- Step-in Targets;
- Goto;
- Terminate;
- exception conditions and filter options.

It does not advertise data or instruction breakpoints, step back, restart,
memory operations, stepping granularity, hit-conditional breakpoints,
single-thread execution, or other capabilities absent from the reference.

## Error Handling and Diagnostics

Benign duplicate Resume and equivalent reference-ignored engine states do not
produce user warnings. Getter exceptions are property values. Parse and
evaluation failures are scoped to their request. VM connection failure,
unexpected disconnect, and protocol corruption are session-fatal.

Termination is idempotent and emits one DAP `terminated` event.

Diagnostic logs contain the build ID, request kind, stop generation, event
ordering, durations, and exception type. They do not contain expression text,
variable values, source contents, or unredacted user paths. The extension has
no telemetry.

## Verification

All production behavior is developed test-first. A failing test must establish
the reference behavior before each implementation change.

Automated verification includes:

1. Engine state tests for attach-first-stop, all stop reasons, rapid Step,
   event ordering, duplicate Resume, and termination.
2. Evaluation tests for enum comparison, getters, getter exceptions,
   `ToString()` fallback, request timeout, lazy expansion, and stale handles.
3. Lifecycle tests for domain unload, breakpoint unbind/rebind, source mapping,
   conditional breakpoints, logpoints, and exception filters.
4. DAP transcript and packaging tests for ordered output, writer integrity,
   capabilities, setting scopes, dependency inventory, and the absence of
   `Mono.Debugging*.dll` in the final package.

Real-editor acceptance uses `D:\workspace\sgproj`, explicitly overriding the
debugger repository's previous MyGame-only test location for this task.
Existing reachable project code is preferred; modifying sgproj solely to make
a breakpoint reachable requires separate user approval.

For this task the user has authorized Cursor and Unity UI operations. Unity MCP
is preferred for supported Unity state, refresh, compilation, Console, and
test operations. Computer Use is reserved for Cursor debug UI and Unity UI
interactions that Unity MCP cannot perform.

Real acceptance covers:

- first-breakpoint marker and hit breakpoint ID;
- repeated Step Into/Over/Out without warnings or marker loss;
- Hover and Watch enum expressions;
- Locals/Variables Getter and `ToString()` behavior;
- slow/failing evaluation without session corruption;
- Domain Reload breakpoint persistence and rebinding;
- detach and reconnect.

Automated test success is not reported as real Unity success. The final status
states whether sgproj acceptance is pending or completed.

## Versioning and Delivery

The backend rewrite releases as UnityDebuggerPure `0.3.0`. Every build carries
a unique build ID in the package metadata and diagnostic startup record.

No intermediate build is installed into Cursor. After all automated gates
pass, one VSIX is packaged and installed. Cursor must reload before the new
adapter is considered active. Installed files and startup diagnostics must
confirm both semantic version and build ID before real-editor acceptance.

