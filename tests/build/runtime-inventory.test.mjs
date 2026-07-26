import assert from "node:assert/strict";
import test from "node:test";
import { describeInventoryDifferences } from "../../scripts/runtime-inventory.mjs";

test("runtime inventory differences include file names and both hashes", () => {
  const committed = {
    assemblies: [
      {
        path: "extension/adapter/win32-x64/Changed.dll",
        sha256: "expected-hash",
      },
      {
        path: "extension/adapter/win32-x64/Missing.dll",
        sha256: "missing-hash",
      },
    ],
  };
  const generated = {
    assemblies: [
      {
        path: "extension/adapter/win32-x64/Changed.dll",
        sha256: "actual-hash",
      },
      {
        path: "extension/adapter/win32-x64/Added.dll",
        sha256: "added-hash",
      },
    ],
  };

  assert.deepEqual(describeInventoryDifferences(committed, generated), [
    "extension/adapter/win32-x64/Added.dll: expected <absent>, actual added-hash",
    "extension/adapter/win32-x64/Changed.dll: expected expected-hash, actual actual-hash",
    "extension/adapter/win32-x64/Missing.dll: expected missing-hash, actual <absent>",
  ]);
});
