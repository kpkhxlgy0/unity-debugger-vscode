import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const fixtureRoot = "tests/fixtures/TuanjieProject";

test("Tuanjie acceptance fixture is pinned and dependency-free", () => {
  assert.equal(
    fs.readFileSync(
      `${fixtureRoot}/ProjectSettings/ProjectVersion.txt`,
      "utf8",
    ).replaceAll("\r\n", "\n"),
    "m_EditorVersion: 2022.3.62t11\n",
  );
  assert.deepEqual(
    JSON.parse(
      fs.readFileSync(`${fixtureRoot}/Packages/manifest.json`, "utf8"),
    ),
    { dependencies: {} },
  );
  assert.equal(
    fs.readFileSync(
      `${fixtureRoot}/Assets/Editor/DebuggerFixture.cs.meta`,
      "utf8",
    ).replaceAll("\r\n", "\n"),
    "fileFormatVersion: 2\n" +
      "guid: 7e67fb1fef0d4c8499232096dbac8f01\n",
  );
});

test("fixture exposes stable managed stepping targets", () => {
  const source = fs.readFileSync(
    `${fixtureRoot}/Assets/Editor/DebuggerFixture.cs`,
    "utf8",
  );
  assert.match(source, /\[InitializeOnLoad\]/);
  assert.match(source, /EditorApplication\.update \+= Tick;/);
  assert.match(source, /health = Decrement\(health\);/);
  assert.match(source, /private static int Decrement\(int value\)/);
  assert.match(source, /return value - 1;/);
});

test("real-Editor verifier is portable and read-only", () => {
  const script = fs.readFileSync(
    "scripts/verify-real-editor-environment.ps1",
    "utf8",
  );
  assert.match(script, /2022\.3\.62t11/);
  assert.match(script, /DisplayVersion/);
  assert.match(script, /DisplayIcon/);
  assert.match(script, /EditorInstance\.json/);
  assert.match(script, /56_?000\s*\+/);
  assert.match(script, /127\.0\.0\.1/);
  assert.doesNotMatch(script, /H:\\workspace/i);
  assert.doesNotMatch(
    script,
    /\b(?:Set-ItemProperty|New-Item|Remove-Item|Start-Process)\b/,
  );
});
