# Unity Debugger Pure Release Checklist

Use these gates in order for every public release. Version `0.1.0` is the
first release candidate.

## 1. Local release candidate

- Confirm `master` is clean.
- Run the complete dependency, build, test, third-party, package, runtime
  inventory, and VSIX verification suites.
- Confirm the release files are exactly
  `unity-debugger-pure-0.1.0.vsix` and
  `unity-debugger-pure-0.1.0.vsix.sha256`.
- Confirm the checksum matches the exact VSIX accepted in MyGame.

## 2. Public repository

- Confirm the public repository is
  `https://github.com/kpkhxlgy0/unity-debugger-vscode`.
- Confirm its default branch is `master`.
- Confirm the packaged manifest repository, bugs, and homepage links resolve
  to that repository.

## 3. GitHub publishing environments

Create two independently reviewed GitHub Environments.

`vscode-marketplace`:

```text
Secret name: VSCE_PAT
VSCE_PUBLISHER=kpk
MARKETPLACE_OWNERSHIP_CONFIRMED=true
```

`open-vsx`:

```text
Secret name: OVSX_PAT
OVSX_NAMESPACE=kpk
OPEN_VSX_OWNERSHIP_CONFIRMED=true
```

The user enters `VSCE_PAT` and `OVSX_PAT` directly in GitHub. Never put token
values in source, logs, shell history, release notes, issues, or chat.

## 4. Visual Studio Marketplace

- Publish only the audited GitHub Release VSIX after checksum and manifest
  verification.
- Confirm the listing is
  `https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure`.

## 5. Open VSX namespace ownership

- After the Marketplace listing and repository are public, submit the Open
  VSX `kpk` namespace ownership request using Option 1.
- Do not grant temporary Marketplace Reader access.
- Wait for ownership to be granted before enabling Open VSX publication.

## 6. Open VSX

- Publish the exact same GitHub Release VSIX used by the Visual Studio
  Marketplace workflow.
- Confirm the listing is
  `https://open-vsx.org/extension/kpk/unity-debugger-pure`.

## 7. Cursor

- Search Cursor Extensions for `@id:kpk.unity-debugger-pure`.
- Confirm Cursor resolves version `0.1.0` through Open VSX.
- If Open VSX is active but Cursor cannot discover the extension, request a
  Cursor marketplace sync/security review. Do not build another VSIX.

## 8. Installed extension migration and MyGame acceptance

- Uninstall `unity-debugger-community.unity-debugger-vscode`.
- Install `kpk.unity-debugger-pure` from the audited VSIX.
- Migrate only the old debugger entry in MyGame's `.vscode/launch.json`.
- Complete the reachable-breakpoint, stepping, pause, inspection, Domain
  Reload, continue, and disconnect acceptance checklist.

## 9. Rollback and versioning

Never reuse or overwrite an accepted `0.1.0` registry release. If a defect is
found, fix it on `master`, increment to at least `0.1.1`, build one new
audited VSIX, and repeat every gate.
