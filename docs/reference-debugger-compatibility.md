# Reference Debugger Compatibility

Reference: installed `zlorn.vstuc 1.2.1`.
Target: `D:\Unity\TuanjieHub\Projects\MyGame`, Tuanjie `2022.3.62t12`.

| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| ATT-01 | Attach in Edit Mode, then enter Play | Breakpoint is gray while attached in Edit Mode; entering Play immediately stops at the breakpoint with a yellow current-statement marker and no source-less user stop | Source-less transient pause | divergent; reference verified | 2026-08-06 MyGame reference run and earlier Pure adapter log | Replace current control layer |
| RLD-01 | Ordinary Play/Domain Reload with default exception filters | No background exception stop; both `All Exceptions` and `User-Unhandled Exceptions` are unchecked by default | Background `ThreadAbortException` exposed | divergent; reference verified | 2026-08-06 MyGame reference run and earlier Pure run | Replace current exception/control mapping; explicit filters still take effect |
| BP-01 | First hit at `GameRuntimeBootstrap.Install()` line 11 | Entering Play immediately stops with a yellow current-statement marker | Hit requires continuing transitional stop | divergent; reference verified | 2026-08-06 MyGame reference run and earlier Pure adapter log | Replace current control layer |
| EVAL-01 | Hover/Watch `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` | Hover and Watch both evaluate to `true` through the property Getter | `Expression evaluation failed` | divergent; reference verified | 2026-08-06 MyGame reference run | Replace handwritten evaluator |
| EVAL-02 | Hover/Watch `RuntimeInitializeLoadType.AfterSceneLoad` | Hover and Watch both report <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code> | Generic `Expression evaluation failed` observed for enum expressions | divergent; reference verified | 2026-08-06 MyGame reference run and earlier Pure run | User confirmed replacement: match reference scope resolution and error presentation; do not add broader implicit type lookup |
| STEP-01 | Step In, then rapid Step Over input | Step In lands in the property Getter at `GameRuntimeBootstrapMenu.cs:13`; three immediate Step Over clicks remain clickable, emit no warning, stop at caller `GameRuntimeBootstrap.cs:24`, and preserve the yellow marker | Warning/no-response/marker loss observed | divergent; reference verified | 2026-08-06 MyGame reference run | User confirmed replacement: reproduce reference request handling and marker continuity |
| VAR-01 | Expand variables across resume/new stop | Variables refresh across the recorded Step In/rapid Step Over sequence without exposing a stale-collection warning | `Variable collection is no longer available` observed | divergent; reference verified | 2026-08-06 MyGame reference run | User confirmed replacement of the current variable-handle lifecycle |
| VAR-02 | Instance Locals topology | Locals has peer synthetic entries `Active scene`, `this`, and `this.gameObject`; `transform` is below `this.gameObject`; Getter values display directly with no `Properties` group | Not verified | reference verified | 2026-08-06 MyGame reference run and screenshot | User confirmed replacement of Pure's Locals presentation to preserve this layout |
| BP-02 | Conditional breakpoint at `GamePrototypeRuntime.cs:214` | `_status == null` never stops; `_status != null` stops normally with the yellow marker and Variables | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |
| BP-03 | Logpoint at `GamePrototypeRuntime.cs:214` | `Reference log status={_status}` outputs `Reference log status=等待登录` without stopping; no warning, expression error, or marker loss | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |
| BP-04 | Function breakpoint | Fully qualified `MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles` is accepted and stops at the method definition around line 1069 | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |
| EX-01 | Caught/unhandled exception filters | Exposes unchecked `All Exceptions` and `User-Unhandled Exceptions`; default Play does not pause; User-Unhandled alone does not stop for the handled `TaskCanceledException`; enabling All stops at its throw site in `UnityAssetLinkReceiver.cs:94` with the yellow marker and inline exception panel | Not verified | reference verified | 2026-08-06 MyGame reference runs and screenshots | Await Pure A/B |
| CTRL-01 | Pause, Continue, Step Out | Continue resumes normally; a manual Pause with no displayable managed source frame shows no source line, yellow marker, warning, or Variables; Step Out from the caller resumes normal execution | Not verified | reference verified | 2026-08-06 MyGame reference runs | Do not synthesize a source frame or variable scope for a source-less manual Pause; await Pure A/B |
| CTRL-02 | Step-in Targets and Goto capability/presentation | All three commands are visible and enabled; Step Into Target enters `EnsureStyles()` at line 1070, Jump to Cursor stops at line 1076, and Set Next Statement immediately moves the yellow marker to line 1076; no warning or marker loss, and subsequent Step Over works | Not verified | reference verified | 2026-08-06 MyGame reference runs | Await Pure A/B |
| EVAL-03 | Locals, Getter, `ToString()`, collection expansion | Getter values display directly; `this.ToString()` and `gameObject.ToString()` return Unity object display strings; `BoardIndexByCell` displays as `int[5,8]` and expands normally; no loading placeholder or delayed update was observed in this run | Getter and `ToString()` previously failed or required Watch; expansion not fully verified | divergent; reference verified | 2026-08-06 MyGame reference run, screenshot, and earlier Pure run | User confirmed replacing the current evaluation path; loading-placeholder behavior remains unverified because evaluation may have completed too quickly |
| EVAL-04 | Invalid expression and Getter failure presentation | Invalid Watch `DefinitelyMissingName` reports <code>The identifier `DefinitelyMissingName` is not in the scope</code>; failing Getter remains to be recorded | Generic `Expression evaluation failed` | divergent; reference partially verified | 2026-08-06 MyGame reference run and earlier Pure run | Match reference diagnostic; await failing-Getter A/B |
| SET-01 | Set Variable | Set Value is available for `this._status`; assigning a new string updates the displayed value immediately | Not verified | reference verified | 2026-08-06 MyGame reference run | User confirmed replacement of the Set Value refresh path; await Pure A/B |
| CFG-01 | Public implicit-evaluation setting | No public implicit-evaluation setting is contributed; Getter/`ToString()` evaluation is built-in | Current Pure contributes `unityDebuggerPure.enableImplicitEvaluation` and sends a private Attach flag | divergent; reference verified | 2026-08-06 installed reference `package.json` manifest inspection | User confirmed deleting the setting and propagation chain to match reference |
| END-01 | Detach and reattach while Unity remains in Play | Detach leaves Unity in Play; reattach immediately hits the recurring breakpoint at `GamePrototypeRuntime.cs:214` with a normal yellow marker and Variables, without an intermediate pause, error, or warning | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |

