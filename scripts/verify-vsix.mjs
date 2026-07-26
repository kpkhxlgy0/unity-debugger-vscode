import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import AdmZip from "adm-zip";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const requested = process.argv[2];
if (!requested) {
  throw new Error("Usage: verify-vsix.mjs <artifact.vsix>");
}
const vsixPath = path.resolve(repositoryRoot, requested);
const archive = new AdmZip(vsixPath);
const files = new Map();
for (const entry of archive.getEntries()) {
  const normalized = entry.entryName.replaceAll("\\", "/");
  if (
    normalized.startsWith("/") ||
    /^[A-Za-z]:/.test(normalized) ||
    normalized.split("/").includes("..")
  ) {
    throw new Error(`Unsafe ZIP path: ${entry.entryName}`);
  }
  if (entry.isDirectory) {
    continue;
  }
  const key = normalized.toLowerCase();
  if (files.has(key)) {
    throw new Error(`Duplicate ZIP path: ${normalized}`);
  }
  if (
    !normalized.startsWith("extension/") &&
    normalized !== "[Content_Types].xml" &&
    normalized !== "extension.vsixmanifest"
  ) {
    throw new Error(`File is outside the allowlisted VSIX root: ${normalized}`);
  }
  if (
    /(?:^|\/)(?:tests|\.git|src)(?:\/|$)/i.test(normalized) ||
    /adapter\/vendor\//i.test(normalized) ||
    /testadapter/i.test(normalized)
  ) {
    throw new Error(`Forbidden packaged path: ${normalized}`);
  }
  files.set(key, {
    path: normalized,
    bytes: entry.getData(),
  });
}

const required = [
  "extension/dist/extension.cjs",
  "extension/adapter/win32-x64/UnityCommunityDebug.exe",
  "extension/LICENSE.txt",
  "extension/README.md",
  "extension/CHANGELOG.md",
  "extension/SECURITY.md",
  "extension/THIRD_PARTY_NOTICES.md",
  "extension/third-party/runtime-assemblies.json",
  "extension/package.json",
];
for (const requiredPath of required) {
  if (!files.has(requiredPath.toLowerCase())) {
    throw new Error(`Required VSIX file is missing: ${requiredPath}`);
  }
}

for (const item of files.values()) {
  const lowerPath = item.path.toLowerCase();
  if (
    lowerPath.startsWith("extension/adapter/") &&
    !lowerPath.startsWith("extension/adapter/win32-x64/")
  ) {
    throw new Error(`Unexpected Adapter subtree: ${item.path}`);
  }
  if (!isAllowedPackagedPath(lowerPath)) {
    throw new Error(`Unexpected production package file: ${item.path}`);
  }
}

const manifest = JSON.parse(
  files.get("extension/package.json").bytes.toString("utf8"),
);
if (manifest.extensionDependencies !== undefined) {
  throw new Error("Packaged manifest must not have extensionDependencies.");
}

const committedInventory = await fs.readFile(
  path.join(repositoryRoot, "third-party", "runtime-assemblies.json"),
  "utf8",
);
const packagedInventory = files
  .get("extension/third-party/runtime-assemblies.json")
  .bytes.toString("utf8");
if (
  packagedInventory.replaceAll("\r\n", "\n") !==
  committedInventory.replaceAll("\r\n", "\n")
) {
  throw new Error("Packaged runtime inventory differs from the reviewed file.");
}
const inventory = JSON.parse(
  packagedInventory,
);
const inventoryByPath = new Map(
  inventory.assemblies.map((item) => [
    item.path.toLowerCase(),
    item,
  ]),
);
const packagedAssemblies = [...files.values()].filter((item) =>
  /\.(?:dll|exe)$/i.test(item.path),
);
for (const assembly of packagedAssemblies) {
  const recorded = inventoryByPath.get(assembly.path.toLowerCase());
  if (!recorded) {
    throw new Error(
      `Packaged runtime assembly is not inventoried: ${assembly.path}`,
    );
  }
  const digest = createHash("sha256")
    .update(assembly.bytes)
    .digest("hex");
  if (digest !== recorded.sha256) {
    throw new Error(`Runtime hash mismatch: ${assembly.path}`);
  }
}
if (packagedAssemblies.length !== inventoryByPath.size) {
  throw new Error(
    "Runtime inventory contains assemblies absent from the VSIX.",
  );
}

const executable = files.get(
  "extension/adapter/win32-x64/unitycommunitydebug.exe",
).bytes;
verifyAmd64Pe(executable);

const artifact = await fs.readFile(vsixPath);
const artifactHash = createHash("sha256")
  .update(artifact)
  .digest("hex");
await fs.writeFile(
  `${vsixPath}.sha256`,
  `${artifactHash}  ${path.basename(vsixPath)}\n`,
  "utf8",
);
console.log(
  `VSIX verification passed: ${files.size} files, ` +
    `${packagedAssemblies.length} runtime assemblies.`,
);

function verifyAmd64Pe(bytes) {
  if (bytes.length < 0x40 || bytes.readUInt16LE(0) !== 0x5a4d) {
    throw new Error("Adapter executable has no DOS PE header.");
  }
  const peOffset = bytes.readUInt32LE(0x3c);
  if (
    peOffset + 6 > bytes.length ||
    bytes.toString("ascii", peOffset, peOffset + 4) !== "PE\0\0"
  ) {
    throw new Error("Adapter executable has no valid PE signature.");
  }
  if (bytes.readUInt16LE(peOffset + 4) !== 0x8664) {
    throw new Error("Adapter executable is not AMD64.");
  }
}

function isAllowedPackagedPath(filePath) {
  const exactFiles = new Set([
    "extension/license.txt",
    "extension/readme.md",
    "extension/changelog.md",
    "extension/security.md",
    "extension/third_party_notices.md",
    "extension/package.json",
    "extension/dist/extension.cjs",
    "extension/third-party/runtime-assemblies.json",
    "extension/third-party/licenses/vscode-mono-debug-mit.txt",
    "extension/third-party/licenses/debugger-libs-mit.txt",
    "extension/third-party/licenses/nrefactory-mit.txt",
    "[content_types].xml",
    "extension.vsixmanifest",
  ]);
  if (exactFiles.has(filePath)) {
    return true;
  }
  return (
    filePath.startsWith("extension/adapter/win32-x64/") &&
    /\.(?:dll|exe|config)$/i.test(filePath)
  );
}
