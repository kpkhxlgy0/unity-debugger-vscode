# Reference Debugger Compatibility

Reference: installed `zlorn.vstuc 1.2.1`.
Target: `D:\Unity\TuanjieHub\Projects\MyGame`, Tuanjie `2022.3.62t12`.

| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| ATT-01 | Attach in Edit Mode, then enter Play | Breakpoint is gray while attached in Edit Mode; entering Play immediately stops at the breakpoint with a yellow current-statement marker and no source-less user stop | Entering Play stopped directly at line 11 with a yellow marker and no source-less stop | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature lifecycle replacement matches this reference sequence |
| RLD-01 | Ordinary Play/Domain Reload with default exception filters | No background exception stop; both `All Exceptions` and `User-Unhandled Exceptions` are unchecked by default | No background exception pause occurred before the managed breakpoint | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Expression-context errors occurred only after the managed stop and are tracked separately |
| BP-01 | First hit at `GameRuntimeBootstrap.Install()` line 11 | Entering Play immediately stops with a yellow current-statement marker | First reachable hit stopped directly at line 11 with a yellow current-statement marker | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature breakpoint lifecycle matches this reference sequence |
| EVAL-01 | Hover/Watch `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` | Hover and Watch both evaluate to `true` through the property Getter | Hover and Watch both evaluate to `true` | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Same-namespace types resolve through the stopped frame's assembly |
| EVAL-02 | Hover/Watch `RuntimeInitializeLoadType.AfterSceneLoad` | Hover and Watch report <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code> with inline presentation only, including automatic Watch refresh at a new stop | Same diagnostics and inline styles with no notification initially or after automatic Watch refresh at the next stop | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | DAP evaluation errors retain failure semantics with `showUser=false` |
| STEP-01 | Step In, then rapid Step Over input | Step In lands in the property Getter at `GameRuntimeBootstrapMenu.cs:13`; three immediate Step Over clicks remain clickable, emit no warning, stop at caller `GameRuntimeBootstrap.cs:24`, and preserve the yellow marker | Same landing lines, responsive controls, no warning, and continuous yellow marker | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature request handling matches the recorded rapid-input sequence |
| VAR-01 | Expand variables across resume/new stop | Variables refresh across the recorded Step In/rapid Step Over sequence without exposing a stale-collection warning | Variables refreshed after continuing from the rapid-step stop to the instance breakpoint; no stale-collection warning appeared | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature variable handles match the recorded resume/new-stop sequence |
| VAR-02 | Instance Locals topology | Locals has peer synthetic entries `Active scene`, `this`, and `this.gameObject`; `transform` is below `this.gameObject`; Getter values display directly with no `Properties` group | Same peer entries and order; `this.gameObject` expands normally with `transform`; Getter values display directly with no `Properties` group | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Frame-bound synthetic Unity values reproduce the recorded reference topology |
| VAR-03 | Expand `this` member hierarchy | Reference preserves a `base` node instead of flattening inherited members | `this` preserves the `base` node and exposes the source-defined `CurrentSnapshot` Getter directly | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Variable-only options preserve base types and direct member presentation |
| BP-02 | Conditional breakpoint at `GamePrototypeRuntime.cs:214` | `_status == null` never stops; `_status != null` stops normally with the yellow marker and Variables | Build `0.3.0+g8c6f030f7256` does not stop for `_status == null`, then immediately stops normally after rebinding to `_status != null` | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Conditional evaluation and breakpoint rebinding match the recorded reference behavior |
| BP-03 | Logpoint at `GamePrototypeRuntime.cs:214` | `Reference log status={_status}` outputs `Reference log status=等待登录` without stopping; no warning, expression error, or marker loss | Build `0.3.0+g8c6f030f7256` outputs `Pure log status=Pure set value test` without pausing and raises no warning, evaluation error, or marker loss | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Log interpolation and non-stopping continuation match the reference; differing literal prefix and runtime value are intentional test inputs |
| BP-04 | Function breakpoint | Fully qualified `MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles` is accepted and stops at the method definition around line 1069 | Build `0.3.0+g8c6f030f7256` accepts the fully qualified method and stops around line 1069 with the yellow marker and Variables | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Mature function-breakpoint binding matches the recorded reference behavior |
| EX-01 | Caught/unhandled exception filters | Exposes unchecked `All Exceptions` and `User-Unhandled Exceptions`; default Play does not pause; User-Unhandled alone does not stop for the handled `TaskCanceledException`; enabling All stops at its throw site in `UnityAssetLinkReceiver.cs:94` with the yellow marker and inline exception panel | Exposes `All Exceptions` unchecked and `Uncaught Exceptions` checked by default | divergent; reference verified | 2026-08-06 MyGame reference runs, screenshots, and Pure source audit | User confirmed replacement of filter identity/defaults and mature exception mapping |
| CTRL-01 | Pause, Continue, Step Out | `CauseBreak` suspends the target, enumerates every runtime thread in order, and reports one all-threads-stopped Pause event per thread; the event selected by VS Code may therefore have a displayable source frame and Variables or may be source-less; Continue and Step Out resume normally | Source replacement now suspends once and reports every runtime thread in order without scanning for a preferred user-code frame; MyGame runtime verification pending | not yet verified | 2026-08-06 MyGame reference run; 2026-08-08 reference binary inspection, Pure intermittent Pause log, and red/green Adapter test | Runtime A/B must check repeated Pause/Continue, no warning, responsive control, detach, and reattach; a yellow marker is permitted and is not itself a divergence |
| CTRL-02a | Step Into Target | Enters `EnsureStyles()` at line 1070 with the yellow marker; subsequent stepping remains responsive and preserves the marker | Build `0.3.0+g19772c378d36` enters `EnsureStyles()` at line 1070; two immediate Step Over operations stop normally with the marker preserved | aligned | 2026-08-06 MyGame reference run; 2026-08-07 Pure run and Adapter log | Unfiltered specific breakpoint plus dispatch-time request/thread matching reproduces the reference result |
| CTRL-02b | Jump to Cursor and Set Next Statement | Jump to Cursor stops at line 1076; Set Next Statement immediately moves the yellow marker to line 1076; no warning or marker loss, and subsequent Step Over works | Build `0.3.0+g8c6f030f7256` matches both commands at an ordinary cursor column; the yellow marker moves to line 1076 without a notification, and the following Step Over works | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Resolved locations, requested-thread SetIP, reference one-shot completion, and column-independent target matching reproduce the recorded reference sequence |
| EVAL-05a | Inspection immediately after Step Into Target | Watches, Hover, Locals, and Scopes remain responsive; no request error or user notification appears | Variables had content; Hover `_titleStyle` and `this` returned values; Watches refreshed with invalid expressions inline only; no notification appeared; Step Over remained immediate | aligned | 2026-08-06 MyGame reference run; 2026-08-07 Pure run and Adapter log | Five automatic Watch requests completed together, followed by Scopes/Variables without the former one-second serialization |
| EVAL-05b | Inspection immediately after ordinary F11 Step In | Watches, Hover, Locals, and Scopes remain responsive; no request error or user notification appears | Build `0.3.0+g8c6f030f7256` enters `EnsureStyles()` through ordinary F11; Variables has content, Hover `_titleStyle` and `this` returns values, Watches refresh with invalid expressions inline only, no notification appears, and subsequent Step Over remains immediate | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Ordinary and targeted Step In inspection paths now both match the recorded reference behavior |
| EVAL-03 | Locals, Getter, `ToString()`, collection expansion | Getter values display directly; `this.ToString()` and `gameObject.ToString()` return Unity object display strings; `BoardIndexByCell` displays as `int[5,8]` and expands normally; no loading placeholder or delayed update was observed in this run | Build `0.3.0+g8c6f030f7256` displays `CurrentSnapshot` directly under `this`, preserves `base`, evaluates both `ToString()` expressions, and displays and expands `BoardIndexByCell` as `int[5,8]` without a placeholder, delay, or notification | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Getter/member topology, explicit evaluation, and collection expansion match the recorded reference behavior |
| EVAL-04 | Invalid expression and Getter failure presentation | Invalid Watch `DefinitelyMissingName` reports <code>The identifier `DefinitelyMissingName` is not in the scope</code>; failing Getter remains to be recorded | Invalid Watch reports the same scope diagnostic; failing Getter remains to be recorded | aligned for invalid identifier; Getter failure unverified | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Do not mark the whole row aligned until a failing Getter has reference and Pure evidence |
| EVAL-06 | Empty automatic Hover expression after Continue and a new stop | Ordinary evaluation failure is returned without requesting a user notification | Build `0.3.0+g8c6f030f7256` re-hit the breakpoint after Continue, refreshed Watches, Scopes, and Variables, and raised no `An evaluation expression is required.` notification | aligned | 2026-08-08 MyGame Pure runs, screenshot, Adapter logs, and full reference protocol inspection | The custom rejection is removed; empty expressions use the ordinary evaluation-failure path with `showUser=false` |
| SET-01 | Set Variable | Set Value is available for `this._status`; assigning a new string updates the displayed value immediately | Build `0.3.0+g8c6f030f7256` accepts `"Pure set value test"` for `_status` and refreshes the displayed value immediately | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Mature SetVariable handling and stopped-view refresh match the recorded reference behavior |
| CFG-01 | Public implicit-evaluation setting | No public implicit-evaluation setting is contributed; Getter/`ToString()` evaluation is built-in | Installed build `0.3.0+g8c6f030f7256` has no contributed configuration, exposes only `name/type/request` Attach properties, and contains no implicit-evaluation setting or private Attach flag | aligned | 2026-08-06 reference manifest; 2026-08-08 Pure installed manifest and bundle inspection | Getter and `ToString()` evaluation remain built-in with no public or private configuration path |
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
- Pure currently advertises this capability but returns an empty function-breakpoint response without backend
  binding. The user confirmed replacing that path with mature function-breakpoint binding.
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
- Pure currently contributes a checked-by-default `Uncaught Exceptions` filter instead of reference's unchecked
  `User-Unhandled Exceptions`. The user confirmed replacing its identity, defaults, and runtime mapping.
