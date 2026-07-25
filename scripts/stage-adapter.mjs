import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const adapterDirectory = path.join(repositoryRoot, "adapter");
const stagingDirectory = path.resolve(
  adapterDirectory,
  "win32-x64",
);
const releaseDirectory = path.resolve(
  adapterDirectory,
  "src",
  "UnityDebugger.Adapter",
  "bin",
  "Release",
  "net48",
);

if (
  path.dirname(stagingDirectory) !== adapterDirectory ||
  path.basename(stagingDirectory) !== "win32-x64"
) {
  throw new Error("Adapter staging path failed its safety check.");
}

const sourceEntries = await fs.readdir(releaseDirectory, {
  withFileTypes: true,
});
const selected = sourceEntries
  .filter((entry) => entry.isFile())
  .map((entry) => entry.name)
  .filter(
    (name) =>
      name.endsWith(".dll") ||
      name.endsWith(".exe") ||
      name.endsWith(".config"),
  )
  .sort();

if (!selected.includes("UnityCommunityDebug.exe")) {
  throw new Error("Release output has no UnityCommunityDebug.exe.");
}
for (const name of selected) {
  if (/(?:test|xunit|testhost)/i.test(name)) {
    throw new Error(`Refusing to stage test binary: ${name}`);
  }
}

await fs.rm(stagingDirectory, { recursive: true, force: true });
await fs.mkdir(stagingDirectory, { recursive: true });
for (const name of selected) {
  await fs.copyFile(
    path.join(releaseDirectory, name),
    path.join(stagingDirectory, name),
  );
}

console.log(
  `Staged ${selected.length} production Adapter files in ` +
    "adapter/win32-x64.",
);
