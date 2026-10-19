import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import AdmZip from "adm-zip";
import { resolveArtifactPath } from "../../scripts/vsix-artifact.mjs";

// The artifact `npm run package` produced: build-stamped, so this test can never
// read the retained release artifact by accident. `npm run package` names it
// through the environment instead of letting this file resolve it again, because
// the stamp comes from the commit and a commit landing mid-run would otherwise
// point the suite at a file that does not exist.
const artifactPath = resolveArtifactPath();

function loadArtifact() {
  assert.ok(
    fs.existsSync(artifactPath),
    `No packaged artifact at ${artifactPath}. ` +
      "Run npm run package before npm run test:package.",
  );
  return new AdmZip(artifactPath);
}

function verifyVsix(vsixPath) {
  return spawnSync(process.execPath, ["scripts/verify-vsix.mjs", vsixPath], {
    cwd: process.cwd(),
    encoding: "utf8",
  });
}

test("packaged VSIX satisfies the audited production contract", () => {
  const result = verifyVsix(artifactPath);

  assert.equal(
    result.status,
    0,
    `VSIX verifier failed:\n${result.stdout}\n${result.stderr}`,
  );
  assert.match(result.stdout, /VSIX verification passed/);

  const archive = loadArtifact();
  const manifestEntry = archive.getEntry("extension/package.json");
  const buildInfoEntry = archive.getEntry(
    "extension/adapter/win32-x64/build-info.json",
  );
  assert.ok(manifestEntry);
  assert.ok(buildInfoEntry);
  const manifest = JSON.parse(manifestEntry.getData().toString("utf8"));
  const buildInfo = JSON.parse(
    buildInfoEntry.getData().toString("utf8"),
  );
  assert.equal(manifest.version, "0.3.3");
  assert.equal(buildInfo.version, "0.3.3");
  assert.match(buildInfo.commit, /^[0-9a-f]{40}$/);
  assert.match(buildInfo.buildId, /^0\.3\.3\+g[0-9a-f]{12}$/);
  assert.equal(
    buildInfo.buildId,
    `0.3.3+g${buildInfo.commit.slice(0, 12)}`,
  );
});

test("rejects a packaged manifest without the initial attach configuration", (t) => {
  const fixtureDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-pure-vsix-"),
  );
  t.after(() => fs.rmSync(fixtureDirectory, { recursive: true, force: true }));

  const fixturePath = path.join(fixtureDirectory, "missing-initial-config.vsix");
  const archive = loadArtifact();
  const manifestEntry = archive.getEntry("extension/package.json");
  assert.ok(manifestEntry);
  const manifest = JSON.parse(manifestEntry.getData().toString("utf8"));
  const debuggerContribution = manifest.contributes.debuggers.find(
    (entry) => entry.type === "unity-debugger-pure",
  );
  assert.ok(debuggerContribution);
  delete debuggerContribution.initialConfigurations;
  archive.updateFile(
    "extension/package.json",
    Buffer.from(JSON.stringify(manifest), "utf8"),
  );
  archive.writeZip(fixturePath);

  const result = verifyVsix(fixturePath);

  assert.notEqual(result.status, 0);
  assert.match(
    `${result.stdout}\n${result.stderr}`,
    /wrong initial debug configuration/i,
  );
});

test("rejects a packaged implicit evaluation setting", (t) => {
  const fixtureDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-pure-vsix-"),
  );
  t.after(() => fs.rmSync(fixtureDirectory, { recursive: true, force: true }));

  const fixturePath = path.join(
    fixtureDirectory,
    "changed-implicit-evaluation.vsix",
  );
  const archive = loadArtifact();
  const manifestEntry = archive.getEntry("extension/package.json");
  assert.ok(manifestEntry);
  const manifest = JSON.parse(manifestEntry.getData().toString("utf8"));
  manifest.contributes.configuration = {
    title: "Unity Debugger Pure",
    properties: {
      "unityDebuggerPure.enableImplicitEvaluation": {
        type: "boolean",
        default: true,
      },
    },
  };
  archive.updateFile(
    "extension/package.json",
    Buffer.from(JSON.stringify(manifest), "utf8"),
  );
  archive.writeZip(fixturePath);

  const result = verifyVsix(fixturePath);

  assert.notEqual(result.status, 0);
  assert.match(
    `${result.stdout}\n${result.stderr}`,
    /implicit evaluation setting/i,
  );
});

test("rejects a packaged private implicit evaluation Attach argument", (t) => {
  const fixtureDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-pure-vsix-"),
  );
  t.after(() => fs.rmSync(fixtureDirectory, { recursive: true, force: true }));

  const fixturePath = path.join(
    fixtureDirectory,
    "private-implicit-evaluation.vsix",
  );
  const archive = loadArtifact();
  const manifestEntry = archive.getEntry("extension/package.json");
  const extensionEntry = archive.getEntry("extension/dist/extension.cjs");
  assert.ok(manifestEntry);
  assert.ok(extensionEntry);
  const manifest = JSON.parse(manifestEntry.getData().toString("utf8"));
  delete manifest.contributes.configuration;
  archive.updateFile(
    "extension/package.json",
    Buffer.from(JSON.stringify(manifest), "utf8"),
  );
  archive.updateFile(
    "extension/dist/extension.cjs",
    Buffer.concat([
      extensionEntry.getData(),
      Buffer.from("\n// __enableImplicitEvaluation\n", "utf8"),
    ]),
  );
  archive.writeZip(fixturePath);

  const result = verifyVsix(fixturePath);

  assert.notEqual(result.status, 0);
  assert.match(
    `${result.stdout}\n${result.stderr}`,
    /private implicit evaluation.*attach argument/i,
  );
});
