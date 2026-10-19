// Where a packaged VSIX goes, and what it is called.
//
// A released artifact and a working-tree build must never share a path. The
// default name identifies the build —
// `unity-debugger-pure-0.3.3+g<commit>.vsix`, the same stamp
// `adapter/win32-x64/build-info.json` carries — so an ordinary
// `npm run package` cannot overwrite a retained release. The version-only
// release name is reached only when it is named explicitly, by
// `scripts/package-vsix.mjs --release` from `npm run package:release`.
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
// Pinned the way `scripts/stage-adapter.mjs` and
// `scripts/verify-release-artifact.mjs` pin it.
export const version = "0.3.3";
export const distributionDirectory = path.join(repositoryRoot, "dist");
export const publishedArtifactName = `unity-debugger-pure-${version}.vsix`;
export const publishedArtifactPath = path.join(
  distributionDirectory,
  publishedArtifactName,
);

export function currentCommit() {
  const commit = execFileSync("git", ["rev-parse", "HEAD"], {
    cwd: repositoryRoot,
    encoding: "utf8",
    windowsHide: true,
  }).trim();
  if (!/^[0-9a-f]{40}$/.test(commit)) {
    throw new Error(
      "Git HEAD is not a 40-character lowercase commit ID.",
    );
  }
  return commit;
}

export function buildArtifactName(commit) {
  return `unity-debugger-pure-${version}+g${commit.slice(0, 12)}.vsix`;
}

// Repository-relative when the artifact is inside the checkout, absolute
// otherwise. Used by the messages that have to say which archive they mean.
export function displayPath(absolutePath) {
  const relative = path.relative(repositoryRoot, absolutePath);
  return relative.startsWith("..") ? absolutePath : relative;
}

// The artifact the current checkout builds: what `npm run package` writes,
// what `npm run verify:vsix` checks when it is given no argument, and what
// `tests/package/vsix.test.mjs` reads.
export function resolveBuildArtifact(commit = currentCommit()) {
  const name = buildArtifactName(commit);
  return {
    name,
    path: path.join(distributionDirectory, name),
    commit,
  };
}

// Set by `scripts/package-vsix.mjs` for the archive suite it runs itself, and
// usable by hand: `UDP_VSIX_ARTIFACT=<path> npm run verify:vsix` checks one
// named archive. The name is resolved once, where the artifact is written, so a
// commit landing mid-run cannot make the next step look for another commit's
// file.
export const artifactEnvironmentVariable = "UDP_VSIX_ARTIFACT";

export function resolveArtifactPath() {
  const override = process.env[artifactEnvironmentVariable];
  return override
    ? path.resolve(override)
    : resolveBuildArtifact().path;
}

// The property this module exists for: the ordinary build path must not be
// able to reach the retained release artifact.
export function assertNotPublishedArtifact(artifactPath) {
  if (path.basename(artifactPath) === publishedArtifactName) {
    throw new Error(
      `Refusing to write the retained release artifact ` +
        `${publishedArtifactName}. Name the release explicitly with ` +
        `npm run package:release, or build to a build-stamped path.`,
    );
  }
}

// Build-stamped artifacts already on disk, for the "which artifact?" hint
// `verify-vsix.mjs` prints when the current checkout has none.
export function existingBuildArtifacts() {
  if (!fs.existsSync(distributionDirectory)) {
    return [];
  }
  return fs
    .readdirSync(distributionDirectory)
    .filter((name) => /\+g[0-9a-f]{12}\.vsix$/.test(name))
    .sort();
}
