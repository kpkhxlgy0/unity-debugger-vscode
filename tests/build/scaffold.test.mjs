import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { attachConfiguration } from "../../scripts/attach-configuration.mjs";

/**
 * The source this repository ships, walked rather than listed: build output
 * (`obj`, `bin`) and dependencies are excluded, because the claim below is about
 * what we wrote and a generated file is neither ours to fix nor present on a
 * fresh checkout.
 */
function sourceFiles(directory) {
  const files = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (
      entry.name === "obj" ||
      entry.name === "bin" ||
      entry.name === "node_modules"
    ) {
      continue;
    }
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...sourceFiles(entryPath));
    } else if (entry.isFile()) {
      files.push(entryPath);
    }
  }
  return files;
}

const shippedFiles = [
  "package.json",
  ...sourceFiles("extension/src"),
  ...sourceFiles("adapter/src"),
  // `adapter/vendor` is the fourth root of the shipped tree, and its absence
  // from this list was the defect: the vendored sources are compiled into the
  // packaged assemblies, so a telemetry identifier added to one of them passed
  // the telemetry assertion below while the test's name claimed the shipped
  // tree. The walk covers the whole tree so the three names are true of it.
  ...sourceFiles("adapter/vendor"),
];

/**
 * The one reviewed exception, keyed by file and listing the exact lines it
 * excuses — not the file itself, so that a *new* telemetry identifier under
 * `adapter/vendor`, including one added to this same file, still fails.
 *
 * The excused lines are the DAP `Message` shape: the `sendTelemetry` field and
 * the `telemetry` parameter that threads it through the constructor and
 * `SendErrorResponse`, defaulting to `false`. It is the protocol's own message
 * shape rather than a reporting path — nothing in `adapter/src` names it, so no
 * call site can set it — and no telemetry client, key or endpoint is excused by
 * this list.
 */
const reviewedAbsences = new Map([
  [
    path.join("adapter", "vendor", "vscode-mono-debug", "DebugSession.cs"),
    new Set([
      "public dynamic sendTelemetry { get; }",
      "public Message(int id, string format, dynamic variables = null, " +
        "bool user = true, bool telemetry = false)",
      "this.sendTelemetry = telemetry;",
      "public void SendErrorResponse(Response response, int id, " +
        "string format, dynamic arguments = null, bool user = true, " +
        "bool telemetry = false)",
      "var msg = new Message(id, format, arguments, user, telemetry);",
    ]),
  ],
]);

