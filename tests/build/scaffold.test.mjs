import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

test("manifest defines an independent attach-only debugger", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  assert.equal(manifest.publisher, "kpk");
  assert.equal(manifest.name, "unity-debugger-pure");
  assert.equal(manifest.displayName, "Unity Debugger Pure");
  assert.equal(
    manifest.description,
    "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, " +
      "without C# Dev Kit or Microsoft's Unity extension.",
  );
  assert.equal(manifest.version, "0.3.0");
  assert.equal(manifest.engines.vscode, "^1.95.0");
  assert.equal(manifest.icon, "images/icon.png");

  const icon = fs.readFileSync(manifest.icon);
  assert.deepEqual(
    [...icon.subarray(0, 8)],
    [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
  );
  assert.equal(icon.readUInt32BE(16), 512);
  assert.equal(icon.readUInt32BE(20), 512);
  assert.deepEqual(manifest.repository, {
    type: "git",
    url: "https://github.com/kpkhxlgy0/unity-debugger-vscode.git",
  });
  assert.deepEqual(manifest.bugs, {
    url: "https://github.com/kpkhxlgy0/unity-debugger-vscode/issues",
  });
  assert.equal(
    manifest.homepage,
    "https://github.com/kpkhxlgy0/unity-debugger-vscode#readme",
  );
  assert.equal(manifest.extensionDependencies, undefined);
  assert.deepEqual(manifest.extensionKind, ["workspace"]);
  assert.deepEqual(manifest.os, ["win32"]);

  const debuggerContribution = manifest.contributes.debuggers.find(
    (entry) => entry.type === "unity-debugger-pure",
  );
  assert.ok(debuggerContribution);
  assert.ok(debuggerContribution.configurationAttributes.attach);
  assert.equal(
    debuggerContribution.configurationAttributes.launch,
    undefined,
  );
  const expectedAttachConfiguration = {
    name: "Attach to Unity Debugger Pure",
    type: "unity-debugger-pure",
    request: "attach",
  };
  assert.deepEqual(
    debuggerContribution.configurationSnippets[0].body,
    expectedAttachConfiguration,
  );
  assert.deepEqual(debuggerContribution.initialConfigurations, [
    expectedAttachConfiguration,
  ]);
  assert.deepEqual(
    manifest.contributes.commands.map((command) => command.command),
    [
      "unity-debugger-pure.refreshTargets",
      "unity-debugger-pure.openLogs",
      "unity-debugger-pure.copyDiagnostics",
    ],
  );
  assert.deepEqual(
    manifest.contributes.commands.map((command) => command.title),
    [
      "Unity Debugger Pure: Refresh Local Editors",
      "Unity Debugger Pure: Open Sanitized Diagnostics",
      "Unity Debugger Pure: Copy Sanitized Diagnostics",
    ],
  );

  assert.equal(
    manifest.contributes.configuration?.properties?.[
      "unityDebuggerPure.enableImplicitEvaluation"
    ],
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

test("packaging excludes nested repository worktrees", () => {
  const ignore = fs.readFileSync(".vscodeignore", "utf8");
  assert.match(ignore, /^\.worktrees\/\*\*$/m);
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
    assert.match(
      buildProps,
      /<EnableSourceLink>false<\/EnableSourceLink>/,
      `${propsPath} must not embed the current commit in Source Link`,
    );
  }
});

test("repository checkout preserves audited C# source bytes", () => {
  const attributes = fs.readFileSync(".gitattributes", "utf8");
  assert.match(attributes, /^\*\.cs -text$/m);
  assert.match(attributes, /^\*\.bat text eol=crlf$/m);
  assert.match(attributes, /^\*\.cmd text eol=crlf$/m);
});

test("vendored builds map shared sources outside each project directory", () => {
  const buildProps = fs.readFileSync(
    "adapter/vendor/Directory.Build.props",
    "utf8",
  );
  assert.match(
    buildProps,
    /\$\(MSBuildThisFileDirectory\)=\/_\/vendor\//,
  );
});

test("release builds omit unpublished adapter symbols", () => {
  for (const targetsPath of [
    "Directory.Build.targets",
    "adapter/vendor/Directory.Build.targets",
  ]) {
    const buildTargets = fs.readFileSync(targetsPath, "utf8");
    assert.match(
      buildTargets,
      /<PropertyGroup Condition="'\$\(Configuration\)' == 'Release'">/,
    );
    assert.match(buildTargets, /<DebugType>none<\/DebugType>/);
    assert.match(
      buildTargets,
      /<DebugSymbols>false<\/DebugSymbols>/,
    );
  }
});
