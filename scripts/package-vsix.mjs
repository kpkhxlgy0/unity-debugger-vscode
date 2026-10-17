// Package the current checkout into a VSIX, then verify the archive with the
// same gate the release flow uses.
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
  assertNotPublishedArtifact,
  displayPath,
  publishedArtifactPath,
  repositoryRoot,
  resolveBuildArtifact,
} from "./vsix-artifact.mjs";

const release = process.argv.includes("--release");
// Spawned through the Node on PATH, which is what npm puts in front of a
// script's PATH and what its own `node_modules/.bin` shims use.
// `process.execPath` is deliberately not used: under an Electron-hosted npm it
// is the host application, not a Node binary.
const nodeExecutable = "node";
const artifact = resolveBuildArtifact();
assertNotPublishedArtifact(artifact.path);

await fs.mkdir(path.dirname(artifact.path), { recursive: true });
packageVsix(artifact.path);
verifyVsix(artifact.path);
await report(artifact.path);

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

async function report(vsixPath) {
  const bytes = await fs.readFile(vsixPath);
  const sha256 = createHash("sha256").update(bytes).digest("hex");
  console.log(
    `Packaged ${displayPath(vsixPath)} ` +
      `(${bytes.length} bytes, sha256 ${sha256}).`,
  );
}
