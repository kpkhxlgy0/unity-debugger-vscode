import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describeInventoryDifferences } from "./runtime-inventory.mjs";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const stagingDirectory = path.join(
  repositoryRoot,
  "adapter",
  "win32-x64",
);
const inventoryPath = path.join(
  repositoryRoot,
  "third-party",
  "runtime-assemblies.json",
);
const write = process.argv.includes("--write");
const verify = process.argv.includes("--verify");
if (write === verify) {
  throw new Error("Use exactly one of --write or --verify.");
}

const sourceOrigins = new Map([
  ["UnityDebuggerPure.exe", ["project", "MIT"]],
  ["VSCodeDebug.dll", ["source-built:vscode-mono-debug", "MIT"]],
  ["Mono.Debugger.Soft.dll", ["source-built:debugger-libs", "MIT"]],
]);
const packageAssemblies = new Map([
  ["Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.Common"],
  [
    "Microsoft.CodeAnalysis.CSharp.dll",
    "Microsoft.CodeAnalysis.CSharp",
  ],
  ["Mono.Cecil.dll", "Mono.Cecil"],
  ["Mono.Cecil.Mdb.dll", "Mono.Cecil"],
  ["Mono.Cecil.Pdb.dll", "Mono.Cecil"],
  ["Mono.Cecil.Rocks.dll", "Mono.Cecil"],
  ["Newtonsoft.Json.dll", "Newtonsoft.Json"],
  ["System.Buffers.dll", "System.Buffers"],
  ["System.Collections.Immutable.dll", "System.Collections.Immutable"],
  ["System.Memory.dll", "System.Memory"],
  ["System.Numerics.Vectors.dll", "System.Numerics.Vectors"],
  ["System.Reflection.Metadata.dll", "System.Reflection.Metadata"],
  [
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Runtime.CompilerServices.Unsafe",
  ],
  ["System.Text.Encoding.CodePages.dll", "System.Text.Encoding.CodePages"],
  [
    "System.Threading.Tasks.Extensions.dll",
    "System.Threading.Tasks.Extensions",
  ],
]);
const packageVersions = await readPackageVersions(
  path.join(
    repositoryRoot,
    "adapter",
    "src",
    "UnityDebugger.Adapter",
  ),
);

const names = (await fs.readdir(stagingDirectory))
  .filter(
    (name) => name.endsWith(".dll") || name.endsWith(".exe"),
  )
  .sort((left, right) => left.localeCompare(right));
const assemblies = [];
for (const name of names) {
  let origin;
  let license;
  const source = sourceOrigins.get(name);
  if (source) {
    [origin, license] = source;
  } else {
    const packageId = packageAssemblies.get(name);
    const resolved = packageId
      ? packageVersions.get(packageId.toLowerCase())
      : undefined;
    if (!packageId || !resolved) {
      throw new Error(`No audited runtime origin for ${name}.`);
    }
    origin = `nuget:${resolved.id}/${resolved.version}`;
    license = "MIT";
  }
  const bytes = await fs.readFile(path.join(stagingDirectory, name));
  assemblies.push({
    path: `extension/adapter/win32-x64/${name}`,
    sha256: createHash("sha256").update(bytes).digest("hex"),
    origin,
    license,
  });
}

const inventory = {
  version: 1,
  generatedFrom: "adapter/win32-x64",
  assemblies,
};
const serialized = `${JSON.stringify(inventory, null, 2)}\n`;
if (write) {
  await fs.writeFile(inventoryPath, serialized, "utf8");
  console.log(`Wrote ${assemblies.length} runtime inventory entries.`);
} else {
  const committed = await fs.readFile(inventoryPath, "utf8");
  if (committed.replaceAll("\r\n", "\n") !== serialized) {
    const differences = describeInventoryDifferences(
      JSON.parse(committed),
      inventory,
    );
    const details =
      differences.length > 0
        ? `\n${differences.join("\n")}`
        : "\nAssembly metadata or ordering differs.";
    throw new Error(
      "Runtime inventory differs from staged binaries. " +
        "Review and regenerate it explicitly with --write." +
        details,
    );
  }
  console.log(`Verified ${assemblies.length} runtime inventory entries.`);
}

async function readPackageVersions(root) {
  const versions = new Map();
  for (const file of await findLockFiles(root)) {
    const lock = JSON.parse(await fs.readFile(file, "utf8"));
    for (const dependencies of Object.values(lock.dependencies ?? {})) {
      for (const [id, details] of Object.entries(dependencies)) {
        if (details.type === "Project" || !details.resolved) {
          continue;
        }
        const key = id.toLowerCase();
        const existing = versions.get(key);
        if (existing && existing.version !== details.resolved) {
          throw new Error(
            `Conflicting locked versions for ${id}: ` +
              `${existing.version} and ${details.resolved}.`,
          );
        }
        versions.set(key, { id, version: details.resolved });
      }
    }
  }
  return versions;
}

async function findLockFiles(directory) {
  const result = [];
  for (const entry of await fs.readdir(directory, {
    withFileTypes: true,
  })) {
    if (
      entry.isDirectory() &&
      ["bin", "obj", "win32-x64"].includes(entry.name)
    ) {
      continue;
    }
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      result.push(...(await findLockFiles(fullPath)));
    } else if (entry.name === "packages.lock.json") {
      result.push(fullPath);
    }
  }
  return result;
}