## Evidence Rules

Each evidence entry records:

- date;
- source line;
- exact user actions;
- visible result;
- sanitized log path when available;
- screenshot path when available.

Shared source fixture:

```text
Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11
GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled
RuntimeInitializeLoadType.AfterSceneLoad
```

A row moves to `aligned` only after the same action sequence is run with the
reference debugger and UnityDebuggerPure on this fixture. If an unlisted
difference appears, add a `divergent` row and stop for the user's decision.

## Reference Baseline Actions

Run these with the installed reference debugger before production backend
changes begin:

```text
BP-02: conditional source breakpoint whose condition is true, then false.
BP-03: logpoint containing one local/property expression.
EX-01: default uncaught filter, then All Exceptions, during ordinary Play/Domain Reload.
CTRL-01: Continue, Pause, Step In, Step Over, Step Out, including rapid Step Over as soon as enabled.
CTRL-02: whether Step Into Targets and Jump to Cursor/Goto are visible and what each does.
EVAL-03: Locals/this expansion, a property Getter, ToString(), and a collection child.
EVAL-04: invalid expression and one failing/unavailable property.
SET-01: whether Set Value is visible for a writable value and its visible success/error result.
END-01: detach, reattach, and first subsequent breakpoint.
```

Mark a captured reference row `reference-verified`. Leave a scenario `not
verified` when MyGame cannot expose it without modifying the project. Do not
infer an unavailable UI capability.

## Evidence Log

### 2026-08-06 - Reference attach, first stop, and initial evaluation

- Debug configuration: `Attach to Unity` (`zlorn.vstuc 1.2.1`).
- Source: `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11`.
- Attached while the Editor was in Edit Mode: breakpoint remained gray.
- Entered ordinary Play Mode: debugger stopped immediately at the breakpoint and displayed the yellow
  current-statement marker.
- Hover and Watch for `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled`: `true`.
- Hover and Watch for `RuntimeInitializeLoadType.AfterSceneLoad`:
  <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code>.
