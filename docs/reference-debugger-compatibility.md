# Reference Debugger Compatibility

Reference: installed `zlorn.vstuc 1.2.1`.
Target: `D:\Unity\TuanjieHub\Projects\MyGame`, Tuanjie `2022.3.62t12`.

| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| ATT-01 | Attach in Edit Mode, then enter Play | Breakpoint is gray while attached in Edit Mode; entering Play immediately stops at the breakpoint with a yellow current-statement marker and no source-less user stop | Entering Play stopped directly at line 11 with a yellow marker and no source-less stop | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature lifecycle replacement matches this reference sequence |
| RLD-01 | Ordinary Play/Domain Reload with default exception filters | No background exception stop; both `All Exceptions` and `User-Unhandled Exceptions` are unchecked by default | No background exception pause occurred before the managed breakpoint | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Expression-context errors occurred only after the managed stop and are tracked separately |
| BP-01 | First hit at `GameRuntimeBootstrap.Install()` line 11 | Entering Play immediately stops with a yellow current-statement marker | First reachable hit stopped directly at line 11 with a yellow current-statement marker | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | Mature breakpoint lifecycle matches this reference sequence |
| EVAL-01 | Hover/Watch `GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled` | Hover and Watch both evaluate to `true` through the property Getter | Hover failed at the same stopped line | divergent; reference verified | 2026-08-06 MyGame reference and Pure 0.3.0 runs | User approved replacing the mature type-resolution bridge |
| EVAL-02 | Hover/Watch `RuntimeInitializeLoadType.AfterSceneLoad` | Hover and Watch both report <code>The identifier `RuntimeInitializeLoadType` is not in the scope</code> | Hover and Watch report the same scope diagnostic | aligned | 2026-08-06 MyGame reference and Pure 0.3.0 runs | The replacement deliberately does not resolve imported Unity types |
| STEP-01 | Step In, then rapid Step Over input | Step In lands in the property Getter at `GameRuntimeBootstrapMenu.cs:13`; three immediate Step Over clicks remain clickable, emit no warning, stop at caller `GameRuntimeBootstrap.cs:24`, and preserve the yellow marker | Warning/no-response/marker loss observed | divergent; reference verified | 2026-08-06 MyGame reference run | User confirmed replacement: reproduce reference request handling and marker continuity |
| VAR-01 | Expand variables across resume/new stop | Variables refresh across the recorded Step In/rapid Step Over sequence without exposing a stale-collection warning | `Variable collection is no longer available` observed | divergent; reference verified | 2026-08-06 MyGame reference run | User confirmed replacement of the current variable-handle lifecycle |
| VAR-02 | Instance Locals topology | Locals has peer synthetic entries `Active scene`, `this`, and `this.gameObject`; `transform` is below `this.gameObject`; Getter values display directly with no `Properties` group | Not verified | reference verified | 2026-08-06 MyGame reference run and screenshot | User confirmed replacement of Pure's Locals presentation to preserve this layout |
| BP-02 | Conditional breakpoint at `GamePrototypeRuntime.cs:214` | `_status == null` never stops; `_status != null` stops normally with the yellow marker and Variables | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |
| BP-03 | Logpoint at `GamePrototypeRuntime.cs:214` | `Reference log status={_status}` outputs `Reference log status=等待登录` without stopping; no warning, expression error, or marker loss | Not verified | reference verified | 2026-08-06 MyGame reference run | Await Pure A/B |
| BP-04 | Function breakpoint | Fully qualified `MyGame.Runtime.DevTools.GamePrototypeRuntime.EnsureStyles` is accepted and stops at the method definition around line 1069 | Advertises function-breakpoint support, but `SetFunctionBreakpoints` returns an empty result without binding | divergent; reference verified | 2026-08-06 MyGame reference run and Pure source audit | User confirmed replacement with mature function-breakpoint binding |
| EX-01 | Caught/unhandled exception filters | Exposes unchecked `All Exceptions` and `User-Unhandled Exceptions`; default Play does not pause; User-Unhandled alone does not stop for the handled `TaskCanceledException`; enabling All stops at its throw site in `UnityAssetLinkReceiver.cs:94` with the yellow marker and inline exception panel | Exposes `All Exceptions` unchecked and `Uncaught Exceptions` checked by default | divergent; reference verified | 2026-08-06 MyGame reference runs, screenshots, and Pure source audit | User confirmed replacement of filter identity/defaults and mature exception mapping |
| CTRL-01 | Pause, Continue, Step Out | Continue resumes normally; a manual Pause with no displayable managed source frame shows no source line, yellow marker, warning, or Variables; Step Out from the caller resumes normal execution | Not verified | reference verified | 2026-08-06 MyGame reference runs | Do not synthesize a source frame or variable scope for a source-less manual Pause; await Pure A/B |
| CTRL-02 | Step-in Targets and Goto capability/presentation | All three commands are visible and enabled; Step Into Target enters `EnsureStyles()` at line 1070, Jump to Cursor stops at line 1076, and Set Next Statement immediately moves the yellow marker to line 1076; no warning or marker loss, and subsequent Step Over works | Not verified | reference verified | 2026-08-06 MyGame reference runs | Await Pure A/B |
| EVAL-03 | Locals, Getter, `ToString()`, collection expansion | Getter values display directly; `this.ToString()` and `gameObject.ToString()` return Unity object display strings; `BoardIndexByCell` displays as `int[5,8]` and expands normally; no loading placeholder or delayed update was observed in this run | Current mature build has not yet been retested at the recorded instance-method stop; the first-stop screenshot was captured in a static method | divergent; reference verified | 2026-08-06 MyGame reference screenshot and earlier Pure run | Retest at the same instance frame after EVAL-01/EVAL-02 replacement; do not infer from out-of-scope Watches |
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
- EVAL-01 remains divergent until this exact candidate is installed and retested in MyGame.
