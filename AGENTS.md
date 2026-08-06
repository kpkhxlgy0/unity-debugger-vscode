# Repository Working Instructions

## Real Editor testing

- Use only the existing project at
  `D:\Unity\TuanjieHub\Projects\MyGame` for real Unity/Tuanjie runs.
- The user opens `MyGame` in the Editor. If it is not already running, stop
  and remind the user to open it; do not create or launch another project.
- Use the VS Code instance opened from `MyGame`. If that instance is not
  open, remind the user to open VS Code from `MyGame`; do not launch an
  isolated or separate VS Code profile.
- Do not create temporary Unity/Tuanjie projects or temporary VS Code
  profiles unless the user explicitly overrides these instructions.
- Before every Computer Use session, ask the user for confirmation and do not
  start it until the user explicitly confirms.
- After confirmation, start Computer Use only when it is needed for an actual
  MyGame, Tuanjie, or VS Code interaction, and close it immediately after that
  interaction is finished instead of leaving it idle.
- Unit, build, package, and simulated integration tests that do not require a
  real Editor may run normally.
- Never assume that a candidate breakpoint or code path will execute. Before
  using it for real-Editor acceptance testing, verify reachability with
  concrete evidence such as active-scene or prefab attachment, bootstrap and
  call-site references, or observed runtime behavior. If reachability is not
  established, do not interpret a missed breakpoint as a debugger failure.
- Prefer verified reachable code under MyGame's `DevTools` for real-Editor
  breakpoint acceptance tests. This preference does not replace the
  reachability check for the specific method and line selected.

## Reference debugger compatibility

- Treat the installed `zlorn.vstuc` debugger as the behavioral reference for
  UnityDebuggerPure.
- Whenever an observed debugger behavior, UI state, protocol result, or user
  experience differs from the reference plugin, stop before preserving or
  designing around that difference and ask the user whether the current
  implementation should be replaced to align more closely with the reference.
- Do not silently introduce or retain a custom debugger behavior when the
  reference plugin already defines the expected experience.
- Maintain an explicit reference-compatibility matrix for affected debugger
  behavior. Mark each item as reference-verified, aligned, divergent, or not
  yet verified, and retain the concrete comparison evidence.
- Call out every known or unverified difference in design reviews, test
  reports, and release handoffs. An unapproved difference blocks claiming
  reference compatibility or completion.
