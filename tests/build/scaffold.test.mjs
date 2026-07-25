import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

test("manifest defines an independent attach-only debugger", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  assert.equal(manifest.name, "unity-debugger-vscode");
  assert.equal(
    manifest.displayName,
    "Community Debugger for Unity 2022 & Tuanjie",
  );
  assert.equal(manifest.version, "0.1.0");
  assert.equal(manifest.extensionDependencies, undefined);
  assert.deepEqual(manifest.extensionKind, ["workspace"]);
  assert.deepEqual(manifest.os, ["win32"]);

  const debuggerContribution = manifest.contributes.debuggers.find(
    (entry) => entry.type === "unity-community",
  );
  assert.ok(debuggerContribution);
  assert.ok(debuggerContribution.configurationAttributes.attach);
  assert.equal(
    debuggerContribution.configurationAttributes.launch,
    undefined,
  );

  assert.match(
    manifest.scripts["build:adapter"],
    /dotnet restore UnityDebugger\.sln --locked-mode/,
  );
  assert.match(
    manifest.scripts["build:adapter"],
    /dotnet build UnityDebugger\.sln -c Release --no-restore/,
  );
  assert.doesNotMatch(
    manifest.scripts["build:adapter"],
    /dotnet build[^\n]*--locked-mode/,
  );
});

test("adapter project targets net48 x64", () => {
  const project = fs.readFileSync(
    "adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj",
    "utf8",
  );
  assert.match(project, /<TargetFramework>net48<\/TargetFramework>/);
  assert.match(project, /<PlatformTarget>x64<\/PlatformTarget>/);
});
