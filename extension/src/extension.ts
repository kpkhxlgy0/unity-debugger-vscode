import path from "node:path";
import * as vscode from "vscode";
import { AdapterLauncher } from "./adapterLauncher.js";
import {
  DebugConfigurationProvider,
  type ConfigurationUi,
} from "./debugConfigurationProvider.js";
import {
  COPY_DIAGNOSTICS_COMMAND,
  getLogDirectory,
  NO_SANITIZED_DIAGNOSTICS,
  OPEN_LOGS_COMMAND,
  readLatestSanitizedReport,
  REFRESH_TARGETS_COMMAND,
  terminationMessageFromLog,
} from "./diagnostics.js";
import { EditorDiscovery } from "./editorDiscovery.js";

export function activate(context: vscode.ExtensionContext): void {
  const discovery = new EditorDiscovery();
  const ui = createConfigurationUi();
  const provider = new DebugConfigurationProvider(
    discovery,
    ui,
    listWorkspaceRoots,
  );
  const launcher = new AdapterLauncher();

  const configurationRegistration =
    vscode.debug.registerDebugConfigurationProvider(
      "unity-community",
      provider,
    );
  const descriptorRegistration =
    vscode.debug.registerDebugAdapterDescriptorFactory(
      "unity-community",
      {
        createDebugAdapterDescriptor: () => {
          try {
            const descriptor = launcher.createDescriptor(
              context.extensionPath,
            );
            return new vscode.DebugAdapterExecutable(
              descriptor.command,
              [...descriptor.args],
              descriptor.options,
            );
          } catch (error) {
            void vscode.window.showErrorMessage(
              (error as Error).message,
            );
            return undefined;
          }
        },
      },
    );

  const refreshRegistration = vscode.commands.registerCommand(
    REFRESH_TARGETS_COMMAND,
    async () => {
      const folders = vscode.workspace.workspaceFolders ?? [];
      const roots = folders.map((folder) => folder.uri.fsPath);
      if (roots.length === 0) {
        await ui.showError(
          "Open a Unity or Tuanjie project folder before refreshing.",
        );
        return;
      }

      const candidates = await discovery.discover(roots);
      await vscode.window.showInformationMessage(
        candidates.length === 1
          ? "Found 1 local debug-enabled Editor."
          : `Found ${candidates.length} local debug-enabled Editors.`,
      );
    },
  );
  const openLogsRegistration = vscode.commands.registerCommand(
    OPEN_LOGS_COMMAND,
    async () => {
      const directory = getLogDirectory();
      if (!directory) {
        await vscode.window.showInformationMessage(
          NO_SANITIZED_DIAGNOSTICS,
        );
        return;
      }
      await vscode.env.openExternal(vscode.Uri.file(directory));
    },
  );
  const copyDiagnosticsRegistration = vscode.commands.registerCommand(
    COPY_DIAGNOSTICS_COMMAND,
    async () => {
      const report = await readLatestSanitizedReport();
      await vscode.env.clipboard.writeText(
        report ?? NO_SANITIZED_DIAGNOSTICS,
      );
      await vscode.window.showInformationMessage(
        report
          ? "Latest sanitized diagnostics copied."
          : "Sanitized diagnostics status copied.",
      );
    },
  );
  const terminationRegistration =
    vscode.debug.onDidTerminateDebugSession(async (session) => {
      if (session.type !== "unity-community") {
        return;
      }
      const message = terminationMessageFromLog(
        await readLatestSanitizedReport(),
      );
      if (message) {
        await vscode.window.showErrorMessage(message);
      }
    });

  context.subscriptions.push(
    discovery,
    configurationRegistration,
    descriptorRegistration,
    refreshRegistration,
    openLogsRegistration,
    copyDiagnosticsRegistration,
    terminationRegistration,
  );
}

export function deactivate(): void {}

function createConfigurationUi(): ConfigurationUi {
  return {
    async selectCandidate(candidates) {
      const items = candidates.map((candidate) => ({
        label:
          `${candidate.projectName} — PID ${candidate.processId} — ` +
          `${candidate.projectVersion} — port ${candidate.port}`,
        candidate,
      }));
      const selected = await vscode.window.showQuickPick(items, {
        placeHolder: "Select a local Unity or Tuanjie Editor",
      });
      return selected?.candidate;
    },
    async showNoEditor(message) {
      const action = await vscode.window.showWarningMessage(
        message,
        "Refresh",
      );
      return action === "Refresh" ? "refresh" : undefined;
    },
    async showError(message) {
      await vscode.window.showErrorMessage(message);
    },
  };
}

function listWorkspaceRoots(currentFolder: string): readonly string[] {
  const candidates = [
    currentFolder,
    ...(vscode.workspace.workspaceFolders ?? []).map(
      (folder) => folder.uri.fsPath,
    ),
  ];
  const roots = new Map<string, string>();

  for (const candidate of candidates) {
    const normalized = path.resolve(candidate);
    const key =
      process.platform === "win32"
        ? normalized.toLowerCase()
        : normalized;
    if (!roots.has(key)) {
      roots.set(key, normalized);
    }
  }

  return [...roots.values()];
}
