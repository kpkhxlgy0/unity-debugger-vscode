# Reference-Compatible Debugger Backend Design

## Decision

This design supersedes the earlier direct-engine design that selected a
project-owned control state machine and expression evaluator. Real-editor
testing showed that choice diverges from the installed `zlorn.vstuc` debugger.
The direct control layer and the handwritten expression evaluator are both
rejected as production architecture.

UnityDebuggerPure will instead use the repository's pinned, MIT-licensed
`Mono.Debugging.Soft`, `Mono.Debugging`, NRefactory, and
`Mono.Debugger.Soft` session stack. The installed `zlorn.vstuc 1.2.1`
debugger is the sole authority for observable debugger behavior.

## Goal

Make UnityDebuggerPure's debugger-only experience match the installed
reference debugger for attach, breakpoints, Domain Reload, exceptions,
execution control, stopped-state presentation, Hover, Locals, Watch, variable
expansion, property getters, `ToString()`, and C# expression evaluation.

An item is compatible only after the reference plugin and UnityDebuggerPure
produce the same result in an A/B run against the same reachable MyGame code.
Automated tests alone do not establish reference compatibility.

## Scope

In scope:

- Unity/Tuanjie discovery and attach.
- Source and function breakpoints, conditions, logpoints, and exception
  breakpoints.
- Continue, Pause, Step Into, Step Over, Step Out, Step-in Targets, and Goto.
- Threads, stack traces, current-statement presentation, scopes, Locals,
  Watch, REPL, Hover, Set Variable, and expandable variables.
- C# source context, `using` directives, type and member binding, enums,
  overload resolution, getters, and `ToString()`.
- Domain Reload, module and AppDomain lifecycle, source mapping, breakpoint
  persistence, unbinding, and rebinding.
- Built-in implicit Getter and `ToString()` evaluation with no public setting,
  matching the reference debugger.
- A maintained reference-compatibility matrix and concrete A/B evidence.

Permanently out of scope:

- Completion, diagnostics, Unity Messages, project-system features, and other
  language-service functionality.
- Shader, UXML, USS, or non-debugger tooling.
- Reusing, separating, decompiling, modifying, or redistributing proprietary
  binaries from the reference extension.
- A second fallback engine or a handwritten expression fallback.

## Reference Authority

The installed `zlorn.vstuc 1.2.1` debugger defines the expected user-visible
and DAP-visible behavior. The implementation may use different lawful
internals, but an internal limitation does not authorize a different user
experience.

For every affected behavior:

1. run the scenario with the reference plugin in MyGame;
2. record the visible result and available protocol/log evidence;
3. encode the observed behavior in a failing test where automation can cover
   it;
4. implement the smallest mature-stack integration needed to pass;
5. repeat the scenario with UnityDebuggerPure;
6. mark the result `aligned`, `divergent`, or `not verified`.

Any newly observed divergence stops implementation at that decision point.
The user must decide whether the current implementation should be replaced to
align more closely. No unapproved divergence may be hidden, accepted by
assumption, or reported as complete.

## Legal and Dependency Boundary

The reference package contains proprietary Microsoft/SyntaxTree assemblies.
Its license does not permit separating, reverse engineering, or redistributing
those components. They are neither build inputs nor runtime dependencies.

The repository already retains pinned MIT sources for:

- `Unity-Technologies/vscode-mono-debug`;
- `Unity-Technologies/debugger-libs`, including `Mono.Debugging` and
  `Mono.Debugging.Soft`;
- `icsharpcode/NRefactory`.

Those mature sources provide the session and evaluation behavior. Their exact
revisions and licenses remain recorded in `THIRD_PARTY_NOTICES.md` and the
runtime assembly inventory.

## Architecture

```text
VS Code
  | DAP
  v
UnityDebugSession
  | protocol translation only
  v
MonoDebuggingBackend
  | thin handle/event mapping
  v
UnitySoftDebuggerSession
  +-- Mono.Debugging session lifecycle
  +-- Mono.Debugging.Soft breakpoint/thread/step control
  +-- Mono.Debugging ObjectValue model
  +-- NRefactory C# expression resolver/evaluator
  |
  v
Mono.Debugger.Soft
  |
  v
Unity/Tuanjie Mono Runtime
```

