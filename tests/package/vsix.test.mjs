import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import test from "node:test";

test("packaged VSIX satisfies the audited production contract", () => {
  const result = spawnSync(
    process.execPath,
    [
      "scripts/verify-vsix.mjs",
      "dist/unity-debugger-vscode-0.1.0.vsix",
    ],
    {
      cwd: process.cwd(),
      encoding: "utf8",
    },
  );

  assert.equal(
    result.status,
    0,
    `VSIX verifier failed:\n${result.stdout}\n${result.stderr}`,
  );
  assert.match(result.stdout, /VSIX verification passed/);
});
