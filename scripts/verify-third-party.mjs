import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const expectedSources = new Map([
  ["vscode-mono-debug", "d233b366b0c67ae4d61488f7e974e2a5b9da2e3b"],
  ["debugger-libs", "cd005e941d18c92ddf0c50084c59ddaae6bf4c5d"],
  ["nrefactory", "0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5"],
]);

const manifest = JSON.parse(
  fs.readFileSync("third-party/sources.json", "utf8"),
);
const notices = fs.readFileSync("THIRD_PARTY_NOTICES.md", "utf8");

assert.equal(
  manifest.sources.length,
  expectedSources.size,
  "third-party source count changed without updating the verifier",
);

for (const source of manifest.sources) {
  const expectedRevision = expectedSources.get(source.name);
  assert.ok(expectedRevision, `unexpected third-party source: ${source.name}`);
  assert.match(
    source.revision,
    /^[0-9a-f]{40}$/,
    `${source.name} must be pinned to a full lowercase Git revision`,
  );
  assert.equal(
    source.revision,
    expectedRevision,
    `${source.name} revision changed without an explicit audit`,
  );
  assert.equal(source.license, "MIT", `${source.name} must remain MIT licensed`);
  assert.ok(fs.existsSync(source.notice), `missing license: ${source.notice}`);
  assert.ok(
    notices.includes(source.name),
    `THIRD_PARTY_NOTICES.md does not mention ${source.name}`,
  );

  for (const sourcePath of source.paths) {
    assert.ok(fs.existsSync(sourcePath), `missing vendored path: ${sourcePath}`);
  }
}

for (const [source, retained] of [
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
]) {
  assert.deepEqual(
    fs.readFileSync(retained),
    fs.readFileSync(source),
    `${retained} differs from ${source}`,
  );
}

const protocolDirectory = "adapter/vendor/vscode-mono-debug";
const protocolSources = fs
  .readdirSync(protocolDirectory)
  .filter((file) => path.extname(file) === ".cs")
  .sort();

assert.deepEqual(
  protocolSources,
  [
    "DebugSession.cs",
    "Protocol.cs",
    "ProtocolTrace.cs",
    "ProtocolUtilities.cs",
  ],
  "VSCodeDebug source allowlist changed without an explicit audit",
);

const protocol = fs.readFileSync(
  path.join(protocolDirectory, "Protocol.cs"),
  "utf8",
);
const debugSession = fs.readFileSync(
  path.join(protocolDirectory, "DebugSession.cs"),
  "utf8",
);

for (const forbidden of [
  "SerializeObject(request.arguments)",
  "SerializeObject(e.body)",
  "SerializeObject(message,",
  "Program.Log",
]) {
  assert.ok(
    !protocol.includes(forbidden),
    `Protocol.cs contains forbidden payload logging: ${forbidden}`,
  );
}

assert.ok(
  protocol.includes("ProtocolTrace.CommandReceived("),
  "Protocol.cs must retain command-name-only tracing",
);
assert.ok(
  !protocol.includes("SendMessage(response)"),
  "Protocol.Dispatch must not emit a duplicate outer response",
);
assert.ok(
  !debugSession.includes("e.StackTrace"),
  "DebugSession.cs must not expose exception stack traces",
);
assert.ok(
  !debugSession.includes("Malformed launch configuration: '{0}'"),
  "DebugSession.cs must not expose a raw launch configuration path",
);
assert.ok(
  !debugSession.includes("path not well formed: '{0}'"),
  "DebugSession.cs must not expose a malformed client path",
);
assert.ok(
  !debugSession.includes("e.Message"),
  "DebugSession.cs must not expose raw exception messages",
);
assert.ok(
  debugSession.includes("e.GetType().Name"),
  "DebugSession.cs must report only the exception type",
);
assert.ok(
  debugSession.includes("ProtocolUtilities.ExpandVariables"),
  "DebugSession.cs must use the project-owned variable formatter",
);

const debuggerSession = fs.readFileSync(
  "adapter/vendor/debugger-libs/Mono.Debugging/Mono.Debugging.Client/DebuggerSession.cs",
  "utf8",
);
const softDebuggerSession = fs.readFileSync(
  "adapter/vendor/debugger-libs/Mono.Debugging.Soft/SoftDebuggerSession.cs",
  "utf8",
);
const softDebuggerProject = fs.readFileSync(
  "adapter/vendor/debugger-libs/Mono.Debugging.Soft/Mono.Debugging.Soft.csproj",
  "utf8",
);

assert.match(
  debuggerSession,
  /public event EventHandler AssemblyUnloaded;/,
  "Domain Reload hook must remain payload-free",
);
assert.ok(
  debuggerSession.includes("protected void OnAssemblyUnloaded ()"),
  "DebuggerSession must expose the payload-free unload hook to subclasses",
);
assert.ok(
  softDebuggerSession.includes("OnAssemblyUnloaded ();"),
  "SoftDebuggerSession must raise the assembly unload hook",
);
assert.ok(
  !debuggerSession.includes("AssemblyUnloadedEventArgs"),
  "Domain Reload events must not introduce path-bearing payloads",
);
assert.match(
  softDebuggerProject,
  /<PackageReference Include="Newtonsoft\.Json" Version="13\.0\.4" \/>/,
  "Mono.Debugging.Soft must use the audited Newtonsoft.Json version",
);
assert.ok(
  !softDebuggerProject.includes("<Version>10.0.3</Version>"),
  "Mono.Debugging.Soft must not restore vulnerable Newtonsoft.Json 10.0.3",
);

for (const project of [
  "adapter/vendor/debugger-libs/Mono.Debugger.Soft/Mono.Debugger.Soft.csproj",
  "adapter/vendor/debugger-libs/Mono.Debugging/Mono.Debugging.csproj",
  "adapter/vendor/debugger-libs/Mono.Debugging.Soft/Mono.Debugging.Soft.csproj",
]) {
  const contents = fs.readFileSync(project, "utf8");
  assert.ok(
    contents.includes('<Project Sdk="Microsoft.NET.Sdk">'),
    `${project} must use the SDK build entry point`,
  );
  assert.ok(
    contents.includes("<TargetFramework>net48</TargetFramework>"),
    `${project} must target net48`,
  );
}

process.stdout.write(
  `Verified ${manifest.sources.length} pinned third-party sources.\n`,
);