function assertAbsent(pattern, what) {
  for (const file of shippedFiles) {
    const excused = reviewedAbsences.get(file) ?? new Set();
    const text = fs
      .readFileSync(file, "utf8")
      .split("\n")
      .filter((line) => !excused.has(line.trim()))
      .join("\n");
    assert.doesNotMatch(text, pattern, `${file} ${what}`);
  }
}

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
  assert.equal(manifest.version, "0.3.2");
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
  // The attach properties and their description strings mirror the reference
  // extension's manifest verbatim. `required` is empty for the same reason:
  // VS Code supplies name/type/request itself, and the adapter validates what
  // it actually needs at attach time.
  assert.deepEqual(
    debuggerContribution.configurationAttributes.attach.required,
    [],
  );
  // `assert.deepEqual` from `node:assert/strict` is already an alias of
  // `deepStrictEqual` (Node swaps the loose methods for the strict ones in
  // strict mode), so this assertion already pinned `default: ""` — a mutation
  // to `default: 0` failed here before the spelling changed. It is spelled out
  // because the two names are indistinguishable at a glance and the strict one
  // is the one the intent is.
  assert.deepStrictEqual(
    debuggerContribution.configurationAttributes.attach.properties,
    {
      name: { type: "string" },
      type: { enum: ["unity-debugger-pure"] },
      request: { enum: ["attach"] },
      projectPath: {
        type: "string",
        default: "${workspaceFolder}",
        description: "Project Path",
      },
      logFile: {
        type: "string",
        default: "",
        description: "Path to the log file for the debugger",
      },
      endPoint: {
        type: "string",
        default: "",
        description: "ip:port",
      },
    },
  );
  // The reviewed attach configuration, shared with `scripts/verify-vsix.mjs`
  // rather than written out again: both gates assert the same fact about the
  // same object, and the two copies had already drifted apart once — this one
  // requiring three keys and the packager four. `projectPath` is part of the
  // reviewed shape, so the object below is the snippet body and the initial
  // configuration at once, on purpose.
  const expectedAttachConfiguration = attachConfiguration;
  assert.deepEqual(
    debuggerContribution.configurationSnippets[0].body,
    expectedAttachConfiguration,
  );
  assert.deepEqual(debuggerContribution.initialConfigurations, [
    expectedAttachConfiguration,
  ]);
  assert.equal(
    debuggerContribution.initialConfigurations[0].projectPath,
    "${workspaceFolder}",
  );
  assert.deepEqual(
    manifest.contributes.commands.map((command) => command.command),
    [
      "unity-debugger-pure.refreshTargets",
      "unity-debugger-pure.openLogs",
      "unity-debugger-pure.copyDiagnostics",
      "unity-debugger-pure.attachUnityDebugger",
      "unity-debugger-pure.showUnityApiReference",
    ],
  );
  assert.deepEqual(
    manifest.contributes.commands.map((command) => command.title),
    [
      "Unity Debugger Pure: Refresh Local Editors",
      "Unity Debugger Pure: Open Sanitized Diagnostics",
      "Unity Debugger Pure: Copy Sanitized Diagnostics",
      // The reference's own two titles are bare (`Attach Unity Debugger`,
      // `Unity API Reference`). Both extensions can be installed at once, and
      // two identically titled palette entries would be indistinguishable, so
      // every one of our commands carries the extension name instead.
      "Unity Debugger Pure: Attach Unity Debugger",
      "Unity Debugger Pure: Unity API Reference",
    ],
  );
  assert.ok(
    manifest.activationEvents.includes(
      "onCommand:unity-debugger-pure.attachUnityDebugger",
    ),
    "the attach command must activate the extension",
  );
  assert.ok(
    manifest.activationEvents.includes(
      "onCommand:unity-debugger-pure.showUnityApiReference",
    ),
    "the Unity API reference command must activate the extension",
  );
  // The reference activates on this event, and the deprecated-extension warning
  // runs at activation: without it, someone who installs both extensions and
  // never starts a debug session never sees the warning at all. Copied from the
  // reference's own `activationEvents` entry character-for-character rather
  // than retyped, and deliberately NOT `onStartupFinished`, which would wake us
  // in every workspace whether or not it is a Unity project. `workspaceContains`
  // is the Unity-scoped form, so no non-Unity workspace pays for the warning.
  assert.ok(
    manifest.activationEvents.includes(
      "workspaceContains:**/Packages/manifest.json",
    ),
    "opening a Unity project must activate the extension, so the " +
      "deprecated-extension warning can fire before a debug session starts",
  );
  // Pinned as an array, not only membership: a widened activation surface is
  // the cost this decision was weighed against, so an extra entry must fail
  // here and be re-argued rather than slip in unnoticed.
  assert.deepEqual(manifest.activationEvents, [
    "onDebugResolve:unity-debugger-pure",
    "onCommand:unity-debugger-pure.refreshTargets",
    "onCommand:unity-debugger-pure.openLogs",
    "onCommand:unity-debugger-pure.copyDiagnostics",
    "onCommand:unity-debugger-pure.attachUnityDebugger",
    "onCommand:unity-debugger-pure.showUnityApiReference",
    "workspaceContains:**/Packages/manifest.json",
  ]);
  // The reference declares `vstuc.displayBackgroundProcesses` as a boolean
  // defaulting to false, with this description verbatim. Our id follows our own
  // extension name for the same reason the reference's follows its own.
  assert.deepEqual(
    manifest.contributes.configuration?.properties?.[
      "unity-debugger-pure.displayBackgroundProcesses"
    ],
    {
      type: "boolean",
      default: false,
      description:
        "Display background processes created by the main Unity process",
    },
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

// The three invariants the goal names are all *negative* claims — the absence
// of a dependency — which is the kind no code path can notice failing and no
// intention preserves. `extensionDependencies` is already pinned by the manifest
// test above; these are its two siblings and the source-tree halves.
test("the manifest declares no extension pack and no runtime dependencies", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));

  // The shape is pinned rather than membership: an `extensionPack` entry is a
  // second extension installed alongside this one, and it would pass any check
  // that only looked for a forbidden name inside an array we had already
  // accepted as present. Absent means `undefined`, and `dependencies` means
  // absent or empty — not merely small.
  assert.equal(manifest.extensionPack, undefined);
  assert.deepStrictEqual(manifest.dependencies ?? {}, {});
});

test("the shipped tree depends on no C# Dev Kit integration", () => {
  // This is the invariant the extension exists to keep: it must work with the
  // Microsoft C# extensions absent, so no shipped file may name one — not as a
  // dependency, not as an extension pack, and not as a string a future
  // contributor could turn into one.
  assertAbsent(/ms-dotnettools|csdevkit/i, "names a Microsoft C# Dev Kit id");
});

test("the shipped tree requires no Unity-side package", () => {
  // The reference requires a `com.unity.ide.*` package in the project. Ours
  // must not: requiring one would reintroduce exactly the dependency this
  // extension removes, and it is the kind of requirement that appears as a
  // string in a preflight check before it appears as a manifest entry.
  assertAbsent(/com\.unity\.ide\b/i, "requires a Unity IDE package");
});

test("the shipped tree carries no telemetry identifier", () => {
  // The reference initializes `@vscode/extension-telemetry` with an `aiKey`.
  // Our invariant is that nothing here reports anything anywhere, so the
  // identifier must not appear even in a comment or a disabled call: the
  // package, the constant and the key name are all this pattern.
  //
  // The vendored DAP `Message` shape is the one exception, and it is excused by
  // line rather than by file — see `reviewedAbsences`. This test's name claims
  // the shipped tree, so the walk covers `adapter/vendor` too: before the final
  // review's F2 it stopped at `adapter/src`, and adding a telemetry identifier
  // to a vendored file left it green.
  assertAbsent(
    /telemetry|appinsights|applicationinsights/i,
    "names a telemetry dependency",
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
