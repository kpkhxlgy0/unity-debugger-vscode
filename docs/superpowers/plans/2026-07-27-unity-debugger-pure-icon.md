# Unity Debugger Pure Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Generate the approved Unity Debugger Pure icon, integrate it into the extension, and prepare a fully verified 0.1.1 VSIX without publishing it.

**Architecture:** Treat the icon as a packaged product asset with an explicit manifest contract. Establish failing tests for the manifest, PNG dimensions, product identity, release filenames, and workflow paths before updating each production boundary. Generate the visual once with the built-in image tool, normalize it deterministically with ImageMagick, then verify small-size appearance and inclusion in the audited VSIX.

**Tech Stack:** Built-in image_gen, ImageMagick 7, Node.js 26.5.0, npm, node:test, Vitest, @vscode/vsce 3.9.2, ovsx 1.0.2, actionlint, .NET SDK 10.0.x

## Global Constraints

- Final asset: images/icon.png, PNG, exactly 512x512 pixels.
- Background: full-bleed #0B1220; no transparency is required.
- Mark: centered cyan isometric wireframe cube using #38BDF8, with optional #0EA5E9 face separation.
- Breakpoint: solid #F14C4C circle replacing the cube's lower-right node.
- Keep all graphic content inside a 12% safe area and readable at 128, 64, and 32 pixels.
- No text, letters, gradients, shadows, texture, watermark, or third-party logo geometry.
- Manifest identity becomes version 0.1.1 with "icon": "images/icon.png".
- Versioned package output becomes unity-debugger-pure-0.1.1.vsix and .sha256 consistently.
- The supported Editor matrix and debugger behavior do not change.
- Historical design/plan documents and the immutable v0.1.0 GitHub Release do not change.
- Tagging, GitHub Release creation, Marketplace upload, and Open VSX publication are out of scope until separately confirmed.

---

## File Structure

- Create images/icon.png: the only committed extension icon asset.
- Modify package.json: version, icon declaration, and versioned package scripts.
- Modify package-lock.json: root package version synchronized by npm.
- Modify extension/src/productIdentity.ts: versioned VSIX filename contract.
- Modify scripts/verify-release-artifact.mjs: accepted release filename and manifest version.
- Modify scripts/verify-vsix.mjs: require and allowlist the declared icon.
- Modify .github/workflows/release.yml: upload 0.1.1 VSIX and checksum.
- Modify .github/workflows/publish-open-vsx.yml: dispatch, download, verify, and publish 0.1.1.
- Modify tests/build/scaffold.test.mjs: manifest icon/version and PNG header contract.
- Modify tests/build/release-artifact.test.mjs: 0.1.1 release verification fixtures.
- Modify tests/build/release-workflows.test.mjs: 0.1.1 Open VSX workflow contract.
- Modify tests/extension/productIdentity.test.ts: 0.1.1 VSIX identity contract.
- Modify tests/package/vsix.test.mjs: package path for 0.1.1.
- Modify current user-facing version references in README.md, SECURITY.md, docs/release-checklist.md, extension/src/versionPolicy.ts, and adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs, together with their existing tests.
- Modify CHANGELOG.md: add the 0.1.1 icon release entry while retaining 0.1.0 history.

---

### Task 1: Establish the Icon and Version Contract

**Files:**
- Modify: tests/build/scaffold.test.mjs
- Modify: tests/extension/productIdentity.test.ts
- Test: tests/build/scaffold.test.mjs
- Test: tests/extension/productIdentity.test.ts

**Interfaces:**
- Consumes: current package.json and PRODUCT_IDENTITY object.
- Produces: failing assertions for manifest version 0.1.1, icon path and PNG dimensions, and PRODUCT_IDENTITY.vsixFileName.

- [ ] **Step 1: Add the failing manifest and PNG assertions**

Replace the old manifest version assertion in tests/build/scaffold.test.mjs and add:

