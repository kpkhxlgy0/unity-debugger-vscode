import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import AdmZip from "adm-zip";

const artifactPath = "dist/unity-debugger-pure-0.2.0.vsix";

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
});

test("rejects a packaged manifest without the initial attach configuration", (t) => {
  const fixtureDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-pure-vsix-"),
  );
  t.after(() => fs.rmSync(fixtureDirectory, { recursive: true, force: true }));

  const fixturePath = path.join(fixtureDirectory, "missing-initial-config.vsix");
  const archive = new AdmZip(artifactPath);
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