- Screenshot evidence:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-c8d174db-6571-4873-8d57-b73c8af63793.png`.
- A failing Getter and slow-loading placeholder refresh remain unverified because MyGame did not expose them without
  source modification.

### 2026-08-06 - UnityDebuggerPure 0.4.0 immutable candidate

- Candidate: `dist/unity-debugger-pure-0.4.0.vsix`.
- VSIX SHA-256: `27647cc1a0fc84dd55ef5132cd8e8aded972ede991ecd0e97f9109c8a66dc883`.
- Adapter SHA-256: `6c7801f043dc6d51a42df1a1eddbbb9785e2cba4c9958a26048f3d917ea95cd0`.
- Version: `0.4.0`.
- Build ID: `0.4.0+g08acea12c5b2`.
- Source commit embedded in the candidate: `08acea12c5b2adfec2616ee0a680c0749ace31ee`.
- Automated verification: 19 build tests, 93 extension tests, 140 adapter tests, 9 integration tests, and 4 package
  contract tests passed. The VSIX verifier accepted 33 packaged files and all 17 audited runtime assemblies.
- Repository/dependency verification passed `git diff --check`, third-party provenance verification, and locked
  restore. Six pre-existing vendor lock files remain unstaged with line-ending-only changes.
- The candidate has not yet been installed. Its embedded source commit intentionally precedes the test-only package
  expectation correction and this evidence entry; the candidate itself was not rebuilt or overwritten afterward.

### 2026-08-06 - UnityDebuggerPure 0.4.0 installation

- Unity MCP selected `MyGame@c3cd500d` and confirmed project `MyGame`, active scene
  `Assets/Scenes/SampleScene.scene`, Tuanjie `2022.3.62t12`, Edit Mode, idle compilation, and no project or compile
  errors. The only Console errors were two controller-disconnect notifications for `Flydigi Vader 5 Pro`.
- The ordinary acceptance entry is SampleScene Play Mode, targeting
  `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11`; the source still has
  `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)` immediately above `Install()`.
- Installed exactly `kpk.unity-debugger-pure@0.4.0` from the immutable candidate into
  `C:\Users\Admin\.vscode\extensions\kpk.unity-debugger-pure-0.4.0`.
- Installed build ID is `0.4.0+g08acea12c5b2`; installed source commit and adapter SHA-256 exactly match the
  immutable candidate. VS Code has not yet been reloaded and no Pure behavior row has been marked aligned.
- The user superseded this semantic-version choice before any Reload confirmation or Pure acceptance. Subsequent
  local candidates retain the unpublished `0.3.0` version and use build ID/commit for identity.

### 2026-08-06 - UnityDebuggerPure 0.3.0 immutable candidate

- Candidate: `dist/unity-debugger-pure-0.3.0.vsix`.
- VSIX SHA-256: `dcd6c1089984a9bb1e9685e92c617808a1a87e1118e21b774a1ca91bfd0fcf2c`.
- Adapter SHA-256: `bd31667903584bbf1e8f50afff8b7e2d6be3aa30fe6c825a5295bcccb1bec674`.
- Version: `0.3.0`.
- Build ID: `0.3.0+ged2012f404fd`.
- Source commit embedded in the candidate: `ed2012f404fd3854e524739cd2e1d20f20035c97`.
- Automated verification passed: typecheck, 19 build tests, 93 extension tests, 140 adapter tests, 9 integration
  tests, 4 package contract tests, third-party provenance, locked restore, 17-entry runtime inventory, and the
  33-file VSIX audit.
- Two earlier same-name artifacts were preserved under explicit `superseded-*` filenames because
  local deletion was blocked. Neither is the standard candidate path and neither is installed by this procedure.

### 2026-08-06 - UnityDebuggerPure 0.3.0 installation

- VS Code CLI reports the active extension as exactly `kpk.unity-debugger-pure@0.3.0`.
- Installed path: `C:\Users\Admin\.vscode\extensions\kpk.unity-debugger-pure-0.3.0`.
- Installed version, source commit, build ID, and adapter SHA-256 exactly match the immutable 0.3.0 candidate.
- The old 0.4.0 extension directory remains inactive on disk; it is absent from the CLI's active extension listing.
  No manual deletion was attempted.
- The user completed `Developer: Reload Window`; runtime build-ID verification is the next acceptance gate.
- After the Reload, the user started `Attach to Unity Debugger Pure`. The newest sanitized adapter log
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T122453883Z-8456.log` records
  `adapterVersion=0.3.0` and `buildId=0.3.0+ged2012f404fd`, followed by normal initialize, attach, function-breakpoint,
  exception-breakpoint, and thread requests with no logged failure.
