import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const repositoryRoot = path.resolve(import.meta.dirname, "../..");

test("source repository exposes a debugger-only manifest and public API", () => {
  const manifest = JSON.parse(
    fs.readFileSync(path.join(repositoryRoot, "package.json"), "utf8"),
  );

  assert.equal(manifest.name, "unity-debugger-pure");
  assert.equal(manifest.version, "0.4.0");
  assert.equal(manifest.workspaces, undefined);
  assert.equal(
    fs.existsSync(path.join(repositoryRoot, "extension/src/publicApi.ts")),
    true,
  );
});
