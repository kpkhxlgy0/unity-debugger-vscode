# Repository Working Instructions

## Real Editor testing

- Use only the existing project at
  `D:\workspace\sgproj1\sgproj_xiaolonghan_0003` for real Unity runs. It is
  Unity `2022.3.23f1`, which is **not** the Tuanjie `2022.3.62t11` baseline the
  extension formally supports, so every acceptance record must state the Editor
  version actually used rather than implying the baseline.
- The user opens that project in the Editor. If it is not already running, stop
  and remind the user to open it; do not create or launch another project.
- Use the VS Code instance opened from that project. If that instance is not
  open, remind the user to open VS Code from it; do not launch an isolated or
  separate VS Code profile.
- Do not create temporary Unity projects or temporary VS Code profiles unless
  the user explicitly overrides these instructions.
- Before every Computer Use session, ask the user for confirmation and do not
  start it until the user explicitly confirms.
- After confirmation, start Computer Use only when it is needed for an actual
  Editor or VS Code interaction, and close it immediately after that
  interaction is finished instead of leaving it idle.
- Unit, build, package, and simulated integration tests that do not require a
  real Editor may run normally.
- Never assume that a candidate breakpoint or code path will execute. Before
  using it for real-Editor acceptance testing, verify reachability with
  concrete evidence such as active-scene or prefab attachment, bootstrap and
  call-site references, or observed runtime behavior. If reachability is not
  established, do not interpret a missed breakpoint as a debugger failure.
- The acceptance project has **no `DevTools` fixture**, so there is no
  pre-verified breakpoint location to prefer there: choose a candidate path in
  that project and prove the reachability of the specific method and line
  before using it. This replaces the earlier preference for verified reachable
  code under `MyGame`'s `DevTools`; that fixture never existed in this project.

## Reference debugger compatibility

- Treat the installed `visualstudiotoolsforunity.vstuc` **1.3.2** debugger
  (Microsoft's "Unity" for VS Code; installed copy
  `C:\Users\Admin\.cursor\extensions\local.vstuc-1.3.2`, provenance
  `D:\Unity\vstuc-mirror\build_local_vsix.py` — the official VSIX repacked with
  packaging metadata only) as the behavioral reference for UnityDebuggerPure.
  It supersedes `zlorn.vstuc 1.2.1`, which stays the authority for observations
  recorded before 2026-10-16; those keep their attribution.
- Whenever an observed debugger behavior, UI state, protocol result, or user
  experience differs from the reference plugin, stop before preserving or
  designing around that difference and ask the user whether the current
  implementation should be replaced to align more closely with the reference.
- Do not silently introduce or retain a custom debugger behavior when the
  reference plugin already defines the expected experience.
- Maintain an explicit reference-compatibility matrix for affected debugger
  behavior in `docs/reference-debugger-compatibility.md`. Mark each item as
  reference-verified, aligned, divergent, identical, not applicable, or not yet
  verified. `identical` and `not applicable` are named because the 2026-10-16
  comparison produced one row of each that no other term describes honestly.
  Retain the concrete comparison evidence.
- Call out every known or unverified difference in design reviews, test
  reports, and release handoffs. An unapproved difference blocks claiming
  reference compatibility or completion.