- Unity MCP confirmed MyGame remained idle in SampleScene Edit Mode with no compilation in progress and zero current
  Console errors before behavioral acceptance began.

### 2026-08-06 - Pure 0.3.0 first managed stop

- Attached in Edit Mode and entered ordinary SampleScene Play with a breakpoint at
  `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11`.
- Pure stopped directly at line 11 with the yellow current-statement marker. No source-less pause or background
  exception stop occurred first, aligning ATT-01, RLD-01, and BP-01 for this sequence.
- Both requested Hover expressions failed, confirming EVAL-01/EVAL-02 divergence. Existing Watches displayed
  `Unknown identifier: BoardIndexByCell`, `Unknown identifier: gameObject`, and `'this' reference not available in the
  current evaluation context.` Because this stop is inside static `GameRuntimeBootstrap.Install()`, those instance
  expressions are genuinely out of scope here and do not constitute EVAL-03 evidence.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T122453883Z-8456.log`.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-3aceaee4-a369-4326-b84f-0ea2b71a6102.png`.
- Acceptance stopped before stepping or further variable actions, pending the user's required replacement decision.

### 2026-08-06 - Pure 0.3.0 evaluation replacement candidate

- The user approved replacing the divergent mature type-resolution bridge while preserving the aligned lifecycle and
  breakpoint behavior.
- Candidate remains version `0.3.0`; build ID is `0.3.0+ga2a32efe9e6f`, from source commit
  `a2a32efe9e6f112cdbb071ac65184f743a350267`.
- VSIX SHA-256: `bc34584b0cfab6787e703293a0c94f68d2563aa36a4556d4fc197b0b7d2b72dc`.
- Adapter SHA-256: `ae8f3a76226303cac65efc22f2b8348de2aa79e74a8cef284ed0cb2eae8fa93e`.
- The bridge now uses the mature session's `TypeResolverHandler` only for a type in the stopped frame's enclosing
  namespace. It deliberately does not resolve imported `UnityEngine.RuntimeInitializeLoadType`, preserving the
  reference-observed scope failure, and maps the mature evaluator's unknown-identifier diagnostic to the reference
  wording.
- Verification passed: 142 adapter tests, 9 integration tests, 19 build tests, 93 extension tests, 4 package contract
  tests, 17-entry runtime inventory, and the 33-file VSIX audit. The existing upstream unused-variable warning in
  `Mono.Debugging.Soft` remains unchanged.
- After the user stopped debugging and exited Play, VS Code successfully replaced the prior build with this candidate.
  The installed build ID, source commit, and adapter SHA-256 match exactly. A Reload is required before retesting.

### 2026-08-06 - Pure 0.3.0 evaluation replacement real run

- Attached after Reload and entered ordinary SampleScene Play. Pure stopped at
  `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11` with the yellow current-statement marker.
- Hover and Watch for `RuntimeInitializeLoadType.AfterSceneLoad` both reported
  <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code>, aligning EVAL-02 with the reference.
- Hover and Watch for `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` reported
  <code>The identifier `GameRuntimeBootstrapMenu` is not in the scope</code>, so EVAL-01 remains divergent.
- Locals displayed the static method's valid local variables. `this`, `gameObject`, and `BoardIndexByCell` are not in
  this static frame and their scope errors are not EVAL-03 evidence.
- Screenshots:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-c2987a81-905e-466b-9233-2428819b9d0a.png`,
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-f74f40d4-0b0a-4bed-87e2-bb2f82560045.png`, and
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-260d200b-b96b-4db4-b18e-4e4efaf3a622.png`.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T125639975Z-29644.log`.
- Root cause: the stopped-frame namespace resolver consulted only the mature session's already-loaded type cache.
  `GameRuntimeBootstrapMenu` had not entered that cache yet, although it exists in the stopped frame's assembly.

### 2026-08-06 - Pure 0.3.0 stopped-frame assembly resolver candidate

- Candidate remains version `0.3.0`; build ID is `0.3.0+g21b588b9b309`, from source commit
  `21b588b9b3094596a600879bd0502a7b48797576`.
- VSIX SHA-256: `c8290dc32ad2fc6c6dd482473aedf989ef902d429543a354361c1caaef71e3f9`.
- Adapter SHA-256: `bb7cf6ee3536f606d983238d754b4e44373b23594ffa4e60bd863208a0fc69ce`.
- Same-namespace identifiers are now resolved against the stopped frame's assembly when they are absent from the
  mature session's loaded-type cache. Imported namespaces are still not scanned, preserving aligned EVAL-02.
- Verification passed: type checking, third-party provenance, 142 adapter tests, 9 integration tests, 19 build tests,
  93 extension tests, 4 package contract tests, 17-entry runtime inventory, and the 33-file VSIX audit. The existing
  upstream unused-variable warning in `Mono.Debugging.Soft` remains unchanged.
