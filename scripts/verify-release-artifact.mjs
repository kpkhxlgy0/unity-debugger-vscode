import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import AdmZip from "adm-zip";

const expectedFileName = "unity-debugger-pure-0.1.0.vsix";
const expectedPublisher = "kpk";
const expectedName = "unity-debugger-pure";
const expectedVersion = "0.1.0";

export async function verifyReleaseArtifact({
  vsixPath,
  checksumPath,
  expectedTag,
}) {
  const resolvedVsixPath = path.resolve(vsixPath);
  const resolvedChecksumPath = path.resolve(checksumPath);
  if (path.basename(resolvedVsixPath) !== expectedFileName) {
    throw new Error(
      `Release artifact filename must be ${expectedFileName}.`,
    );
  }

  const checksumText = await fs.readFile(
    resolvedChecksumPath,
    "utf8",
  );
  const checksumMatch = new RegExp(
    `^([0-9a-f]{64})  ${escapeRegExp(expectedFileName)}\\n$`,
  ).exec(checksumText);
  if (!checksumMatch) {
    throw new Error("Release checksum file has an invalid format.");
  }

  const artifact = await fs.readFile(resolvedVsixPath);
  const sha256 = createHash("sha256")
    .update(artifact)
    .digest("hex");
  if (sha256 !== checksumMatch[1]) {
    throw new Error("Release artifact SHA-256 mismatch.");
  }

  const archive = new AdmZip(artifact);
  const manifestEntry = archive.getEntry("extension/package.json");
  if (!manifestEntry || manifestEntry.isDirectory) {
    throw new Error("Release artifact has no extension/package.json.");
  }
  const manifest = JSON.parse(
    manifestEntry.getData().toString("utf8"),
  );
  if (
    manifest.publisher !== expectedPublisher ||
    manifest.name !== expectedName ||
    manifest.version !== expectedVersion
  ) {
    throw new Error("Release manifest identity does not match.");
  }
  if (expectedTag !== `v${manifest.version}`) {
    throw new Error(
      "Release tag does not match manifest version.",
    );
  }

  return {
    sha256,
    publisher: expectedPublisher,
    name: expectedName,
    version: expectedVersion,
  };
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  const [, , vsixPath, checksumPath, expectedTag] = process.argv;
  if (!vsixPath || !checksumPath || !expectedTag) {
    throw new Error(
      "Usage: verify-release-artifact.mjs " +
        "<artifact.vsix> <artifact.vsix.sha256> <tag>",
    );
  }
  const verified = await verifyReleaseArtifact({
    vsixPath,
    checksumPath,
    expectedTag,
  });
  console.log(
    `Verified ${path.basename(path.resolve(vsixPath))} ` +
      `${verified.sha256}`,
  );
}
