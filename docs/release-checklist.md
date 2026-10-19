# Unity Debugger Pure Release Checklist

Use these gates in order for every public release. Version `0.3.3` is the
current release candidate.

## 0.3.3 release verification (2026-10-19)

Recorded because the same bytes reaching all three channels is the property worth
proving, and it is not implied by one upload succeeding.

- **One artifact, three channels.** The reference artifact is the one CI built and
  attached to the GitHub Release: `unity-debugger-pure-0.3.3.vsix`, 5,862,017 bytes,
  sha256 `77a2fdc42596a2fc76ffb5bb09326e935d8e870b2270964c6a265528fd82dc54`.
- **Open VSX** was published by `publish-open-vsx.yml` (run 39215708998, 50s, log line
  `Published kpk.unity-debugger-pure v0.3.3`). The registry's own `.sha256` for 0.3.3
  matches the reference artifact, and a VSIX downloaded from the registry hashes to it, so
  the registry copy is the same file rather than a re-pack.
- **VS Code Marketplace** was uploaded by hand, because no workflow publishes there and a
  test asserts that `publish-marketplace.yml` does not exist. The VSIX served from
  `kpk.gallerycdn.vsassets.io` is 5,862,017 bytes and hashes to the same value, and its
  `package.json` reports `unity-debugger-pure 0.3.3` with 5 commands and adapter buildId
  `0.3.3+gf797391e7e2b`.
- **The local build is not the reference and must not be substituted for it.** The same
  tree produced `bf7ee783a848097d...` at 5,861,914 bytes - 103 bytes different, which is
  the ZIP-metadata non-reproducibility this file already warns about. Uploading that one
  would have shipped bytes that no channel had verified.
- **A note on reading the registry too early.** Immediately after the publish, Open VSX
  served `latest=0.3.2` and 404'd `0.3.3` for about ninety seconds while the CI log
  already said `Published`. That is index propagation, not failure: the 0.3.3 record now
  carries the publish timestamp `07:24:41` from that same log line.

## 1. Local release candidate

- Confirm `master` is clean.
- Run the complete dependency, build, test, third-party, package, runtime
  inventory, and VSIX verification suites. Build the candidate with
  `npm run package:release`, not `npm run package`: only the former names the
  version-named release path and then runs `verify:release-artifact` against it,
  while the latter deliberately writes a commit-stamped name so that an ordinary
  build cannot overwrite a retained release.
- Confirm the release files are exactly
  `unity-debugger-pure-0.3.3.vsix` and
  `unity-debugger-pure-0.3.3.vsix.sha256`.
- Confirm the checksum matches the exact VSIX accepted in
  `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`.
- **Compare that checksum against the downloaded artifact, never against a
  rebuild.** The VSIX is not byte-reproducible: the same tree has produced
  different archives because ZIP entry metadata varies between runs. What *is*
  reproducible is the adapter binary and the 17-entry runtime inventory that the
  suites above check. So a checksum is an identity for one file, not a property of
  a tree — and a rebuild that fails to match it is not evidence of a wrong build.
- **If adapter source changed, expect the runtime inventory check to refuse the
  staged binary, and regenerate it deliberately.** `npm run package` runs
  `inventory:runtime`, which compares the staged assemblies against the pinned
  `third-party/runtime-assemblies.json` and stops with
  `Runtime inventory differs from staged binaries` naming the expected and actual
  hashes. That is the guard that keeps an unaudited assembly out of the package
  working as designed, not a broken build: rebuild the adapter, then regenerate the
  inventory explicitly with `node scripts/generate-runtime-inventory.mjs --write`
  and review the diff, because the changed hash is a changed shipped binary.