`UnityDebugSession` translates DAP requests and responses. It does not infer a
running state, synthesize stops, queue steps, implement expression semantics,
or own Domain Reload policy.

`MonoDebuggingBackend` is the unavoidable boundary adapter. It maps mature
session objects and events to the repository's DAP-neutral backend contracts.
It contains no independent control state machine and no expression parser.

`UnitySoftDebuggerSession` is the single owner of the debugger connection,
target lifecycle, threads, stops, breakpoints, exceptions, stepping, stack
frames, evaluation contexts, and ObjectValue lifetime.

The following current production components leave the runtime path and are
removed rather than retained as fallbacks:

- `UnityDebuggerEngine` and its direct connection/event dispatcher;
- `StepManager` and direct step-target state;
- `SuspendedState` as an independent execution-generation authority;
- direct breakpoint, exception, module, and Domain Reload managers;
- `EvaluationService`, handwritten `ExpressionEvaluator`, frame environment,
  property/value model, and custom formatters.

## Attach and Lifecycle

Attach constructs one mature debugger session, subscribes to its target,
thread, breakpoint, output, and termination events, and connects to the
discovered Unity/Tuanjie endpoint. The session is the sole source of execution
truth.

Attaching in Edit Mode may leave source breakpoints pending until the relevant
runtime types are loaded. It must not create a user-visible stopped state.

Entering Play or performing a Domain Reload is handled by the mature session's
AppDomain, assembly, and breakpoint lifecycle. Pending user breakpoints survive
the transition and rebind when the new runtime domain loads. Transitional
runtime exceptions or suspend states must not appear as user stops unless the
reference plugin exposes the same stop in the identical scenario.

A real breakpoint, exception, step, or user pause produces the same stopped
reason, selected thread behavior, stack availability, and current-statement
marker as the reference plugin. Disconnect and termination are idempotent and
emit the same DAP lifecycle as the reference.

## Execution Control

Continue, Pause, Step Into, Step Over, Step Out, Step-in Targets, and Goto call
the corresponding mature session operations. The DAP boundary does not add
custom queues, retries, coalescing, warning suppression, artificial waits, or
optimistic running/stopped transitions.

Rapid-step behavior is not designed from preference. The exact sequence of
accepted, ignored, or disabled operations is first captured from the reference
plugin and then treated as the contract. The result must not expose warnings,
lose the current-statement marker, discard a real stop, or leave VS Code and
the target in contradictory states when the reference does not.

Frame and ObjectValue handles follow the mature session's stop lifetime.
Resuming invalidates the previous stopped context; a subsequent real stop
creates a fresh context. The DAP boundary must not invalidate handles earlier
or retain them later than the reference plugin.

## Evaluation and Variables

All expression parsing, source resolution, type lookup, `using` handling,
static and instance member binding, overload resolution, property invocation,
and value formatting are delegated to the mature Mono.Debugging/NRefactory
evaluation stack.

The production adapter does not special-case expressions such as enum members
or Unity types. Expressions including
`RuntimeInitializeLoadType.AfterSceneLoad`, static properties, instance
properties, locals, fields, method calls, casts, and compound expressions use
one evaluator and one stack-frame context.

Scopes and expandable variables use Mono.Debugging `ObjectValue` objects.
Getter errors remain property-scoped when the reference presents them that
way. Evaluation errors, timeout presentation, lazy loading, grouping,
DebuggerDisplay, debugger proxies, collections, enums, and `ToString()` follow
the reference result established by A/B evidence.

Implicit Getter and `ToString()` evaluation is built into the mature session,
matching the reference debugger. UnityDebuggerPure contributes no public
implicit-evaluation setting and sends no private Attach policy flag. Hover,
Locals, Variables, Watch, and REPL use the reference-compatible mature-session
evaluation options; the adapter does not retain a second safe-mode evaluator.

## Error Handling and Diagnostics

The DAP boundary preserves mature-session error categories and reference
presentation. It must not replace actionable evaluator errors with a generic
message when the reference supplies a specific result, and it must not turn an
evaluation failure into a session stop or disconnect.