- After the user stopped debugging and exited Play, VS Code installed this exact candidate. The installed version,
  build ID, source commit, and Adapter SHA-256 match the audited VSIX; Reload is required before the real retest.
- EVAL-01 remained divergent until this exact candidate was installed and retested in MyGame.

### 2026-08-06 - Pure 0.3.0 stopped-frame assembly resolver real run

- After Reload, Pure attached in Edit Mode and ordinary SampleScene Play stopped at
  `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11` with the yellow current-statement marker.
- Hover and Watch for `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` both evaluated to `true`, aligning EVAL-01.
- Hover and Watch for `RuntimeInitializeLoadType.AfterSceneLoad` both reported
  <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code>. Pure also displayed the same diagnostic
  in a bottom-right Hover error notification. The user confirmed that the reference debugger never displayed that
  notification, establishing an EVAL-02 presentation divergence even though the value and diagnostic text align.
- Watch `DefinitelyMissingName` reported <code>The identifier `DefinitelyMissingName` is not in the scope</code>,
  aligning the invalid-identifier portion of EVAL-04. Watch
  `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled.ToString()` returned <code>"True"</code>, matching the reference.
- Step Into landed in the property Getter at `GameRuntimeBootstrapMenu.cs:13`. Three immediate Step Over clicks stayed
  responsive, produced no warning, stopped at `GameRuntimeBootstrap.cs:24`, and preserved the yellow marker throughout,
  aligning STEP-01.
- A normal breakpoint was set at `GamePrototypeRuntime.cs:214`, then Continue moved from the rapid-step stop to that
  reachable instance frame immediately. Variables refreshed normally, the yellow marker was present, and no
  `Variable collection is no longer available` warning appeared, aligning VAR-01.
- No additional behavior difference was reported in this sequence.
- Screenshots:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-8e8ecefd-0770-43d9-ab9e-42a8e4ad0eb9.png` and
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-3f0779c2-7c5a-4c5f-9d0a-015453b27305.png`.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T131933757Z-32816.log`.

### 2026-08-06 - Pure 0.3.0 reference Hover-error presentation candidate

- The user confirmed that the reference debugger does not raise a bottom-right error notification for the invalid
  Hover and approved replacing Pure's divergent response behavior.
- Candidate remains version `0.3.0`; build ID is `0.3.0+g5d7538137831`, from source commit
  `5d75381378317b6eeeaceeeee75d5a7324475c21`.
- VSIX SHA-256: `9b24c916025c37f6c80aa80306426386bc98e09afa0d29575d34c5a00be0d801`.
- Adapter SHA-256: `c2efe6dc092d37b8358f8b879e6bd17b16506c72036a5391a6df57a15182a2d5`.
- A `BackendEvaluationException` in the `hover` context is now returned as a successful Evaluate response containing
  the diagnostic and no expandable handle. `watch` remains a failed Evaluate response so VS Code preserves its
  inline error presentation; valid evaluation is unchanged.
- Verification passed: type checking, third-party provenance, 143 adapter tests, 9 integration tests, 19 build tests,
  93 extension tests, 4 package contract tests, 17-entry runtime inventory, and the 33-file VSIX audit. The existing
  upstream unused-variable warning in `Mono.Debugging.Soft` remains unchanged.
- After the user stopped debugging and exited Play, VS Code installed this exact candidate. The installed version,
  build ID, source commit, and Adapter SHA-256 match the audited VSIX; Reload is required before the real retest.
- EVAL-02 remained divergent until this exact candidate was installed and the identical Hover was retested in MyGame.

### 2026-08-06 - Pure 0.3.0 reference Hover-error presentation real run

- After Reload, Pure attached in Edit Mode and ordinary SampleScene Play stopped at
  `Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11` with the yellow current-statement marker.
- Hover and Watch for `RuntimeInitializeLoadType.AfterSceneLoad` retained the exact reference scope diagnostic.
- Hover raised no bottom-right error notification, while Watch retained its inline error styling.
- Hover `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` still evaluated to `true`; no new difference or marker loss
  appeared. This initially appeared to align EVAL-02.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T133854973Z-82480.log`.
- After Continue stopped at `GamePrototypeRuntime.cs:214`, automatic Watch refresh raised bottom-right notifications for
  both `RuntimeInitializeLoadType.AfterSceneLoad` and `DefinitelyMissingName`, even though their Watch rows retained
  the correct inline diagnostic style. The reference debugger never raises these notifications, so EVAL-02 remains
  divergent across stop transitions.
- Screenshots:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-31e0d672-fed1-47f9-8c74-535cabbbc20c.png` and
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-f8a1c901-ceef-4464-91e6-033d468ea551.png`.

### 2026-08-06 - Pure 0.3.0 silent evaluation-error candidate

- Candidate remains version `0.3.0`; build ID is `0.3.0+gd09610c2dffe`, from source commit
  `d09610c2dffea9594c8be3f2c2563aec93d81830`.
- VSIX SHA-256: `cd74f5bc1b481cc8568db6aff2e6d35438e9a7952a02d0817f60490130f4cdcf`.
- Adapter SHA-256: `520eb0cbd158201a0e1aa53e10ca1b013c82d106472ef18973ea00c977ed4d89`.
- Hover and Watch evaluation failures remain failed DAP Evaluate responses with their exact diagnostics, but now set
  `body.error.showUser` to `false`. This preserves inline error styling while suppressing global notifications on
  initial evaluation and automatic Watch refresh at a later stop.
- Verification passed: type checking, third-party provenance, 143 adapter tests, 9 integration tests, 19 build tests,
  93 extension tests, 4 package contract tests, 17-entry runtime inventory, and the 33-file VSIX audit. The existing
  upstream unused-variable warning in `Mono.Debugging.Soft` remains unchanged.
- After the user stopped debugging and exited Play, VS Code installed this exact candidate. The installed version,
  build ID, source commit, and Adapter SHA-256 match the audited VSIX; Reload is required before the real retest.
- EVAL-02 remains divergent until this exact candidate is installed and retested through the same line 11 to line 214
  stop transition in MyGame.

### 2026-08-06 - Pure 0.3.0 silent evaluation-error real run

- After Reload, Pure attached in Edit Mode and stopped at `GameRuntimeBootstrap.cs:11` with a normal yellow marker.
- The scope diagnostic remained correct in Hover and Watch without a bottom-right notification.
- Continue stopped at `GamePrototypeRuntime.cs:214`; both invalid Watches refreshed with inline errors and no global
  notification. Variables and the yellow marker remained available, aligning the complete EVAL-02 sequence.