~~~js
  assert.equal(manifest.version, "0.1.1");
  assert.equal(manifest.icon, "images/icon.png");

  const icon = fs.readFileSync(manifest.icon);
  assert.deepEqual(
    [...icon.subarray(0, 8)],
    [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
  );
  assert.equal(icon.readUInt32BE(16), 512);
  assert.equal(icon.readUInt32BE(20), 512);
~~~

- [ ] **Step 2: Add the failing product identity assertion**

Add to tests/extension/productIdentity.test.ts:

~~~ts
    expect(PRODUCT_IDENTITY.vsixFileName).toBe(
      "unity-debugger-pure-0.1.1.vsix",
    );
~~~

- [ ] **Step 3: Run focused tests and verify RED**

~~~powershell
node --test tests/build/scaffold.test.mjs
npx vitest run tests/extension/productIdentity.test.ts
~~~

Expected: the build test fails because package.json is still 0.1.0 and has no icon; the product identity test fails because it still names 0.1.0.

- [ ] **Step 4: Commit the failing contract tests**

~~~powershell
git add tests/build/scaffold.test.mjs tests/extension/productIdentity.test.ts
git commit -m "test: require extension icon and 0.1.1 identity"
~~~

---

### Task 2: Generate and Integrate the Icon Asset

**Files:**
- Create: images/icon.png
- Modify: package.json
- Modify: package-lock.json
- Modify: extension/src/productIdentity.ts
- Test: tests/build/scaffold.test.mjs
- Test: tests/extension/productIdentity.test.ts

**Interfaces:**
- Consumes: approved icon design and Task 1 failing tests.
- Produces: exact 512x512 PNG, manifest icon path, version 0.1.1, and VSIX filename identity.

- [ ] **Step 1: Generate the approved square icon**

Invoke the imagegen skill and built-in image generation tool with this prompt:

~~~text
Use case: logo-brand
Asset type: VS Code and Open VSX extension marketplace icon
Primary request: Create a minimal flat icon for an independent managed C# game-engine debugger.
Scene/backdrop: A perfectly uniform full-bleed deep navy square background, exact visual target #0B1220.
Subject: One centered cyan isometric wireframe cube. Replace the cube's lower-right vertex with one solid circular debugger breakpoint.
Style/medium: Crisp flat vector-friendly geometric mark rendered as a raster PNG; bold simple strokes; no photorealism.
Composition/framing: Square, centered, graphic content inside a 12% safe area, balanced at 32px.
Color palette: Cube #38BDF8 with optional #0EA5E9 edge separation; breakpoint #F14C4C; background #0B1220.
Constraints: The red breakpoint is structurally part of the cube and not a notification badge. No text, no letters, no gradients, no shadows, no texture, no reflections, no watermark. Do not reproduce or resemble the Unity, Tuanjie, Microsoft, or VS Code logos.
Avoid: extra symbols, bugs or insects, play buttons, terminal windows, code brackets, bevels, glow, tiny details.
~~~

- [ ] **Step 2: Inspect the generated source**

Use view_image on the generated file. Reject and regenerate once with one targeted correction if text, trademark-like geometry, gradient, shadow, glow, extra shapes, a detached notification-style dot, or an unreadable cube appears.

- [ ] **Step 3: Normalize the accepted source**

Assign the exact absolute path returned by the image generation tool to the
PowerShell variable `$generatedImagePath`; do not infer it from a temporary
directory or select a different recent image. Then run:

~~~powershell
New-Item -ItemType Directory -Force images | Out-Null
magick $generatedImagePath -resize '512x512!' -strip -colorspace sRGB 'PNG32:images/icon.png'
magick identify -format '%m %wx%h %[colorspace]' images/icon.png
~~~

Expected: PNG 512x512 sRGB.

- [ ] **Step 4: Update manifest and lockfile version**

~~~powershell
npm version 0.1.1 --no-git-tag-version
~~~

Add directly after displayName in package.json:

~~~json
"icon": "images/icon.png",
~~~

- [ ] **Step 5: Update product identity filename**

~~~ts
  vsixFileName: "unity-debugger-pure-0.1.1.vsix",
~~~

- [ ] **Step 6: Verify GREEN and inspect small sizes**

~~~powershell
node --test tests/build/scaffold.test.mjs
npx vitest run tests/extension/productIdentity.test.ts
$previewDir = Join-Path $env:TEMP 'unity-debugger-pure-icon-preview'
New-Item -ItemType Directory -Force $previewDir | Out-Null
magick images/icon.png -resize 128x128 (Join-Path $previewDir 'icon-128.png')
magick images/icon.png -resize 64x64 (Join-Path $previewDir 'icon-64.png')
magick images/icon.png -resize 32x32 (Join-Path $previewDir 'icon-32.png')
~~~

Use view_image on all three preview files. Expected: cube and breakpoint remain distinct at each size.

- [ ] **Step 7: Commit asset and identity implementation**

~~~powershell
git add images/icon.png package.json package-lock.json extension/src/productIdentity.ts
git commit -m "feat: add Unity Debugger Pure icon"
~~~

---

### Task 3: Migrate Versioned Release Contracts to 0.1.1

**Files:**
- Modify: tests/build/release-artifact.test.mjs
- Modify: tests/build/release-workflows.test.mjs
- Modify: tests/package/vsix.test.mjs
- Modify: scripts/verify-release-artifact.mjs
- Modify: package.json
- Modify: .github/workflows/release.yml
- Modify: .github/workflows/publish-open-vsx.yml

**Interfaces:**
- Consumes: version 0.1.1 and unity-debugger-pure-0.1.1.vsix from Task 2.
- Produces: consistent package, checksum, release, and Open VSX paths for 0.1.1.

- [ ] **Step 1: Update release tests first**

In tests/build/release-artifact.test.mjs, change the fixture filename, accepted tag, fixture manifest version, and returned version to 0.1.1. Change the mismatched-tag case to v0.1.2.

In tests/build/release-workflows.test.mjs, require:

~~~js
  assert.match(commands, /unity-debugger-pure-0\.1\.1\.vsix/);
~~~

In tests/package/vsix.test.mjs, invoke:

~~~js
      "dist/unity-debugger-pure-0.1.1.vsix",
~~~

- [ ] **Step 2: Run release tests and verify RED**

~~~powershell
node --test tests/build/release-artifact.test.mjs tests/build/release-workflows.test.mjs
~~~

Expected: production verifier and workflow still name 0.1.0.

- [ ] **Step 3: Update release artifact verifier**

~~~js
const expectedFileName = "unity-debugger-pure-0.1.1.vsix";
const expectedVersion = "0.1.1";
~~~

- [ ] **Step 4: Update versioned npm scripts**

~~~json
"verify:vsix": "node scripts/verify-vsix.mjs dist/unity-debugger-pure-0.1.1.vsix",
"verify:release-artifact": "node scripts/verify-release-artifact.mjs dist/unity-debugger-pure-0.1.1.vsix dist/unity-debugger-pure-0.1.1.vsix.sha256 v0.1.1",
"package": "npm run build && npm run stage:adapter && npm run inventory:runtime && vsce package --out dist/unity-debugger-pure-0.1.1.vsix && npm run verify:vsix && npm run test:package"
~~~

- [ ] **Step 5: Update workflows**

In .github/workflows/release.yml, upload dist/unity-debugger-pure-0.1.1.vsix and its .sha256.

In .github/workflows/publish-open-vsx.yml:
- description: Audited release tag, exactly v0.1.1
- both download patterns: 0.1.1
- both verifier paths: 0.1.1
- ovsx publish path: 0.1.1

- [ ] **Step 6: Verify GREEN and lint workflows**

~~~powershell
node --test tests/build/release-artifact.test.mjs tests/build/release-workflows.test.mjs
go run github.com/rhysd/actionlint/cmd/actionlint@latest .github/workflows/ci.yml .github/workflows/release.yml .github/workflows/publish-open-vsx.yml
~~~

Expected: focused tests pass and actionlint emits no diagnostics.

- [ ] **Step 7: Commit versioned release migration**

~~~powershell
git add package.json scripts/verify-release-artifact.mjs .github/workflows/release.yml .github/workflows/publish-open-vsx.yml tests/build/release-artifact.test.mjs tests/build/release-workflows.test.mjs tests/package/vsix.test.mjs
git commit -m "build: prepare 0.1.1 release paths"
~~~

---

### Task 4: Admit and Require the Icon in the Audited VSIX

**Files:**
- Modify: scripts/verify-vsix.mjs
- Test: tests/package/vsix.test.mjs
- Consume: images/icon.png

**Interfaces:**
- Consumes: manifest icon and package path from Tasks 2 and 3.
- Produces: audited VSIX containing extension/images/icon.png.

- [ ] **Step 1: Package once and verify allowlist RED**

~~~powershell
npm run package
~~~

Expected: scripts/verify-vsix.mjs rejects extension/images/icon.png as an unexpected production package file.

- [ ] **Step 2: Require and allowlist the icon**

Add to the required array:

~~~js
  "extension/images/icon.png",
~~~

Add to exactFiles in isAllowedPackagedPath:

~~~js
    "extension/images/icon.png",
~~~

After parsing the packaged manifest, add:

~~~js
if (manifest.icon !== "images/icon.png") {
  throw new Error("Packaged manifest has the wrong icon path.");
}
~~~

- [ ] **Step 3: Repackage and verify GREEN**

~~~powershell
npm run package
npm run verify:release-artifact
~~~

Expected: dist/unity-debugger-pure-0.1.1.vsix and checksum are produced; VSIX, package, and release artifact verification pass.

- [ ] **Step 4: Commit package allowlist**

~~~powershell
git add scripts/verify-vsix.mjs
git commit -m "build: include icon in audited VSIX"
~~~

---

### Task 5: Update Current Version Copy Without Changing Support Scope

**Files:**
- Modify: tests/extension/versionPolicy.test.ts
- Modify: tests/extension/debugConfigurationProvider.test.ts
- Modify: tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs
- Modify: extension/src/versionPolicy.ts
- Modify: adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs
- Modify: README.md
- Modify: SECURITY.md
- Modify: docs/release-checklist.md
- Modify: CHANGELOG.md

**Interfaces:**
- Consumes: prepared version 0.1.1.
- Produces: current release copy identifying 0.1.1 with unchanged support behavior.

- [ ] **Step 1: Update current-version tests first**

Replace current policy-message expectations saying version 0.1.0 with version 0.1.1 in the three test files.

~~~powershell
npx vitest run tests/extension/versionPolicy.test.ts tests/extension/debugConfigurationProvider.test.ts
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~AttachArgumentsTests
~~~

Expected: tests fail because production messages still name 0.1.0.

- [ ] **Step 2: Update production policy messages**

Change only displayed text in extension/src/versionPolicy.ts and adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs to:

~~~text
outside the version 0.1.1 compatibility policy
~~~

Do not change supported versions, warning conditions, or attach behavior.

- [ ] **Step 3: Update release documentation**

Add above 0.1.0 in CHANGELOG.md:

~~~markdown
## 0.1.1

- Add the Unity Debugger Pure extension icon to Marketplace and Open VSX packages.
~~~

- README.md: update sideload filename and current security paragraph to 0.1.1.
- SECURITY.md: update current supported release sentence to Version 0.1.1.
- docs/release-checklist.md: make 0.1.1 current, update asset names and Cursor version, protect both accepted 0.1.0 and 0.1.1, and require at least 0.1.2 for a later defect.

- [ ] **Step 4: Verify only intentional 0.1.0 history remains**

~~~powershell
rg -n '0\.1\.0' --glob '!docs/superpowers/**' --glob '!node_modules/**' .
~~~

Expected: 0.1.0 remains only in CHANGELOG history and rollback text explicitly protecting the immutable earlier release.

- [ ] **Step 5: Verify GREEN**

~~~powershell
npx vitest run tests/extension/versionPolicy.test.ts tests/extension/debugConfigurationProvider.test.ts
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~AttachArgumentsTests
~~~

Expected: all focused tests pass.

- [ ] **Step 6: Commit current release copy**

~~~powershell
git add extension/src/versionPolicy.ts adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs tests/extension/versionPolicy.test.ts tests/extension/debugConfigurationProvider.test.ts tests/adapter/UnityDebugger.Adapter.Tests/Dap/AttachArgumentsTests.cs README.md SECURITY.md docs/release-checklist.md CHANGELOG.md
git commit -m "docs: prepare 0.1.1 icon release"
~~~

---

### Task 6: Run Complete Release Candidate Verification

**Files:**
- Verify: all files changed by Tasks 1-5
- Output, ignored: dist/unity-debugger-pure-0.1.1.vsix
- Output, ignored: dist/unity-debugger-pure-0.1.1.vsix.sha256

**Interfaces:**
- Consumes: complete 0.1.1 tree.
- Produces: evidence that the icon-bearing VSIX is ready for a separately approved release.

- [ ] **Step 1: Restore exact dependencies**

~~~powershell
npm ci
dotnet restore UnityDebugger.sln --locked-mode
~~~

Expected: both exit 0; npm reports zero known vulnerabilities.

- [ ] **Step 2: Run source and runtime checks**

~~~powershell
npm run verify:third-party
npm test
~~~

Expected: all build, extension, Adapter, and integration tests pass with zero failures.

- [ ] **Step 3: Build and verify candidate package**

~~~powershell
npm run package
npm run verify:release-artifact
~~~

Expected: only the 0.1.1 VSIX and checksum are created; release verifier prints successful SHA-256 verification.

- [ ] **Step 4: Lint workflows and inspect repository state**

~~~powershell
go run github.com/rhysd/actionlint/cmd/actionlint@latest .github/workflows/ci.yml .github/workflows/release.yml .github/workflows/publish-open-vsx.yml
git diff --check
git status --short --branch
~~~

Expected: actionlint and diff check are silent; only planned tracked changes exist.

- [ ] **Step 5: Perform final visual inspection**

Use view_image on images/icon.png and 128, 64, and 32 pixel previews. Confirm cube, integrated breakpoint, flat palette, safe area, and absence of text or trademark geometry.

- [ ] **Step 6: Stop at release boundary**

Report candidate VSIX path and SHA-256. Do not create v0.1.1, push a release tag, create a GitHub Release, upload to Marketplace, or run Open VSX publication without a new explicit request.