Connection loss and protocol corruption are session-fatal. Per-expression,
per-property, breakpoint-condition, and step-state failures remain scoped as
the reference scopes them.

Diagnostic logs retain build ID, request kind, event order, stop reason,
duration, and exception type. They do not record expression text, variable
values, source contents, credentials, or unredacted user paths. The extension
has no telemetry.

## Reference-Compatibility Matrix

The maintained execution matrix and evidence index is
[`docs/reference-debugger-compatibility.md`](../../reference-debugger-compatibility.md).

The repository maintains an explicit matrix for every affected behavior. Each
row contains:

- scenario and exact user action;
- reference result;
- UnityDebuggerPure result;
- status: `aligned`, `divergent`, or `not verified`;
- reference and Pure evidence;
- user decision for any divergence.

Known status at design approval:

| Scenario | Status | Current evidence |
| --- | --- | --- |
| Attach in Edit Mode and enter Play | divergent | Pure creates a transient source-less pause before the real breakpoint. |
| Domain Reload internal exception handling | divergent | Pure exposed a background `ThreadAbortException` stop. |
| First reachable source breakpoint | divergent | The real breakpoint binds and hits only after manually continuing the transient stop. |
| Static property Getter evaluation | divergent | Pure returns `Expression evaluation failed`. |
| Imported Unity enum member evaluation | divergent | Pure returns `Expression evaluation failed`. |
| Rapid Step and marker stability | divergent | Earlier real-editor runs produced warnings, ignored steps, and marker loss not seen in the reference. |
| Variable collection lifetime | divergent | Earlier real-editor runs exposed stale collection errors not seen in the reference. |
| Remaining breakpoint, exception, inspection, and detach scenarios | not verified | A complete same-project A/B run is required. |

The matrix is updated during implementation and included in every release
handoff. A `divergent` or `not verified` row blocks a claim of full reference
compatibility or completion.

## Verification

All production changes follow test-driven development. Each automated change
starts with a failing test tied to recorded reference behavior.

Automated verification covers:

1. DAP translation and lifecycle ordering without a second control state.
2. Mature session mapping for attach, real stops, resume, stepping,
   exceptions, Domain Reload, termination, and breakpoint persistence.
3. ObjectValue scopes, expansion, Getter/ToString policy, Watch/REPL policy,
   enum/static member expressions, errors, and stopped-context lifetime.
4. Extension/package assertions that the removed implicit-evaluation setting
   and private Attach flag cannot reappear.
5. Build and package inventory proving the mature session/evaluation
   assemblies are shipped and the rejected direct engine is absent from the
   runtime path.

Real-editor acceptance uses the existing MyGame project at
`D:\Unity\TuanjieHub\Projects\MyGame`, the existing VS Code window opened from
that project, the active `SampleScene`, and verified reachable code under
`Assets/Scripts/GamePlay/Runtime/DevTools`.

The user performs VS Code UI actions. Unity MCP controls and inspects the
MyGame/Tuanjie instance, scene, Play Mode, runtime objects, and Console. No
Computer Use is required unless the user explicitly changes that instruction.

The reference and Pure runs use the same source line and action sequence and
cover:

- Edit Mode attach and first Play;
- Domain Reload breakpoint persistence;
- first-breakpoint marker and hit identity;
- exception behavior;
- Step Into/Over/Out and rapid Step input;
- Hover, Watch, Locals, static properties, imported enum members, Getter,
  `ToString()`, and variable expansion;
- stale-context behavior;
- detach, reattach, and final Unity Console state.

## Delivery

The replacement backend remains on the unpublished UnityDebuggerPure `0.3.0` version with a unique
build ID and refreshed third-party/runtime inventory.

No intermediate VSIX is installed. After automated gates and package
verification pass, exactly one final VSIX is installed into VS Code. VS Code
must reload before real-editor acceptance, and startup diagnostics must confirm
the installed semantic version and build ID.

The final handoff includes the compatibility matrix. Any remaining divergent
or unverified item is called out explicitly and prevents a completion claim.