- A new VAR-02 difference was observed at that instance frame: Pure Locals contained only top-level `this`, while the
  reference Locals contains peer entries `Active scene`, `this`, and `this.gameObject`.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260806T135012903Z-79980.log`.

### 2026-08-06 - Pure 0.3.0 reference Locals source candidate

- The user approved replacing Pure's top-level instance Locals output after the VAR-02 difference was recorded.
- The replacement follows the reference-observed main-thread-only topology and exact order: `Active scene`, `this`,
  `this.gameObject`, visible locals, then parameters.
- Unity synthetic values use the mature Soft Debugger mirror path with breakpoints disabled, single-threaded invocation,
  and the reference timeout boundary. Missing Unity types, non-Component frames, failed Getters, and timeouts omit only
  the unavailable synthetic entry; ordinary frame values remain available.
- Focused mature-evaluation tests passed 5/5. Full verification passed: TypeScript type checking, third-party
  provenance, 145 adapter tests, 9 integration tests, 19 build tests, and 93 extension tests. The existing upstream
  unused-variable warning in `Mono.Debugging.Soft` remains unchanged.
- This is source evidence only. VAR-02 remains divergent until the resulting `0.3.0` candidate is installed and the
  reference action sequence is repeated at `GamePrototypeRuntime.cs:214`.

### 2026-08-06 - Pure 0.3.0 reference Locals packaged candidate

- Candidate remains version `0.3.0`; build ID is `0.3.0+g22c0cbfa14a6`, from source commit
  `22c0cbfa14a6fe5ce36b6971ebfc94299de9aa11`.
- VSIX SHA-256: `3d10cc9b34642ea763a0d78271934755ae282aae481ce5db0b84bb74e2bfc950`.
- Adapter SHA-256: `fb16b25ec0ec890beda0680f5d40f402b9b51bc48ddecf3a6a2e115e52d4e18c`.
- Runtime inventory verification passed for 17 assemblies; VSIX verification passed for 33 packaged files; all 4
  package contract tests passed.
- The previously installed `0.3.0+gd09610c2dffe` VSIX and checksum were preserved as
  `dist/unity-debugger-pure-0.3.0.superseded-before-reference-locals.vsix` and its adjacent `.sha256` file.
- VS Code installed this exact candidate after debugging and Play Mode were stopped. The installed version, build ID,
  source commit, and Adapter SHA-256 match the audited VSIX; Reload and the MyGame real run remain pending. VAR-02 is
  not yet aligned.

### 2026-08-06 - Pure 0.3.0 reference Locals real run

- After Reload, Pure attached in Edit Mode, entered Play, and stopped at `GameRuntimeBootstrap.cs:11` with the yellow
  current-statement marker.
- Continue reached `GamePrototypeRuntime.cs:214`. Locals now contained the reference-ordered peer entries
  `Active scene`, `this`, and `this.gameObject`, and no warning, notification, or marker loss appeared.
- Expanding `this.gameObject` did not expose `transform` or other members. It produced the child diagnostic
  `Object reference not set to an instance of an object.`, which differs from the reference plugin.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-928298ce-be6e-44bd-8d3f-e78398e70124.png`.
- VAR-02 and EVAL-03 remain divergent. Per the compatibility rule, implementation stops for the user's decision
  before the synthetic GameObject child source is replaced.
- A second member-tree difference was then identified while expanding `this`: the source-defined `CurrentSnapshot`
  Getter was not visible, and Pure did not reproduce the reference plugin's `base` node. This is tracked separately as
  VAR-03; the exact reference placement of remaining non-public properties still requires matched-tree evidence.

### 2026-08-06 - Pure 0.3.0 expandable Locals source candidate

- The user approved replacing both divergent paths: the unbound synthetic GameObject child source and the flattened
  member presentation.
- Root cause of the GameObject failure was an Adapter-created `ObjectValue` that had not been connected to its stopped
  `StackFrame`; child callback connection therefore dereferenced a missing parent frame on first expansion.
- Synthetic Unity values now use the same frame callback binding as normal locals. Variable creation, expansion, and
  Set Value use cloned variable-only options that preserve `base` nodes and inline non-public fields/Getter properties;
  Hover and Watch retain the unchanged session evaluation options.
- Focused mature-evaluation tests passed 7/7. Full verification passed: TypeScript type checking, third-party
  provenance, 147 adapter tests, 9 integration tests, 19 build tests, and 93 extension tests. The existing upstream
  unused-variable warning in `Mono.Debugging.Soft` remains unchanged.
- VAR-02, VAR-03, and EVAL-03 remain divergent until this source candidate is packaged, installed, and exercised at
  the matched MyGame instance frame.

### 2026-08-06 - Pure 0.3.0 expandable Locals packaged candidate

- Candidate remains version `0.3.0`; build ID is `0.3.0+g74de71ec9004`, from source commit
  `74de71ec9004675ac5a303b73cec2609d931a2ee`.
- VSIX SHA-256: `6a538ee7d4b5042aee53fc63950678a843580cc71f9c85a530054b48d3ef5e40`.
- Adapter SHA-256: `d86575927c0005c3aff93f98a28df8b31d05efd38db8bad547fdd2f25f4c1143`.
- Runtime inventory verification passed for 17 assemblies; VSIX verification passed for 33 packaged files; all 4
  package contract tests passed.
- The installed predecessor and checksum were preserved as
  `dist/unity-debugger-pure-0.3.0.superseded-before-expandable-locals.vsix` and its adjacent `.sha256` file.
- VS Code installed this exact candidate after debugging and Play Mode were stopped. The installed version, build ID,
  source commit, and Adapter SHA-256 match the audited VSIX; Reload and matched MyGame verification remain pending,
  so no compatibility row changes status yet.

### 2026-08-06 - Pure 0.3.0 expandable Locals real run

- After Reload, Pure attached in Edit Mode, entered Play, stopped at `GameRuntimeBootstrap.cs:11`, and continued to
  `GamePrototypeRuntime.cs:214`.
- Locals preserved the reference order `Active scene`, `this`, and `this.gameObject`.
- `this.gameObject` expanded without the prior null-reference child and exposed `transform`.
- `this` exposed the reference `base` node and the source-defined `CurrentSnapshot` Getter directly, with no
  `Properties` group.
- Hover and Watch remained normal. No warning, error notification, or yellow-marker loss occurred.
- VAR-02 and VAR-03 are aligned. EVAL-03's Getter/member-tree portion is aligned; the collection-expansion portion was
  not explicitly repeated in this run and remains tracked separately rather than inferred.

