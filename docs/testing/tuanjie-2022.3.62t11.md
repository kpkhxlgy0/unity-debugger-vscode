# Tuanjie 2022.3.62t11 acceptance procedure

This procedure validates release `0.1.0` against the formally supported
Windows x64 Editor. Run it from the repository root and record every result in
`docs/testing/release-checklist.md`.

The fixture contains no IDE package, scene, or user-created GameObject. Its
`InitializeOnLoad` script provides stable managed-code targets as soon as the
Editor finishes compiling.

## 1. Build and audit the candidate

```powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
npm run verify:third-party
npm test
npm run package
Get-FileHash -Algorithm SHA256 `
  dist/unity-debugger-vscode-0.1.0.vsix
git rev-parse HEAD
```

Copy the VSIX SHA-256 and commit into the acceptance record. Do not rebuild the
VSIX after testing begins.

## 2. Create an isolated VS Code profile

Use new temporary directories so no C# Dev Kit, Microsoft Unity extension, or
other debugger participates in the dependency-free run:

```powershell
$acceptanceRoot = Join-Path `
  ([IO.Path]::GetTempPath()) `
  ("unity-debugger-vscode-" + [guid]::NewGuid().ToString("N"))
$userData = Join-Path $acceptanceRoot "user-data"
$extensions = Join-Path $acceptanceRoot "extensions"

code `
  --user-data-dir $userData `
  --extensions-dir $extensions `
  --install-extension `
  (Resolve-Path dist/unity-debugger-vscode-0.1.0.vsix) `
  --force

code `
  --user-data-dir $userData `
  --extensions-dir $extensions `
  (Resolve-Path tests/fixtures/TuanjieProject)
```

In that window, run **Extensions: Show Installed Extensions** and record that
the candidate is the only installed extension.

## 3. Open and verify the fixture Editor

Open `tests/fixtures/TuanjieProject` in Tuanjie `2022.3.62t11`. Wait for script
compilation, then set **Code Optimization** to **Debug**.

Run the read-only preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-real-editor-environment.ps1 `
  -ProjectPath tests/fixtures/TuanjieProject
```

All five rows must be `PASS`, including the running PID and derived loopback
debugger port.

## 4. Attach and test breakpoints

In the isolated VS Code window, select **Run and Debug: Add Configuration...**
and choose **Attach Tuanjie / Unity 2022 Editor**:

```json
{
  "name": "Attach Tuanjie / Unity 2022 Editor",
  "type": "unity-community",
  "request": "attach"
}
```

Start debugging with F5.

1. Put a standard breakpoint on `health = Decrement(health);` and confirm it
   binds and stops.
2. Add condition `health == 1`, continue, and confirm it stops only when the
   condition is true.
3. Clear the condition before continuing with the remaining checks.

## 5. Inspect and control execution

While stopped in `Tick`:

1. Confirm Threads contains the stopped managed thread.
2. Confirm Call Stack contains `DebuggerFixture.Tick`.
3. Confirm Locals displays `health`.
4. Add `health` to Watch and confirm its numeric value.
5. Evaluate `health` in Debug Console and confirm the same value.
6. Step Into at `health = Decrement(health);` and confirm the current frame is
   `Decrement`.
7. Step Over `return value - 1;`.
8. Step Out and confirm execution returns to `Tick`.

## 6. Test exception modes

Temporarily replace the first statement in `Tick` with the following caught
exception, save, and wait for compilation:

```csharp
try
{
    throw new System.InvalidOperationException("debugger fixture caught");
}
catch (System.InvalidOperationException)
{
}
health = Decrement(health);
```

Set exception break mode to **All**, continue, and confirm the debugger stops
on the throw. Set it to **Uncaught**, continue, and confirm the caught exception
does not stop.

Next, temporarily remove the `try`/`catch` and use:

```csharp
throw new System.InvalidOperationException("debugger fixture uncaught");
```

With **Uncaught** enabled, confirm the debugger stops on the throw. Restore the
committed `Tick` body before sign-off:

```powershell
git diff -- tests/fixtures/TuanjieProject/Assets/Editor/DebuggerFixture.cs
```

The command must print no diff.

## 7. Test Domain Reload recovery

Keep a breakpoint on `health = Decrement(health);`. Make and save this temporary
change:

```csharp
private static int health = 2;
```

During compilation, confirm the breakpoint becomes pending. After Domain
Reload, confirm it rebinds and stops again without starting a new debug
session. Restore `health = 1` and confirm the fixture has no diff.

## 8. Disconnect and reattach

Stop the debug session, then verify the Adapter exited:

```powershell
Get-Process UnityCommunityDebug -ErrorAction SilentlyContinue
```

The command must produce no process. Start F5 again and confirm a fresh attach
and breakpoint stop.

## 9. Verify ReSharper coexistence

Close the isolated window. In a second isolated profile, install the candidate
and ReSharper for VS Code, then repeat attach, breakpoint, and evaluation.

Confirm ReSharper code analysis remains active and that its ordinary .NET
launch/attach configuration types are unchanged. The candidate must own only
the `unity-community` debug type.

## 10. Check an unverified Editor when available

If another Unity `2022.3` or Unity 6 Editor is already installed, open the same
fixture with that Editor and try attach. Confirm exactly one unverified-version
warning appears for the session. Do not install another Editor solely for this
optional check; record `N/A (not installed)` when unavailable.

## 11. Inspect privacy behavior

Run **Unity Community Debugger: Open Sanitized Diagnostics** and **Copy
Sanitized Diagnostics**. Check both outputs and confirm they contain none of:

- source text;
- expression text or evaluation results;
- variable values;
- the complete user profile path;
- the complete repository or fixture path.

Only sanitized operation names, versions, endpoint, outcomes, and redacted
paths may remain.

## 12. Sign off and clean up

Complete all metadata and PASS/FAIL rows in
`docs/testing/release-checklist.md`. Confirm:

```powershell
git diff --check
git status --short
```

The fixture and repository must have no uncommitted changes. The temporary
isolated VS Code directories may then be removed by the tester.
