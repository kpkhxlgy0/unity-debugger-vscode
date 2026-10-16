# VSTU User-Visible Alignment Design

**Status:** Draft for review
**Date:** 2026-10-16
**Amends:** `2026-08-06-reference-debugger-engine-parity-design.md`

## Decision

Unity Debugger Pure will align its **user-visible debugger surface** with the
official Microsoft **Visual Studio Tools for Unity for VS Code** extension
(`VisualStudioToolsForUnity.vstuc`) at version **1.3.2**, for the scenarios this
extension supports (local, Windows x64, Unity/Tuanjie 2022.3, Mono managed C#).

The implementation stays independent: no C# Dev Kit dependency, no Unity-side
package requirement, no telemetry, and no Microsoft/SyntaxTree binary in the
package. Only the observable experience is aligned.

This document also **lifts one clause** of the previously approved parity design
(see [Recorded decisions](#recorded-decisions)).

## Goal

An item is aligned only when the reference extension and UnityDebuggerPure
produce the same user-visible result for the same action on the same project.
The maintained matrix at
[`docs/reference-debugger-compatibility.md`](../../reference-debugger-compatibility.md)
gains a row for every item below.

## Reference identity

| Item | Value |
| --- | --- |
| Product | `VisualStudioToolsForUnity.vstuc` ("Unity" for VS Code), Microsoft |
| Version | **1.3.2**, released 2026-09-30 (`changelog.md`) |
| Installed reference copy | `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2` |
| Provenance | `D:\Unity\vstuc-mirror\build_local_vsix.py` — official VSIX repacked with **only packaging metadata changed**; every code/binary entry copied byte for byte. Differences: publisher id `local`, a provenance notice prepended to `readme.md`, and removal of `ms-dotnettools.csdevkit` from `extensionDependencies`. |
| Adapter | `bin\UnityDebugAdapter.dll`, assembly `1.3.2.0`, SHA-256 `d0d4fd336ee3c0ae611ed587255cb3843bc46f9a9a54e54e6992880206efc75f` |
| Probe | `bin\UnityAttachProbe.dll`, SHA-256 `bf9164e6920fb3186a76bb18112c9daa950e908dd758b5388fdd5960d917922f` |
| Mono engine | `bin\SyntaxTree.Mono.Debugger.Soft.dll`, SHA-256 `7be7fd7995e092e473dfe1dc78a4843cf11ada624b1deb2e9c5e28d5db348f7a` |

`zlorn.vstuc 1.2.1`, the reference named in `AGENTS.md`, is a repack of the same
product at an older version. It is **superseded as the reference** for this work.
`AGENTS.md` must be updated accordingly.

### Evidence base

All reference claims in this document are `observed` and come from one of:

- **Static, REA managed provider** — type/method/field inventories, call edges,
  field accesses, signature hashes for `UnityDebugAdapter.dll`,
  `UnityAttachProbe.dll`, `UnityCodeModel.dll`,
  `SyntaxTree.Mono.Debugger.Soft.dll`. REA evidence ids are retained in the
  matrix rows.
- **Static, shipped-artifact bytes** — REA's JavaScript analyzer exhausted its
  worker heap on the 322 KB `out/extension.js` in both versions (1 GB and 12 GB;
  `retained_relations: 0` for every semantic family), so extension-side claims
  come from bounded direct inspection of the shipped bundle and manifest. This
  limitation is recorded, not hidden.
- **Runtime** — the capability block below was obtained by launching the shipped
  adapter and issuing a real DAP `initialize` request, then reading the framed
  response. The target list schema was obtained by launching the shipped
  attach probe (`bin\UnityAttachProbe.dll`) against a live Editor's
  `app_contents_path` and reading the framed JSON-RPC notification it emits on
  stdout. Both are live observations of shipped artifacts, not reconstructions.

## Reference capability diff (runtime-observed)

Reference `initialize` response body, VSTU 1.3.2, verbatim:

```json
{"supportsConfigurationDoneRequest":false,"supportsFunctionBreakpoints":true,
 "supportsConditionalBreakpoints":true,"supportsHitConditionalBreakpoints":false,
 "supportsEvaluateForHovers":true,
 "exceptionBreakpointFilters":[
   {"filter":"all","label":"All Exceptions","description":"Break on all exceptions",
    "default":false,"supportsCondition":true,"conditionDescription":"List of exceptions ..."},
   {"filter":"unhandled","label":"User-Unhandled Exceptions",
    "description":"Break only on unhandled exceptions","default":false,
    "supportsCondition":true,"conditionDescription":"List of exceptions ..."}],
 "supportsStepBack":false,"supportsSetVariable":true,"supportsGotoTargetsRequest":true,
 "supportsStepInTargetsRequest":true,"completionTriggerCharacters":[],
 "additionalModuleColumns":[],"supportedChecksumAlgorithms":[],
 "supportsRestartRequest":false,"supportsDelayedStackTraceLoading":false,
 "supportsLogPoints":true,"supportsTerminateRequest":true,"supportsDataBreakpoints":false,
 "supportsReadMemoryRequest":false,"supportsWriteMemoryRequest":false,
 "supportsDisassembleRequest":false,"supportsSteppingGranularity":false,
 "supportsInstructionBreakpoints":false,"supportsExceptionFilterOptions":true,
 "supportsSingleThreadExecutionRequests":false,"breakpointModes":[],
 "supportsExceptionConditions":true,"pid":49888}
```

Comparison with the current adapter
(`adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs:51-74`):

| Capability | Reference | Pure | Action |
| --- | --- | --- | --- |
| `supportsConfigurationDoneRequest` | false | false | aligned |
| `supportsFunctionBreakpoints` | true | true | aligned |
| `supportsConditionalBreakpoints` | true | true | aligned |
| `supportsHitConditionalBreakpoints` | **false** | not advertised | aligned — the reference does **not** support hit conditions, so our numeric-equality parsing is extra, not a gap |
| `supportsEvaluateForHovers` | true | true | aligned |
| `supportsSetVariable` | true | true | aligned |
| `supportsGotoTargetsRequest` | true | true | aligned |
| `supportsStepInTargetsRequest` | true | true | aligned |
| `supportsLogPoints` | true | true | aligned |
| `supportsTerminateRequest` | true | true | aligned |
| `supportsStepBack`/`Restart`/`DataBreakpoints`/`ReadMemory`/`WriteMemory`/`Disassemble`/`SteppingGranularity`/`InstructionBreakpoints`/`SingleThreadExecutionRequests`/`DelayedStackTraceLoading` | false | not advertised | aligned |
| **`supportsExceptionFilterOptions`** | **true** | not advertised | **gap** |
| **`supportsExceptionConditions`** | **true** | not advertised | **gap** |
| **`supportsExceptionOptions`** (legacy) | **absent** | **true** | **divergence — remove** |
| **filter id, user-unhandled** | **`unhandled`** | **`uncaught`** | **gap** |
| filter `description` | present | absent | gap |
| filter `supportsCondition` + `conditionDescription` | `true` / "List of exceptions ..." | absent | gap |
| filter `default` | false | false | aligned |
| **`pid`** (adapter's own process id) | present | absent | gap |

## Reference extension-surface diff

| Area | Reference 1.3.2 | Pure 0.3.2 | Action |
| --- | --- | --- | --- |
| Attach command | `vstuc.attachUnityDebugger` → QuickPick titled "Select Unity Instance", placeholder "Detecting... press any key to filter", `busy: true` | only `refreshTargets` (current workspace, editors only) | implement |
| Target list | Editors **and** Players (local, Android via adb/`Mono.AndroidTools`, Apple via usbmuxd P/Invoke); background processes filtered by a setting; non-background first | players parsed then discarded (`editorDiscovery.ts:110-126`) | implement |
| Item label | `Unity <displayType>, <projectName>, <machine>, <displayConnection>` joined with ", ". Observed live: `Unity Editor, sgproj, xiaolonghan, Port:56548 (VSTU)` | n/a | implement, with the three label divergences below recorded |
| Player `debug` gating | Lists an Android player whose advertisement carried `debug: false` as `isValidForAttachment: true` | `playerAdvertisement.ts:52` maps `debug === "1"` to `allowDebugging`, and W2 filters on it | **divergent — recorded, not changed** |
| Connection label engine token | `Port:56548 (VSTU)` | will render `Port:<port> (Mono)` | **divergent — accepted cosmetic** |
| Machine label for network players | `samsung_SM-S9260@10.30.154.73` | Unity advertisements carry no device name; we use the advertised `ip` | **divergent — recorded** |
| Attach properties | `projectPath` (default `${workspaceFolder}`), `logFile`, `endPoint`; `required: []` | `name`/`type`/`request` only | implement |
| `endPoint` | `address + ":" + debuggerPort`; address is `127.0.0.1` or the docker host | loopback only, hardcoded | implement (policy change) |
| Settings | 5 contributed; **`displayBackgroundProcesses` is the only one that changes debugger behaviour** | **0 contributed** | implement the debugger-relevant one |
| Function-breakpoint warning | raised from `createDebugAdapterDescriptor` whenever any `vscode.FunctionBreakpoint` exists | absent | implement |
| Attach errors | "Unity is not running for the current project"; "No Unity endPoint to connect to"; "No Process to attach to"; "No processes to attach to" | "No local debug-enabled Tuanjie or Unity Editor was found. Switch Code Optimization to Debug, then refresh." | align |
| Unity API Reference | `vstuc.showUnityAPIReference` → opens `https://docs.unity3d.com/ScriptReference/30_search.html?q=<selection>` | absent | implement |
| Output channel | one channel titled with the extension display name | absent | implement |
| Deprecated-extension warning | warns when `unity.unity-debug` is active | absent | implement |
| Discovery mechanism | spawned .NET probe over stdio JSON-RPC, notification `debuggerTargets`, plus `Library/EditorInstance.json` (`process_id`, `app_path`, `app_contents_path`) | multicast UDP `225.0.0.222` + `Library/EditorInstance.json` + `ProjectSettings/ProjectVersion.txt` | keep ours (internal, lawful) |
| Debugger port | `56000 + processId % 1000` | `56000 + processId % 1000` (`editorInstance.ts:32`) | identical — confirms protocol compatibility |
| Engine routing | `DebuggerEngine.CoreCLR` (delegates to the `coreclr` debug type with `processId`) or `VSTU` (Mono, with `endPoint`) | Mono only | keep Mono |
| Telemetry | `@vscode/extension-telemetry` with an `aiKey`; launch/end/project events | none | keep none |
| Concord | writes `%USERPROFILE%\.dotnet\vsdbg-extensions\<id>.link` at activation | absent | not applicable (vsdbg/CoreCLR only) |

## Observed target-list payload

Captured live from `bin\UnityAttachProbe.dll` (JSON-RPC over stdio, notification
`debuggerTargets`), against a running Editor. One local Editor entry:

```json
{"processId":54548,"address":"127.0.0.1","machine":"xiaolonghan","projectName":"sgproj",
 "information":"","profilerPort":0,"debuggerPort":56548,"messagerPort":56550,
 "unityPlayer":null,"type":"Editor","discoveryType":"Automatic","debuggerEngine":"VSTU",
 "isBackground":false,"isProcessDetection":true,"displayType":"Editor",
 "displayConnection":"Port:56548 (VSTU)","isValidForAttachment":true}
```

and one network player, which resolves three questions the static analysis left open:

```json
{"processId":0,"address":"10.30.154.73","machine":"samsung_SM-S9260@10.30.154.73",
 "projectName":"\u5934\u53F7\u7981\u533A","information":null,"profilerPort":55000,
 "debuggerPort":56322,"messagerPort":0,
 "unityPlayer":{"ip":"10.30.154.73","port":55000,"processId":0,
   "flags":"RequestImmediateConnect, SupportsProfile, AutoConnectProfiler",
   "guid":3598034322,"editorId":2737672203,"version":1048832,"id":"AndroidPlayer",
   "debug":false,"debuggerPort":56322,"machine":"samsung_SM-S9260@10.30.154.73",
   "packageName":"AndroidPlayer","projectName":"\u5934\u53F7\u7981\u533A"},
 "type":"Player","discoveryType":"Automatic","debuggerEngine":"VSTU",
 "isBackground":false,"isProcessDetection":false,"displayType":"Player",
 "displayConnection":"Port:56322 (VSTU)","isValidForAttachment":true}
```

This resolves: the `ProcessType` values are `Editor` and `Player`; `DiscoveryType`
is `Automatic`; and `displayType` is what feeds the `Unity <displayType>` label
prefix. Note `messagerPort` is `debuggerPort + 2`, confirming the UDP refresh
channel, and that the reference reports `isValidForAttachment: true` for a player
whose advertisement carried `debug: false` — the divergence recorded above.

## Scope

In scope:

- everything in the two diff tables above, restricted to the debugger;
- robustness fixes the acceptance project exposed (see
  [Acceptance](#acceptance));
- documentation: the matrix, `AGENTS.md`, `README.md`, and the amendment to the
  2026-08-06 parity design.

Permanently out of scope, restated and extended:

- Completion, diagnostics, CodeLens, Hover, Unity Message IntelliSense,
  project-system features, and shader/USS/UXML grammars and colouring.
- `vstuc.refreshOnSave` and Unity Asset Database refresh: the reference uses its
  own UDP channel to a Unity package we deliberately do not require.
- The `Visual Studio Editor` Unity package dependency and its
  missing/too-old warnings.
- CoreCLR engine routing, Android/iOS Player enumeration (adb, usbmuxd), and
  Concord vsdbg extension registration.
- Telemetry of any kind.
- A second fallback engine or a handwritten expression fallback.

## Design by work item

### W1 — Attach command

New command `unity-debugger-pure.attachUnityDebugger` in
`contributes.commands` and `activationEvents`. Behaviour mirrors the reference:
`createQuickPick()`, title "Select Unity Instance", placeholder
"Detecting... press any key to filter", `busy = true` while discovery runs,
items sorted non-background-first, and the accepted item starts a debug session
through `vscode.debug.startDebugging` with a synthesized attach configuration.

Item labels use the reference format
`Unity <type>, <projectName>, <machine>, <connection>` so the two extensions are
directly comparable in an A/B run.

### W2 — Discovery surfaces Players

`extension/src/editorDiscovery.ts` currently parses Player advertisements and
uses them only as a port source for the current workspace's Editor
(`:110-126`), and `EditorCandidate.host` is fixed to `"127.0.0.1"`
(`model.ts:18`). Change:

- an advertisement that is not attributable to the current workspace's Editor
  becomes its own candidate, carrying its advertised `ip` as `host`;
- keep the existing Editor path unchanged, including PID liveness and the
  `allowDebugging` gate;
- deduplicate by `host:port` in addition to `processId:port`.

Transport stays multicast UDP. This is an internal difference from the
reference's stdio probe and is recorded as such.

### W3 — Attach configuration properties

Add to `configurationAttributes.attach`: `projectPath` (string, default
`${workspaceFolder}`), `logFile` (string), `endPoint` (string). All optional.

`required` becomes `[]`, matching the reference. Ours currently requires
`[name, type, request]`, which is stricter than the reference and could reject a
configuration the reference accepts; VS Code supplies those three fields itself,
so relaxing the manifest changes nothing for ordinary use. The adapter keeps
validating what it actually needs at attach time.

Resolution order mirrors the reference: expand `${workspaceFolder}` in
`projectPath`; if `endPoint` is absent, discover; if discovery fails, raise the
W7 error; `projectPath` defaults to the workspace folder and is never required.

`initialConfigurations` gains `"projectPath": "${workspaceFolder}"` to match.

`logFile` is **implemented**, not merely declared: the adapter writes its
existing sanitized per-session log to this path instead of the default
`%LOCALAPPDATA%\unity-debugger-pure\logs` location. The reference declares this
property and never reads it (`logFile` occurs 0 times in `out/extension.js`);
implementing it is a superset that changes no reference-visible behaviour. The
redirected file keeps the existing allowlist and path redaction.

### W4 — Non-loopback endPoint

`adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs` and
`Backend/MonoDebuggingBackend.cs` currently reject any non-loopback host.
Accept an explicit `endPoint` host. Discovery-derived targets remain loopback.
`README.md` sections "Security and privacy" and "Currently excluded" must be
rewritten: remote Player attach becomes supported, and the "automatically
connects only to a discovered loopback address" claim must go.

Because Unity's managed debug transport has no authentication, attaching to a
remote endpoint executes arbitrary code reachable by whoever controls that
endpoint. The README must state this plainly.

### W5 — Settings

Contribute `unity-debugger-pure.displayBackgroundProcesses` (boolean, default
`false`) with the reference's description, and honor it in W1's filter. This is
the only contributed setting that changes debugger behaviour; the reference's
other four settings are language-service or Unity-package features and are out
of scope.

### W6 — Function-breakpoint warning

When `createDebugAdapterDescriptor` runs and any `vscode.FunctionBreakpoint`
exists, show the reference's warning: "You are using function breakpoints. Even
if they are fully supported, they have a major negative impact on debugging
performance. Use them only when necessary."

### W7 — Message alignment

Replace the single discovery failure message with the reference's distinct
cases: Unity not running for the current project; no Unity endpoint to connect
to; no process to attach to. Messages are localized through the same mechanism
the rest of the extension uses.

### W8 — Unity API Reference command

New command `unity-debugger-pure.showUnityApiReference`. With a selection or the
word at the cursor, open
`https://docs.unity3d.com/ScriptReference/30_search.html?q=<text>`; otherwise
open `https://docs.unity3d.com/ScriptReference`. No RPC, no local state.

### W9 — Output channel

Create one `LogOutputChannel` titled with the extension display name, used for
extension-side diagnostics. It must not carry expression text, variable values,
or source contents, consistent with the existing diagnostics rules.

### W10 — Adapter capability alignment

In `UnityDebugSession.cs:51-74`:

- remove `supportsExceptionOptions` (the reference does not send it);
- add `supportsExceptionFilterOptions = true` and `supportsExceptionConditions = true`;
- rename the user-unhandled filter id from `uncaught` to `unhandled` in the
  advertised filters, and keep accepting both ids on input so an existing
  `launch.json` or `exceptionOptions` payload that names `uncaught` still works
  rather than failing with the current `2030` error;
- add `description`, `supportsCondition`, and `conditionDescription` to both
  filters, matching the reference strings;
- report `pid` (adapter process id) in the initialize response body.

`ReferenceCapabilityTests.cs` encodes the current expectations and must be
updated from the observed reference response above, not from preference.

### W11 — Domain-reload cleanup

`Breakpoints/BreakpointManager.cs:231-309` (`MarkAllPending`, `BeginReload`,
`CompleteReload`, `RebindAll`, `RebindPending`) has **zero callers** in
`adapter/src`. The reference has no domain-reload-specific type or method either;
it delegates breakpoint ownership to the VS debug engine. In our architecture
the mature vendored `Mono.Debugging.Soft` session owns rebinding, and matrix row
RLD-01 was verified `aligned` on 0.3.0.

Therefore: **delete the dead reload methods** rather than wiring them up, and
correct `README.md:52` to describe the real mechanism. Add a matrix row
re-verifying RLD-01 on the current build.

The same cleanup applies to the unreferenced `Continued`/`ReloadStarted`/
`ReloadProgress`/`ReloadCompleted`/`ReconnectFailed` events declared only by
`UnavailableDebuggerBackend.cs:11-21` and the test scenario backend, and to the
uncalled `UnityDebugSession.NotImplemented` helper.

### W12 — Discovery robustness

Two real defects found while establishing the acceptance environment:

1. `ProjectVersion.txt` absent or lacking `m_EditorVersion` throws inside
   `readProjectVersion` (`projectVersion.ts:21`), is swallowed by
   `editorDiscovery.ts:140`, and makes discovery return **zero targets with no
   diagnostic**. A missing version file is not fatal to attaching.
2. A stale `Library/EditorInstance.json` whose PID is dead produces the same
   silent empty result, indistinguishable from "no Editor running".

Both must surface the W7 messages instead of silence.

### W13 — Documentation

- New rows in `docs/reference-debugger-compatibility.md` for every table row
  above, each with reference evidence, our result, status, and the user decision
  where one was required. REA evidence ids and the observed `initialize`
  response are recorded as evidence.
- `AGENTS.md`: replace the `zlorn.vstuc` reference and the MyGame acceptance
  path (see [Acceptance](#acceptance)).
- `README.md`: features, support policy, security, and the RLD-01 wording.
- The 2026-08-06 parity design's out-of-scope bullet about decompiling the
  reference binaries is amended per [Recorded decisions](#recorded-decisions).

## Invariants

Unchanged and enforced by existing tests:

- no C# Dev Kit dependency, and no `ms-dotnettools.*` runtime dependency;
- no Unity-side package requirement;
- no telemetry and no network egress other than the debug transport and the
  multicast discovery socket;
- no Microsoft/SyntaxTree binary in the VSIX (the package audit and runtime
  inventory continue to enforce this);
- the sanitized diagnostics allowlist stays closed.

Changed: the loopback-only attach restriction (W4).

## Recorded decisions

1. **Reverse-engineering boundary lifted — 2026-10-16, explicit user decision.**
   `2026-08-06-reference-debugger-engine-parity-design.md:50-51` and `:76-80`
   placed "reusing, separating, decompiling, modifying, or redistributing
   proprietary binaries from the reference extension" permanently out of scope.
   The user lifted the *analysis* restriction: the reference binaries may be
   inspected with REA as **behavioural evidence**. The remaining prohibitions
   stand unchanged — no reference code is copied, linked, packaged, or
   redistributed, and no reference binary becomes a build input or runtime
   dependency. The parity design must be amended to say exactly this.
   Note that the repository had already decompiled
   `SyntaxTree.VisualStudio.Unity.Common.dll` once (see
   `docs/reference-debugger-compatibility.md:151`), so this decision brings the
   written policy in line with prior practice rather than changing practice.
2. **Remote Player attach accepted — 2026-10-16, explicit user decision.**
   `endPoint` may name any `ip:port` (W4). This overrides the loopback-only
   stance in `README.md:85-90` and the "remote Player attach" exclusion in
   `README.md:81-83`. Security disclosure requirements are in W4.
3. **`logFile` implemented rather than stubbed** (W3).
4. **Domain-reload dead code deleted, not wired up** (W11), because the
   behaviour is already provided by the mature session and was verified aligned.

## Acceptance

Acceptance project, **chosen by the user on 2026-10-16**:
`D:\workspace\sgproj1\sgproj_xiaolonghan_0003`.

`AGENTS.md:5-11` currently mandates `D:\Unity\TuanjieHub\Projects\MyGame`, which
**no longer exists** (`D:\Unity\TuanjieHub\Projects` is gone). A bare,
script-free `MyGame` exists at `D:\Unity\UnityHub-D\Projects\MyGame`
(Unity 2022.3.62f3, `SampleScene` only) but does not carry the `DevTools` fixture
that every existing matrix row's line numbers refer to. `AGENTS.md` must be
updated to the new project.

Known environment facts and risks:

| Fact | Consequence |
| --- | --- |
| Project is Unity **2022.3.23f1**, not the Tuanjie `2022.3.62t11` baseline | `versionPolicy.ts:5` classifies it `compatible-unverified`; every acceptance row must record that the run was not on the formal baseline |
| `ProjectSettings\ProjectVersion.txt` **is absent** | discovery returns zero targets silently (W12) until fixed |
| `Library\EditorInstance.json` records PID 42260, which is **not alive** | the user must open the project in the Editor before any run |
| No `DevTools` fixture exists | a reachable breakpoint path must be identified and its reachability proven before use (`AGENTS.md:21-28`) |
| `com.unity.ide.visualstudio 2.0.22` is installed | the reference extension's Unity-package requirement is satisfied, so A/B runs are possible |
| `.vscode/launch.json` currently configures `"type": "vstuc"` | a `unity-debugger-pure` configuration must be added for our side of the A/B |

The user performs VS Code UI actions; no Computer Use is required unless the
user changes that instruction.

## Verification

1. Automated: new unit tests for label construction, background filtering,
   `endPoint` parsing, `projectPath` defaulting, filter-id acceptance, and the
   capability block — written before the implementation (TDD).
2. Protocol: a test asserts our `initialize` response body matches the observed
   reference body for every field we claim to align, and enumerates the
   intentional differences explicitly rather than ignoring them. The reference
   emits four empty arrays that come from the VS protocol library's serializer
   defaults — `completionTriggerCharacters`, `additionalModuleColumns`,
   `supportedChecksumAlgorithms`, `breakpointModes` — and our adapter, built on
   the vendored `VSCodeDebug` protocol layer, does not emit them. An empty array
   and an absent property are equivalent to a DAP client, so these are recorded
   as accepted representation differences.
3. Package: the existing VSIX audit, runtime inventory, and third-party
   provenance checks must continue to pass, proving no reference binary entered
   the package.
4. Real Editor: A/B runs on the acceptance project for the W1, W2, W6, W7, W10
   and W11 items, recorded as matrix rows. Any item that cannot be exercised is
   recorded `not verified` with the reason; no row is marked `aligned` from
   automated evidence alone.
5. The final handoff states every `divergent` or `not verified` row explicitly.
   A `not verified` row blocks a claim of reference compatibility.

## Unresolved evidence

Recorded so later work does not re-derive it:

- ~~The exact `ProcessType` / `DiscoveryType` enum string values are not pinned,
  so the literal `Unity <type>` label prefix for a local Editor is inferred.~~
  **Resolved** by the live probe capture above: `Editor` / `Player`, `Automatic`,
  and `displayType` drives the label prefix.
- Which lines of `out/extension.js` changed between 1.3.1 and 1.3.2 is not
  determined; REA's JavaScript semantic graph is partial for both and the
  reference ships no source map. The adapter itself is a pure version-bump
  rebuild (byte-identical IL, 7 differing bytes before the metadata root), and
  `UnityAttachProbe.dll` differs only by a namespace move, so behavioural risk is
  low.
- The reference's `debuggerTargets` payload schema is known from call edges and
  field names but not confirmed against a live capture.
- `SyntaxTree.VisualStudio.Unity.Common.dll` has not had a full member
  inventory; only a targeted scan.