### 2026-08-07 - Pure targeted Step In and post-step inspection divergence

- At `GamePrototypeRuntime.cs:214`, `Debug: Step Into Target` did not visibly enter `EnsureStyles()`. Ordinary F11 did
  enter the method, then VS Code displayed a raw `scopes` request error plus `Managed inspection failed. Pause again
  and retry.` and `Expression evaluation failed.` notifications.
- The sanitized adapter log records two `stepInTargets` -> targeted `stepIn` sequences followed by stopped-view
  refreshes. The subsequent ordinary F11 refresh starts one evaluation, waits one second, then queues the remaining
  Watches; those evaluations complete serially at one-second intervals before `scopes` is processed.
- Installed reference 1.2.1 binary inspection confirms that its adapter delegates target discovery to
  `IDebugProgramEnhancedStep90.EnumCodePaths`. The Unity engine scans from the current IL offset to the next IL offset,
  accepts `call`/`callvirt`, de-duplicates by metadata token, resolves iterator targets through
  `TryGetDebuggableTarget`, registers `UnityDebugCodePath` objects, and executes the selected code-path stepper through
  `BreakpointManager.SimulateStepIntoSpecific`.
- Pure instead derives the range from source `Method.Locations`, rejects targets without direct locations, and owns a
  separate temporary-breakpoint request path in the vendored Mono.Debugging session. This is not the reference
  control path and the real run proves it is not behaviorally equivalent.
- The post-step error chain is also Pure-specific: backend inspection methods ignore their request timeout argument;
  expression evaluation uses the one-second session timeout; `GetScopes` eagerly waits for every composed local; and
  `BackendEvaluationException` is not classified as an inspection failure. The raw exception therefore escapes the
  `scopes` request, while generic inspection errors are sent as user-visible DAP errors.
- CTRL-02 and EVAL-05 are divergent. Per the compatibility rule, implementation stops for one combined user decision
  before replacing the target-control and post-step inspection/error paths.
- The user approved replacing both divergent paths. The source candidate now uses the reference next-IL target
  selection rules and specific target-breakpoint resume sequence. Scopes no longer waits for every composed local;
  request timeouts reach the Mono evaluation options; Scopes/Variables inspection failures return empty collections;
  and evaluation failures retain inline failure semantics with `showUser=false`.
- Automated source-candidate verification passes 152 Adapter tests, including the next-IL target selector,
  non-blocking Scopes with propagated request timeouts, and notification-free inspection failures. Runtime status
  remains divergent pending a packaged MyGame A/B; test success is not substituted for the visible reference result.
- Packaged candidate: version `0.3.0`, build ID `0.3.0+gaaca01c722f7`, source commit
  `aaca01c722f764e94711dcbf642e7f5e5e4f30c7`, VSIX SHA-256
  `a79391a454303ca7786a58a42a89c10a2b555fc3cc732bcbd98a9a44ae292ee2`, and Adapter SHA-256
  `11d0fcad86b2b62bbd5b92a4c0e3d41f161de548844c4552b82cbcd2ca6f7982`. Package verification passed with 33
  files and 17 audited runtime assemblies. After the previous debug session stopped, the VSIX was installed to VS
  Code and the installed build ID and Adapter SHA-256 matched the package. Reload and MyGame runtime A/B remain
  pending.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-9625be40-9ee1-4bc4-a61b-0ef855b6c362.png`.
- Sanitized adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260807T060641279Z-46232.log`.

### 2026-08-07 - Targeted Step In candidate still fails at runtime

- After Reload and Attach with build `0.3.0+gaaca01c722f7`, the first acceptance item failed: `Debug: Step Into
  Target` did not enter `EnsureStyles()`.
- The new Adapter log confirms `stepInTargets` at `06:59:57.049Z`, targeted `stepIn` at `06:59:59.018Z`, and the
  stopped-view refresh beginning at `06:59:59.335Z`. This rules out a missing command or stale installation; the
  control path resumed and then reported a stop without the reference-visible target position.
- MyGame IL/PDB inspection confirms the stopped `OnGUI()` statement has `EnsureStyles()` as a `call` at IL offset
  `0x0002`, before the next sequence-point offset `0x0008`, and `EnsureStyles()` has a valid first location at IL
  offset `0x0000`, source line 1070. Target discovery and symbol availability are therefore not the remaining gap.
- Full reference IL inspection found one material request-lifecycle mismatch. Reference
  `SpecificBreakpointRequest` creates and enables the breakpoint without assigning
  `BreakpointEventRequest.Thread`; `ProcessSpecificRequest` later requires both request-object identity and matching
  `ThreadMirror.ThreadId`. The failed Pure candidate instead assigned the runtime request's Thread filter and treated
  request identity alone as completion.
- The replacement source candidate removes the runtime Thread filter, stores the expected thread ID beside the
  request, and performs reference-equivalent request/thread matching when the breakpoint event is dispatched. A
  focused red/green regression test records both required matches. Runtime status remains divergent until this new
  candidate is packaged and verified in MyGame.
- Replacement package: version `0.3.0`, build ID `0.3.0+g19772c378d36`, source commit
  `19772c378d363e2f77836fb101abd576dd8f1a24`, VSIX SHA-256
  `e73893565d92a252171289b44fd0218912e72770a0901ae4f86482c11d268756`, and changed
  `Mono.Debugging.Soft.dll` SHA-256 `83418b3dc1568a07095bf88a6fd9b407bcaff5fb4c61fe9dd2d144ef036bfe81`.
  All 19 build tests, 93 extension tests, 153 Adapter tests, 9 integration tests, and 4 package tests pass. Installation
  completed after the active failed-candidate session stopped; the installed build ID and changed DLL hash match the
  package. Targeted Step In and its stopped-view inspection now pass in MyGame; Jump/Set Next Statement and ordinary
  F11 inspection remain separate pending checks.

### 2026-08-07 - Targeted Step In replacement aligns in MyGame

- After Reload and Attach with `0.3.0+g19772c378d36`, `Debug: Step Into Target` entered `EnsureStyles()` at line 1070.
- At that stop, Variables contained values; Hover `_titleStyle` and `this` returned values; all Watch entries refreshed;
  invalid expressions stayed inline; and no right-corner error notification appeared.