- **Run the adapter suite with `UNITY_DEBUGGER_TEST_MONO` set, or read the skipped
  count as a warning rather than a number.** The Mono-backed tests skip themselves when
  that variable does not point at a standalone Mono executable, and the skip is applied
  per attribute rather than per case, so a handful of `[MonoFact]`/`[MonoTheory]` methods
  expand into far more cases that quietly do not run. Without it the suite reports
  **227 passed, 6 skipped**; with it, **249 passed, 0 skipped**. Unity's own runtime is
  enough - `Data\MonoBleedingEdge\bin\mono.exe` beside the Editor, which on 2022.3.23f1 is
  Mono 6.13.0, the same version that Editor reports over DAP. A release verified on the
  smaller number has verified less than it says.

## 2. Public repository

- Confirm the public repository is
  `https://github.com/kpkhxlgy0/unity-debugger-vscode`.
- Confirm its default branch is `master`.
- Confirm the packaged manifest repository, bugs, and homepage links resolve
  to that repository.

## 3. GitHub publishing environment

Create the reviewed `open-vsx` GitHub Environment:

```text
Secret name: OVSX_PAT
OVSX_NAMESPACE=kpk
OPEN_VSX_OWNERSHIP_CONFIRMED=true
```

The user enters `OVSX_PAT` directly in GitHub. Never put token values in
source, logs, shell history, release notes, issues, or chat.

## 4. Visual Studio Marketplace

- Download the audited VSIX and checksum from the matching GitHub Release.
- Verify the checksum before uploading.
- Upload the VSIX manually through
  `https://marketplace.visualstudio.com/manage/publishers/kpk`.
- Do not add a Marketplace token or automated Marketplace publishing workflow.
- Confirm the listing is
  `https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure`.

## 5. Open VSX namespace ownership

- After the Marketplace listing and repository are public, submit the Open
  VSX `kpk` namespace ownership request using Option 1.
- Do not grant temporary Marketplace Reader access.
- Wait for ownership to be granted before enabling Open VSX publication.

## 6. Open VSX

- Publish the exact same GitHub Release VSIX manually uploaded to the Visual
  Studio Marketplace.
- Confirm the listing is
  `https://open-vsx.org/extension/kpk/unity-debugger-pure`.

## 7. Cursor

- Search Cursor Extensions for `@id:kpk.unity-debugger-pure`.
- Confirm Cursor resolves version `0.3.3` through Open VSX.
- If Open VSX is active but Cursor cannot discover the extension, request a
  Cursor marketplace sync/security review. Do not build another VSIX.

## 8. Installed extension migration and acceptance-project run

- Uninstall `unity-debugger-community.unity-debugger-vscode`.
- Install `kpk.unity-debugger-pure` from the audited VSIX.
- Migrate only the old debugger entry in the acceptance project's
  `.vscode/launch.json` — `D:\workspace\sgproj1\sgproj_xiaolonghan_0003`.
- Complete the acceptance checklist below on Unity `2022.3.23f1`, which is
  **not** the Tuanjie `2022.3.62t11` baseline; record the Editor version actually
  used rather than implying the baseline:
  - the reachable-breakpoint, stepping, pause, inspection, Domain Reload,
    continue, and disconnect scenarios tracked in
    `docs/reference-debugger-compatibility.md`;
  - **Unity Debugger Pure: Attach Unity Debugger** and its picker: the item list,
    the label format, and starting a session from an accepted item;
  - Player attach, and attach through an explicit `endPoint`, including the
    rejection of a non-IP-literal host and of a non-canonical IP literal;
  - **Unity Debugger Pure: Unity API Reference**, with a selection and with the
    cursor on a plain identifier;
  - the `unity-debugger-pure.displayBackgroundProcesses` setting and the
    function-breakpoint warning;
  - the `Unity Debugger Pure` output channel, and the deprecated-extension
    warning with `unity.unity-debug` installed.
- Record every `divergent` and `not yet verified` row the run leaves in place: a
  `not yet verified` row blocks a claim of reference compatibility.

## 9. Rollback and versioning

Never reuse or overwrite accepted `0.1.0`, `0.1.1`, `0.2.0`, `0.3.0`, or `0.3.2`
registry releases. If a defect is found after publishing `0.3.2`, fix it on
`master`, increment to at least `0.3.3`, build one
new audited VSIX, and repeat every gate.