- User confirmed that Pure must reproduce this reference scope failure and its specific presentation rather than
  adding broader implicit enum/type lookup.
- Invalid Watch `DefinitelyMissingName`:
  <code>The identifier `DefinitelyMissingName` is not in the scope</code>.
- Watch `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled.ToString()`: <code>"True"</code>.
- Step In entered the property Getter at `GameRuntimeBootstrapMenu.cs:13`; the yellow marker remained visible.
- Three immediate Step Over clicks stayed clickable, produced no warning, stopped at
  `GameRuntimeBootstrap.cs:24`, and did not lose the yellow marker.
- Step Out from that caller location resumed normal execution, which is the expected reference behavior.
- No `Variable collection is no longer available` warning appeared during the sequence.
- User confirmed replacing Pure's step-request handling and variable-handle lifecycle to match these reference
  behaviors.
- At `GamePrototypeRuntime.cs:88`, Locals contained the peer entries `Active scene`, `this`, and
  `this.gameObject`. The inherited `transform` value appeared below `this.gameObject`, and Getter values were
  displayed directly without a `Properties` group.
- `this.ToString()` returned a `GamePrototypeRuntime` Unity object string. `gameObject` and
  `gameObject.ToString()` returned <code>"GamePrototypeRuntime (UnityEngine.GameObject)"</code>.
- `BoardIndexByCell` displayed as `int[5,8]` and its children expanded normally.
- No loading placeholder or delayed update was visible, but the operation may have completed too quickly; placeholder
  refresh behavior therefore remains unverified.
- Set Value was available for `this._status`, and assigning a new string updated the value immediately.
- User confirmed replacing Pure's Locals presentation, Getter/ToString evaluation, structured expansion, and Set
  Value refresh paths to reproduce these results.
- The installed reference manifest contributes no implicit-evaluation setting. Its exposed settings are message
  completion, Unity message scope, refresh on save, code model, and background-process display. The previously
  requested Pure setting is therefore a product difference. The user confirmed deleting the public setting, private
  Attach argument, and evaluation-mode branch so implicit evaluation remains built in like the reference.
- Continue resumed normally. A subsequent manual Pause, while no displayable managed source frame was available,
  showed no current file/line, yellow marker, warning, or Variables.
- Detaching left Unity in Play Mode. Reattaching immediately hit `GamePrototypeRuntime.cs:214` with a normal yellow
  marker and Variables and produced no intermediate pause, error, or warning.
- Conditional breakpoint `_status == null` did not stop; changing the condition to `_status != null` stopped normally
  with the yellow marker and Variables.
- Logpoint `Reference log status={_status}` emitted `Reference log status=等待登录` without pausing. No warning,
  expression error, or marker loss appeared during either breakpoint test.
- `Debug: Step Into Target`, `Debug: Jump to Cursor`, and `Debug: Set Next Statement` were all visible and enabled.
  Step Into Target entered `EnsureStyles()` at line 1070. Jump to Cursor stopped at line 1076 with no warning or
  marker loss. Set Next Statement immediately moved the yellow marker to line 1076, and the following Step Over
  completed normally.
- A Function Breakpoint for `MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles` was available, bound, and
  stopped at the method definition around line 1069.
- The Breakpoints panel exposed unchecked `All Exceptions` and `User-Unhandled Exceptions` filters. Re-entering Play
  with that default state produced no exception pause.
- Enabling `All Exceptions` stopped at thrown
  `System.Threading.Tasks.TaskCanceledException: A task was canceled.` in
  `FilePackages/unity-links/unity-package/Editor/UnityAssetLinkReceiver.cs:94`, with a yellow marker and inline
  exception panel. This proves reference behavior is filter-driven rather than a blanket suppression of background
  exceptions. With only `User-Unhandled Exceptions` enabled, the same handled exception produced no pause. The user
  restored the default unchecked state afterward.
- Exception-filter screenshots:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-dd6f9f58-792b-4e0c-974e-0d68e148081b.png` and
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-6b1816a6-3e68-40a9-9097-195b419b4f7b.png`.
- Screenshot evidence:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-c8d174db-6571-4873-8d57-b73c8af63793.png`.
- Continue/Pause, a failing Getter, and the remaining breakpoint capabilities remain pending.
