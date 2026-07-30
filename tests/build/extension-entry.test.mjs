import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

test("the packaged extension entry loads in the VS Code CommonJS host", () => {
  execFileSync(process.execPath, ["esbuild.mjs"], {
    cwd: process.cwd(),
    stdio: "pipe",
  });

  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  const entryPath = path.resolve(manifest.main);
  const fixtureRoot = fs.mkdtempSync(
    path.join(os.tmpdir(), "unity-debugger-extension-entry-"),
  );
  const vscodeModuleRoot = path.join(
    fixtureRoot,
    "node_modules",
    "vscode",
  );
  fs.mkdirSync(vscodeModuleRoot, { recursive: true });
  fs.writeFileSync(
    path.join(vscodeModuleRoot, "index.js"),
    String.raw`
      const disposable = () => ({ dispose() {} });
      module.exports = {
        Uri: { file: (fsPath) => ({ fsPath }) },
        DebugAdapterExecutable: class DebugAdapterExecutable {
          constructor(command, args, options) {
            this.command = command;
            this.args = args;
            this.options = options;
          }
        },
        workspace: {
          workspaceFolders: [],
          getWorkspaceFolder: () => undefined,
        },
        debug: {
          registerDebugConfigurationProvider: disposable,
          registerDebugAdapterDescriptorFactory: disposable,
          onDidTerminateDebugSession: disposable,
          onDidStartDebugSession: disposable,
          startDebugging: async () => true,
        },
        commands: { registerCommand: disposable },
        window: {
          showErrorMessage: async () => undefined,
          showInformationMessage: async () => undefined,
          showWarningMessage: async () => undefined,
          showQuickPick: async () => undefined,
        },
        env: {
          openExternal: async () => undefined,
          clipboard: { writeText: async () => undefined },
        },
      };
    `,
  );
  const loader = String.raw`
    const extension = require(process.argv[1]);
    if (typeof extension.activate !== "function") {
      throw new Error("Extension entry does not export activate()");
    }
    const api = extension.activate({
      extensionPath: process.cwd(),
      extension: { packageJSON: { version: "0.2.0" } },
      subscriptions: [],
    });
    if (
      !api ||
      api.apiVersion !== 1 ||
      api.debugType !== "unity-debugger-pure" ||
      typeof api.discoverTargets !== "function" ||
      typeof api.startAttach !== "function"
    ) {
      throw new Error("Extension activation did not return the public debugger API");
    }
    const ownKeys = Reflect.ownKeys(api).sort();
    if (JSON.stringify(ownKeys) !== JSON.stringify([
      "apiVersion",
      "debugType",
      "extensionVersion",
    ])) {
      throw new Error("Extension activation exposed properties outside the v1 API contract");
    }
    for (const internalName of ["discovery", "registry", "starter"]) {
      if (api[internalName] !== undefined) {
        throw new Error("Extension activation exposed an internal dependency");
      }
    }
  `;
  try {
    const result = spawnSync(
      process.execPath,
      ["--input-type=commonjs", "-e", loader, entryPath],
      {
        cwd: process.cwd(),
        encoding: "utf8",
        env: {
          ...process.env,
          NODE_PATH: path.join(fixtureRoot, "node_modules"),
        },
      },
    );

    assert.equal(
      result.status,
      0,
      `Extension entry failed to load:\n${result.stderr}`,
    );
  } finally {
    fs.rmSync(fixtureRoot, { force: true, recursive: true });
  }
});