- Step Over remained immediate and preserved the yellow current-statement marker. The Adapter log records the targeted
  `stepIn` at `07:15:53.759Z`, the stopped-view refresh starting at `07:15:53.765Z`, five automatic Watch evaluations
  within about five milliseconds, Scopes/Variables immediately afterward, and the later Step Over refreshes without
  the former one-second gaps.
- CTRL-02a and EVAL-05a are aligned. CTRL-02b and EVAL-05b remain separate pending checks because the current run did
  not repeat Jump/Set Next Statement or ordinary F11; reference compatibility is not inferred across those controls.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260807T071541380Z-28656.log`.

### 2026-08-07 - Goto replacement source pending MyGame verification

- The installed targeted-step candidate accepted `gotoTargets`, then threw `NotSupportedException` while processing
  `goto`. The Adapter log records those requests at `07:21:15.153Z` and `07:21:15.160Z` respectively.
- Source tracing found that Pure registered only the requested source line, ignored the DAP thread ID during execution,
  and called the active-thread `DebuggerSession.SetNextStatement` overload directly.
- Full reference decompilation shows a different complete path: `gotoTargets` resolves a real `Location`; `goto` validates
  that location against the requested thread's top frame, calls `ThreadMirror.SetIP`, installs an unfiltered one-shot
  breakpoint at the same location, resumes, and matches completion by request identity plus runtime thread ID. The DAP
  side emits one `stopped` event with reason `goto` and does not expose an unsupported-operation notification.
- Replacement source now follows that sequence and keeps the version at `0.3.0`. Focused red/green tests cover the
  requested-thread/resolved-location API and notification-free `NotSupportedException` behavior.
- Packaged candidate: build ID `0.3.0+g4869d3b8cd91`, behavior source commit
  `9f132eb3df7eeba2d75fd8785ed982eb09749484`, inventory/build commit
  `4869d3b8cd91134beca123c5e9b1fc23bb3600da`, VSIX SHA-256
  `8a9cde5eb690c707cf66a140677a7ebaffe053f58861028446ed8310f7cc5954`, and changed
  `Mono.Debugging.Soft.dll` SHA-256 `b0cdd5d90618391146588504a559f411a4320811a1c45b7d454acce47def7dfd`.
  All 19 build tests, 93 extension tests, 155 Adapter tests, 9 integration tests, and 4 package tests pass.
- The VSIX is installed in VS Code. Installed build identity and the hashes of `Mono.Debugging.Soft.dll` and
  `UnityDebuggerPure.exe` match the package. Reload and MyGame A/B remain pending.

### 2026-08-07 - First Goto replacement candidate returns no target

- After Reload and Attach with `0.3.0+g4869d3b8cd91`, both Jump to Cursor and Set Next Statement reported
  `No executable code is associated at the current cursor position.` The Adapter log contains three `gotoTargets`
  requests and no `goto`, proving execution never reached the SetIP/request lifecycle.
- Further reference comparison found that `SourceFileLocationHandler` matches only the mapped file and line. Pure passed
  the cursor column into the legacy location resolver, which requires an exact PDB column unless the requested column is
  one. This rejected normal cursor positions before returning a target.
- The replacement source normalizes every Goto lookup to column one while preserving the requested-thread and resolved
  `Location` execution path. A red/green theory records cursor columns 0, 1, and 37.
- Packaged and installed replacement: version `0.3.0`, build ID `0.3.0+gc88182a24783`, behavior source commit
  `434d1831dc7681a7708388c5639d21c922af5f83`, inventory/build commit
  `c88182a24783c1732f7008f374ee29a045ca6011`, VSIX SHA-256
  `07550c4aa0334c2b119bdecf25ac9d3b69570303c6c939c63be19bb3b420f12d`, and changed
  `Mono.Debugging.Soft.dll` SHA-256 `8b9f2bb74feb492e0760c2d51a3f1b59806d459012131d832cbe9b3578a75971`.
  All 19 build tests, 93 extension tests, 158 Adapter tests, 9 integration tests, and 4 package tests pass. Installed
  build identity and core binary hashes match the package; Reload and MyGame A/B remain pending.

### 2026-08-08 - Empty Hover notification replacement pending MyGame verification

- With build `0.3.0+gc88182a24783`, Jump to Cursor reached the `goto` request, refreshed the stopped view, and allowed
  the following Step Over. After Continue re-hit the breakpoint, VS Code displayed the bottom-right notification
  `An evaluation expression is required.` The Adapter log records the new stop followed by automatic evaluation,
  Scopes, and Variables requests.
- Reference `HandleEvaluateRequest` does not create a special empty-expression user error. It passes the expression to
  `EvaluateExpression`; an evaluation failure becomes a plain `ProtocolException`. The installed reference protocol
  library serializes that exception without a `showUser` message flag.
- Pure instead rejected whitespace before reaching the backend and used `SendErrorResponse` with its default
  `user=true`. The replacement removes that custom rejection, passes an absent expression as an empty string to the
  ordinary evaluator, and retains the existing evaluation-failure response with `showUser=false`.
- A red/green Adapter test proves the former branch returned `An evaluation expression is required.`, then proves the
  replacement calls the backend and returns the reference parse failure without requesting a notification.
- Packaged candidate: version `0.3.0`, build ID `0.3.0+g8c6f030f7256`, behavior source commit
  `e6cf5311f6a467dbabd209e433ea9475c8fed332`, inventory/build commit
  `8c6f030f725671d98e7c36fbb0ee62f20dff0c7c`, VSIX SHA-256
  `359111230658f997136d0b2d7cd8107a8b9281a20afb185e782581ee21b4a99a`, and changed
  `UnityDebuggerPure.exe` SHA-256 `4ac05c306db31c5d3e52d7958d2f81da55d6a694b5f97b0150340a60e600d647`.
  All 19 build tests, 93 extension tests, 159 Adapter tests, 9 integration tests, and 4 package tests pass. The VSIX is
  installed in VS Code, and the installed build identity and executable hash match the package; Reload and MyGame A/B
  remain pending.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-87bcc62e-fabd-4eaa-b9c2-52875a4dbeca.png`.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T084341917Z-85392.log`.

### 2026-08-08 - Goto and empty Hover replacement align in MyGame

- After Reload and Attach with build `0.3.0+g8c6f030f7256`, Jump to Cursor from line 1070 to line 1076 moved the
  yellow current-statement marker without a warning or notification. The immediately following Step Over stopped
  normally.
- Continue re-hit line 1070 and completed automatic Watch, Scopes, and Variables refresh without displaying
  `An evaluation expression is required.` or another error notification.
- Set Next Statement to line 1076 also moved the yellow marker without a warning or notification, and its following
  Step Over stopped normally.
- The Adapter log records the first `gotoTargets` and `goto` at `08:57:28Z`, its `next` at `08:57:31Z`, the new-stop
  inspection refresh at `08:57:35Z`, the second `gotoTargets` and `goto` at `08:57:41Z`, and its `next` at
  `08:58:01Z`. Both Goto stops and both Step Over stops were followed by evaluation, Scopes, and Variables requests.
- CTRL-02b and EVAL-06 are aligned with the recorded reference behavior.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T085714794Z-67828.log`.

