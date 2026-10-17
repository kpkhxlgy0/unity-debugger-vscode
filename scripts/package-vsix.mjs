// Package the current checkout into a VSIX, verify the archive with the same
// gate the release flow uses, and run `tests/package/vsix.test.mjs` against the
// artifact that was just written.
//
// Default output is build-stamped (`unity-debugger-pure-0.3.2+g<commit>.vsix`),
// so this command cannot destroy a retained release artifact. `--release` also
// writes the version-only release name, byte-identical, and is meant for the
// tagged release workflow: .github/workflows/release.yml names that path
// explicitly and uploads it.
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import {
  artifactEnvironmentVariable,
  assertNotPublishedArtifact,
  displayPath,
  publishedArtifactPath,
  repositoryRoot,
  resolveBuildArtifact,
} from "./vsix-artifact.mjs";

const release = process.argv.includes("--release");
// Spawned as `node`: the interpreter npm puts in front of a script's PATH, and
// the one its own `node_modules/.bin` shims call. `process.execPath` is avoided
// so nothing here depends on the host executable being a Node binary — an
// Electron-hosted npm reports the host application — or on its
// ELECTRON_RUN_AS_NODE mode being inherited.
const nodeExecutable = "node";
const artifact = resolveBuildArtifact();
assertNotPublishedArtifact(artifact.path);

// The archive is published by moving it into place, never by writing it there:
// vsce spends seconds writing the ZIP, and a reader that arrives in that window
// — a second packaging run is enough — would see a truncated archive and report
// every archive check as broken.
const pendingPath = `${artifact.path}.pending-${process.pid}`;
await fs.mkdir(path.dirname(artifact.path), { recursive: true });
try {
  packageVsix(pendingPath);
  await fs.rename(pendingPath, artifact.path);
} finally {
  await fs.rm(pendingPath, { force: true });
}

verifyVsix(artifact.path);
await report(artifact.path);
runArchiveTests(artifact.path);

if (release) {
  // The same bytes under the version-only name, so the published artifact is
  // exactly the build this checkout produced.
  await fs.copyFile(artifact.path, publishedArtifactPath);
  verifyVsix(publishedArtifactPath);
  await report(publishedArtifactPath);
}

function packageVsix(outputPath) {
  const cli = path.join(
    repositoryRoot,
    "node_modules",
    "@vscode",
    "vsce",
    "vsce",
  );
  execFileSync(nodeExecutable, [cli, "package", "--out", outputPath], {
    cwd: repositoryRoot,
    stdio: "inherit",
  });
}

function verifyVsix(vsixPath) {
  execFileSync(
    nodeExecutable,
    [path.join(repositoryRoot, "scripts", "verify-vsix.mjs"), vsixPath],
    { cwd: repositoryRoot, stdio: "inherit" },
  );
}

// The suite is told which artifact to read instead of resolving one again: the
// build stamp comes from the commit, so a commit landing between packaging and
// testing would otherwise send it after a file that does not exist.
function runArchiveTests(vsixPath) {
  execFileSync(
    nodeExecutable,
    [
      "--test",
      path.join(repositoryRoot, "tests", "package", "vsix.test.mjs"),
    ],
    {
      cwd: repositoryRoot,
      stdio: "inherit",
      env: { ...process.env, [artifactEnvironmentVariable]: vsixPath },
    },
  );
}

async function report(vsixPath) {
  const bytes = await fs.readFile(vsixPath);
  const sha256 = createHash("sha256").update(bytes).digest("hex");
  console.log(
    `Packaged ${displayPath(vsixPath)} ` +
      `(${bytes.length} bytes, sha256 ${sha256}).`,
  );
}
