import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import AdmZip from "adm-zip";
import { verifyReleaseArtifact } from "../../scripts/verify-release-artifact.mjs";

const artifactFileName = "unity-debugger-pure-0.4.0.vsix";

test("accepts the audited VSIX, checksum, manifest, and tag", async (t) => {
  const fixture = await createFixture(t);

  const verified = await verifyReleaseArtifact({
    vsixPath: fixture.vsixPath,
    checksumPath: fixture.checksumPath,
    expectedTag: "v0.4.0",
  });

  assert.equal(verified.sha256, fixture.sha256);
  assert.equal(verified.publisher, "kpk");
  assert.equal(verified.name, "unity-debugger-pure");
  assert.equal(verified.version, "0.4.0");
});

test("rejects a VSIX changed after its checksum was written", async (t) => {
  const fixture = await createFixture(t);
  await fs.appendFile(fixture.vsixPath, Buffer.from([0]));

  await assert.rejects(
    verifyReleaseArtifact({
      vsixPath: fixture.vsixPath,
      checksumPath: fixture.checksumPath,
      expectedTag: "v0.4.0",
    }),
    /Release artifact SHA-256 mismatch/,
  );
});

test("rejects a release tag that differs from the manifest version", async (t) => {
  const fixture = await createFixture(t);

  await assert.rejects(
    verifyReleaseArtifact({
      vsixPath: fixture.vsixPath,
      checksumPath: fixture.checksumPath,
      expectedTag: "v0.4.1",
    }),
    /Release tag does not match manifest version/,
  );
});

async function createFixture(t) {
  const directory = await fs.mkdtemp(
    path.join(os.tmpdir(), "unity-debugger-pure-release-"),
  );
  t.after(async () => {
    await fs.rm(directory, { recursive: true, force: true });
  });

  const vsixPath = path.join(directory, artifactFileName);
  const checksumPath = `${vsixPath}.sha256`;
  const archive = new AdmZip();
  archive.addFile(
    "extension/package.json",
    Buffer.from(
      JSON.stringify({
        publisher: "kpk",
        name: "unity-debugger-pure",
        version: "0.4.0",
      }),
      "utf8",
    ),
  );
  archive.writeZip(vsixPath);

  const bytes = await fs.readFile(vsixPath);
  const sha256 = createHash("sha256").update(bytes).digest("hex");
  await fs.writeFile(
    checksumPath,
    `${sha256}  ${artifactFileName}\n`,
    "utf8",
  );
  return { vsixPath, checksumPath, sha256 };
}
