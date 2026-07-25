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

test("release assemblies do not embed the changing Git revision", () => {
  for (const propsPath of [
    "Directory.Build.props",
    "adapter/vendor/Directory.Build.props",
  ]) {
    const buildProps = fs.readFileSync(propsPath, "utf8");
    assert.match(
      buildProps,
      /<IncludeSourceRevisionInInformationalVersion>false<\/IncludeSourceRevisionInInformationalVersion>/,
      `${propsPath} must produce Git-revision-independent binaries`,
    );
  }
});

test("Adapter support URL matches the reviewed Marketplace identity", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  const source = fs.readFileSync(
    "adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs",
    "utf8",
  );
  const expected =
    `https://marketplace.visualstudio.com/items?itemName=` +
    `${manifest.publisher}.${manifest.name}#support-policy`;
  const stringLiterals = [
    ...source.matchAll(/"([^"\r\n]*)"/g),
  ].map((match) => match[1]);
  assert.ok(
    stringLiterals.join("").includes(expected),
    `Adapter source must contain reviewed support URL ${expected}`,
  );
  assert.match(
    fs.readFileSync("README.md", "utf8"),
    /<a id="support-policy"><\/a>/,
  );
});
