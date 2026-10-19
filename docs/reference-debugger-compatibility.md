# Reference Debugger Compatibility

Reference: installed `visualstudiotoolsforunity.vstuc` **1.3.2** (Microsoft's "Unity" for
VS Code), copy `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2`; provenance
`D:\Unity\vstuc-mirror\build_local_vsix.py` — the official VSIX repacked with packaging
metadata only, every code and binary entry copied byte for byte. This supersedes
`zlorn.vstuc 1.2.1` as the authority, re-targeted on 2026-10-16
(`docs/superpowers/specs/2026-10-16-vstuc-user-visible-alignment-design.md`, "Reference
identity"). **Observations recorded before that date keep their `1.2.1` attribution**; only
the live-authority statements name 1.3.2.
Target: `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`, Unity **2022.3.23f1**
(`Library\EditorInstance.json`; `ProjectSettings\ProjectVersion.txt` also declares
`m_EditorVersion: 2022.3.23f1`). This replaces `D:\Unity\TuanjieHub\Projects\MyGame` and the
Tuanjie `2022.3.62t12` baseline, chosen by the user on 2026-10-16; rows recorded against the
retired project keep their own attribution and their own line numbers.

| ID | Scenario/action | Reference result | Pure result | Status | Evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| EVAL-08 | Pass integer IDs `1033/1034` to the enum parameter of `GetAttrCurrValue` | User reports the original Unity plugin accepts the numeric Watch expressions | Type resolution succeeds; the previous candidate rejected `int` to `SGBattleCommon.EFightUnitAttr`; installed 0.3.1 candidate adds integer-to-enum fallback after ordinary method matching | not yet verified | 2026-09-22 user screenshot, full running-adapter error, red/green standalone Mono invocation tests | User explicitly requested the conversion fix; real Editor/reference comparison remains pending |
| EVAL-09 | Keep unrelated threads suspended during Watch methods, getters and ToString, then step | Installed reference invocations use DisableBreakpoints plus SingleThreaded | MethodCall uses the same invocation flags; background-counter and Step Over tests pass; user confirms the reported step symptom is resolved on the 0.3.2 candidate | aligned | 2026-09-24 installed reference binary inspection, real Mono red/green test, and user acceptance | Editor acceptance is user-reported; no new agent-operated reference A/B session was performed |
| EVAL-07 | Watch `FightUnitAttrUtils.GetAttrCurrValue(e, 1033)` from a global-namespace frame with `using SGBattleLogic;` | User reports the Unity reference extension evaluates the expression correctly | Installed Pure reports identifier out of scope; candidate resolves visible source imports and type aliases before invoking the mature evaluator | not yet verified | 2026-09-22 user screenshot, installed binary/source inspection, source-resolution regression tests and actual source probe | User requested alignment; candidate requires real-Editor comparison; EVAL-02 is evidence for that specific expression, not a universal ban on imported type resolution |
| BP-06 | Bound breakpoint filename after status changes, including a saved source outside the attached workspace | User reports the Unity reference extension retains the normal breakpoint presentation | Installed Pure replaces the requested source with an unavailable source; candidate preserves filename, path format, line and ID through status changes | not yet verified | 2026-09-22 user screenshot showing `.` and line 786, read-only saved-breakpoint inspection, four protocol regression cases | User requested alignment; candidate requires real-Editor comparison; runtime stack source mapping remains unchanged |
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
| EX-01 | Caught/user-unhandled exception filters: default Play, then **User-Unhandled Exceptions** alone, then **All Exceptions** | Exposes unchecked `All Exceptions` and `User-Unhandled Exceptions`; default Play does not pause; User-Unhandled alone does not stop for the handled `TaskCanceledException`; All stops on user-code exceptions with a yellow marker and Variables, while source-less exceptions do not surface; subsequent valid exceptions also stop | Build `0.3.0+g0ecce4d08bfb` matched the filter defaults and stop presentation, filtered the source-less empty-Variables stop, and stopped on the same subsequent user-code exceptions as the reference — observed while that build still advertised the filter id `uncaught`. **Re-read 2026-10-16:** the current build advertises `unhandled` (CAP-15) and receives its ids through `filterOptions[]` rather than `filters[]` (CAP-12), a request path the 2026-08-10 run never exercised | not yet verified | 2026-08-06 and 2026-08-10 MyGame reference/Pure A/B runs and sanitized Adapter logs, attributed to `0.3.0+g0ecce4d08bfb`; 2026-10-16 re-read of `UnityDebugSession.cs:60-73` and `:418` against the observed reference body | The recorded A/B run stays true of the build it names, but Task 1 renamed the advertised id and changed which request field carries it, so the evidence no longer covers what ships now and the row is downgraded instead of left asserting verified equivalence for a behaviour that moved; **2026-10-17 attempt, inconclusive and recorded as such**: the session attached to pid 91740 and `setExceptionBreakpoints(["all"])` was accepted, but the exception could not be fired because the Editor had already stopped responding - the MCP call answered `Unity instance ... not found`. **No `stopped` event arrived and the trigger never fired, so this is not evidence that our adapter fails to stop on exceptions**; the Editor stopped responding for about three minutes and its MCP bridge has not re-registered since. Row deliberately left `not yet verified` rather than moved either way |
| CTRL-01 | Pause, Continue, Step Out | `CauseBreak` suspends the target, enumerates every runtime thread in order, and reports one all-threads-stopped Pause event per thread; the event selected by VS Code may therefore have a displayable source frame and Variables or may be source-less; Continue and Step Out resume normally | Build `0.3.0+g10b1d65475b8` suspends once and reports every runtime thread in order without scanning for a preferred user-code frame; six rapid Pause/Continue cycles remained responsive with normal stopped views and no warning or error; Detach preserved Play and reattach hit normally | aligned | 2026-08-06 MyGame reference run; 2026-08-08 reference binary inspection, Pure red/green Adapter test, MyGame run, and Adapter logs; **2026-10-17 A/B on Editor `2022.3.23f1` confirming the per-thread Pause event on both sides** — the reference emitted **46** `stopped reason=pause` events for one Pause and ours 56–60, so the shape this row describes is the reference's own behaviour and not a defect of ours | A yellow marker on Pause is permitted and is not itself a divergence; control flow and all-thread dispatch match the reference |
| CTRL-02a | Step Into Target | Enters `EnsureStyles()` at line 1070 with the yellow marker; subsequent stepping remains responsive and preserves the marker | Build `0.3.0+g19772c378d36` enters `EnsureStyles()` at line 1070; two immediate Step Over operations stop normally with the marker preserved | aligned | 2026-08-06 MyGame reference run; 2026-08-07 Pure run and Adapter log | Unfiltered specific breakpoint plus dispatch-time request/thread matching reproduces the reference result |
| STACK-01 | Call stack after a Pause stop, and the scopes, variables and evaluation it feeds | `stackTrace` on the stopped thread returns a real managed stack: **13 frames** on Editor `2022.3.23f1`, headed `System.Threading.WaitHandle:WaitMultiple` then `WaitAny` then `BlockingCollection`1:TryTakeFromAnyCoreSlow` | **Times out on every thread tried in Play Mode, and answers `0 frames` once then times out in edit mode** | divergent | 2026-10-17 A/B against pid 91740 at `127.0.0.1:56740`: reference launched as its own bundle does (`dotnet bin/UnityDebugAdapter.dll`, `{ projectPath, endPoint }`), ours as the extension does; same Editor, same sequence, minutes apart; matrix Evidence Log entry of the same date | **Observed, not inferred, and it is ours.** The reference walked a 13-frame managed stack at the same kind of stop where ours produced nothing, and because `stackTrace` is what feeds scopes, variables and evaluation the user-visible effect is that our debugger has nothing to inspect in this project. `tests/integration` asserts exactly this stack against `ScenarioDebuggerBackend`, a fake that returns its own frame — so the suite passed while the real Editor returned nothing. **Approval requested 2026-10-17: this row is recorded as observed and no ruling has been given yet**, which is the one place in this matrix where a `divergent` row does not yet cite one.; **2026-10-17 fix committed `8cf499b`** — the timeout is threaded through `IDebuggerBackend` and `ISoftDebuggerSessionFacade` as the four sibling handlers already thread theirs, the walk runs on a worker with a bounded wait, and the handler answers an empty frame list on expiry; three new tests in `Dap/InspectionRequestTests.cs`, each proved to discriminate by mutation (passing `0` instead of the timeout fails two of them; deleting the cancellation catch fails the third). **Claim 1 is delivered: the request is answered. Claim 2 is NOT: the walk itself is still unbounded underneath, so an empty stack is now returned where the reference returns 13 frames.** Real-Editor confirmation of the fix is still outstanding; **2026-10-17 real-Editor A/B after the fix, and it delivered BOTH claims - which is better than the fix was predicted to do.** Same Editor, same sequence, minutes apart: the reference answered in **0.1 s with 13 frames**, and the fixed adapter answered in **0.1 s with 14 frames** whose chain is the reference's own - `System.Threading.WaitHandle` `WaitMultiple`/`WaitAny` into `BlockingCollection<...CompilerServerJob>.TryTakeFromAnyCoreSlow`. Before the fix the same request never answered at all. **The prediction recorded in this row was that claim 2 would still fail and an empty stack would come back; it was wrong and the fix is better than advertised.** Why it now completes is a hypothesis rather than an established cause - the walk moved off the DAP request thread onto a pool worker, which may have been avoiding a re-entrancy deadlock rather than merely being bounded - and it is recorded as a hypothesis. **One difference remains and it is the frame TEXT, not the stack**: we render `System.Threading.WaitHandle.WaitMultiple` from the source location's method name, while the reference renders `Int32 System.Threading.WaitHandle:WaitMultiple (WaitHandle[], Int32, Boolean, Boolean)` with the return type, a colon, and the parameter list. That is user-visible in the Call Stack panel and has no row of its own yet; **2026-10-17 follow-up: aligning that text is reachable entirely from our own code, and the formatter is already in the tree.** `Mono.Debugger.Soft.MethodMirror.FullName` (`adapter/vendor/debugger-libs/Mono.Debugger.Soft/Mono.Debugger.Soft/MethodMirror.cs:59`) builds `ReturnType.Name + " " + Namespace + "." + TypeName + ":" + Name + " (" + ParameterType.Name joined with ", " + ")"` — read line by line at `:64-80`, and it produces `Int32 System.Threading.WaitHandle:WaitMultiple (WaitHandle[], Int32, Boolean, Boolean)`, **character for character the reference's string**. The reference adapter's own literal pool does **not** contain that format (`:WaitMultiple`, `WaitHandle:` and `System.Threading.WaitHandle` are all absent from `UnityDebugAdapter.dll`), which is consistent with it reading the same mirror property rather than formatting anything itself. So this is not a design question about how to build the text — it is only plumbing the mirror through to the frame record. The class carries a `// FIXME:` above that property, whose subject is not stated; **2026-10-17 correction to the paragraph above: it said "one-expression change", and that was wrong.** The place the wrapper actually chooses the short name is `SoftDebuggerBacktrace.cs:192`, which is **vendored source pinned by `scripts/verify-third-party.mjs` and therefore must not be edited**, so the fix cannot be made where the name is chosen. It is still reachable, but through a route I had not checked when I wrote that: `SoftDebuggerSession.VirtualMachine` is **public** (`SoftDebuggerSession.cs:835`), so a facade can get the Mono `ThreadMirror` by id, walk **its** backtrace, and read `MethodMirror.FullName` directly - all inside our own file, with no vendored edit. The cost is therefore a second thread lookup and moving the frame-name source onto the mirror walk, not one expression. **The format match above stands unchanged**; only the effort estimate was wrong, and it is corrected here rather than left to be discovered by whoever implements it; **2026-10-17: I then tried it, and failed, and the failure is the useful part - the route I described above does not exist.** I did not lodge that as a finding, because it is not one: it would repeat the same mistake a third time. The attempt looked up the `ThreadMirror` by thread id through `session.VirtualMachine`, and it compiled and ran but changed nothing, because the wrapper does **not** key threads by the mirror's id. `SoftDebuggerSession.GetThread(long)` (`SoftDebuggerSession.cs:1191`) matches on `GetId(thread)`, and `GetId` (`:3646`) is a **private synthetic renumbering** - `localThreadIds`, `id = localThreadIds.Count + 1` - which is why our own `threads` response shows id `1`. There is no public reverse path from a wrapper `ThreadInfo` to its mirror, and `GetThread` is private, so the alignment cannot be done from `SoftDebuggerSessionFacade` at all. Reverted, and the revert verified: `GetReferenceFrameNames` and `RunBounded` gone, the bounded wait restored inline, 227 adapter tests green, `npm run package` exit 0, the retained release untouched. **The route that does exist is a vendored one, and it is legitimate**: `verify-third-party.mjs` contains no `createHash` at all - it governs vendored code through the `modifications` record in `third-party/sources.json` rather than by pinning content - so the honest change is `SoftDebuggerBacktrace.cs:168`, where `methodName = typeDisplayName + "." + methodName` is what produces the short form, plus a `modifications` entry. **That is where this stops being mine to decide.** `SourceLocation.MethodName` is not only the Call Stack: it also feeds breakpoint labels and `GetLocationSignature`, which `SoftDebuggerSession.cs:2356` hands to `Catchpoint.ShouldIgnore` for exception matching - an area this matrix already records as aligned. So the one-line change is cross-cutting into behaviour that currently matches, and it waits for the user rather than being taken unilaterally; **2026-10-17 the user chose to align, and it is aligned.** One vendored line changed - `adapter/vendor/debugger-libs/Mono.Debugging.Soft/SoftDebuggerBacktrace.cs`, the concatenation that decided the text - with the change recorded in `third-party/sources.json` as that manifest requires. `verify-third-party.mjs` has no `createHash` and still exits 0. Real-Editor A/B, same Editor and sequence: both sides now read `Int32 System.Threading.WaitHandle:WaitMultiple (WaitHandle[], Int32, Boolean, Boolean)` and `Int32 System.Collections.Concurrent.BlockingCollection`1:TryTakeFromAnyCoreSlow (BlockingCollection`1[], CompilerServerJob, Int32, Boolean, CancellationToken)` - **character for character identical**. The change is deliberately narrower than "use FullName": generic methods, generated state-machine types and anonymous methods keep the previous rendering, because `FullName` carries neither a generic method's type arguments nor those user-friendly names, and flattening them would have traded one difference for three regressions. **Two things are still open and are recorded rather than glossed.** First, `SourceLocation.MethodName` also feeds `GetLocationSignature`, which `SoftDebuggerSession.cs:2356` passes to `Catchpoint.ShouldIgnore` for exception location matching - **no test in the suite exercises `GetLocationSignature` or `ShouldIgnore` at all**, and the one real-Editor experiment that would cover it is the exception-filter run that froze this Editor on 2026-10-17, so that path is unverified and is recorded as such. Second, the frame COUNT differs: ours returns 14 frames where the reference returns 13, ours carrying an extra `Int32 System.Threading.WaitHandle:Wait_internal (IntPtr*, Int32, Boolean, Int32)` that the reference omits. Ours shows more, not less, and it reproduces across runs - but it is a difference and the row stays `divergent` on it rather than being closed on the text alone; **2026-10-17 the alignment now has a regression guard, and running it proved it discriminates.** The guard is `Stack_frame_text_matches_the_reference_debugger_format` in `EnumArgumentEvaluationTests`, a `[MonoFact]` that reads the fixture frame's name through the live Mono fixture. Reverting the vendored line makes it fail with `Assert.Matches() Failure: Pattern not found in value` and `Frame text: Fixture.Main` - the old short form - and restoring it passes, so the guard pins the alignment rather than merely accompanying it. **The same work turned up something that had been hiding in plain sight all session: 21 Mono test cases had never run.** `UNITY_DEBUGGER_TEST_MONO` was unset, so every `[MonoFact]` and `[MonoTheory]` skipped itself, and the suite reported `227 passed, 5 skipped` where the honest figure with Unity's own bundled Mono (`Data\MonoBleedingEdge\bin\mono.exe`, Mono 6.13.0 - the same version the Editor reports over DAP) is **249 passed, 0 skipped**. Every earlier count in this matrix that said "227 passed, 5 skipped" was therefore a count of the tests that ran, not of the tests that exist. The release checklist now carries the step; **2026-10-17 correction to the sentence above, and it is mine to correct: "hiding in plain sight" was wrong.** This matrix had already recorded it, in the 2026-09-24 entry at `:845-848`, in these words: "207 adapter tests (all 21 real Mono cases executed, zero skipped) ... GitHub does not provision the optional Mono fixture, so its skipped cases must not be counted as real-runtime coverage." **The exact number 21 was already written down, and so was the warning.** What was actually wrong was my own reporting: I described `227 passed, 5 skipped` as the honest full suite in round after round, when this document already said not to count those cases as coverage. So the honest description of last round is narrower than I first wrote - the genuinely new parts are that **Unity's own bundled Mono works as the fixture** (`Data\MonoBleedingEdge\bin\mono.exe`, Mono 6.13.0, the same version the Editor reports over DAP, which the earlier entry did not name), that the **release checklist** now carries the step where it previously did not, and the **regression guard**, which is new and does discriminate. The rediscovery is not a finding; my misreporting was the defect; **2026-10-19 accepted in a real VS Code session, and the earlier "empty Call Stack" reports were not a defect.** The user opened the acceptance project in VS Code, attached, paused, and expanded threads. The adapter logged the outcome of every `stackTrace` - which thread was asked about and how many frames came back - which is how this was settled rather than argued. Across 45 threads the responses were mostly 14 frames, and the panel showed them with the reference text: `Void System.Threading.Thread:SleepI...`, `Void System.Threading.Thread:Sleep`, `Void System.IO.DefaultWatcher:Monit...` - return type, namespace-qualified type, colon, method, parameter list truncated only by the panel width. **The cross-check that makes this evidence rather than impression: thread 7 returned `eventCount=8` and the user's screenshot shows exactly eight rows under `Thread #7`.** The two threads reported as "still empty" - `Thread #8` and `Thread #9` - returned `eventCount=0` on every request, three times each, so those threads genuinely have no stack; expanding a different thread showed frames immediately. **Root cause of the empty panel: the reported threads had no frames, not a failure to deliver them.** Two things are recorded rather than closed. First, `threadId=1`, the thread the pause request names, also returns 0 frames, which is counter-intuitive for the thread that just stopped and deserves its own look. Second, the count difference stands: ours returns 14 where the reference returns 13, so the status stays `divergent` on that alone; **2026-10-19 the paused thread returning zero frames is the reference's behaviour too, so it is not a defect.** The open question was that `threadId=1`, the thread the pause names, returned 0 frames in every session while other threads returned 14. A probe against the live Editor showed it is named `Thread #1` - the main thread, which a paused Unity Editor spends inside native code with no managed frames. The comparison settles it: running the **same** attach, threads, pause and stackTrace sequence against the reference (`vstuc` 1.3.2 driving `UnityDebugAdapter.dll`) returned `ANSWERED: true after 0.0s` and **`FRAMES: 0`**. Both adapters answer empty for the paused main thread, so a Call Stack that looks empty on open is the reference experience rather than a pure-adapter defect, and the earlier 13-frame reference reading came from a session where the paused thread had managed frames. **No change made.** Recorded so the next person does not re-open it as a bug |
| CTRL-02b | Jump to Cursor and Set Next Statement | Jump to Cursor stops at line 1076; Set Next Statement immediately moves the yellow marker to line 1076; no warning or marker loss, and subsequent Step Over works | Build `0.3.0+g8c6f030f7256` matches both commands at an ordinary cursor column; the yellow marker moves to line 1076 without a notification, and the following Step Over works | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Resolved locations, requested-thread SetIP, reference one-shot completion, and column-independent target matching reproduce the recorded reference sequence |
| EVAL-05a | Inspection immediately after Step Into Target | Watches, Hover, Locals, and Scopes remain responsive; no request error or user notification appears | Variables had content; Hover `_titleStyle` and `this` returned values; Watches refreshed with invalid expressions inline only; no notification appeared; Step Over remained immediate | aligned | 2026-08-06 MyGame reference run; 2026-08-07 Pure run and Adapter log | Five automatic Watch requests completed together, followed by Scopes/Variables without the former one-second serialization |
| EVAL-05b | Inspection immediately after ordinary F11 Step In | Watches, Hover, Locals, and Scopes remain responsive; no request error or user notification appears | Build `0.3.0+g8c6f030f7256` enters `EnsureStyles()` through ordinary F11; Variables has content, Hover `_titleStyle` and `this` returns values, Watches refresh with invalid expressions inline only, no notification appears, and subsequent Step Over remains immediate | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Ordinary and targeted Step In inspection paths now both match the recorded reference behavior |
| EVAL-03 | Locals, Getter, `ToString()`, collection expansion | Getter values display directly; `this.ToString()` and `gameObject.ToString()` return Unity object display strings; `BoardIndexByCell` displays as `int[5,8]` and expands normally; no loading placeholder or delayed update was observed in this run | Build `0.3.0+g8c6f030f7256` displays `CurrentSnapshot` directly under `this`, preserves `base`, evaluates both `ToString()` expressions, and displays and expands `BoardIndexByCell` as `int[5,8]` without a placeholder, delay, or notification | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Getter/member topology, explicit evaluation, and collection expansion match the recorded reference behavior |
| EVAL-04 | Invalid expression and Getter failure presentation | Invalid Watch reports the scope diagnostic inline; a Getter throwing `NotImplementedException` remains inline without a notification, preserves the stopped view, and does not break the following Step Over | Build `0.3.0+g0ecce4d08bfb` matches both invalid-identifier and throwing-Getter presentation and remains responsive afterward | aligned | 2026-08-06 and 2026-08-10 MyGame reference/Pure A/B runs; sanitized Adapter log | Mature evaluation failure presentation matches the reference for both resolution and target-invocation failures |
| EVAL-06 | Empty automatic Hover expression after Continue and a new stop | Ordinary evaluation failure is returned without requesting a user notification | Build `0.3.0+g8c6f030f7256` re-hit the breakpoint after Continue, refreshed Watches, Scopes, and Variables, and raised no `An evaluation expression is required.` notification | aligned | 2026-08-08 MyGame Pure runs, screenshot, Adapter logs, and full reference protocol inspection | The custom rejection is removed; empty expressions use the ordinary evaluation-failure path with `showUser=false` |
| SET-01 | Set Variable | Set Value is available for `this._status`; assigning a new string updates the displayed value immediately | Build `0.3.0+g8c6f030f7256` accepts `"Pure set value test"` for `_status` and refreshes the displayed value immediately | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure run and Adapter log | Mature SetVariable handling and stopped-view refresh match the recorded reference behavior |
| CFG-01 | Public implicit-evaluation setting | No public implicit-evaluation setting is contributed; Getter/`ToString()` evaluation is built-in | Build `0.3.0+g8c6f030f7256` contributed no configuration and exposed only `name/type/request` Attach properties. **Re-read 2026-10-16:** the current build contributes `unity-debugger-pure.displayBackgroundProcesses` (CFG-06) and declares `projectPath`, `logFile` and `endPoint` beside `name/type/request` (CFG-04, CFG-05), and still contains **no implicit-evaluation setting and no private Attach flag** | aligned | 2026-08-06 reference manifest; 2026-08-08 Pure 0.3.0 installed manifest and bundle inspection; 2026-10-16 re-derivation against the current manifest and source (`package.json:65-121`, `tests/build/scaffold.test.mjs`, `tests/extension/productIdentity.test.ts`, `scripts/verify-vsix.mjs`) | The subject survives and is what the status rests on — no public or private implicit-evaluation path exists — while both halves of the 0.3.0 sentence about our configuration surface stopped being true when Tasks 7 and 8 landed, so they are corrected here rather than left asserting a surface that no longer exists |
| END-01 | Detach and reattach while Unity remains in Play | Detach leaves Unity in Play; reattach immediately hits the recurring breakpoint at `GamePrototypeRuntime.cs:214` with a normal yellow marker and Variables, without an intermediate pause, error, or warning | Build `0.3.0+g10b1d65475b8` leaves Unity in Play after Detach; reattach immediately stops normally with the yellow marker and Variables and no intermediate warning or error | aligned | 2026-08-06 MyGame reference run; 2026-08-08 Pure MyGame run and Adapter logs | Lifecycle and stopped-view refresh match the recorded reference sequence |
| CAP-01 | Read the `initialize` response field `supportsConfigurationDoneRequest` | `false` | `false` (`DapCapabilityModels.cs:24`) | aligned | 2026-10-16 reference 1.3.2 adapter `initialize` body from a live DAP probe (spec `:70-90`); 2026-10-16 Pure `DapCapabilityModels.cs:24-46` and `ReferenceCapabilityTests.cs` | Both bodies carry `false`, so neither client is offered a configuration-done step, and a wire-contract value that this work item did not change cannot be altered by a later run |
| CAP-02 | Read `supportsFunctionBreakpoints` | `true` | `true` (`DapCapabilityModels.cs:25`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body and `ReferenceCapabilityTests.cs` | Both accept function breakpoints; BP-04 records the binding behaviour this capability enables and BP-07 the warning it triggers |
| CAP-03 | Read `supportsConditionalBreakpoints` | `true` | `true` (`DapCapabilityModels.cs:26`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both accept a condition on a source breakpoint; BP-02 records the behaviour |
| CAP-04 | Read `supportsHitConditionalBreakpoints` | `false` | field absent from the body | aligned | 2026-10-16 reference body (spec `:100`); 2026-10-16 Pure body has no such member | The reference does **not** support hit conditions, so our numeric-equality parsing of a hit condition is an extra path the reference lacks rather than a gap against it — aligned *with this explanation* so nobody later reads the extra behaviour as a defect or the absence as a missing feature |
| CAP-05 | Read `supportsEvaluateForHovers` | `true` | `true` (`DapCapabilityModels.cs:28`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both evaluate for Hover; EVAL-01 and EVAL-02 record the behaviour behind the flag |
| CAP-06 | Read `supportsSetVariable` | `true` | `true` (`DapCapabilityModels.cs:30`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both expose Set Value; SET-01 records the behaviour |
| CAP-07 | Read `supportsGotoTargetsRequest` | `true` | `true` (`DapCapabilityModels.cs:31`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both offer Jump to Cursor; CTRL-02b records the behaviour |
| CAP-08 | Read `supportsStepInTargetsRequest` | `true` | `true` (`DapCapabilityModels.cs:32`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both offer Step Into Target; CTRL-02a records the behaviour |
| CAP-09 | Read `supportsLogPoints` | `true` | `true` (`DapCapabilityModels.cs:34`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both offer logpoints; BP-03 records the behaviour |
| CAP-10 | Read `supportsTerminateRequest` | `true` | `true` (`DapCapabilityModels.cs:35`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Both advertise terminate, so the client offers the same control; END-01 exercises detach and reattach instead, leaving the terminate request itself unrecorded on either side |
| CAP-11 | Read the ten capability flags both sides report `false`: `supportsStepBack`, `supportsRestartRequest`, `supportsDataBreakpoints`, `supportsReadMemoryRequest`, `supportsWriteMemoryRequest`, `supportsDisassembleRequest`, `supportsSteppingGranularity`, `supportsInstructionBreakpoints`, `supportsSingleThreadExecutionRequests`, `supportsDelayedStackTraceLoading` | `false` for each | `false` for each (`DapCapabilityModels.cs:29`, `:33`, `:36-41`, `:43`) | aligned | 2026-10-16 reference body (spec `:107`); 2026-10-16 Pure body | The two bodies agree on all ten, so no client-visible control is offered on one side that the other lacks, and the reference's own `false` values are what make this an alignment rather than a gap against a richer protocol |
| CAP-12 | Read `supportsExceptionFilterOptions` and the handler that has to receive the ids it advertises | `true` | `true` (`DapCapabilityModels.cs:42`), with `SetExceptionBreakpoints` accepting `filterOptions[].filterId` (`UnityDebugSession.cs:418`, `:1157`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body, handler and `ReferenceCapabilityTests.cs` | Advertising this capability is what makes the client send an empty `filters` array and carry the ids in `filterOptions`, so the handler change is part of the same item and neither the client's payload nor our handler has been run against a live session ; **2026-10-17 A/B, both sides observed**: each adapter's live `initialize` response on Editor `2022.3.23f1` reports `supportsExceptionFilterOptions: true` (ours 23 capability keys, the reference 28); **2026-10-19 corroborated in the user's VS Code session:** the Breakpoints panel showed `All Exceptions` and `User-Unhandled Exceptions` as two unchecked checkboxes, which is both filters present, named and ordered as the reference defines them. This is a UI observation rather than a protocol capture, so it corroborates the A/B above rather than replacing it |
| CAP-13 | Read `supportsExceptionConditions` | `true` | `true` (`DapCapabilityModels.cs:44`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | The advertised flag is present, and its consequence — the client offering the per-filter condition box — has not been exercised; what happens to a typed condition is EX-02 ; **2026-10-17 A/B, both sides observed**: each adapter's live `initialize` response on Editor `2022.3.23f1` reports `supportsExceptionConditions: true` |
| CAP-14 | Read the legacy `supportsExceptionOptions` field | absent | previously `true`, now absent: `DapCapabilities` carries no such property, so the vendored shape that always serialized it is bypassed (`DapCapabilityModels.cs:8-14`) | aligned | 2026-10-16 reference body does not contain the field; 2026-10-16 Pure `DapCapabilities` and `ReferenceCapabilityTests.cs` | The spec recorded this as `divergence — remove`, and removing it is what makes the two bodies agree; the vendored `VSCodeDebug` source stays untouched, so `verify-third-party` remains valid |
| CAP-15 | Read the user-unhandled exception filter id, and the ids accepted on input | `unhandled` | `unhandled` in the advertised filter list (`UnityDebugSession.cs:67-73`), with `uncaught` still accepted so an existing `launch.json` does not fail | aligned | 2026-10-16 reference body (spec `:74-79`); 2026-10-16 Pure filter list, id acceptance and `ReferenceCapabilityTests.cs` | The advertised id now matches the reference's, which is the gap the spec recorded at `:111`, but whether the client's `filterOptions` payload reaches the handler on a real session is CAP-12's unrun path — which is also why EX-01 was downgraded ; **2026-10-17 A/B, both sides observed**: the advertised filter lists are identical - `all` and `unhandled`, both `default=false`, both `supportsCondition=true`, on Editor `2022.3.23f1` |
| CAP-16 | Read both filters' `description` strings | `Break on all exceptions` / `Break only on unhandled exceptions` | the same two strings (`UnityDebugSession.cs:60-73`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Character-for-character match read from both bodies, so the exception-breakpoints UI shows the reference's own sentences |
| CAP-17 | Read both filters' `supportsCondition` and `conditionDescription` | `true` / `List of exceptions ...` | `true` / `List of exceptions ...` (`UnityDebugSession.cs:25-26`, `:60-73`) | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | The advertised shape matches exactly; that we accept `filterId` and ignore the condition a user then types is a separate difference and is EX-02, recorded there so the matching advertisement is not read as a matching behaviour |
| CAP-18 | Read both filters' `default` | `false` | `false` | aligned | 2026-10-16 reference body; 2026-10-16 Pure body | Neither filter is active until the user selects it; EX-01 and RLD-01 both record that default state as the reference's |
| CAP-19 | Read `pid` in the `initialize` body | present, the adapter's own process id (`49888` in the probe) | present, `Process.GetCurrentProcess().Id` (`UnityDebugSession.cs:57`) | aligned | 2026-10-16 reference body (spec `:89`); 2026-10-16 Pure body | Both report the adapter's own process id, so the value differs per process while the field's meaning and presence match — which is the whole of the claim |
| CAP-20 | Read the four empty arrays the reference emits: `completionTriggerCharacters`, `additionalModuleColumns`, `supportedChecksumAlgorithms`, `breakpointModes` | `[]` for each | absent from the body | divergent | 2026-10-16 reference body (spec `:81-88`); 2026-10-16 Pure body; spec Verification 2 | Accepted representation difference in wire shape: they come from the VS protocol library's serializer defaults, our vendored `VSCodeDebug` layer does not emit them, and an empty array is equivalent to an absent property for a DAP client — enumerated by name here because "we did not notice" and "we decided it does not matter" look identical in a matrix that omits them. **Approved 2026-10-17 (batch)**: kept — an accepted wire-shape difference with no user-visible effect ; **2026-10-17 A/B reproduces this independently**: all four are `undefined` on our side against `[]` on the reference's, in the same run |
| CAP-21 | Read `supportsDelayedStackTraceLoading` | `false` | absent from the body | divergent | 2026-10-17 A/B: both adapters' live `initialize` responses compared key by key against Editor `2022.3.23f1` | **Approved 2026-10-17 on the user's instruction to register it as its own row.** Same class as `CAP-20`: an absent property against an explicit default, where DAP's own default for the flag is `false` and the reference only emits it because its VS protocol library serialises defaults. No user-visible effect, and it is enumerated by name so that "we did not notice" and "we decided it does not matter" stay distinguishable |
| CAP-22 | When the `initialized` event is emitted relative to `attach` | Emitted only **after** `attach` | Emitted **before** `attach` - observed 2.5 s after the `initialize` response with no attach sent | divergent | 2026-10-17 A/B: driving the reference required restructuring because it held `initialized` for 40 s until an attach arrived, where ours had already sent it | **Approved 2026-10-17 on the user's instruction to register it.** Both orderings are legal DAP and a client copes with either, so **no user-visible effect is claimed**; it is recorded because it is a real protocol-shape difference and because it cost a wasted run to discover |
| CMD-01 | Run **Unity Debugger Pure: Attach Unity Debugger** | `vstuc.attachUnityDebugger` opens a QuickPick titled `Select Unity Instance` with placeholder `Detecting... press any key to filter` and `busy: true`, over Editors and Players | `unity-debugger-pure.attachUnityDebugger` opens the same QuickPick title over the candidates discovery collected and starts a synthesized attach configuration on acceptance (`extension.ts`, `attachTargetPicker.ts:8`, `package.json:56-59`) | aligned | 2026-10-16 reference `out/extension.js` and `out/nls.bundle.json` (strings 1-2, spec `:153-154`); 2026-10-16 Pure command registration and picker module | The command, its title and its items are implemented; nothing has executed it against a live Editor, which is the only observation that settles discovery, the picker and the started session together; **2026-10-19 verified in the user's VS Code session**: running **Unity Debugger Pure: Attach Unity Debugger** from the Command Palette opened the capture picker with an editor/Player list. That is the scenario this row names, and the one thing it previously lacked was someone actually running the command rather than reading its registration; **2026-10-19 the picker itself, from the user's screenshot:** titled **`Select Unity Instance`** - the reference's title exactly, not merely a picker of some kind - with a filter box and one candidate: `Unity Editor, sgproj_xiaolonghan_0003, xiaolonghan, Port:56340 (Mono)`. The port is the evidence worth keeping: `56340` is `56000 + 107340 % 1000` for the running Editor's pid, so the picker is showing a target discovery actually resolved rather than a placeholder |
| DISC-01 | Compare the target lists the two attach pickers offer | Editors **and** Players (local, Android via adb/`Mono.AndroidTools`, Apple via usbmuxd P/Invoke), background processes filtered by a setting, non-background first | Editors plus every advertisement that is not the open project's Editor, carries `debug === "1"` and a usable port (`editorDiscovery.ts:87-101`, `:303-319`) | not yet verified | 2026-10-16 reference bundle and `debuggerTargets` payload (spec `:176-203`); 2026-10-16 Pure `editorDiscovery.ts:87-101` and `editorDiscovery.test.ts` | Player candidates are produced by the same discovery the Editor path uses rather than being discarded, and the two coverage limits that remain are DISC-03 and DISC-04; no live run has listed both extensions' targets side by side; **2026-10-19 partial observation from the user's session: the picker opened with an editor/Player list, so the list is produced.** What that list actually contained - how many candidates, whether the open project's Editor appeared, whether any Player was present, and whether background processes were filtered - was not recorded, and this row is precisely about the contents rather than their existence. It therefore stays `not yet verified` on its own merits rather than being closed by the neighbouring command; **2026-10-19 the contents are now recorded, and they settle half of it:** the list held **one** candidate - `Unity Editor, sgproj_xiaolonghan_0003, xiaolonghan, Port:56340 (Mono)` - the open project's own Editor, at the port `56000 + pid % 1000` predicts, described with its kind, project, user, port and transport. No Player appeared, and none was running, so a Player entry could not have been expected. **What this does not settle is the transport difference the reference column describes**: the reference reaches local, Android and Apple players through adb, `Mono.AndroidTools` and usbmuxd, where ours lists any Player that advertises itself with `debug === "1"` and a usable port. Whether a Player that only the reference's transports can see would appear here is untested, so the row stays `not yet verified` rather than being closed on the Editor case |
| DISC-02 | Read the label built for one picker item | `Unity <displayType>, <projectName>, <machine>, <displayConnection>`, observed live as `Unity Editor, sgproj, xiaolonghan, Port:56548 (VSTU)` | the same four components, filtered to non-blank and joined with `, ` (`attachTargetPicker.ts:50-59`) | aligned | 2026-10-16 reference live label (spec `:123`) and bundle; 2026-10-16 Pure `formatTargetLabel` and `attachTargetPicker.test.ts` | The format is a pure function of one candidate, so reading both implementations is a complete comparison; the engine token inside it differs and is DISC-05, and the machine component is DISC-06 |
| EX-02 | Type a list into an exception filter's condition box, then start debugging | Advertises `supportsCondition: true`, so the client offers the box and sends `filterOptions[].condition` | `filterId` is accepted and `condition` is ignored, so a typed condition list is not honoured (`UnityDebugSession.cs:418`) | divergent | 2026-10-16 reference body (spec `:74-79`) and the spec's `:124`; 2026-10-16 Pure `SetExceptionBreakpoints` | Recorded rather than implemented by this work item: the filters themselves agree (CAP-15/16/17) and only the condition list is dropped. **Approved 2026-10-17**: the user chose to record this as an approved divergence rather than implement it — but it is a **known functional limitation, not a cosmetic difference**, because the reference offers the condition box and honours what is typed into it while we silently do not filter, so the handoff has to keep carrying it rather than the completion claim |
| CFG-02 | Attach with an `endPoint` that is not an IP literal, or with no port | Answers `success:true` with no message and attaches to nothing — `localhost:1`, `unity-box.local:1`, `not a valid endpoint`, and `127.0.0.1` becoming `127.0.0.1:0` | Rejected with an explicit error before anything is attempted (`AttachArguments.cs:43-60`) | divergent | 2026-10-16 reference one-process-per-case attach probe (spec `:225-234`); 2026-10-16 Pure `AttachArguments.Parse` and `AttachArgumentsTests.cs` | **Approved 2026-10-16**: the user chose to keep all strictness rather than reproduce a silent `success:true` that reports a live session which will never attach, so this difference is in our favour and must never be "aligned" to the no-op |
| CFG-03 | Attach with a non-canonical IP literal: `1.2.3`, `010.1.1.1`, `192.168.001.001`, `::FFFF:127.0.0.1` | Its `IPAddress.TryParse` is inferred to reject the short forms and fall into the CFG-02 silent no-op — the `endPoint` table's rows cover other inputs, so that mechanism was not measured for these three forms | Rejected unless `IPAddress.Parse(host).ToString()` round-trips to the input (`AttachArguments.cs:53-55`) | divergent | 2026-10-16 reference `endPoint` table (spec `:126`, `:225-234`); 2026-10-16 Pure round-trip guard and `AttachArgumentsTests.cs` | **Approved 2026-10-16** together with CFG-02: `.NET`'s parser rewrites these forms to different addresses (`1.2.3` → `1.2.0.3`, `010.1.1.1` → `8.1.1.1`), so a permissive parse can attach to a machine the user never named, and the tightening is stated here rather than left for a reviewer to read as arbitrary |
| DISC-03 | Read how an advertised Player whose `[Debug]` field is `0` is treated | Lists it as `isValidForAttachment: true` | `playerAdvertisement.ts:57` maps `debug === "1"` to `allowDebugging` and `editorDiscovery.ts:91` filters on it, so that target is not offered | divergent | 2026-10-16 reference `debuggerTargets` payload carrying `debug:false` and `isValidForAttachment:true` (spec `:189-209`); 2026-10-16 Pure parser and discovery filter | Neither behaviour is changed by this work item; recorded because the two extensions disagree about the same advertised target and the reference's own payload is the evidence, so a later reader cannot mistake the exclusion for a discovery bug. **Approved 2026-10-17 (batch)**: kept — the difference is in which advertised field the filter reads |
| DISC-04 | Read which port is used for a Player, and whether the target is offered | Reports `debuggerPort: 56322`, obtained by reaching the device (adb port forwarding / `/proc/net/tcp`; usbmuxd for iOS) | The advertisement carries `[Port] 55000`, the player-connection port, and no debugger port, so the parser yields no usable port and the target is not offered (`editorDiscovery.ts:92`) | divergent | 2026-10-16 reference payload `debuggerPort:56322` (spec `:192-202`); 2026-10-16 Pure `playerAdvertisement.ts` and `editorDiscovery.ts` | The supported Player path is an explicit `endPoint` (CFG-05) rather than device-side port probing, which this plan does not add; recorded so the missing target is attributed to the advertisement and not to the picker. **Approved 2026-10-17 (batch)**: kept — device-side port probing is a deliberate non-goal of this plan |
| DISC-05 | Read the debugger-engine token inside the connection label | `Port:56548 (VSTU)` | `Port:<port> (Mono)` (`attachTargetPicker.ts:15`) | divergent | 2026-10-16 reference live label (spec `:123`); 2026-10-16 Pure `formatTargetLabel` | Accepted cosmetic difference in the same format: the token names the engine actually in use, and rendering `VSTU` would misreport which debugger is attached. **Approved 2026-10-17 (batch)**: kept — an accepted cosmetic difference |
| DISC-06 | Read the machine component of a network Player's label | `samsung_SM-S9260@10.30.154.73` | the same value, parsed from the second component of the advertisement's `[Id]` field, e.g. `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)` | aligned | 2026-10-16 reference payload `machine` (spec `:192-193`, `:130`); 2026-10-16 Pure `playerAdvertisement.ts` `[Id]` parsing and `playerAdvertisement.test.ts` | Tasks 5 and 8 parse and reproduce the reference's exact value, so an earlier revision of this plan's "we cannot reproduce the machine label" note was withdrawn rather than recorded as a divergence |
| CFG-04 | Read `configurationAttributes.attach.properties` in both manifests | exactly `projectPath` (default `${workspaceFolder}`), `logFile` and `endPoint`, the last two with a `description` and no `default` | the same three, with the same descriptions verbatim (`Project Path`, `Path to the log file for the debugger`, `ip:port`) — `projectPath` also carrying the reference's default, while `logFile` and `endPoint` declare `""` where the reference declares nothing — plus the standard `name`, `type` and `request` (`package.json:87-121`) | divergent | 2026-10-16 reference manifest and `package.nls.json`; 2026-10-16 Pure `package.json` and `debugConfigurationProvider.test.ts` | Purely additive — the three extra entries appear only in `launch.json` IntelliSense and document fields every configuration must carry — so removing them to match a reference that omits them would make our IntelliSense worse for no user-visible gain, and the difference is recorded rather than silently kept. **Approved 2026-10-17 (batch)**: kept — purely additive, so removing the entries would remove IntelliSense for no gain |
| CFG-05 | Attach with an explicit `endPoint`, including a non-loopback host | `address + ":" + debuggerPort`, where the address is `127.0.0.1` or the docker host; host names are not resolved | Discovered loopback by default; an explicit `endPoint` is accepted and attaches to that `host:port`, including a non-loopback literal (`AttachArguments.cs:36-60`, `MonoDebuggingBackend.cs`) | not yet verified | 2026-10-16 spec W4 and the `endPoint` table (spec `:132`, `:225-234`); 2026-10-16 Pure adapter attach path and its tests; 2026-10-17 negative check — `git grep -i loopback` over `adapter/**/*.cs` returns only the comment at `AttachArguments.cs:36-37` and the vendored `VirtualMachineManager.BeginLaunch` listen address, a launch path this adapter does not use | The loopback-only restriction this work item removes is gone and the strictness differences are CFG-02/CFG-03, but no non-loopback attach has been performed, so the removed restriction is proven by the code and not yet by a session |
| CFG-06 | Read the contributed settings of both extensions | five, of which `displayBackgroundProcesses` is the only one that changes debugger behaviour | one, `unity-debugger-pure.displayBackgroundProcesses` (boolean, default `false`), carrying the reference's exact description (`package.json:65-74`) | aligned | 2026-10-16 reference manifest and `package.nls.json`; 2026-10-16 Pure manifest | The debugger-relevant subset is contributed and the excluded settings are CFG-11; what selecting the setting actually does to a real target list is CFG-08's divergence; **2026-10-17 A/B, both manifests read and the one shared setting compared field by field**: the reference's `vstuc.displayBackgroundProcesses` and our `unity-debugger-pure.displayBackgroundProcesses` are both `boolean`, both default `false`, and resolve to the identical text "Display background processes created by the main Unity process" - the reference through `markdownDescription` pointing at `%configuration.displayBackgroundProcesses.description%` in `package.nls.json`, ours through a literal `description`, which is the approved no-localization decision and not a text difference. The reference's other four settings are `CFG-11` |
| BP-07 | Set a function breakpoint, then start debugging | Raises `You are using function breakpoints. Even if they are fully supported, they have a major negative impact on debugging performance. Use them only when necessary.` from `createDebugAdapterDescriptor` whenever any `vscode.FunctionBreakpoint` exists | Raises the same string at warn level from the same point (`functionBreakpointWarning.ts:6-9`, `extension.ts:187`) | aligned | 2026-10-16 reference bundle (string 6, spec `:158`); 2026-10-16 Pure warning module, `extension.ts` and `functionBreakpointWarning.test.ts` | The string and the trigger point match the reference; no debug session has been started with a function breakpoint set, and the empty-function-name edge where the two predicates differ is BP-08; **2026-10-17 verbatim check against the reference's own English bundle**: our 159-character string is an **exact case-sensitive element** of `out/nls.bundle.json`'s `strings` array (37 entries, one match), so the wording is the reference's own and not a paraphrase. Raise point and level also match - both from `createDebugAdapterDescriptor` at warn level. The one input where the two behave differently is an empty `functionName`, and that is **BP-08**, already `divergent` with the ruling "recorded rather than fixed" |
| MSG-01 | Start debugging with no Unity running, with no endpoint, or against an endpoint that refuses | `Unity is not running for the current project`; `No Unity endPoint to connect to`; `No Process to attach to`; and `Failed to attach debugger to <host>:<port>` | the same four strings (`diagnostics.ts:47-48` for the first, `:65` and `:66` for the second and third; the adapter's `Failed to attach debugger to …` at `MonoDebuggingBackend.cs:72-75`), replacing the single earlier discovery message | not yet verified | 2026-10-16 reference bundle (strings 3-5, spec `:155-157`) and live attach-failure response (spec `:211-221`); 2026-10-16 Pure message constants and the adapter error path | The strings are the reference's verbatim and the W12 robustness fixes make both formerly-silent empty-discovery cases reach them, but which message a live project produces depends on discovery state and has not been run |
| CMD-02 | Select a symbol, run **Unity Debugger Pure: Unity API Reference** | `vstuc.showUnityAPIReference` opens `https://docs.unity3d.com/ScriptReference/30_search.html?q=<selection>`, or the bare index when there is nothing searchable | the same URL rule and the same index fallback (`unityApiReference.ts:47-71`) | aligned | 2026-10-16 reference bundle handler; 2026-10-16 Pure `buildUnityReferenceUrl` and `unityApiReference.test.ts` | The URL rule is a pure function of the selected text and both implementations agree on it; we additionally percent-encode, which the plan records as a deliberate improvement rather than a matrix divergence because it only differs on input the reference mishandles |
| MSG-02 | Open the extension's output channel | one channel titled with the extension display name | one `LogOutputChannel` titled `Unity Debugger Pure` (`outputChannel.ts:91-94`) | aligned | 2026-10-16 reference bundle and spec `:137`; 2026-10-16 Pure `createOutputChannel` and `outputChannel.test.ts` | The channel exists with the reference's `LogOutputChannel` shape, which is what lets the warning sit at warn level; its name is the coexistence divergence NAME-01 and its boundary guard is MSG-05/MSG-06, and none of the three has been seen in a live host; **2026-10-19 verified in the user's VS Code session:** asked whether the `Unity Debugger Pure` channel appears in the Output panel's channel list, the user confirmed it does. That is the whole scenario - one channel, titled with the extension display name - so the row is aligned rather than inferred from the unit test that constructs it |
| MSG-03 | Activate with the deprecated `unity.unity-debug` extension loaded | Warns `The {0} extension is deprecated and incompatible with the {1} extension. Please uninstall or disable it.` | the same template, with `{0}` the deprecated extension's `displayName` and `{1}` ours (`deprecatedExtension.ts:25-27`, `:115-131`) | not yet verified | 2026-10-16 reference bundle (string 11, spec `:163`) and spec `:138`; 2026-10-16 Pure warning module and `deprecatedExtension.test.ts` | The string and the `isActive` precondition match; the reference's broken markdown link is not reproduced because this notification surface takes plain text and has no markdown path at all — so the difference is a defect not copied rather than a security judgement — and the warning's timing is MSG-07; **2026-10-17 verbatim check against the reference's shipped English bundle**: the template `The {0} extension is deprecated and incompatible with the {1} extension. Please uninstall or disable it.` is an **exact case-sensitive element** of `out/nls.bundle.json`'s 37-entry `strings` array, so the wording is verified against the artifact rather than against a reading of it. **Status deliberately not moved**: the string and the `isActive` precondition match, but this row's own decision records that the reference's broken markdown link is not reproduced, which is a difference and needs the user's ruling before the row can be called `aligned` |
| DISC-07 | Compare how each extension discovers targets | a spawned .NET probe over stdio JSON-RPC emitting a `debuggerTargets` notification, plus `Library/EditorInstance.json` | multicast UDP on `225.0.0.222` plus `Library/EditorInstance.json` plus `ProjectSettings/ProjectVersion.txt` (`editorDiscovery.ts`) | divergent | 2026-10-16 reference probe and payload (spec `:59-64`, `:176-203`); 2026-10-16 Pure discovery module and `editorDiscovery.test.ts` | An internal difference in mechanism that the plan keeps deliberately, so it is recorded rather than aligned away; its user-visible consequences are the Player coverage in DISC-03/DISC-04. **Approved 2026-10-17 (batch)**: added to the batch by name on the same grounds — a pure mechanism difference whose user-visible consequences are the approved `DISC-03`/`DISC-04` |
| DISC-08 | Derive the debugger port for an Editor whose advertisement was not seen | `56000 + processId % 1000` | `56000 + processId % 1000` (`editorInstance.ts:32`) | identical | 2026-10-16 reference bundle and adapter; 2026-10-16 Pure `defaultEditorPort` and `editorInstance.test.ts` | Recorded because an identity is positive evidence that the protocol matches rather than because anything was done: it is also what makes an advertised port and a derived one interchangeable on both sides |
| ENG-01 | Attach to a CoreCLR target | Branches on `debuggerEngine`: `coreclr` sends `type: coreclr` with a `processId` and no `endPoint` | Mono only: it sends `endPoint` and no `processId` | not applicable | 2026-10-16 reference bundle; 2026-10-16 Pure attach path and `AttachArgumentsTests.cs` | Out of this extension's declared scope rather than a compatibility gap — the CoreCLR path belongs to the vsdbg/CoreCLR product — recorded so a reader comparing `type` values does not read the missing branch as a missing feature |
| TEL-01 | Read whether each extension reports usage | Initializes an Application Insights client (`AppInsights`, seven occurrences in the bundle) and calls `telemetryService?.logDebuggerLaunch…` on the attach path | none: no telemetry code in `extension/src` or `adapter/src`, and the package is not installed — `@opentelemetry/api` appears only as vitest's optional peer dependency (`package-lock.json:6256`, `:6272`) | divergent | 2026-10-16 reference bundle; 2026-10-16 Pure source tree and lockfile; `README.md` "Security and privacy" | The one row where the reference's behaviour is the thing being declined: the no-telemetry invariant is a project constraint rather than a user choice about this difference, the README states that the reference reports usage while we do not, and the difference is recorded so nobody "aligns" by adding it. **Approved 2026-10-17 (batch)**: kept — the no-telemetry invariant is a deliberate project constraint, not a gap |
| CON-01 | Register a Concord vsdbg extension | Writes `%USERPROFILE%\.dotnet\vsdbg-extensions\<id>.link` at activation | absent | not applicable | 2026-10-16 reference bundle; 2026-10-16 Pure source tree | Genuinely out of scope because Concord is a vsdbg/CoreCLR mechanism and this extension ships no CoreCLR engine (ENG-01); recorded so the absence is a decision rather than an omission |
| CFG-07 | Set `logFile` on an attach configuration and start debugging | Declares the property and its adapter names it: `UnityDebugAdapter.dll` carries the literal run `.ProjectFileMapper: unable to map .Assets.Packages.Library.pid.projectPath.logFile.endPoint.\|`, placing `logFile` beside the other attach keys it reads | Implemented and exercised by unit tests: the per-session sanitized log is written to the configured path instead of the default directory, keeping the existing allowlist and path redaction (`AttachArguments.cs:119-130`, `UnityDebugSession.cs:105`) | not yet verified | 2026-10-16 reference adapter literal and the spec's W3 correction (spec `:332-348`); 2026-10-16 Pure redirection path and its tests | Neither `aligned` (the reference's behaviour is unproven, because a name in the adapter's literal pool proves a reference and not an effect) nor `divergent` (equally unproven), so it waits for a real attach that sets `logFile` on both sides |
| CFG-08 | Select `unity-debugger-pure.displayBackgroundProcesses` and read a real target list | The processes arrive from the adapter's `debuggerTargets` notification with `isBackground` already supplied, and the extension only reads it: `t.processes.map(…).filter(e => !e.process.isBackground \|\| setting)` | the same filter expression and the same non-background-first sort, but `isBackground` is `false` at both construction sites (`editorDiscovery.ts:170`, `:316`) because neither the advertisement nor `EditorInstance.json` reports a parent process, so the setting cannot change the list | divergent | 2026-10-16 reference bundle notification handler and setting read; 2026-10-16 Pure `editorDiscovery.ts:170`, `:316` and `attachTargetPicker.ts:78-85` | Not marked `aligned` on the strength of the identical filter expression, because the expression matches and the outcome does not: the classification is adapter-supplied in the reference and our adapter emits no equivalent notification, which makes this a missing data source rather than a missing feature and keeps it out of the unimplemented bucket. **Approved 2026-10-17**: the user chose to keep the setting, keep this behaviour, and have the row record the gap honestly, so the setting stays in `package.json` unchanged and this row stays `divergent` |
| CFG-09 | Read how each manifest documents the same setting | `markdownDescription` in `package.nls.json`, resolving to `Display background processes created by the main Unity process` | `description` in `package.json` with the byte-identical string (`package.json:68-72`) | divergent | 2026-10-16 reference `package.nls.json`; 2026-10-16 Pure `package.json` | Recorded because both render the same string for plain text while the mechanism differs, and because the next person comparing manifests will look first in `out/nls.bundle.json` and then under a `description` property — the two places the reference's value is not. **Approved 2026-10-17 (batch)**: kept — the same string through a different manifest mechanism |
| CFG-10 | Read `contributes.debuggers[0]` keys in both manifests | `type, languages, label, configurationAttributes, initialConfigurations` — no `configurationSnippets` | the same keys plus `configurationSnippets` with one entry, `Attach to Unity Debugger Pure` (`package.json:122-132`) | divergent | 2026-10-16 reference manifest; 2026-10-16 Pure manifest | Purely additive — it enriches the `launch.json` snippet list and removes nothing — and it carries the coexistence naming (NAME-01): the reference uses `Unity Debugger` for `initialConfigurations[0].name` and `Unity` for the configuration it builds at runtime, where ours says `Attach to Unity Debugger Pure` in both. **Approved 2026-10-17 (batch)**: kept — purely additive |
| CFG-11 | Read the rest of the reference's `contributes` block | also contributes `csharpExtensionLoadPaths`, `jsonValidation`, `languages`, `grammars` and `configuration` with six settings, five of them language-service | contributes `commands`, `breakpoints`, `debuggers` and `configuration` with one setting | not applicable | 2026-10-16 reference manifest; 2026-10-16 Pure manifest | Scope rather than divergence: that half is the C# language-service product, five of its six settings drive IntelliSense, CodeLens and Unity-message generation, and `csharpExtensionLoadPaths` is the C# Dev Kit hook this project forbids — so a row claiming divergence from it would misreport a required boundary as a compatibility gap |
| CFG-12 | Install each extension, or read its `package.json`, and see what else the client is asked to install alongside it | `extensionDependencies` names `ms-dotnettools.vscode-dotnet-runtime`, so installing the reference offers or installs that runtime extension with it | `extensionDependencies` is absent, in the source manifest and in the packaged one, and no shipped file names an `ms-dotnettools.*` id (`package.json` carries no such key; `tests/build/scaffold.test.mjs:117` and `:304` pin both halves, and `scripts/verify-vsix.mjs:175` re-checks the packaged manifest) | divergent | 2026-10-17 read of the reference manifest `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2\package.json`: `"extensionDependencies": ["ms-dotnettools.vscode-dotnet-runtime"]` — the repack that produced that copy removed `ms-dotnettools.csdevkit` from the same array and kept this entry, which is recorded at spec `:40`, so the comparison is against the reference's own declared set; 2026-10-17 read of `package.json` and of `extension/package.json` extracted from the build-stamped VSIX (`33` entries, version `0.3.2`), the key absent from both | **Approved 2026-10-17**, ruled on its own rather than in the batch, after being put to the user rather than assumed, per `AGENTS.md`. **What kind of difference this is:** not a limitation we accept and not an addition we make, but **a dependency we decline to carry because we do not need it** — the reference needs a .NET runtime on the user's machine for its own .NET adapter and asks for the extension that supplies it, while our adapter is a self-contained net48 program whose assemblies ship inside the VSIX, so the reference column is *a dependency the user is prompted for* and ours is none at all. It is also the only approved row that descends from a project invariant rather than from a choice weighed about this difference in isolation: the no-`ms-dotnettools.*` rule is asserted by `tests/build/scaffold.test.mjs:117` and `:304`, so declaring the dependency would fail the suite rather than merely change a cell |
| L10N-01 | Read a command title and a property description with the editor locale set to `zh-cn` | Ships 14 `package.nls*.json` locales and routes its titles and descriptions through them, so the same command reads `附加 Unity 调试程序` | no localizations at all: every string is English regardless of locale, and the manifest uses no `%…%` placeholders | divergent | 2026-10-16 reference `package.nls*.json` inventory and `out/nls.bundle.json`; 2026-10-16 Pure `package.json` and the absence of any `package.nls.json` | **Approved 2026-10-16**: the user chose English-only over adding localization; the fix is additive — one `package.nls.json`, translations, and the manifest switched to placeholders — rather than architectural, so it is flagged to the handoff as a scope decision instead of being treated as closed by this work item |
| CMD-03 | Read the Command Palette title of all five commands | two commands with bare titles, `Attach Unity Debugger` and `Unity API Reference` | all five carry the `Unity Debugger Pure: ` prefix, including the two whose actions match the reference (`package.json:43-64`) | divergent | 2026-10-16 reference bundle command titles; 2026-10-16 Pure `package.json` and `extensionCommands.test.ts` | **Approved 2026-10-16**: the user chose one uniform prefix over verbatim titles, for the coexistence reason in NAME-01; the other three commands (`Refresh Local Editors`, `Open Sanitized Diagnostics`, `Copy Sanitized Diagnostics`) are ours alone and have no reference counterpart to prefix |
| CMD-04 | Watch the picker while discovery is still running | opens its QuickPick inside the `debuggerTargets` notification handler (`r.onNotification(S, t => { e.items = … })`), so items fill as targets arrive and the placeholder reads `Detecting... press any key to filter` for that window | collects every candidate first and opens afterwards, so that phase does not exist and the placeholder is the empty-state message `No Process to attach to` only when nothing is attachable (`attachTargetPicker.ts:61-94`) | divergent | 2026-10-16 reference bundle notification handler; 2026-10-16 Pure `selectTarget` and `attachTargetPicker.test.ts` | Recorded rather than faked, because the items end up equivalent while the timing is user-visible whenever discovery is slow: `attachTargetPicker.ts:68-69` carries the decision. **Approved 2026-10-17 (batch)**: kept — the picker's recorded collect-then-open decision |
| CMD-05 | Run the API Reference command with the cursor not on a word | `getText(selection)`, then `getText(getWordRangeAtPosition(selection.start))`; when the cursor is not on a word the range is `undefined` and VS Code's `getText(undefined)` returns the whole document | the same call chain, so on a single-line file the command searches for the whole line instead of doing nothing (`unityApiReference.ts:98-110`) | aligned | 2026-10-16 reference bundle handler; 2026-10-16 Pure `selectedText` and `unityApiReference.test.ts` | Deliberately reproduced, oddity included: the match was established by reading both implementations rather than by a run, and this is the row most likely to be edited away by a reader treating `aligned` as a to-do list, so what the status means here is stated rather than left to the comment at `:104-106` |
| CMD-06 | Read which API the API Reference command opens its URL with | `commands.executeCommand("vscode.open", Uri.parse(url))` | `vscode.env.openExternal(Uri.parse(url))`, injected as a dependency so the command is testable without a host (`unityApiReference.ts:31-34`, `:93-95`) | divergent | 2026-10-16 reference bundle handler; 2026-10-16 Pure command module and `unityApiReference.test.ts` | Recorded on mechanism only: both reach the same browser for an `https` URI, and there is no user-visible difference to align. **Approved 2026-10-17 (batch)**: kept — a mechanism difference with no user-visible effect |
| NAME-01 | Read the three names both extensions would otherwise share | `Unity` (debugger label), `Unity Debugger` (`initialConfigurations[0].name`), `Unity` (output channel) | `Unity Debugger Pure` for the label and the channel, `Attach to Unity Debugger Pure` for the initial configuration and the snippet (`package.json:83`, `:124-139`; `outputChannel.ts:91-94`) | divergent | 2026-10-16 reference manifest and bundle; 2026-10-16 Pure manifest and `productIdentity.ts` | **Approved 2026-10-16**: the user chose to keep our names because both extensions can be installed at once and indistinguishable names would be a defect rather than alignment; the reference is itself inconsistent — its `initialConfigurations` says `Unity Debugger` while the configuration it builds at runtime is `Unity` — so ours agreeing with itself is a small point in our favour |
| MSG-04 | Read the DAP response to a failing attach | `{"type":"response","request_seq":2,"success":false,"command":"attach","message":"Failed to attach debugger to 127.0.0.1:1","body":{},"seq":2}` | the same `success`, `command` and `message`, with the vendored error body `{"error":{"id":2004,"format":"Failed to attach debugger to …"}}` and `seq:3` (`UnityDebugSession.cs:140-144`) | divergent | 2026-10-16 reference attach probe (spec `:213-221`); 2026-10-16 Pure adapter error path and its tests | Accepted representation difference in wire shape: the body is what this adapter emits for every error rather than something attach-specific, and the three fields a client reads for the failure match character for character — recorded rather than left unmentioned. **Approved 2026-10-17 (batch)**: kept — an accepted wire-shape difference whose client-read fields match |
| MSG-05 | Read the output channel after a diagnostics line naming the user's home directory is refused | no such line and no such guard: the reference's channel writes whatever it is given | the line is refused and `event=diagnostics.channel.refused` is written at `info` in its place (`diagnostics.ts:31-40`, `outputChannel.ts:101-118`) | divergent | 2026-10-16 reference bundle; 2026-10-16 Pure `writeSanitized` and `outputChannel.test.ts` | **Approved 2026-10-16** (round 116): the user chose to keep the marker at `info` over a silent drop, because it closes an observability gap — an over-broad match would otherwise eat a diagnostic with nothing to notice — and it is path-free by construction, so it cannot leak the thing the guard exists to hide |
| MSG-06 | Log a line naming a path outside the home directory, e.g. `D:\build\proj`, `\\build-server\share`, `/opt/unity/proj` | no guard at all | only the user's home directory is refused; workspace paths on another drive, UNC shares and POSIX paths are written, and the adapter's `PathRedactor` remains responsible for redacting them (`outputChannel.ts:74-76`, `:133-143`) | divergent | 2026-10-16 reference bundle; 2026-10-16 Pure `namesUserPath` and `outputChannel.test.ts` | **Approved 2026-10-16** (round 116): the user chose not to widen the guard to a syntactic "looks like an absolute path" test, which would refuse legitimate lines naming a Unity install root and start eating diagnostics that are safe to log; the two sub-limits — an unreadable `os.homedir()` fails open, and the ASCII continuation class refuses `C:\Users\Adminé` as though it were a sibling — are stated here rather than in separate rows because both are unreachable with today's callers |
| MSG-07 | Install both extensions, open a Unity project, and never start a debug session | activates on `workspaceContains:**/Packages/manifest.json`, so the deprecated-extension warning appears when a Unity project is opened | activated only on `onDebugResolve:unity-debugger-pure` and the five `onCommand:*` entries until `38e61f6` added the same `workspaceContains` event (`package.json:33-41`) | divergent | 2026-10-16 reference manifest `activationEvents`; 2026-10-16 Pure `package.json`, `deprecatedExtension.ts` and `extensionCommands.test.ts` | **Approved 2026-10-16** (round 118): the user chose adding the event over `onStartupFinished`, which would activate in every workspace whether or not it is a Unity project, and over leaving the gap — recorded as `divergent` because the affected user is the point: someone who installed both extensions and never started a session saw no warning for the whole session, and the event closes that without charging a non-Unity workspace |
| MSG-08 | Start debugging with a target that fails to launch: a workspace folder and a chosen target, but `vscode.debug.startDebugging` resolves `false`, or discovery throws on the way there | `await s.debug.startDebugging(t, new I(r, t))` in its attach command: the result is discarded and the handler has no `catch`, so a refused or throwing start leaves the user with no message at all | the result is read, and `false` — or a rejection from discovery or from the start — reports `The Unity debug session could not be started.` (`extension.ts:112`, `:122`) | divergent | 2026-10-17 reference `out/extension.js` attach command handler read out of the bundle: the bundle's single `startDebugging` call site is awaited with its boolean never tested, and nothing in that handler catches; 2026-10-17 Pure `runAttachUnityDebugger` and `extensionCommands.test.ts` ("reports a refused start instead of doing nothing", "reports a discovery rejection instead of doing nothing") | **Approved 2026-10-17**: the user approved the message after it was flagged rather than assumed, because `AGENTS.md` requires an unobserved difference to be put to the user before it is kept. The difference is in kind, and it is the one row where **Pure does more than the reference rather than less**: not a behaviour kept, dropped or moved, but **error feedback over a failure the reference leaves silent** — so the reference column is the absence of a message, not a different one. Nothing else moves: when a session does start, both sides start the same configuration |
| MSG-09 | Open the Run and Debug configuration list, or the debugger picker, in a workspace where no other activation event has fired | declares `onDebugDynamicConfigurations:vstuc`, so the client's dynamic-configuration pass activates it and its type is offered through that path | declares no such event: `onDebugResolve:unity-debugger-pure`, five `onCommand:*` entries and `workspaceContains:**/Packages/manifest.json`, and nothing else (`package.json:33-41`) | divergent | 2026-10-16 reference manifest `activationEvents`; 2026-10-17 read of the installed client's workbench bundle (Cursor 3.25.6, VS Code 1.128.0 base — the client in use, above this extension's `^1.95.0` floor), two findings. (a) Its `implicitActivationEvents` module builds implicit events only from the `activationEventsGenerator`s registered per contribution point; twelve points register one (`terminal`, `taskDefinitions`, `authentication`, `debugVisualizers`, `languages`, `commands`, `views`, `notebooks`, `notebookRenderer`, `customEditors`, `terminalQuickFixes`, `walkthroughs`) and `debuggers` is not among them, so no `onDebugDynamicConfigurations:<type>` — or any other `onDebug*` event — is synthesised for a contributed debug type, and the event wakes only an extension that declares it. (b) Its `getDynamicProviders()` derives its debug-type set from the same declared events (`onDebugDynamicConfigurations:<type>`, or the bare event with the extension's first `contributes.debuggers[].type`) plus any registered `Dynamic`-trigger provider, and the Run and Debug dropdown, the debugger picker and the start-with-type command all read that set — so the declaration places `vstuc` in a list our type is absent from, and it is not only an activation difference; 2026-10-17 read of both bundles — the reference registers its configuration provider with no trigger kind and contains no `provideDebugConfigurations`, and ours passes no trigger kind either (`extension.ts:145-148`), so neither side has a `Dynamic`-trigger provider to answer from that list | `not yet verified` rather than `aligned` or `divergent`, because the reads settle the mechanism and not the user's view. Settled: the client synthesises nothing for a contributed debug type, so the declaration is load-bearing rather than redundant; in a Unity workspace both extensions are activated by `workspaceContains` anyway (MSG-07), and no `Dynamic`-trigger provider exists on either side, so the dynamic entry has nothing to answer with on the reference's side either. Not settled, and the reason for the status: whether the extra entry is visible in the dropdown or the picker, and whether a workspace that is not a Unity project shows the reference waking where we stay dormant until `onDebugResolve`, have not been observed on a live client. `divergent` is not claimed either — `AGENTS.md` requires an observed difference to be put to the user before one is ruled on, and this one is asserted nowhere. Recorded because no row compared the two `activationEvents` arrays, which is how a later reader could take agreement on the three shared entries for agreement on the whole surface; **Approved 2026-10-17 (round 118): ours activates through `workspaceContains:**/Packages/manifest.json` instead of declaring `onDebugDynamicConfigurations`, so opening the project activates it and its type reaches the configuration list by that route rather than by the client's dynamic-configuration pass.** **2026-10-19 the observable outcome is verified in the user's session:** the Run and Debug configuration list offers both `Attach to Unity` and `Attach to Unity (Pure)`, with ours present as its own distinctly named entry and selected. So the difference this row records is the activation mechanism, which is approved and which the acceptance project does satisfy by being opened, and **not** a missing entry - the entry is there. The row is `divergent` rather than `aligned` because the declared event is genuinely absent, and calling that aligned would write off a real declaration difference as a cosmetic one |
| MSG-10 | Whether an `output` event's text carries its own line terminator | The reference reaches the same `Mono.Debugging` log writer, and no reference transcript was found that records the terminator either way, so its behaviour on this path is **unverified** | The backend's text is forwarded as it arrives; the fix appends a terminator when one is missing, so consecutive messages stay on separate lines | divergent | 2026-10-19 the user's VS Code Debug Console showed `Protocol Version: 2.58Editor 2022.3.23f1 is unverified` run together as one line, which is what forwarding Mono's unterminated log line does to the next message. Fixed in `UnityDebugSession.OnOutput`, guarded by `ReferenceControlTranscriptTests.TypedOutputAndModuleEventsAreTranslatedDirectly`, and proved to discriminate by disabling the termination: `Expected: "message\r\n"`, `Actual: "message"` | **Approved by the user on 2026-10-19 as a deliberate difference**, chosen from two options, after being told the reference's own behaviour here was not verified. A search of the installed reference bundle for its own version wording found nothing, so this is alignment by intent with a known unknown rather than by observation. The Editor used was `2022.3.23f1`, not the supported `2022.3.62t11` baseline; **2026-10-19 confirmed fixed in the user's own session:** after installing the build that carries it (`0.3.2+g7c8045f6264c`, all 19 adapter files hash-verified against the VSIX), the user reported the two lines no longer run together - `Protocol Version: 2.58` and `Editor 2022.3.23f1 is unverified` are now separate. That closes the loop from observation to fix to observation, in the product rather than in a test |
| BP-08 | Set a function breakpoint whose name is the empty string, then start debugging | Its predicate is exactly `s.debug.breakpoints.find(e => e instanceof s.FunctionBreakpoint)` with no name check, so an empty name raises the warning | `functionBreakpointWarning.ts:22-26` requires `typeof functionName === "string" && functionName.length > 0`, so an empty name raises nothing | divergent | 2026-10-16 reference bundle predicate; 2026-10-16 Pure predicate and `functionBreakpointWarning.test.ts` | Recorded rather than fixed: the only input where the two differ is unreachable through VS Code's own function-breakpoint prompt — it needs a corrupted stored entry or another extension calling the API — and the direction is benign, since we emit strictly fewer non-interrupting log lines; the guard is brief-mandated, so this is a boundary to state rather than a defect to undo, and a matrix that recorded only the reachable-through-the-UI comparison would be technically true and materially misleading. **Approved 2026-10-17 (batch)**: kept — a brief-mandated guard whose only differing input is unreachable through the function-breakpoint prompt |
| SEC-01 | Read the security posture each extension documents for remote attach | documents none at all: a keyword sweep of its `readme.md` for `secur\|warn\|caution\|note\|auth\|trust\|expos\|firewall\|risk` returns nothing | states plainly that attaching to a non-loopback `endPoint` is supported, that Unity's managed debug transport has no authentication, and therefore that anyone who can reach the debugger port can attach and execute code in the target Editor or player, and recommends loopback or a trusted network (`README.md` "Security and privacy", `SECURITY.md`) | divergent | 2026-10-16 reference `readme.md` (4474 bytes) keyword sweep; 2026-10-16 Pure `README.md` and `SECURITY.md` | An intentional addition beyond the reference rather than an alignment failure, descending from the 2026-10-16 decision that accepted remote Player attach with disclosure (spec "Recorded decisions" 2); recorded so the extra sentences are a decision on the record rather than an unexplained difference, and so no future reader tries to "align" by deleting them. **Approved 2026-10-17 (batch)**: kept — the deliberate remote-attach security disclosure |

## Status vocabulary

`AGENTS.md:51-55` declares the set as `reference-verified`, `aligned`, `divergent`, `identical`,
`not applicable`, or `not yet verified`. This file added `identical` and `not applicable` to
the four terms the instructions originally carried, because the 2026-10-16 comparison
produced one row of each that no other term describes honestly.

**The boundary between `reference-verified` and `aligned` was undefined until 2026-10-16, and
this is the definition now in force.** `aligned` means the two results were compared and are
the same, and the comparison is complete for the item — which for a wire body, a manifest, a
label format or a pure function of its inputs means reading both implementations, because
neither result depends on the machine the Editor runs on, and for a runtime behaviour means a
recorded run of both extensions. `not yet verified` means the result depends on live
discovery, a live attach, or the client's request path, none of which reading an artifact can
settle; Task 13's acceptance run is what resolves those seventeen rows. `reference-verified`
would mean the reference's side was established from its shipped artifacts with our side not
yet compared at all, and **no row uses it**: every reference claim below is paired with a Pure
side that is either compared (so `aligned`) or explicitly awaiting a run (so `not yet
verified`). Saying that here is the point — an unused term with a guessed meaning is how
inconsistent usage starts.

**Which class each `aligned` row belongs to, because the two routes to `aligned` are
otherwise indistinguishable.** Of the 44 `aligned` rows, **22 are run-verified** — a recorded
run of both extensions on the same reachable code — and **22 are read-verified** — both
implementations read, which is complete for an artifact comparison and claims no run:

- run-verified (22): `ATT-01`, `RLD-01`, `BP-01`, `EVAL-01`, `EVAL-02`, `STEP-01`, `VAR-01`,
  `VAR-02`, `VAR-03`, `BP-02`, `BP-03`, `BP-04`, `CTRL-01`, `CTRL-02a`, `CTRL-02b`,
  `EVAL-05a`, `EVAL-05b`, `EVAL-03`, `EVAL-04`, `EVAL-06`, `SET-01`, `END-01`;
- read-verified (22): `CAP-01` through `CAP-11`, `CAP-14`, `CAP-16` through `CAP-19`,
  `DISC-02`, `DISC-06`, `CMD-02`, `CMD-05`, `CFG-01`, `EVAL-09`.

The run-requirement in "Evidence Rules" below governs the first list only; the second list is
exactly the artifact class this paragraph defines, and no row in it claims a run.

`divergent` means a known difference. **All twenty-six `divergent` rows cite a user ruling**
in `Decision`, and the twelve rulings are listed under [Approved divergences](#approved-divergences);
`EX-02` is the only one of the twenty-six that approves a functional limitation rather than a
mechanism, additive or constraint difference. `AGENTS.md:56-58` is explicit that an unapproved
difference blocks a claim of reference compatibility or completion, and no `divergent` row is
in that state.

## Where these observations come from

Every 2026-10-16 row cites an observation that exists in the repository, and none of them was
produced by an A/B run of both extensions on the acceptance project. The observations are:

- the reference adapter's `initialize` response body, read from a live DAP probe of the
  shipped adapter (`docs/superpowers/specs/2026-10-16-vstuc-user-visible-alignment-design.md:66-115`);
- the `debuggerTargets` JSON-RPC notification, captured live from the shipped attach probe
  against a running Editor (`…design.md:176-203`);
- the attach-failure response and the one-process-per-case `endPoint` table
  (`…design.md:211-254`);
- the reference's own strings and manifest, read from
  `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2\out\nls.bundle.json` and
  `…\package.json` (`…design.md:145-174`), and its `readme.md` (4474 bytes);
- our side, read from `extension/src`, `package.json` and the adapter's `Dap` sources as they
  stand on this branch.

The artifacts those observations are pinned to are the ones `…design.md:29-39` records:
adapter `bin\UnityDebugAdapter.dll` (assembly 1.3.2.0, SHA-256
`d0d4fd336ee3c0ae611ed587255cb3843bc46f9a9a54e54e6992880206efc75f`), probe
`bin\UnityAttachProbe.dll` (SHA-256
`bf9164e6920fb3186a76bb18112c9daa950e908dd758b5388fdd5960d917922f`), and
`bin\SyntaxTree.Mono.Debugger.Soft.dll` (SHA-256
`7be7fd7995e092e473dfe1dc78a4843cf11ada624b1deb2e9c5e28d5db348f7a`). A fourth
assembly named by `…design.md:53-57`, `bin\UnityCodeModel.dll`, has SHA-256
`afc6b1dff4f22f4c958cba21a94b2ba9d8272c5b0a2be168bd3557280f691ec2` — **it was
named in the spec but no digest was ever pinned for it**, and no row cites it.

**The `ev_…` REA evidence identifiers required by `…design.md:52-53` were added on
2026-10-17, and all four assemblies were analysed afresh for them.** The earlier
statement in this section — that no id was retained — was accurate when written and is
now superseded. The ids below were produced by REA's **managed** provider against the
1.3.2 binaries whose digests are pinned above, verified by re-hashing each file first,
and exported to a bundle that is git-ignored beside this work item. Every id was then
re-read back out of the exported file rather than copied from the analysis output.

| Assembly | Id | What it supports |
| --- | --- | --- |
| `UnityDebugAdapter.dll` | `ev_ad14f2fb75cff5e05af638f664b7aa8e0ccf30dfec9d9cead520412f3486d466` | Type/method/field inventory (39/86/226), 1,082 call edges, field accesses, per-member signature SHA-256. Carries `Microsoft.VisualStudio.Unity.ProjectFileMapper` for `CFG-07`, and the `AD7DebugAdapter.Handle*Request` inventory for `CAP-02`/`03`/`05`–`10`/`12`/`13`, `BP-02`/`03`/`04`, `CTRL-02a`/`b`, `EVAL-01`/`03`/`04` — **as handler existence only, not as advertised capability values.** |
| `UnityDebugAdapter.dll` | `ev_bb75b6dd972e6cacb2434d6a7d3bed43a1a8ea7dc66aefbecb796c6cd287eb54` | Assembly identity: `1.3.2.0`, MVID `68ed2bfc…`, .NETCoreApp v10.0, reference to `Microsoft.VisualStudio.Shared.VSCodeDebugProtocol` 18.0.0.0 (`…design.md:41`). |
| `UnityDebugAdapter.dll` | `ev_afb2fc407b9d4833c6949bf9276bfade06e7f6bb66097fdff2fd8006a046a2b7` | Zero `ModuleRef`/P/Invoke — the adapter is IL-only. A **negative** observation; it supports no row's positive content. |
| `UnityAttachProbe.dll` | `ev_aebae709f8a1a6c03d3a22787ad075edc1844d0fb473396b22fd3d8a73c958ae` | Probe inventory: `AndroidProbe.GetAdbExecutable`/`GetSdkRoot`, `AppleProbe.GetUnityProcesses`/`StartIosProxy`/`FindFreePort`, type `DebuggerTargets`, `SendDebuggerTargetsAsync`, and the IL anchor `ldc.i4 56000` in `AppleProbe.StartIosProxy` → `DISC-01` (both device families), `DISC-04`, `DISC-07`. |
| `UnityAttachProbe.dll` | `ev_00d3c5f57833a2ce2699ba81d009ef00bb08e497515ce3ad9fa2c3ba085d1e3f` | Assembly identity: `1.3.2.0`, MVID `eb873933…`, 14 `AssemblyRef`s including `JsonRpcSharp` and `System.Text.Json` → `DISC-07`'s "spawned .NET probe over stdio JSON-RPC". |
| `UnityAttachProbe.dll` | `ev_a8bd06d5a8233085fb142f3ecca566dd61dd1f7f448e85fcd98973dfcf4b9ed1` | The only declared P/Invoke are `libdl` and `kernel32.dll`; **usbmuxd is a runtime-resolved `delegate* unmanaged` field** reached through `NativeLibrary.LoadSymbol` → supports `DISC-01` and **corrects that row's "usbmuxd P/Invoke" wording.** |
| `UnityCodeModel.dll` | `ev_ccf64c1576f09a1dffb2ac9a2d1322d11bc7a0cdd16870227e2d07aeceeb9ff6` | Inventory (29/85/145): `CodeModelParser`, `CodeModelProtocol.CodeModelUpdated`, `ScriptCodeModel`, `UnityTypeInfo`/`FieldInfo`/`MessageInfo` → `…design.md:55` and `CFG-11`'s language-service half. **No matrix row names this DLL.** |
| `UnityCodeModel.dll` | `ev_7bdeed493a1e77eab75ff41e0ebb358af0d8fe57a491f63290ca00e93f81848b` | Zero P/Invoke. Negative observation. |
| `SyntaxTree.Mono.Debugger.Soft.dll` | `ev_e3ddbf22b5daa007936f6919ed9205c458d7f59388779b8c0ab2c8f31367dea4` | 187 types / 819 fields / 1,232 methods / 3,985 call edges; `InvokeOptions.DisableBreakpoints` + `SingleThreaded`; `ExceptionEventRequest.caught`/`uncaught`; `VirtualMachineManager.BeginLaunch` → `EVAL-09`'s flag vocabulary, `EX-01`/`RLD-01`/`CAP-15`'s filter vocabulary, `CFG-05`'s `BeginLaunch` negative check. |
| `SyntaxTree.Mono.Debugger.Soft.dll` | `ev_d26ea6bc58af79c1b78085d708613f4443ed1a0c8a320a860506b8d9d6706d30` | Zero P/Invoke; 28 runtime-provided delegate stubs. Negative observation. |

**Four claims could NOT be re-observed, and the rows that make them keep their original citations rather than gaining an id.** Saying so is the point of the table above:

- **`CFG-07`'s literal text.** The managed provider decodes `ldstr` as a `#US` token (`0x70000001`) and never returns the user string; `unable to map` and `logFile` appear nowhere in the adapter's decoded member record. The `ProjectFileMapper` **type** is re-observed in the first id above; **the literal run is not, and cannot be with this provider** — so that cell keeps its shipped-artifact-bytes citation.
- **`DISC-08`'s `56000 + processId % 1000` formula.** No `rem` opcode exists in **any** of the four assemblies, and the only `ldc.i4 56000` is in the probe's `StartIosProxy`. **The row's "and adapter" attribution is unsupported by the adapter's decoded IL**, and that clause should be corrected rather than cited.
- **`DISC-01`'s `Mono.AndroidTools`.** It is not among the probe's 14 `AssemblyRef`s and the string appears nowhere in its decoded metadata names. **This bounds rather than refutes** the prose, because the provider returns `TypeRef` row counts rather than names.
- **The `CAP-*` advertised values and `EVAL-09`'s call site** are runtime and live in another assembly. **The adapter has no call edges into Mono engine types**, so no id from these four binaries can show which `InvokeOptions` flags the reference actually passes.

**One correction to how this section previously described the remedy.** It said that if the earlier REA session were still available, its bundle should be exported — but **the REA server session is cumulative, and the sixteen records already in it target a different build**: `…\visualstudiotoolsforunity.vstuc-1.3.1\bin\*.dll`, whose own `package.json` says **1.3.1** (adapter `ceac2ba5…`, probe `8a067a5b…`). **Citing those would have attributed 1.3.1 inventories to 1.3.2 rows**, and that session never analysed `UnityCodeModel.dll` or the Mono engine at all — so `…design.md:53-57`'s "four assemblies" was true of the **intent** and not of the **retention**. The ten ids above are the first 1.3.2 managed evidence for all four.

## Approved divergences

Twelve user rulings are recorded here: seven put to the user and approved on 2026-10-16, and
five more on 2026-10-17 — a batch approval, a ruling on `CFG-08`, a ruling on `EX-02`, a
ruling on `MSG-08`, and a ruling on `CFG-12`. **All twenty-six `divergent` rows cite one of them**, in the `Decision` cell:

| # | Approved difference | Rows that cite it |
| --- | --- | --- |
| 1 | English-only strings: no localization is added | `L10N-01` |
| 2 | Kept attach-host strictness: a non-IP-literal host or a missing port is rejected instead of reproducing the reference's silent `success:true` no-op, and a non-canonical IP literal is rejected rather than re-interpreted | `CFG-02`, `CFG-03` |
| 3 | Kept coexistence names: `Unity Debugger Pure` label, `Attach to Unity Debugger Pure` initial configuration, `Unity Debugger Pure` output channel | `NAME-01` |
| 4 | Kept the uniform `Unity Debugger Pure: ` prefix on all five command titles | `CMD-03` |
| 5 | Kept the path-free `event=diagnostics.channel.refused` marker at `info`, over a silent drop | `MSG-05` |
| 6 | Kept the guard home-scoped rather than widening it to a syntactic absolute-path test | `MSG-06` |
| 7 | Added `workspaceContains:**/Packages/manifest.json` so the deprecated-extension warning fires when a Unity project is opened, rather than `onStartupFinished` or leaving the gap | `MSG-07` |
| 8 | **Batch, 2026-10-17**: kept fourteen recorded differences unchanged, on the grounds that each is (a) a mechanism-or-cosmetic difference with identical user-visible behaviour, (b) a purely additive change that removes nothing, or (c) a deliberate project constraint or prior decision — including the no-telemetry invariant and the remote-attach security disclosure. Granted as thirteen rows and **extended by one named addition on the same day: `DISC-07`**, whose own `Decision` cell already recorded that its user-visible consequences are the approved `DISC-03`/`DISC-04` | `CAP-20`, `DISC-03`, `DISC-04`, `DISC-05`, `CFG-04`, `CFG-09`, `CFG-10`, `CMD-04`, `CMD-06`, `TEL-01`, `SEC-01`, `BP-08`, `MSG-04`, `DISC-07` |
| 9 | **2026-10-17**: kept `unity-debugger-pure.displayBackgroundProcesses` and its status quo, with `CFG-08` recording honestly that the setting cannot change a target list until the adapter emits the background classification it filters on | `CFG-08` |
| 10 | **2026-10-17**, ruled separately from the batch because its character differs: **recorded as an approved divergence rather than implemented** — the reference advertises `supportsCondition: true`, so the client offers the per-filter condition box and sends `filterOptions[].condition`, and we accept `filterId` while ignoring `condition`, so a typed condition list silently does not filter. **A known functional limitation, not a cosmetic difference** | `EX-02` |
| 11 | **2026-10-17**, put to the user rather than assumed, because `AGENTS.md` requires an unobserved difference to be raised before it is kept: **approved surfacing `The Unity debug session could not be started.`** when `startDebugging` resolves `false` or a step throws, where the reference awaits the same call, never tests the boolean and has no `catch`. The one ruling that approves **doing more** than the reference rather than keeping, dropping or recording a difference — error feedback over silence | `MSG-08` |
| 12 | **2026-10-17**, ruled on its own rather than in the batch, and put to the user rather than assumed for the same reason: **kept `extensionDependencies` undeclared** — the reference names `ms-dotnettools.vscode-dotnet-runtime` and we name nothing. The one ruling that **declines a dependency because it is not needed**: not a limitation accepted and not an addition made, but a dependency not carried, because our adapter is a self-contained net48 program whose assemblies ship inside the VSIX while the reference has to ask the user for the runtime extension its own adapter needs | `CFG-12` |

Every `divergent` row now cites a ruling, so none is an unapproved difference under
`AGENTS.md:56-58`. `EX-02` is the one ruling that approves a **functional limitation** rather
than a mechanism, additive or constraint difference: it is recorded, not closed, and the
handoff must keep carrying it — an approved limitation is still a limitation. `CFG-12` is the
one that **declines a dependency** rather than keeping a behaviour or a mechanism, and
`MSG-08` the one that adds feedback the reference lacks; the three are named here so the
rulings are not read as three variations on "approved" when they approve different kinds of
thing.

## Row re-validation (2026-10-16)

All 28 pre-existing rows were re-read against the current build, because a stale `aligned` row
is worse than a missing one: `aligned` is a claim of verified equivalence. Two rows were wrong
and are corrected above; the rest were checked and stand. **This was a source re-read, not a
new run** — no row below gained an observation it did not already have, and Task 13's
acceptance is what turns any of them into a fresh one.

| Row | Status before | Verdict | What was checked |
| --- | --- | --- | --- |
| `CFG-01` | aligned | **re-derived** | Both halves of its Pure column were false: Task 7 added `endPoint` and `projectPath` to `configurationAttributes.attach` (now CFG-04) and Task 8 added `contributes.configuration` (now CFG-06). The row's subject — no implicit-evaluation setting, public or private — survives, which the absence assertions in `tests/build/scaffold.test.mjs`, `tests/extension/productIdentity.test.ts` and `scripts/verify-vsix.mjs` still enforce, so it stays `aligned` on a re-read build. Its `0.3.0+g8c6f030f7256` observation is kept rather than rewritten. |
| `EX-01` | aligned | **downgraded** | Its subject is the caught/user-unhandled filters, and Task 1 renamed the advertised id `uncaught` → `unhandled` (CAP-15) and changed which request field carries the ids (CAP-12). The 2026-08-10 A/B evidence no longer covers what ships, so the row moves to `not yet verified` with its historical build id and observation intact. |
| `EVAL-09` | aligned | stands | Invocation flags (`DisableBreakpoints` plus `SingleThreaded`) in the vendored session. This work item changes no invocation flag: the adapter's attach and evaluation paths are untouched by Tasks 1–11b. Listed here precisely because it cites no build, which is not automatically safer. |
| `ATT-01` | aligned | stands | Scenario is Edit Mode → Play with a **discovered** Editor. `endPoint` (CFG-05) and `projectPath` (CFG-04) add optional inputs only, and the version gate adds a console warning for a non-baseline Editor without altering the grey-then-hit sequence — so the reasoning is written down rather than left as "attach rows are fine". |
| `RLD-01` | aligned | stands | Scenario is Play/Domain Reload with **default** exception filters. Task 1 renamed filter ids but both still default to `false` (`UnityDebugSession.cs:60-73`), so "default" still means none active and the scenario is unchanged; Task 4 deleted only reload methods with **zero callers** in `adapter/src`, so rebinding is still owned by the vendored session exactly as the row records. |
| `BP-01` | aligned | stands | First-hit marker at `GameRuntimeBootstrap.Install()` line 11; no breakpoint-hit path was changed. |
| `EVAL-01`, `EVAL-02` | aligned | stand | Static property Getter and imported Unity enum member presentation; the evaluation stack is untouched. |
| `STEP-01` | aligned | stands | Step In and rapid Step Over sequence; no execution-control change in this work item. |
| `VAR-01`, `VAR-02`, `VAR-03` | aligned | stand | Variable-handle refresh, Locals topology and `base`-node preservation; `ObjectValue` lifetime and the variable presentation layer are untouched. |
| `BP-02` | aligned | stands | Conditional breakpoint binding on `0.3.0+g8c6f030f7256`; condition evaluation is unchanged. |
| `BP-03` | aligned | stands | Logpoint interpolation and non-stopping continuation; unchanged. |
| `BP-04` | aligned | stands | Function-breakpoint binding is unchanged, and Task 9's change is additive: the reference's performance warning now also fires for this scenario and is recorded as BP-07, with the empty-name edge as BP-08. |
| `CTRL-01` | aligned | stands | Pause/Continue/Step Out and all-thread dispatch; execution control is unchanged. |
| `CTRL-02a`, `CTRL-02b` | aligned | stand | Step Into Target and Jump to Cursor / Set Next Statement; unchanged. |
| `EVAL-05a`, `EVAL-05b` | aligned | stand | Inspection immediately after targeted and ordinary Step In; unchanged. |
| `EVAL-03` | aligned | stands | Getter/`ToString()` topology and collection expansion; unchanged. |
| `EVAL-04` | aligned | stands | Invalid-expression and throwing-Getter presentation; the evaluation failure path is unchanged. |
| `EVAL-06` | aligned | stands | Empty automatic Hover expression after a new stop; unchanged. |
| `SET-01` | aligned | stands | Set Variable and stopped-view refresh; unchanged. |
| `END-01` | aligned | stands | Detach and reattach while in Play; lifecycle is unchanged. |
| `EVAL-08`, `EVAL-07`, `BP-06` | not yet verified | left alone | None is touched by Tasks 1–11b: the subjects are the enum-argument conversion, imported-type resolution, and breakpoint filename preservation. All three still need a real-Editor run; Task 13's Step 4 traced a reachable call site for the first two (`CharacterDebug.cs:383`) and its report records what they cannot settle. |

Arithmetic: 28 rows read; 25 `aligned` and 3 `not yet verified` before; 2 rows changed
(`CFG-01` re-derived in place, `EX-01` downgraded), 23 `aligned` rows and 3 `not yet verified`
rows left with the reason above. After the new rows, the file holds **91 data rows: 44
`aligned`, 26 `divergent`, 17 `not yet verified`, 1 `identical` and 3 `not applicable`, with
`reference-verified` unused.** (`MSG-09` is the seventeenth unverified row: it was added on
2026-10-17 without a ruling because it is `not yet verified` rather than `divergent` — see its own
`Decision` cell. `CFG-12` is the twenty-sixth `divergent` row and cites the twelfth ruling, both of
which that count covers.)

### Known stale spots (recorded, not corrected)

Two staleness items found while re-validating the rows are recorded here rather than left in a
task report, because the reader they would mislead is the person reading this matrix:

- **The bare term `not verified` survives in the two frozen 2026-08-06 documents.** The backend
  plan uses it in its Global Constraints, its reference-baseline recording step, its pre-run
  matrix placeholders, its status search recipe and its acceptance-report instruction; the
  parity design uses it in its authority procedure, its evidence-entry shape, its
  remaining-scenarios row and its completion clause. This matrix and
  `docs/release-checklist.md` use the three-word `not yet verified` for the same status. The
  sites are pre-existing and are terminology rather than false claims, and those two files are
  outside this work item's edit boundary, so the divergence is recorded and left for a
  follow-up instead of edited here.
- **Both frozen 2026-08-06 documents also still cite `AGENTS.md:41` for the status
  vocabulary**, which now lives at `AGENTS.md:51-55`; the 2026-10-16 plan and design carry the
  same stale citation, and the design still quotes the four-term set rather than the six terms
  `AGENTS.md` now declares. Left alone for the same reason.
- **The frozen 2026-08-06 backend plan still names the retired acceptance project in
  live-sounding clauses** — its Global Constraints, its target line, and one acceptance step's
  read-only target all still read as instructions even though that path is gone. The plan keeps
  its own attribution note at its head and is frozen, so the sites are recorded here rather
  than edited.

## Evidence Rules

Each evidence entry records:

- date;
- source line;
- exact user actions;
- visible result;
- sanitized log path when available;
- screenshot path when available.

Shared source fixture (the retired `MyGame` project's `DevTools` tree, which every 2026-08
row's line numbers refer to; the acceptance project has no `DevTools` fixture, so rows run
there must prove the reachability of the method and line they select instead of inheriting it
from this block):

```text
Assets/Scripts/GamePlay/Runtime/DevTools/GameRuntimeBootstrap.cs:11
GameRuntimeBootstrapMenu.IsRuntimeInstallEnabled
RuntimeInitializeLoadType.AfterSceneLoad
```

A **runtime-behaviour** row moves to `aligned` only after the same action sequence is run with
the reference debugger and UnityDebuggerPure on the same reachable code; that is the rule the
status vocabulary's run-verified list applies. An **artifact comparison** — a wire body, a
manifest, a label format, or a pure function of its inputs — is complete by reading both
implementations and needs no run, which is that vocabulary's read-verified list. If an
unlisted difference appears, add a `divergent` row and stop for the user's decision.

## Reference Baseline Actions

Run these with the installed reference debugger before production backend
changes begin:

```text
BP-02: conditional source breakpoint whose condition is true, then false.
BP-03: logpoint containing one local/property expression.
EX-01: default user-unhandled filter, then All Exceptions, during ordinary Play/Domain Reload.
CTRL-01: Continue, Pause, Step In, Step Over, Step Out, including rapid Step Over as soon as enabled.
CTRL-02: whether Step Into Targets and Jump to Cursor/Goto are visible and what each does.
EVAL-03: Locals/this expansion, a property Getter, ToString(), and a collection child.
EVAL-04: invalid expression and one failing/unavailable property.
SET-01: whether Set Value is visible for a writable value and its visible success/error result.
END-01: detach, reattach, and first subsequent breakpoint.
```

Mark a captured reference row `reference-verified`. Leave a scenario `not
yet verified` when the acceptance project cannot expose it without modifying the
project. Do not infer an unavailable UI capability.

## Evidence Log

### 2026-10-17 - Task 13 automated gate, package audit, and breakpoint reachability (non-Editor steps)

Non-Editor evidence for the 2026-10-16 alignment work item: Task 13 Steps 1, 2, 4 and 6. Steps 3 and
5 — the Editor confirmation and the live A/B acceptance — were **not** run, so **no row status changes
in this entry**, and every `not yet verified` row stays `not yet verified`. Nothing below is a
runtime observation of either debugger.

Baseline for the whole entry: branch `feature/vstuc-user-visible-alignment` at `47089f0` (docs-only
over `ded5356`), working tree carrying only the six pre-existing `adapter/vendor/*/packages.lock.json`
modifications. Gate logs were written outside the repository.

**Step 1 - the automated gate is green, and reproduces every expected count.**

- `npm run build` exit 0: esbuild `dist/extension.cjs` 43.2 kb; `dotnet build UnityDebugger.sln -c
  Release` 0 errors and one pre-existing CS0168 warning in vendored `SoftDebuggerSession.cs:1713`.
- `npx tsc --noEmit` exit 0 with no output.
- `npm run test:build` (`node --test tests/build/*.test.mjs`): 20 tests, 20 pass, 0 fail.
- `npm run test:extension` (`vitest run tests/extension`): 17 files, 190 tests, 190 pass.
- `npm run test:adapter` (`dotnet test`, net48): Failed 0, Passed 207, Skipped 5, Total 212.
- `npm run test:integration` (`vitest run tests/integration`): 1 file, 9 tests, 9 pass.
- `npm run verify:third-party` exit 0, "Verified 3 pinned third-party sources."
- `npm run build:extension` (esbuild) exit 0.

No gate in the `npm test` chain is red, and no count differs from the accepted baseline.

**Step 2 - the package gate is RED on two independent checks, and `npm test` cannot see either.**

`npm run package` exits 1 and stops before `vsce package`. **No VSIX was produced for this branch, and
`dist/unity-debugger-pure-0.3.2.vsix` was not rewritten.**

1. `npm run inventory:runtime` (`generate-runtime-inventory.mjs --verify`) fails with
   `Runtime inventory differs from staged binaries`:
   `extension/adapter/win32-x64/UnityDebuggerPure.exe: expected
   4c161cdc79ec41e649160ea9009475b6960bc98198818ecb36d61ac46e972297, actual
   e9d5e5e9642e0aed72ff682e9d42be81a0775ece478874c8bec34cc38ded42f3`.
   The pin in `third-party/runtime-assemblies.json` was last written by `f46985b` (2026-09-24);
   seven adapter commits landed after it (`58565df`, `2bfc474`, `b90562d`, `7eb965d`, `1eb4dae`,
   `ea0db8e`, `2777f88`), all touching `adapter/src`. The other sixteen assemblies still hash-match
   their pins, so the build is reproducible and the delta is that source change rather than build
   noise. No task brief in this work item assigns re-pinning the inventory, and nothing under `tests/`
   reads `runtime-assemblies.json`, so `package` is the only gate that can catch it. The command that
   would resolve it is `npm run inventory:runtime:write`, which re-pins an audited provenance record
   and was deliberately left to review rather than done here.
2. Even after that, the archive audit would fail on the same executable.
   `verify-vsix.mjs:119-133` requires `configurationSnippets[0].body` and `initialConfigurations[0]`
   to equal exactly `{name, type, request}`, but `package.json` carries a fourth key,
   `projectPath: "${workspaceFolder}"`, added by `ea0db8e`. `tests/build/scaffold.test.mjs:81-97`
   asserts the **four-key** object, so the source test and the archive verifier contradict each other;
   `verify-vsix.mjs` was last modified by `f46985b` (2026-09-24), before that key existed. Confirmed
   independently against a fresh archive: packaged exe `e9d5e5e9…` against packaged inventory
   `4c161cdc…`, so `verify-vsix.mjs:215` would also raise `Runtime hash mismatch`.

**Path and assembly evidence that the step did establish.** Because `package` stops at check 1, the
audit was run against an archive of the current tree built to a temporary path,
`%TEMP%\task13-audit-candidate.vsix` (33 files, 5,860,523 bytes, SHA-256
`cc07224391293c84cb697e38bdea0e1d18ca94115bfc9ba40f0ade284dce8798`). `scripts/verify-vsix.mjs`
passed every **path** check — ZIP path safety, duplicate detection, the allowlisted root, the five
forbidden patterns (`tests/`, `.git`, `src`, `adapter/vendor/`, `testadapter`), the eleven required
paths, the Adapter-subtree restriction, product identity and build identity — and failed only at
the initial-configuration comparison, on the `projectPath` key (`verify-vsix.mjs:129-138`, whose
message is `Packaged manifest has the wrong initial debug configuration.`). The archive ships 17
assemblies, and neither the entry names nor
any archive path matches `SyntaxTree`, `Microsoft.VisualStudio`, `csdevkit`, `MonoDevelop`, or the
retired `unitycommunitydebug` executable. `inventory:runtime` had already enumerated the staged
directory without raising `No audited runtime origin for <name>`, so every shipped assembly maps to an
audited origin: the reference families are absent from the package, not merely absent from the pin.
`dist/unity-debugger-pure-0.3.2.vsix` **is the published 0.3.2 release artifact** (SHA-256
`4449c1f22385302fe4149fb1454749e9c1dfa0a800aa9914227bee299a1e2c5e`, the same bytes as the 2026-09-24
and 2026-09-25 entries above) and was therefore **not** overwritten. Running `verify:vsix` against it
prints `VSIX verification passed: 33 files, 17 runtime assemblies.` — a silent false PASS describing
the superseded September state, exactly the failure mode the Step 2 note predicted.

**Manifest invariants, in source and in the packaged manifest.** `extensionDependencies`,
`extensionPack` and `dependencies` are absent from both `package.json` and the packaged
`extension/package.json`; `devDependencies` is present with nine dev-only entries. Neither contains
`ms-dotnettools`, `csdevkit`, `com.unity.ide.*`, `telemetry` or `appInsights`. **At the time this
entry was written, only `extensionDependencies` was covered by a test** — that assertion is at
`tests/build/scaffold.test.mjs:117` — plus the package-time check now at `verify-vsix.mjs:175`;
`extensionPack`, the `ms-dotnettools`/`csdevkit` strings, `com.unity.ide.*` and telemetry were
enforced by **no** test then, so this entry's evidence for them is the grep over source plus the
archive inspection. Note that this entry's own `scaffold.test.mjs:37` resolved to the `];` that
closed that file's input list at the reviewed pin, not to an assertion; that list now ends at `:43`.
**Superseded — 2026-10-17, exactly as this entry's last clause predicted ("Task 14 item 46 is what
adds those tests").** Item 46 landed, so every claim in the paragraph above is now asserted rather
than grepped: `tests/build/scaffold.test.mjs:292` (`extensionPack` absent and `dependencies`
absent-or-empty, pinned by shape rather than by membership), `:304` (`ms-dotnettools`/`csdevkit`),
`:312` (`com.unity.ide.*`), `:320` (telemetry identifiers) and `:117`
(`extensionDependencies`), with `verify-vsix.mjs:175` repeating the last of those against the
packaged manifest. Each of the four was re-checked on 2026-10-17 by mutating the tree to make it
fail — the manifest shape, a vendor file naming `ms-dotnettools.csharp`, a vendor file naming
`com.unity.ide.visualstudio`, and a telemetry identifier in a vendored file — so none of them is
decorative.
**The three `assertAbsent` assertions also walk `adapter/vendor` as of 2026-10-17**, because before
that the walk stopped at `adapter/src` and the telemetry test's name claimed the shipped tree while
missing the one telemetry identifier that tree contains; the vendored DAP `Message.sendTelemetry`
shape is the single reviewed exception, listed by file *and line* in the test, so a new identifier
in that file or in any other vendored file still fails. The walk, the exception and the helper are
`tests/build/scaffold.test.mjs:33-83`.

**Step 4 - breakpoint reachability in the acceptance project. Evidence only; no Editor was driven.**

Candidate: `CharacterDebug.cs:383`, `var value = FightUnitAttrUtils.GetAttrCurrValue(LogicEntity, attr);`
inside `public void UpdateAttrInfo()` (`:366`), which is called only by `SafeUpdateAttrInfo()`
(`:1611`, body `:1613`). Every line below was read, not assumed.

- **Attachment hop: established in code, and not by any scene or prefab.** `CharacterDebug.cs.meta`
  declares guid `112235e05c4553b4b94b9585047b7ce5`, and that guid occurs in **none** of the project's
  227 `.unity` scenes, 21,297 `.prefab` files, or any `.asset`; its only occurrence anywhere in the
  project is the `.meta` itself. The component is added at runtime instead:
  `SGBattleView.UnitView.Link` (`UnitView.cs:240`) runs
  `transform.GetOrAddComponent<CharacterDebug>().SceneDebugPanel = true;` at `:278`, inside
  `#if UNITY_EDITOR && !BATTLE_LOGIC` (`:266`-`:279`), and `Link` is called from
  `CreateEntityViewSystem.OnAddCharacter` at `CreateEntityViewSystem.cs:181`. `BATTLE_LOGIC` is defined
  for no platform in `ProjectSettings.asset`, so the guard compiles in for the Editor on both Android
  and Standalone. **Consequence: the method is reachable only in Play Mode with a battle running, and
  only on a unit whose `UnitView` has been created.** There is no serialized attachment that would make
  it reachable from an idle Editor, which is why the guideline's active-scene/prefab check cannot be
  satisfied here and the code path carries the burden instead.
- **All three call-site hops are present.** Route A: the `SceneView.duringSceneGui` handler
  `OnSceneGUIForCharacterDebug` (`:1800`) → `DrawScenePanelGUI()` (`:1825`) → `:612`
  `if (AttrInfoDebug) SafeUpdateAttrInfo();`, gated by `SceneDebugPanel` (`:1802`), Hierarchy selection
  (`:1804`) and a `Layout` event (`:604`); `AttrInfoDebug` defaults to `false` (`:23`). Route B: the
  scene-panel button "复制面板数据" (`:635`) → `CopyPanelDataToClipboard()` (`:1357`) → `:1361`,
  unguarded by `AttrInfoDebug` but behind the same panel and the same selection. Route C (reflection):
  `CharacterAttributeTestWindow.RefreshSnapshot()` (`CharacterAttributeTestWindow.cs:604`) →
  `CharacterDebugSnapshotReader.Read` (`:53`) → `:567` `MatchCharacterEntity`, `:571` `SelectAllAttrs`,
  `:575` `SafeUpdateAttrInfo`, and `:639` in its `finally`; the window opens from the menu
  `便捷工具/属性快捷对比` (`:34`). All three are Editor-side, so they execute inside the attached
  Editor process rather than a player.
- **Short-session plausibility: only after entering Play Mode and reaching a battle.**
  `CharacterDebugSnapshotReader.cs:58-66` returns the message `请进入战斗` whenever
  `!Application.isPlaying`, which is direct source evidence of that requirement. Route C is the
  cheapest of the three triggers because `SelectAllAttrs` (`:571`) guarantees a non-empty
  `SelectedAttrs`, so `:383` is reached on the first attribute that has a `CData_UnitAttr` row; Route A
  additionally needs the "Attrs" toggle flipped by hand.
- **`EVAL-07` and `EVAL-08` share this one breakpoint** and need no second location: both are Watch
  expressions evaluated at the same stop. `LogicEntity` is the public `WorldEntity` field at
  `CharacterDebug.cs:18`, `(EFightUnitAttr)1033` is the *named* member `eMonsterArmor`
  (`EUnitAttr.cs:56`; enum at `:7`, namespace `SGBattleCommon`), and `GetAttrCurrValue` has a single
  overload, `FightUnitAttrUtils.cs:628 (WorldEntity, EFightUnitAttr)`, so the bare `1033` form tests
  the int-to-enum conversion rather than overload selection.
- If a breakpoint at `:383` does not fire, this entry is the reason to establish which trigger ran
  before reading it as a debugger fault.

**Acceptance-project environment read at 2026-10-17 11:18-11:37.**
`ProjectSettings\ProjectVersion.txt` **exists** — 85 bytes, written 2026-10-17 10:30:49 — declaring
`m_EditorVersion: 2022.3.23f1` and `m_EditorVersionWithRevision: 2022.3.23f1 (11dcd41583a0)`, so the
design spec's "absent" is stale and Task 6's fallback is not the live path in this project.
`Library\EditorInstance.json` (written 11:31:08) records pid 91740, `version 2022.3.23f1` with no
revision suffix, and `app_path D:/Unity/Unity2022/Editor/Unity.exe`; pid 91740 is alive, and
`Temp\UnityLockfile` is dated 10:18:37, that process's start time, so the derived attach port is still
56740. The Editor version **actually exercised** by this entry is none: no attach, breakpoint,
stepping or evaluation was performed, and Steps 3 and 5 remain outstanding.

### 2026-10-17 - Task 13 real-Editor attach over DAP (Step 3, and what Step 4 reached)

**The Editor version actually exercised is `2022.3.23f1`** — pid 91740, the acceptance project, in a
running battle. The entry above recorded that no attach had been performed; one now has, driven
directly over DAP against the shipped adapter rather than through a VS Code session, because that is
the same process the extension spawns (`UnityDebuggerPure.exe`, no arguments, JSON over stdio).

**Established, all from the live Editor:**

- **Attach succeeds.** `initialize` → `attach` → `disconnect`, adapter exit code 0. The attach carried
  `__host 127.0.0.1`, `__port 56740`, `__processId 91740`, `__projectVersion 2022.3.23f1`.
- **The version policy classifies this project as `compatible-unverified`, not `unsupported`**, and
  says so in the user's own words: *"Editor 2022.3.23f1 is unverified; the supported baseline is
  2022.3.62t11. Support policy: …"*. **This is the end-to-end evidence the design spec says cannot be
  reproduced against a synthetic `EditorInstance.json`** — the value Unity actually writes is the
  input that matters, and `2022.3.23f1` has no revision suffix, so it exercises the guard's
  accepting branch rather than the suffixed case the guard exists for.
- **Because `ProjectVersion.txt` exists, this run exercises the primary path and not Task 6's
  fallback.** The fallback remains unobserved in a real Editor, and saying so is more useful than
  implying both were covered.
- **The Mono VM is 6.13.0 at DAP protocol 2.58** (`VM Version:` on attach).
- **`setBreakpoints` before `attach` is rejected**: `"Attach to an Editor before setting breakpoints."`
  Pinned behaviour, observed rather than read.
- **A breakpoint resolves but no stop arrives.** `Resolved pending breakpoint at
  '…\CharacterDebug.cs:139,1' to void CharacterDebug.Update () [0x00001].` — and then no `stopped`
  event within 75 s, with **40–600 `thread started` events for thread #1** and `configurationDone`
  answered `success: false`. **Recorded as an observation, not a defect**: a soft debugger cannot
  always re-JIT a method that is already executing, and one client is not enough to separate that from
  our behaviour. **No row moves on this evidence.**

**Step 4's traced target, `CharacterDebug.cs:383`, is not reachable in this project, and the tracing
did not reach two reasons.** The `:612` guard `if (AttrInfoDebug) SafeUpdateAttrInfo();` sits inside
`private void DrawScenePanelGUI()` at `:600` — an **IMGUI** method — so it runs only when that panel
draws. And the only live `CharacterDebug` (on `'playerView_buid:0 unitId:106 entLogicId:2'`) has
**`LogicEntity == NULL`**, with the field declared `[HideInInspector]public WorldEntity LogicEntity;`
at `:18` and **no assignment anywhere under `Assets`** — a recursive search for `LogicEntity\s*=`
returns only null-checks. So `:383`'s `FightUnitAttrUtils.GetAttrCurrValue(LogicEntity, attr)` cannot
run with a live entity even if the guard fires.

**`EVAL-07` and `EVAL-08` therefore stay `not yet verified`, and the reason is reachability rather
than the debugger.** `AGENTS.md` requires exactly this reading. **Step 5 has not been run**, the
reference side of every A/B is still a user report rather than an observation, and the three
acceptance-movable rows are unmoved.

### 2026-10-17 - Task 13 pause, stack walk, and thread reporting against the live Editor

**Four runs against the same Editor, and everything below is what an adapter over stdio actually
answered — not what a document says it should.** The Editor version exercised is `2022.3.23f1`.

**What works.** `attach` succeeds in every run. `threads` returns the real thread set — 42 to 50
entries named `Burst-CompilerThread-N`, `Thread #N`, `<Thread Pool>`, `Thread Pool I/O Selector`,
`Timer-Scheduler`, `StreamingScene`. **`pause` suspends the runtime and the adapter reports it**:
`stopped reason=pause threadId=1 allThreadsStopped=true`.

**What does not, and it is reproducible.** **`stackTrace` never answers.** Seven threads were tried
in one run with a 12-second timeout each — the reported thread 1 and six managed-looking ones — and
**all seven timed out**; earlier runs timed out on thread 1 and on thread 2 as well. The stop is
reported, `allThreadsStopped` is `true`, and the walk behind it produces nothing. **If this holds in
a VS Code session then that session has no call stack, no scopes and no evaluation**, which is why it
is recorded here rather than left in a scratch log.

> **Corrected 2026-10-17, after the run that followed.** The paragraph above is partly **my client's
> fault and not the debugger's**, and the correction matters more than the original claim. Every
> request in those runs omitted `startFrame`, and the integration test's working session passes
> `{ threadId, startFrame: 0, levels: 20 }`. Re-run in that exact shape, **the first `stackTrace`
> answered** — `thread 1 (<Thread Pool>): OK, 0 frame(s)` — where every earlier attempt had timed out.
> So **"never answers" is wrong** and the observation it should have recorded is narrower: the
> adapter answers **once, with zero frames, and then stops answering any thread at all**, timing out
> on thread 1 again and on threads 3 and 7. A request that hangs when an optional field is absent is
> still worth knowing, but it is not the same finding as a stack walk that does not work, and the
> first version of this entry overstated it.

**And the thread list contains no main thread, which is the more likely explanation for the zero
frames.** Across four runs the adapter reported 42–50 threads named `Burst-CompilerThread-N`,
`Thread #N`, `<Thread Pool>`, `Thread Pool I/O Selector`, `Timer-Scheduler` and `StreamingScene` —
**and never one named for the managed main thread**, while the integration test's TestAdapter
reports exactly `[{ id, name: "Main Thread" }]` and its stack walk succeeds. So every thread this
Editor offers for inspection is a worker, `0 frames` is what a worker returns, and the `stopped`
event names thread 1 — a pool or Burst thread — rather than a thread that can be walked. **That is
the shape of the user-visible failure**: no call stack, and therefore no scopes and no evaluation.
It is recorded as the leading hypothesis and not as a conclusion, because the reference side has not
been run and cannot yet separate our thread selection from the Editor's thread set.

> **Corrected again, and this time the correction is to a source-code fact.** The paragraph above
> reads the missing name as evidence about thread *selection*, and **the names are not ours to
> choose**: `threads` returns whatever the Mono soft debugger calls each thread, which is why the
> list reads like a runtime's thread table. **`"Main Thread"` is the TestAdapter's invention** —
> `tests/integration/UnityDebugger.TestAdapter/ScenarioDebuggerBackend.cs:59` returns literally
> `new[] { new BackendThread(1, "Main Thread") }` — so the integration test's clean
> `[{ id, name: "Main Thread" }]` is a property of the fake, and **the real Editor's absence of that
> name is not by itself a defect.** What survives is narrower and still worth the space: the
> **adapter does know how to identify the Unity main thread** — `SoftDebuggerSessionFacade.cs:920-924`
> reflects `CurrentThreadIsMainThread` on `UnityEngine.Object` — so it can tell, and the `stopped`
> event nonetheless names **thread 1, which its own list calls `Burst-CompilerThread-17`**, with
> `allThreadsStopped=true`. **That** is the part a reader should weigh: a stop attributed to a Burst
> worker, on a thread whose stack then walks to nothing.

**Measured in Play Mode with a battle, the path a user actually takes, and it is worse there.** With
the Editor in a battle — `scene=Battle`, `frame=2165`, one live `CharacterDebug` — the adapter
reported **56 threads, none of them named for a main thread** (28 Burst, 13 `Thread #N`, 12 pool, 3
other), `pause` reported `stopped reason=pause threadId=1 allThreadsStopped=true`, **one `stopped`
event per thread (56–60 across runs)**, and **no `stackTrace` answered at all** — thread 1, thread 5
(`Timer-Scheduler`), thread 6 (`<Thread Pool>`) and thread 7 all timed out. In edit mode the first
request had answered with `0 frames` before later ones hung; **in Play Mode not even the first one
answered.** Since a call stack is what feeds scopes, variables and evaluation, the user-visible
effect is that there is nothing to inspect — and the scratch client cannot be blamed for the thread
list or the `pause` attribution, because neither request takes a meaningful client-side argument.

**And a single `pause` emits one `stopped` event per thread** — 42 in one run, 50 in another, 42 in a
third, 43 in a fourth — every one of them `reason=pause`, `threadId=1`, `allThreadsStopped=true`. A
client that takes `allThreadsStopped` at its word will show the user dozens of stops for one pause.

**Bounds on this, because a finding this size has to be bounded or it is a claim.**
- **All of it was measured in edit mode.** The breakpoint run was in Play Mode with a battle, and
  there the breakpoint resolved and no stop arrived — so **`stackTrace` after a breakpoint stop in
  Play Mode is untested**, and that is the path a user actually takes.
- **The reference side was not run.** Its adapter needs its own launcher handshake
  (`BackendProcess` over `dotnet.acquire`), so whether the reference walks these threads or reports
  stops the same way is **unknown**, and until it is known this cannot be attributed to us.
- **The client is minimal.** A real session also sends `setExceptionBreakpoints` and a fuller
  configuration phase; the differences are not covered here.

**None of this moves a row.** It is recorded because it is the first real-Editor evidence about
mechanisms the matrix currently describes from unit tests and packaged artifacts, and because a
release decision should not be taken on the assumption that a stack walk is fine.

**The next run that would settle it, and it is one command.** The open question is whether the
managed main thread appears once the Editor is in Play Mode with a battle — the path a user actually
takes — and whether a stack then walks. Edit mode is where every observation above was taken.

### 2026-10-17 - The reference adapter run against the same Editor, and what it settles

**The A/B was run, and it divides the three observations into two shared behaviours and one defect
that is ours.** The reference was driven the way its own bundle launches it rather than by guesswork:
`createRunCommand(DotNetProject.debugAdapter)` yields `{ cmd: <acquired dotnet>, args:
["UnityDebugAdapter.dll"], cwd: <extension>/bin }`, `createDebugAdapterDescriptor` passes no extra
arguments, and `resolveDebugConfiguration` sets **`endPoint = address + ":" + debuggerPort`** — so the
attach arguments are the launch configuration itself, `{ projectPath, endPoint }`. Both adapters were
pointed at **pid 91740 / `127.0.0.1:56740`**, the same Editor, minutes apart, with the same sequence:
`initialize` → `attach` → `threads` → `pause` → `stackTrace { startFrame: 0, levels: 20 }`.

| Observed | Reference | Ours |
| --- | --- | --- |
| `attach` | success | success |
| `threads` count | 46 | 56 |
| threads named for a main thread | **0** | **0** |
| `pause` → `stopped` | `reason=pause`, `allThreadsStopped=true` | same |
| `stopped` events for one pause | **46** | 56–60 |
| **`stackTrace` on the stopped thread** | **OK, 13 frames** | **timeout, or 0 frames** |
| thread identity | raw Mono pointers (`686898752`) | small sequential integers |

- **The missing "main thread" name is Unity's and Mono's, not a defect of ours** — the reference's
  own list has no such name either, and its names are the same `Burst-CompilerThread-N`,
  `Timer-Scheduler`, `Thread Pool Worker` and `Thread #N` strings ours reports. **The earlier entry's
  leading hypothesis is withdrawn on that evidence.**
- **The per-thread `stopped` flood is also shared** — the reference emitted **46 events for one
  pause**, the same shape as ours. A client sees dozens of stops either way, so this is **aligned
  behaviour and not a divergence**, and it should not be filed as one.
- **`stackTrace` is the one real divergence, and it is ours.** On the same Editor, at the same kind
  of stop, the reference returned **13 frames** beginning
  `System.Threading.WaitHandle:WaitMultiple` → `WaitAny` → `BlockingCollection…TryTakeFromAny` — a
  real managed stack — while ours timed out in Play Mode and returned `0 frames` in edit mode. Since
  `stackTrace` is what feeds scopes, variables and evaluation, **the user-visible effect is that our
  debugger has nothing to inspect in this project.** **This row is `divergent` and it is recorded as
  observed, not inferred.**
- **The thread-id difference is minor and in our favour** — the reference exposes raw Mono pointers,
  ours sequential DAP ids — recorded for completeness rather than as a defect.

**And the reason our own suite never caught it is worth its own line.** `tests/integration` asserts a
13-frame-equivalent stack — `stack.body.stackFrames[0]` matching `FixtureBehaviour.Update` — against
`ScenarioDebuggerBackend`, which returns `new[] { new BackendThread(1, "Main Thread") }` and frames of
its own construction. **The assertion passes against the fake and the real Editor returns nothing**,
so a green integration test was never evidence that a stack walks in Unity. That is the strongest
argument in this log for the real-Editor acceptance existing at all.

### 2026-10-17 - `STACK-01` root cause: `StackTrace` is the only inspection handler without a timeout

**Reading the source after the A/B produced a specific cause rather than a suspicion, and it is one
missing guard.** `UnityDebugSession.cs` gives four of its five inspection handlers a bounded wait:

| Handler | Declared | Timeout applied |
| --- | --- | --- |
| `ExceptionInfo` | `:324` | yes, `:376` |
| `Scopes` | `:652` | yes, `:665` |
| `Variables` | `:689` | yes, `:704` |
| `Evaluate` | `:756` | yes, `:793` |
| **`StackTrace`** | **`:596`** | **none** |

`GetTimeoutMilliseconds(request)` (`:1223-1230`) reads an optional per-request `timeout` and falls
back to `DefaultInspectionTimeoutMilliseconds = 10000` (`:19`). **`StackTrace` — 55 lines, `:596` to
`:651` — contains no timeout call and no `timeout` reference of any kind.**

- **The blocking call is at the bottom of that path**: `UnityDebugSession` calls
  `value.GetStackTrace(backendThreadId, startFrame, levels)`, and
  `SoftDebuggerSessionFacade.GetStackTrace` (`Backend/SoftDebuggerSessionFacade.cs:248`) re-enumerates
  the processes, matches `item.Id == threadId`, and then reads **`thread.Backtrace`** (`:262`) — a
  Mono soft-debugger query with **no bound**. On this Editor it does not return.
- **So the failure is not a wrong answer, it is no answer.** The four sibling handlers would have
  replied with a timeout error; `StackTrace` simply never sends a response, which is why every
  `stackTrace` attempt timed out on the client side and why the one that did answer had already got
  its frames before blocking on a later call. **In VS Code that is a Call Stack panel that never
  finishes loading**, and because frames are what `Scopes`, `Variables` and `Evaluate` are addressed
  against, the rest of the inspection surface goes with it.
- **And the reference answers the same question in well under a second**, which rules out the Editor
  being unable to walk these threads and points at how we ask.

**Two things I raised as leads and am withdrawing, so they do not get "fixed".** Our small sequential
thread ids are **deliberate**: `Dap/State/ThreadIdMap.cs` maps backend `long` ids onto DAP `int` ids
from 1 upward, with `TryGetBackendId` for the return trip — a design choice, not the bug, and the
reference's raw pointers (including negatives such as `-881642304`) are simply a different choice.
And the 10-second inspection default is **not** too short: the reference walks a 13-frame stack
almost instantly on the same Editor, so a handler that had a timeout would have failed fast and
visibly instead of hanging.

**The aligned fix is one guard mirroring the four siblings**, and it is **not applied**: `AGENTS.md`
requires the user's decision before a reference difference is preserved or designed around, and that
ruling has been requested and not yet given.

### 2026-10-17 - The capability A/B: three rows settled, one reproduced, two new differences raised

**Both adapters were asked for their `initialize` response and the two bodies compared key by key.**
No Editor interaction is involved, so this is a clean wire observation of both sides on the same day:
ours spawned as the extension spawns it, the reference as its own bundle does.

- **`CAP-12`, `CAP-13` and `CAP-15` move from `not yet verified` to `aligned`.** Both sides report
  **`supportsExceptionFilterOptions: true`** and **`supportsExceptionConditions: true`**, and both
  advertise the same filter list - **`all` and `unhandled`, each `default=false` and
  `supportsCondition=true`**. These are cases where **nothing differs**, so no ruling is required and
  none was sought; the previous status was "unverified", not "in doubt", and it is now verified from
  both ends rather than from our body alone.
- **`CAP-20` is reproduced independently** - all four empty arrays (`completionTriggerCharacters`,
  `additionalModuleColumns`, `supportedChecksumAlgorithms`, `breakpointModes`) are `undefined` on our
  side against `[]` on the reference's **in the same run**, which is the difference that row already
  records and the user already approved. **A second independent observation of an accepted difference
  is worth having**: it shows the row describes a stable property rather than an artefact of the day it
  was written.
- Across 19 asked-for flags, **every one matches**; the only body differences are those four arrays,
  `pid` (which differs by nature - it is the adapter's own process id), and one new item below.

**Two differences are new here and are raised rather than decided, because `AGENTS.md` reserves that
to the user.**

1. **`supportsDelayedStackTraceLoading`: absent on our side, `false` on the reference's.** This is the
   same class as `CAP-20` - an absent property against an explicit default, where DAP's own default for
   the flag is `false` and `CAP-20` already records the user's acceptance of exactly that shape for
   four other keys. **It is not filed as a row until the user says whether it should be**, since
   deciding that an accepted difference extends to a new key is the user's call and not a pattern
   match.
2. **The `initialized` event arrives at a different point.** Ours is emitted **before** `attach` -
   observed 2.5 seconds after the `initialize` response with no attach sent - while the reference's
   arrives **only after** an attach, which is why the first attempt to drive the reference held that
   event for 40 seconds and had to be restructured. Both orderings are legal DAP and a client copes
   with either, so this is recorded as an observation and **no user-visible effect is claimed** - but it
   is a real protocol-shape difference and it should not sit undocumented in a scratch script.

- **`CFG-06` also moves to `aligned` on the same day, from the two manifests rather than from the
  wire.** The reference contributes five settings and we contribute one, and the one we contribute is
  its direct counterpart: `vstuc.displayBackgroundProcesses` against
  `unity-debugger-pure.displayBackgroundProcesses`, **both `boolean`, both defaulting to `false`, and
  both resolving to the identical text** "Display background processes created by the main Unity
  process". The reference reaches that text through `markdownDescription` pointing at
  `%configuration.displayBackgroundProcesses.description%` in `package.nls.json` and we use a literal
  `description`, **which is the approved no-localization decision rather than a wording difference** -
  the same text arrives either way. The reference's other four settings are code-model and
  message-completion features, already recorded as `not applicable` under `CFG-11`, which is why this
  is not a divergence and needs no ruling.

### 2026-10-17 - The user's rulings: STACK-01 to be fixed, and two differences registered as rows

**All four open questions were answered, and two of the answers add rows rather than close them.**

- **`STACK-01` is to be fixed**, not preserved. The aligned fix is the guard its four siblings already
  have, and the work is in progress against `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`.
  This row is the only `divergent` entry in this matrix that cites a requested-but-ungiven approval;
  once the fix lands and is verified against a real Editor, it carries an approval like the others.
- **`CAP-21` is registered** for `supportsDelayedStackTraceLoading` — the reference emits `false`, we omit
  the key. The user chose a row of its own rather than folding it into `CAP-20`, so the two accepted
  absent-vs-default differences are now enumerated separately and neither is silently absorbed by the
  other.
- **`CAP-22` is registered** for the `initialized` event ordering — the reference emits it only after
  `attach`, we emit it before. **No user-visible effect is claimed**, because both orderings are legal
  DAP and a client copes with either; it is recorded because it is a real protocol-shape difference and
  because discovering it cost a run.

**Approvals are now 14**, and the matrix stands at **94 rows**: 49 `aligned`, 29 `divergent`, 12
`not yet verified`, 3 `not applicable`, 1 `identical`. Every `divergent` row cites a ruling except
`STACK-01`, whose fix is underway.


### 2026-09-25 - Visual Studio Marketplace publication verified

- The user completed the manual Visual Studio Marketplace upload. The public gallery API reports version 0.3.2
  with the validated flag under publisher kpk, extension unity-debugger-pure.
- An independent download of the Marketplace VSIX contains 5,853,054 bytes and has SHA-256
  4449c1f22385302fe4149fb1454749e9c1dfa0a800aa9914227bee299a1e2c5e.
  This is byte-identical to the audited GitHub Release and Open VSX assets; all three publication channels are verified.
- The verification read the public API and downloaded the asset in memory; no browser interaction, additional
  upload, or new temporary file was needed. Existing candidate acceptance and reference-comparison limits remain.

### 2026-09-24 - 0.3.2 published to GitHub and Open VSX

- Release tag v0.3.2 points to e7d74f1dcda7e1eab4960e9c32b2c0d23e3a55a0; build ID is 0.3.2+ge7d74f1dcda7.
  CI run 35988272535 and Release run 35988653344 passed. The release is public at
  https://github.com/kpkhxlgy0/unity-debugger-vscode/releases/tag/v0.3.2.
- The downloaded GitHub VSIX passed the release identity/checksum verifier and the complete 33-file/17-assembly
  package audit. Its code entries and build identity match the local package; only ten text files differ by
  checkout line endings. Published VSIX SHA-256:
  4449c1f22385302fe4149fb1454749e9c1dfa0a800aa9914227bee299a1e2c5e.
- Open VSX run 35989055478 downloaded and verified that exact GitHub asset, then reported successful publication.
  After the registry visibility delay, both the 0.3.2 endpoint and latest endpoint returned 0.3.2. An independent
  download from Open VSX has the same SHA-256, proving both channels serve the same VSIX.
- Cursor uses Open VSX; its local UI/index was not operated or checked. The installed 0.3.2 candidate was left
  undisturbed. Visual Studio Marketplace still reports 0.3.0 and requires the documented manual upload.
- The exact published VSIX and checksum are retained at dist/unity-debugger-pure-0.3.2.vsix and its .sha256 sibling.
  Candidate user acceptance and remaining reference-comparison limits are unchanged; no extra MyGame run is claimed.

### 2026-09-24 - 0.3.2 publication preparation

- After accepting the installed 0.3.2 candidate, the user explicitly requested publication. Release metadata,
  adapter build identity, verification scripts, and both release workflows now use 0.3.2. The published artifact
  is rebuilt from a committed tree; candidate acceptance does not claim another MyGame run of the final archive.
- EVAL-09 has user acceptance. EVAL-07, EVAL-08, and BP-06 still lack a new controlled reference A/B comparison.
  Source-import syntax limits and the pre-existing int-to-long invocation limitation remain as recorded below.
- The first cloud CI run failed before a rebind worker entered its test barrier. Two existing race tests used
  thread-pool tasks with synchronous waits; they now use dedicated long-running workers and always release/join
  them before disposing the barriers. Their two-second assertions and production breakpoint code are unchanged.
- Pin .NET SDK 10.0.401 and have CI/release install it from global.json. Recursive VSIX/checksum exclusions prevent
  existing candidate archives from entering the production package.
- Update build/test-only adm-zip, js-yaml, and Vitest to fixed patch versions and refresh affected transitive locks.
  npm ci and npm audit passed with zero reported vulnerabilities; shipped runtime dependencies are unchanged.
- Local verification passed type checking, 20 build tests, 93 extension tests, 207 adapter tests (all 21 real Mono
  cases executed, zero skipped), 9 simulated integration tests, 4 package tests, the 33-entry/17-assembly VSIX audit,
  and release artifact/checksum verification. GitHub does not provision the optional Mono fixture, so its skipped
  cases must not be counted as real-runtime coverage. The existing upstream unused-variable warning remains.

### 2026-10-19 - Acceptance closed

- **The acceptance is complete and the user closed it.** Unity `2022.3.23f1` in
  `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`, not the `2022.3.62t11` baseline the
  extension supports, so the warning this build shows about that is correct behaviour and
  was seen working.
- **What was verified by running it, not by reading it:** the attach; the Call Stack filling
  immediately with 14-frame stacks whose text is the reference's format; `Thread #7`
  showing the eight rows its log predicted; the **Unity Debugger Pure: Attach Unity
  Debugger** command opening a picker titled `Select Unity Instance` with a candidate at the
  port `56000 + pid % 1000` predicts; both `Attach to Unity` and `Attach to Unity (Pure)` in
  the configuration list; `All Exceptions` and `User-Unhandled Exceptions` in the
  Breakpoints panel; and the output-terminator fix confirmed by the user after installing
  the build that carries it.
- **Ten rows remain `not yet verified`, recorded here as known and not as pending work.**
  They need one of four things, none of which this session could supply:
  - **A Player that is running and advertising** - `DISC-01`, `CFG-05`, `MSG-01`. The
    Editor half of `DISC-01` is verified; the Player half is not.
  - **The extension's own output channel** - `MSG-02`. The session captured the Debug
    Console, which is a different surface.
  - **A `logFile` set on both sides** - `CFG-07`. Not attempted; the setting exists and is
    covered by unit tests, but no real session exercised it.
  - **Fixtures this project does not have** - `EVAL-07`, `EVAL-08`, `BP-06`, whose cause is
    established: the only live `CharacterDebug` has a null `LogicEntity` that nothing under
    `Assets` assigns, so there is no reachable code to break on. `EX-01` needs a live
    exception trigger, and the one experiment that would produce it froze this Editor on
    2026-10-17, so it is not being repeated unattended. `MSG-03` waits on a ruling about
    whether to reproduce the reference's own broken markdown link.
- **Why this is an honest close rather than an incomplete one.** Every one of those ten has
  a named reason, and none of them is an unapproved difference: the 31 `divergent` rows all
  cite a user ruling or a committed fix, and `STACK-01` is the only one held open by an
  observed behaviour difference the user has seen and left standing, the extra frame. What
  is unknown is recorded as unknown rather than written off, which is the condition
  `AGENTS.md` sets for handing this on.

### 2026-10-19 - Acceptance in a real VS Code session

- The user opened `D:\workspace\sgproj1\sgproj_xiaolonghan_0003` in VS Code with Unity
  `2022.3.23f1` (not the Tuanjie `2022.3.62t11` baseline the extension formally supports),
  attached, paused, and expanded threads. **This is the first acceptance evidence taken from
  the product a user actually runs rather than from a script driving the adapter.**
- **The Call Stack works and matches the reference.** Frames appeared immediately, and the
  text reads `Void System.Threading.Thread:Sleep`, `Void System.IO.DefaultWatcher:Monit...`
  - return type, namespace-qualified type, colon, method, parameter list - which is the
  reference format. Parameter lists are truncated by panel width, not by the adapter.
- **Two reports of "the Call Stack is still empty" were investigated and were not defects.**
  The diagnosis came from diagnostics added for exactly this: the adapter now logs, for each
  `stackTrace`, the thread asked about and the number of frames returned. `Thread #8` and
  `Thread #9` returned `eventCount=0` on every request - three each - because those threads
  have no stack. Expanding another thread showed frames at once.
- **The cross-check that makes this evidence and not an impression:** thread 7 logged
  `eventCount=8`, and the screenshot of `Thread #7` shows exactly eight rows.
- **Diagnostics added and kept.** Four outcomes are logged - `stackTrace.result` with a frame
  count, and `timeout`, `failure`, `unavailable` - using `threadId` and `eventCount`, which
  were already in the diagnostic allowlist. **The boundary that keeps paths out of the log
  was not widened.** The 249-test adapter suite, which covers that allowlist, still passes.
- **Still open, and recorded rather than closed:** `threadId=1`, the thread the `pause` request
  names, also returns 0 frames, which is counter-intuitive for the thread that just stopped
  and wants its own look. And the frame COUNT difference stands - ours 14, the reference 13 -
  which is the sole reason `STACK-01` remains `divergent`.
- **What the session does NOT verify, stated here so it is not inferred later: the attach was
  started from the workspace `launch.json` entry, not from the Command Palette.** The user
  confirmed which. So this session verifies the attach path the configuration drives, and it
  does **not** exercise `CMD-01` (the **Unity Debugger Pure: Attach Unity Debugger** command)
  or `DISC-01` (the target list its picker shows). Both stay `not yet verified`, and the
  distinction matters because a working attach makes it very easy to assume the command that
  starts one was also exercised - they are different code paths, and only one of them ran.

- **Credit where the evidence came from:** the Editor restarts, the VSIX installs, the reloads
  and the repeated thread expansions were all done by the user. Two of my own hypotheses in
  this stretch - a missing `resume` on disconnect, and a vendored route for the frame text -
  were wrong and were corrected in place rather than quietly dropped.

### 2026-10-17 - Final verification of the aligned tree

- Local verification on `master` at `e87a26f`, with `UNITY_DEBUGGER_TEST_MONO` set to
  Unity's own bundled runtime (`Data\MonoBleedingEdge\bin\mono.exe`, Mono 6.13.0 - the
  version the attached Editor reports over DAP): type checking clean, **24 build tests**,
  **213 extension tests across 18 files**, **249 adapter tests, zero failed and zero
  skipped**, **10 integration tests**, **4 package tests**, the 33-entry/17-assembly VSIX
  audit, and release artifact verification. `npm test` and `npm run package` both exit 0.
  The retained release artifact is untouched at
  `4449c1f22385302fe4149fb1454749e9c1dfa0a800aa9914227bee299a1e2c5e`.
- **244 of the 249 adapter tests is what a Mono-less run reports as 227 passed and 6
  skipped**, which is the trap the 2026-09-24 entry below already warned about and which
  I fell into anyway while reporting this work. The release checklist now carries the step.
- The two changes on top of the reference alignment are the bounded stack walk, which made
  `stackTrace` answer in 0.1 s with 14 frames where it previously never answered at all,
  and the Call Stack frame text, which now reads character-for-character what the reference
  reads. Both were confirmed against the acceptance Editor over DAP rather than only in
  unit tests, and both have guards that were proved to discriminate by mutation.
- **Still unverified, and carried deliberately rather than closed:** the frame COUNT
  (ours 14, the reference 13, ours carrying an extra `Wait_internal` the reference omits);
  `GetLocationSignature`, which `SourceLocation.MethodName` also feeds and which no test
  exercises, so the exception-location-matching path is unverified after the frame-text
  change; and the twelve matrix rows above that need a VS Code session, a non-loopback
  Player, or a reachability fixture this project does not have.

### 2026-09-24 - Step disappears after a Watch/Scopes refresh

- Current user session runs the installed 0.3.1 candidate. Its log
  `adapter-20260924T084412271Z-47596.log` shows a normal breakpoint stack/scopes/variables
  sequence, evaluation at 08:46:08Z, another scopes refresh, then `next` at 08:46:17Z
  with no following stack refresh. The active Editor and adapter were inspected passively;
  no user process was suspended, resumed, terminated, or sent a diagnostic target invocation.
- Adapter memory shows `IsRunning=true`, `current_thread=null`, an enabled step request,
  no queued break/event sets, and idle operation/evaluation workers. All retained invoke
  handles are completed; the callback dictionary's live count is zero. The receiver waits
  for a packet and the event dispatcher waits for an event set.
- The Editor main thread is inside the Mono debugger suspension condition-variable loop.
  Read-only disassembly shows the wait condition's runtime word remains 1 while its TLS
  word is 0. Packet buffers retained in adapter memory contain successful step-request and
  resume replies. This establishes a runtime/adapter state mismatch, not a missing source
  filename. It does not establish which suspend increment was left outstanding.
- The Editor's `EmbedRuntime/mono-2.0-bdwgc.dll` contains a custom-build source path under
  `D:/svn/UnityMono2022_3_23/Mono_release`; both locally available PDBs mismatch that DLL.
  The standalone probe instead loads `bin/mono-2.0-sgen.dll`. Its successful step result
  therefore must not be presented as reproduction or repair of this exact Editor hang.
- A concrete reference difference was found: Pure `MethodCall` used DisableBreakpoints
  (plus Virtual when supported), which resumes unrelated threads during a target invoke.
  Installed `zlorn.vstuc-1.2.1` `SyntaxTree.VisualStudio.Unity.Common.dll` was decompiled
  (attributed to 1.2.1; the reference authority was later re-targeted to
  `visualstudiotoolsforunity.vstuc` 1.3.2 on 2026-10-16, and the 2026-10-16 decision that
  permits analysis as behavioural evidence cites this record as prior practice —
  `docs/superpowers/specs/2026-10-16-vstuc-user-visible-alignment-design.md`
  "Recorded decisions" 1):
  `Evaluation.Invoker.GetInvokeOptions`, `Properties.UnityAccessorProperty.GetValueAsync`,
  `ValueExtensions.InvokeToString`, and `Properties.UnityMethodInvocationProperty.EnumerateChildren`
  all use DisableBreakpoints plus SingleThreaded. The one-line production correction adds
  SingleThreaded and retains the existing Virtual/ReturnOutArgs/ReturnOutThis handling.
- A real-facade background-counter test failed twice before the correction: the worker
  advanced 14 ticks during a Watch invocation (expected zero). It passes after the correction.
  The test also obtains a fresh Step stop, checks the next source line, and evaluates the
  new frame. Two further cases cover enum-method/field evaluation followed by scopes and step.
- Fresh verification: 207 adapter tests (21 standalone Mono cases, zero skips), 20 build
  tests, 93 extension tests and 9 simulated protocol integration tests pass. The existing
  upstream unused `exc` compiler warning remains. No full Unity session was exercised.
- Review found no issue in the one-line invocation change. The optional Mono tests are not
  provisioned by existing CI/release workflows; they skip without `UNITY_DEBUGGER_TEST_MONO`.
  This local candidate ran them explicitly with zero skips. CI provisioning is unchanged;
  the local evidence must not be represented as automatic release coverage.
- Packaged and installed local candidate 0.3.2 to
  `C:/Users/Admin/.cursor/extensions/kpk.unity-debugger-pure-0.3.2` through the Cursor CLI.
  Four base-version package tests passed, then all 33 versioned package files were compared
  against that tested package: only package/build/VSIX version metadata differs. The versioned
  package contract, registry selection, candidate identity, and installed hashes were verified.
  Source release metadata remains 0.3.0; no release, tag, commit or registry publication was made.
  Candidate: `dist/unity-debugger-pure-0.3.2-single-threaded-evaluation-candidate.vsix`;
  SHA-256 `1a3e41e4db748c13b18d42d7d00a8725aed7642a7acc3e64d73572cd00f57928`.
  Engine SHA-256 `1af1cf2da9732602a9f00cd7a6d4bed35e1e03dd49087da1dc339df0adf4feaf`.
  Existing debug processes were not interrupted. Stop debugging, reload Cursor and reattach
  to activate the candidate. All task-owned native-temp diagnostics and fixture processes
  were cleaned up; no game-project code was changed.
- This correction removes verified unintended execution of unrelated threads while paused.
- User acceptance on 2026-09-24: after installation of the 0.3.2 candidate and the requested
  step retest, the user replied `没问题了`. Record the reported disappearance of the paused
  location / stuck-after-step symptom as resolved by user verification. This does not extend
  acceptance to other expressions or unrelated hang scenarios, or claim a new agent-operated
  Editor/reference A/B run.

### 2026-09-22 - Enum method arguments after source type resolution

- The user's next screenshot changed from identifier lookup failure to argument conversion failure.
  Read-only inspection of the running adapter recovered the complete message:
  `Invalid arguments for method 'GetAttrCurrValue': Argument 1: Cannot implicitly convert 'int' to 'SGBattleCommon.EFightUnitAttr'`.
  The zero-based argument index identifies the second parameter, not `e`.
- A standalone console fixture runs on the existing Editor-shipped `mono.exe`, hits a source breakpoint,
  and evaluates through the production `SoftDebuggerSessionFacade`. Before the fix, the two original
  expressions and equivalent integer-variable/method-result arguments reproduced the reported failure.
  Named enums and explicit casts already passed.
- Candidate behavior: select ordinary overloads first; only if no ordinary candidate applies, consider
  the eight signed/unsigned integer types (excluding Char) for enum parameters. Reject ambiguous
  fallback candidates and retain rejection of floating-point, string, Boolean, and other incompatible arguments. Actual
  argument conversion reuses `TryCast` and `EnumMirror`; the expression is not retried or evaluated twice.
  Indexer binding is excluded from this compatibility fallback.
- Verification passed 204 adapter tests with zero skips, including 18 standalone Mono cases covering
  actual return values, signed/unsigned integers, byte/long enum storage, explicit enum expressions,
  overload priority, ambiguity, invalid arguments, and a side-effecting argument evaluated once.
  The 93 extension, 20 build, 9 simulated protocol integration, and 4 candidate-package tests also passed.
- The probe also exposed a pre-existing plain `int` to `long` invocation-marshalling failure. This change
  does not alter that path; numeric overload selection is separately verified to retain the `long`
  overload instead of diverting the call to an enum overload. This unrelated limitation remains open.
- Cursor rejected same-version reinstalls because the running adapter held the 0.3.0 extension directory.
  Packaged the same tested payload as a local 0.3.1 candidate using `vsce --no-update-package-json`;
  repository release metadata remains 0.3.0. The package verifier was run with its expected version
  adjusted in memory to 0.3.1, and all 33 packaged files were compared against the tested 0.3.0 candidate:
  only package/build/VSIX version metadata changed. No release or Git tag was published.
- Cursor CLI successfully installed the upgrade to
  `C:\Users\Admin\.cursor\extensions\kpk.unity-debugger-pure-0.3.1`.
  Registry selection, candidate metadata, and installed executable/engine hashes were verified.
  The existing debug process was left running; stop debugging, reload Cursor, and attach again to activate.
  VSIX: `dist/unity-debugger-pure-0.3.1-enum-arguments-candidate.vsix`, SHA-256
  `717654b408b1c9a01b546355874653b7439bcdae399182dfb8a42a62b1099b04`.
- No real Editor or active debug session was used for execution testing. This does not establish a fix
  for the separately reported Unity hang; EVAL-08 remains `not yet verified` for Editor acceptance.

### 2026-09-22 - Imported Watch type and breakpoint source correction

- User supplied Watch scope errors for `FightUnitAttrUtils.GetAttrCurrValue(e, 1033)` and `1034`,
  plus a breakpoint row displaying `.` and line 786, and requested both corrections.
- Read-only inspection found the stopped source is a global-namespace partial class with
  `using SGBattleLogic;`. The installed resolver only tried the identifier itself and the frame namespace.
  The previous EVAL-02 observation had been generalized beyond the captured reference evidence.
- The candidate parses the source at the stopped location, resolves ordinary namespace imports and type
  aliases in lexical scope, and still delegates method evaluation to the mature Mono debugger.
  This is not a claim of complete C# language-service equivalence: compilation-specific preprocessor
  symbols, namespace aliases, `using static`, `extern alias`, and file-scoped namespaces are not covered.
  The reported source uses an unconditional ordinary import and a global-namespace class.
- The saved breakpoint points outside the attached workspace. Its initial response retained the requested
  path, but later status events passed that client path through runtime source filtering and removed it.
  The candidate uses the client breakpoint path consistently and converts it to the negotiated DAP format.
- Regression tests cover imported types, lexical scope, alias precedence, ambiguity, unavailable imports,
  parser stdout safety, mature expression qualification, and breakpoint updates with native paths and URIs
  both inside and outside the workspace.
- Automated checks passed: 186 adapter tests and 9 simulated protocol integration tests. Builds used the
  installed .NET SDK 10.0.401 explicitly because the repository-pinned 10.0.302 is not installed; the
  repository SDK pin was not changed. Roslyn stays at the existing debugger runtime version 2.10.0.
- Build checks passed (20 tests), and the actual reported source resolved `SGBattleLogic.FightUnitAttrUtils`
  without writing to the DAP output stream. The local candidate
  `dist/unity-debugger-pure-0.3.0-watch-breakpoints-candidate.vsix` passed the package verifier with
  33 files and the existing 17 runtime assemblies. Only the adapter executable's runtime hash changed.
  Its build metadata explicitly marks an uncommitted working-tree candidate based on the recorded Git
  commit, with the candidate executable's SHA-256. Existing nested release checksums were excluded through
  a temporary packaging ignore file; that file was removed afterward.
- No real Editor session was changed or exercised. EVAL-07 and BP-06 remain `not yet verified` until the
  candidate is compared with the reference in the permitted existing MyGame session.

### 2026-08-06 - Reference attach, first stop, and initial evaluation

- Debug configuration: `Attach to Unity` (`zlorn.vstuc 1.2.1`, the authority at the time of
  this observation; re-targeted to `visualstudiotoolsforunity.vstuc` 1.3.2 on 2026-10-16, see
  the header). This run is attributed to 1.2.1 and is not re-attributed to 1.3.2.
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
- Installed reference 1.2.1 binary inspection (attributed to 1.2.1; the authority was later
  re-targeted to `visualstudiotoolsforunity.vstuc` 1.3.2 on 2026-10-16, see the header)
  confirms that its adapter delegates target discovery to
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

### 2026-08-08 - Pause replacement aligns in MyGame

- After Reload and Attach with build `0.3.0+g10b1d65475b8`, six rapid Pause/Continue cycles completed responsively.
  Stopped views behaved normally whether or not the selected thread exposed source, a yellow marker, and Variables;
  no warning or error appeared.
- Detach left Unity in Play Mode. Reattaching immediately produced a normal breakpoint stop with the yellow marker
  and Variables.
- The first Adapter log records six Pause requests, six matching Continue requests, per-thread Threads/StackTrace
  refreshes, and the final Disconnect. The reattach log records the same installed build ID, breakpoint rebinding,
  StackTrace, Watch evaluation, Scopes, and Variables refresh.
- CTRL-01 is aligned with the installed reference's all-thread Pause dispatch and recorded runtime experience.
- Sanitized Adapter logs:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T094002238Z-95568.log` and
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260808T094055445Z-61648.log`.

### 2026-08-10 - Exception stop remains divergent in MyGame

- Build `0.3.0+g6bbcf11aab44` used the requested `All Exceptions` filter and opened
  `UnityAssetLinkReceiver.cs:94`, proving the workspace-relative source mapping is fixed.
- The stopped view displayed only `Exception has occurred.` and did not show the expected reference yellow marker.
- Continue at `08:28:48.255Z` was followed by another stopped-view refresh about 22 milliseconds later. A second
  Continue at `08:29:01.966Z` was followed by another stop about 14 milliseconds later, so the target did not finish
  exiting Play.
- The stop handler was still calling `Backtrace.GetException`, which enters the ordinary managed-evaluation path
  while the exception is being dispatched. The replacement now carries the exception type and backing message from
  the runtime event without invoking a Getter or `ToString()` and uses that metadata for both the inline stopped text
  and DAP exception information. Runtime verification is pending.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-6020d1b8-d917-4e28-99cd-ac28b8ac5b5f.png`.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T082718117Z-51716.log`.

### 2026-08-10 - Exception request overlap replacement pending MyGame verification

- After Reload and Attach with build `0.3.0+g265581f30ed5`, one Continue from the exception stop was followed by a
  new Threads/StackTrace refresh about 12 milliseconds later. The user confirmed that one Continue still did not
  complete Play exit without another pause.
- Removing managed exception inspection from the stop path therefore fixed neither the repeated stop nor its extra
  suspension. The remaining control difference was two simultaneously active runtime requests: the session's
  always-on unhandled-exception request and the All-mode request matching both caught and uncaught exceptions.
- The replacement configures those requests as mutually exclusive: None disables both, Uncaught enables only the
  unhandled request, and All enables only the all-exceptions request. Reapplying the same mode does not disable and
  recreate its active request.
- Three focused regressions first failed against the overlapping lifecycle and pass after the replacement. Runtime
  verification remains pending; EX-01 remains divergent until the MyGame stop text, marker, and single-Continue exit
  all match the installed reference.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T084156499Z-59316.log`.

- Packaged and installed candidate: version `0.3.0`, build ID `0.3.0+gc675cc2db296`, behavior source commit
  `3ffb080bfcea8196f29fe658e5bc59146f29991e`, inventory/build commit
  `c675cc2db296b916c805f14cacfc7361858a83df`, and VSIX SHA-256
  `723a7d756437212f3dfcee0cd213a7e7e6c8663e4f0506dad2cf5a9136f8c395`.
- Installed `UnityDebuggerPure.exe` SHA-256 is
  `0e5999bad1cfe081a324f347d714ee779e1ab9b06c536968b3ac2615c5e20bb6`; installed
  `Mono.Debugging.Soft.dll` SHA-256 is
  `24ccb96f844b466717dbe4d1b665595deaa7f249ce1d93615bedc6ffd895e2a3`. Both match the audited package.
- Verification passed: type checking, third-party provenance, 19 build tests, 93 extension tests, 165 Adapter tests,
  9 integration tests, 17-entry runtime inventory, 33-file VSIX audit, and 4 package tests. The existing upstream
  unused-variable warning in `Mono.Debugging.Soft` remains unchanged. Reload and the combined EX-01 MyGame runtime
  check remain pending.

### 2026-08-10 - Exception event identity diagnostic pending MyGame capture

- After Reload and Attach with build `0.3.0+gc675cc2db296`, the earlier Unity MCP `EditorPrefs.GetBool` exception no
  longer blocked the run, but returning to `UnityAssetLinkReceiver.cs:94` still reproduced the EX-01 divergence: one
  Continue did not complete Play exit.
- The Adapter log records Continue at `09:12:47.339Z` and a new Threads/StackTrace refresh about 12 milliseconds
  later. This rules out a lost DAP Continue response and also disproves the overlapping-request hypothesis because
  this build keeps the All and unhandled runtime requests mutually exclusive.
- Public Unity/Mono agent source shows that one exception event set with `SuspendPolicy.All` adds one VM suspension,
  so one matching Resume should balance it. The remaining distinction is whether the post-Continue stop carries the
  same exception object again or a newly thrown exception object.
- A diagnostics-only candidate now carries the runtime's opaque numeric exception object ID, request ID, event-set
  count, thread ID, and stop kind into the sanitized Adapter log. It does not record exception text, source paths,
  expressions, or values and does not alter exception filters, stopping, or Continue behavior.
- Three focused regressions prove the new fields are allowlisted, preserved through the backend boundary, and logged
  without exception text. All 19 build tests, 93 extension tests, 168 Adapter tests, 9 integration tests, and 4
  package tests pass; third-party provenance, the 17-entry runtime inventory, and the 33-file VSIX audit also pass.
- Packaged and installed diagnostics-only candidate: version `0.3.0`, build ID `0.3.0+g82691588494f`, diagnostics
  source commit `75294b64085d4ac112e0cce8c6a1025aac909715`, inventory/build commit
  `82691588494f7165c30ef67171d4d70faa8df494`, and VSIX SHA-256
  `379fd6d416c62cb1ef3a2a22bfcd41cd9a5c69cfd160ea0c732d13242877a98c`.
- Installed `UnityDebuggerPure.exe` SHA-256 is
  `ddeea062d5fd650c3ce2e7da99047fa1842da1469d42626d251edcc559b573a6`; installed
  `Mono.Debugger.Soft.dll`, `Mono.Debugging.dll`, and `Mono.Debugging.Soft.dll` SHA-256 values are respectively
  `fe4fb7a055912891278e299436c1e12d1d8f177e11f37a2fa67ced3c36e83dda`,
  `116e1ee6771d531d1b28a419612846e67bd7da09e0848f1cceeafd955860d3a2`, and
  `ea7dfada784a9d53be0eba38e0d6dc10b3fe8eb47623466a408a8190ae274899`; all match the audited package.
- Reload and one MyGame capture remain pending; EX-01 remains divergent.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T091222409Z-42020.log`.

### 2026-08-10 - Reference exception wire contract identified

- After Reload and Attach with diagnostics build `0.3.0+g82691588494f`, the first line-94 exception stop was runtime
  object ID `1337` on request `156`. Continue at `09:31:40.075Z` was followed about 13 milliseconds later by another
  stop on the same thread and request, but runtime object ID `1344`. Both event sets contained exactly one event.
  Continue is therefore delivered and resumes the VM; the second stop is a distinct exception event rather than the
  same event being redelivered or an unbalanced suspension.
- A black-box connection from the installed reference Adapter to an isolated minimal SDB endpoint captured its actual
  exception-request wire contract without reading or decompiling any reference binary. The reference always installs
  two complementary standard requests at startup, even before any DAP exception filter is sent:
  - uncaught request: `caught=false`, `uncaught=true`;
  - caught request: `caught=true`, `uncaught=false`.
- Both reference requests use `SuspendPolicy.All`, include subclasses, and set `notFilteredFeature=false` and
  `everythingElse=false`. A second run with no `setExceptionBreakpoints` request produced the same two startup
  requests, proving they are fixed engine requests rather than dynamically created DAP-filter requests.
- Pure currently diverges at this control boundary: All mode disables the ordinary uncaught request and enables one
  dynamic `caught=true`, `uncaught=true`, `notFilteredFeature=true`, `everythingElse=true` request. The previous
  mutually-exclusive request replacement is therefore refuted by direct reference-wire evidence and must be
  replaced rather than extended with deduplication or timing heuristics.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T093127451Z-85568.log`.

### 2026-08-10 - Reference exception request replacement ready for packaging

- The user approved replacing the refuted dynamic/mutually-exclusive request design with the installed reference
  debugger's captured startup contract.
- The runtime session now installs the same two ordinary complementary requests in the same order: uncaught-only
  first and caught-only second. Both are enabled once during connection and remain active independently of DAP
  filter changes; the previous explicit unhandled-request enable/disable extension was removed.
- DAP `None`, `Uncaught`, and `All` now control only event disposition. Ignored caught or unhandled events immediately
  continue the suspended target; selected events are reported to VS Code. The complete mapping is: None continues
  both, Uncaught stops only unhandled, and All stops both.
- Focused regressions first failed against the old request lifecycle and missing disposition policy, then passed for
  the exact complementary request flags/order/enabling and all six filter/event combinations. Packaging and MyGame
  verification are pending, so EX-01 remains divergent until one Continue completes Play exit like the reference.
- Packaged and installed candidate: version `0.3.0`, build ID `0.3.0+gf2481db7c0b1`, behavior commit
  `a8b7b233bb3cb83a6e55bec3a7ce6a0837e01f50`, inventory/build commit
  `f2481db7c0b196da19eaa202d9cf4b2c96ea68b2`, and VSIX SHA-256
  `df7e54b3139fcd9b62806a9eab30ae04f73eafbd9ce836f251441a85dd0cbc21`.
- All 17 installed runtime assemblies match the audited package inventory. Full verification passed: type checking,
  third-party provenance, 19 build tests, 93 extension tests, 172 Adapter tests, 9 integration tests, 17-entry runtime
  inventory, 33-file VSIX audit, and 4 package tests. The existing upstream unused-variable warning remains unchanged.
- VS Code Reload and the combined EX-01 MyGame runtime check remain pending.

### 2026-08-10 - Source-less exception stop identified

- After Reload and Attach with build `0.3.0+gf2481db7c0b1`, the first caught exception stopped normally. Continue at
  `10:06:09.755Z` resumed the target, then a distinct caught-only exception object arrived about 10 milliseconds
  later on the same runtime request.
- The second stop had no current-statement marker, no Variables, and no stack-bound Watch context; all seven Watches
  displayed `not available`. This proves the remaining divergence is a source-less/no-frame exception stop rather
  than another user-code exception at line 94.
- The mature session already has the reference-compatible filter boundary: with `ProjectAssembliesOnly` enabled,
  caught exceptions are reported only when at least one non-external frame exists. Pure had left that session option
  at its default `false`, which short-circuited the frame check and exposed the source-less stop.
- The user approved replacing that setting. The connection now enables `ProjectAssembliesOnly`; a focused regression
  first failed against the old options and passes with the reference option. MyGame verification remains pending, so
  EX-01 remains divergent until the first exception still stops normally and one Continue completes Play exit.
- Screenshot:
  `C:\Users\Admin\AppData\Local\Temp\codex-clipboard-7d82f399-5154-4f65-926e-4a3620517514.png`.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T100559225Z-33340.log`.
- Packaged and installed candidate: version `0.3.0`, build ID `0.3.0+g0ecce4d08bfb`, behavior commit
  `83cf922786490df147fd806c641ec1387d49063f`, inventory/build commit
  `0ecce4d08bfb17cf36b6eb7aece7fca563d4d68c`, and VSIX SHA-256
  `45b96b268bc3bc5ad1e091cadbede8b0f1734c9140ea414e167f36f7625bb24a`.
- All 17 installed runtime assemblies match the audited package inventory. Verification passed: type checking,
  third-party provenance, 19 build tests, 93 extension tests, 173 Adapter tests, 9 integration tests, 17-entry runtime
  inventory, 33-file VSIX audit, and 4 package tests. The existing upstream unused-variable warning remains unchanged.
- After Reload, the source-less stop with no marker, Variables, or Watch context no longer appeared. Later stops had
  valid stack, scope, and variable contexts and represented distinct exception objects rather than redelivery.
- The user repeated the same state with the installed reference debugger and confirmed that it also stops on those
  later valid exceptions. The remaining stops are therefore expected `All Exceptions` behavior rather than a Pure
  divergence. EX-01 is aligned.
- Sanitized acceptance log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T103922333Z-51128.log`.

### 2026-08-10 - Failing Getter presentation aligned

- At the ordinary `GamePrototypeRuntime.cs:214` stop with both exception filters disabled, the reference debugger and
  Pure evaluated `new Google.FlatBuffers.DynamicTableDictionay<int, int>().Keys`. The Getter deterministically throws
  `NotImplementedException` without modifying MyGame.
- The user confirmed identical behavior in both debuggers: the Getter failure remained in the Watch presentation,
  no bottom-right notification appeared, the yellow marker and Variables remained normal, and one subsequent Step
  Over completed normally.
- The Pure log records the Watch evaluation, the following `next`, the new StackTrace, and refreshed Scopes/Variables
  with no request failure. EVAL-04 is aligned.
- Sanitized Adapter log:
  `C:\Users\Admin\AppData\Local\unity-debugger-pure\logs\adapter-20260810T105244336Z-27940.log`.

### 2026-08-10 - Final 0.3.0 release candidate accepted

- Local candidate: version `0.3.0`, build ID `0.3.0+g6e1720c70c75`, source/build commit
  `6e1720c70c75bd28c7fd582f04735354744adf86`, local VSIX SHA-256
  `274e560a5647bcd13509ef2bab26268ed021f856b32245bf7dde8780fbcbfd9e`, and Adapter SHA-256
  `21913c99c45dfeffff97095d483ce45a76c588ac4032b9bacbc5571913cfb6bc`.
- Fresh verification passed: locked restore, type checking, third-party provenance, dependency audit with zero known
  vulnerabilities, 20 build tests, 93 extension tests, 173 Adapter tests, 9 integration tests, 17-entry runtime
  inventory, 33-file VSIX audit, 4 package tests, checksum verification, and release-tag/manifest verification. The
  existing upstream unused-variable warning remains unchanged.
- The installed VS Code extension reports the same build ID and Adapter hash, and all 17 installed runtime
  assemblies match the audited package inventory.
- After Reload, the user completed the final MyGame Attach/breakpoint smoke check and confirmed the yellow current
  statement marker, Variables, Hover, Step Over, and Continue remained normal with no error notification or marker
  loss. The compatibility matrix has no remaining required divergence.
- The successful `v0.3.0` GitHub workflow rebuilt the same commit and published VSIX SHA-256
  `0002ecd8fd7ace1b839c151d1c332bfc674d7650c495a164c70c8f5d72f8a29e`. All packaged executable/code entries and
  the build identity matched the local candidate; the differing archive bytes were confined to checkout-dependent
  line endings in ten packaged text/license/metadata files.
- The GitHub Release VSIX and checksum passed independent release-artifact verification. After installing that exact
  downloaded VSIX, the user repeated Reload, Attach, breakpoint hit, Step Over, and Continue and confirmed that the
  marker, Variables, Hover, and notification behavior remained normal. The exact registry-bound release asset is
  accepted.
- Visual Studio Marketplace and Open VSX both published version `0.3.0`. Open VSX workflow run `31383468421`
  downloaded and re-verified the GitHub Release asset before confirming namespace ownership and credentials; the
  publish command reported `Published kpk.unity-debugger-pure v0.3.0`, and the public API subsequently returned the
  verified `0.3.0` record.
- The VSIX downloaded independently from each registry is byte-identical to the GitHub Release asset: all three have
  SHA-256 `0002ecd8fd7ace1b839c151d1c332bfc674d7650c495a164c70c8f5d72f8a29e`, contain 33 files, and have no entry-level
  content difference.
