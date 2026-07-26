import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

test("the packaged extension entry loads in the VS Code CommonJS host", () => {
  execFileSync(process.execPath, ["esbuild.mjs"], {
    cwd: process.cwd(),
    stdio: "pipe",
  });

  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  const entryPath = path.resolve(manifest.main);
  const fixtureRoot = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-extension-entry-"),
  );
  const vscodeModuleRoot = path.join(
    fixtureRoot,
    "node_modules",
    "vscode",
  );
  fs.mkdirSync(vscodeModuleRoot, { recursive: true });
  fs.writeFileSync(
    path.join(vscodeModuleRoot, "index.js"),
    "module.exports = {};",
  );
  const loader = String.raw`
    const extension = require(process.argv[1]);
    if (typeof extension.activate !== "function") {
      throw new Error("Extension entry does not export activate()");
    }
  `;
  try {
    const result = spawnSync(
      process.execPath,
      ["--input-type=commonjs", "-e", loader, entryPath],
      {
        cwd: process.cwd(),
        encoding: "utf8",
        env: {
          ...process.env,
          NODE_PATH: path.join(fixtureRoot, "node_modules"),
        },
      },
    );

    assert.equal(
      result.status,
      0,
      `Extension entry failed to load:\n${result.stderr}`,
    );
  } finally {
    fs.rmSync(fixtureRoot, { force: true, recursive: true });
  }
});