### 2026-08-08 - Variables, collection expansion, and Set Variable align in MyGame

- With build `0.3.0+g8c6f030f7256`, expanding `this` preserved the `base` node and displayed the `CurrentSnapshot`
  Getter directly without a `Properties` group.
- Watch displayed `BoardIndexByCell` as `int[5,8]` and expanded its array elements normally. `this.ToString()` and
  `gameObject.ToString()` both returned normal display values. `DefinitelyMissingName` remained an inline evaluation
  error and raised no bottom-right notification.
- Set Value changed `_status` to `"Pure set value test"`, and the displayed value refreshed immediately.
- The Adapter log records the member and collection `variables` requests at `09:04:08Z` and `09:04:12Z`, the Watch and
  inspection refresh batches at `09:04:26Z` and `09:04:33Z`, and `setVariable` followed by automatic evaluation refresh
  at `09:05:02Z`.
- EVAL-03 and SET-01 are aligned. EVAL-04 remains aligned for invalid identifiers; a deliberately failing Getter is
  still unverified because this class exposes no stable failing property.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T085714794Z-67828.log`.

### 2026-08-08 - Implicit evaluation configuration removal aligns

- The installed `0.3.0+g8c6f030f7256` manifest has no contributed configuration and exposes only `name`, `type`, and
  `request` under Attach properties.
- Neither the installed manifest nor the bundled extension contains `enableImplicitEvaluation`,
  `__enableImplicitEvaluation`, or another implicit-evaluation setting path.
- CFG-01 is aligned; Getter and `ToString()` evaluation remain built-in like the reference.

### 2026-08-08 - Conditional breakpoint, Logpoint, and Function Breakpoint align in MyGame

- With build `0.3.0+g8c6f030f7256`, a line-214 conditional breakpoint using `_status == null` did not stop. Rebinding
  it to `_status != null` immediately stopped normally with the yellow marker and Variables.
- Replacing it with Logpoint `Pure log status={_status}` produced `Pure log status=Pure set value test` without
  pausing. Removing it after the first output prevented the `OnGUI` call site from continuing to print.
- Function Breakpoint `MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles` stopped around line 1069 with the
  yellow marker and Variables. No warning, error notification, or marker loss appeared in any of the three checks.
- The Adapter log records the false-condition bind and Continue at `09:07:10Z`-`09:07:12Z`, the true-condition rebind
  and stop at `09:07:21Z`, Logpoint rebinding and non-stopping Continue at `09:07:37Z`-`09:07:44Z`, and the function
  breakpoint request and stopped-view refresh at `09:08:12Z`.
- BP-02, BP-03, and BP-04 are aligned with the recorded reference behavior.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T085714794Z-67828.log`.

### 2026-08-08 - Ordinary F11 inspection aligns in MyGame

- With build `0.3.0+g8c6f030f7256`, a breakpoint at the `EnsureStyles()` call on line 214 stopped normally. One F11
  entered `EnsureStyles()` around lines 1069-1071 and preserved the yellow current-statement marker.
- Variables contained values; Hover `_titleStyle` and `this` returned values; all Watches refreshed; invalid
  expressions stayed inline; and no bottom-right error notification appeared.
- Step Over responded immediately and moved the yellow marker normally. The user also issued two further rapid Step
  Over requests, which each produced a normal stop and full inspection refresh without an error or marker loss.
- The Adapter log records ordinary `stepIn` at `09:01:11.630Z`, five automatic Watch evaluations beginning about 86
  milliseconds later, Scopes and Variables immediately afterward, the explicit Hover/Variables inspection at
  `09:01:34Z`, and responsive `next` requests at `09:01:51Z`, `09:01:55Z`, and `09:01:56Z`.
- EVAL-05b is aligned with the recorded reference behavior.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T085714794Z-67828.log`.

### 2026-08-08 - Pause control-path replacement pending MyGame verification

- Repeated Pause/Continue with build `0.3.0+g8c6f030f7256` produced three Pause stops. The first two were followed by
  Threads/StackTrace only; the third was followed by evaluations, Scopes, and Variables and displayed a source line
  with the yellow current-statement marker. This confirms the observed Pause presentation is intermittent rather
  than an unconditional synthetic source stop.
- Full installed-reference inspection shows that DAP Pause calls `CauseBreak`; `CauseBreak` suspends once, enumerates
  all runtime threads, and calls `SendBreak` for each thread. The adapter converts every callback to its own Pause
  stopped event with `allThreadsStopped=true`. A thread with document context can expose source and Variables, while
  another thread can remain source-less.
- Pure instead called the vendored Mono session's former `OnStop`, which searched thread backtraces for a user-code
  frame, selected that one thread, and emitted one stop. Besides differing from the reference event sequence, that
  search eagerly fetched stack data during Pause.
- The user confirmed replacing this behavior. The new source suspends once and reports every runtime thread in its
  existing order, emitting no eager backtrace and retaining the last reported thread as the control thread. The old
  preferred-user-frame scan has been removed.
- A focused regression first failed with only thread `42` observed where `7, 42, 99` were expected, then passed after
  the replacement. All 19 build tests, 93 extension tests, 160 Adapter tests, 9 integration tests, and 4 package tests
  pass.
- Packaged and installed candidate: version `0.3.0`, build ID `0.3.0+g10b1d65475b8`, behavior source commit
  `725d130f49046df217073d56dfa34a3209ec4ff2`, inventory/build commit
  `10b1d65475b8b3adca39e5b7e0ea961fa4ec0e57`, VSIX SHA-256
  `2af46a12d9c7524e1cba830761f7f0cef4d1f65ca263e73c4f439ad8ed1a1dec`, and changed
  `Mono.Debugging.Soft.dll` SHA-256 `138ab26d375d434beb179fb28df11461085eaec35c81cdafddb8d1d6a8a173c3`.
  Installed build identity and core binary hashes match the package. Reload and repeated MyGame Pause/Continue plus
  detach/reattach verification remain pending.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T091142741Z-98028.log`.
