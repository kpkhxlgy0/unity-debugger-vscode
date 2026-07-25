import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import test from "node:test";

const expected = new Map([
  ["vscode-mono-debug", "d233b366b0c67ae4d61488f7e974e2a5b9da2e3b"],
  ["debugger-libs", "cd005e941d18c92ddf0c50084c59ddaae6bf4c5d"],
  ["nrefactory", "0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5"],
]);

test("every vendored source has exact provenance and a retained notice", () => {
  const manifest = JSON.parse(
    fs.readFileSync("third-party/sources.json", "utf8"),
  );
  assert.equal(manifest.sources.length, expected.size);

  for (const source of manifest.sources) {
    assert.equal(source.revision, expected.get(source.name));
    assert.equal(source.license, "MIT");
    assert.ok(fs.existsSync(source.notice), source.notice);
    assert.ok(source.paths.length > 0);
    source.paths.forEach((sourcePath) => {
      assert.ok(fs.existsSync(sourcePath), sourcePath);
    });
    assert.ok(source.modifications.length > 0);
  }
});

test("VSCodeDebug imports only the audited protocol files and local helpers", () => {
  const directory = "adapter/vendor/vscode-mono-debug";
  const actual = fs
    .readdirSync(directory)
    .filter((file) => path.extname(file) === ".cs")
    .sort();

  assert.deepEqual(actual, [
    "DebugSession.cs",
    "Protocol.cs",
    "ProtocolTrace.cs",
    "ProtocolUtilities.cs",
  ]);
});

test("retained MIT licenses are byte-identical to the pinned sources", () => {
  const copies = [
    [
      "adapter/vendor/vscode-mono-debug/LICENSE.txt",
      "third-party/licenses/vscode-mono-debug-MIT.txt",
    ],
    [
      "adapter/vendor/debugger-libs/LICENSE",
      "third-party/licenses/debugger-libs-MIT.txt",
    ],
    [
      "adapter/vendor/nrefactory/license.txt",
      "third-party/licenses/nrefactory-MIT.txt",
    ],
  ];

  for (const [source, retained] of copies) {
    assert.deepEqual(
      fs.readFileSync(retained),
      fs.readFileSync(source),
      `${retained} differs from ${source}`,
    );
  }
});

test("third-party verifier accepts the audited source tree", () => {
  assert.doesNotThrow(() => {
    execFileSync(process.execPath, ["scripts/verify-third-party.mjs"], {
      encoding: "utf8",
      stdio: "pipe",
    });
  });
});
