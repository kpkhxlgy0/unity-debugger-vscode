import path from "node:path";
import * as vscode from "vscode";
import { AdapterLauncher } from "./adapterLauncher.js";
import { AttachRequestRegistry } from "./attachRequestRegistry.js";
import {
  formatTargetLabel,
  selectTarget,
  type TargetPicker,
  type TargetPickerItem,
} from "./attachTargetPicker.js";
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
import type { EditorCandidate } from "./model.js";
import { PRODUCT_IDENTITY } from "./productIdentity.js";
import {
  UnityDebuggerPureApi,
  VscodeDebugSessionStarter,
} from "./publicApi.js";
import type { UnityDebuggerPureApiV1 } from "./publicApiTypes.js";

const ATTACH_UNITY_DEBUGGER_COMMAND =
  PRODUCT_IDENTITY.commandIds.attachUnityDebugger;
/**
 * VS Code addresses a contributed setting as `<section>.<key>`, so this is the
 * key inside the section named by our extension name — the same shape the
 * reference uses when it reads `displayBackgroundProcesses` from `vstuc`.
 */
const DISPLAY_BACKGROUND_PROCESSES_SETTING = "displayBackgroundProcesses";
const NO_WORKSPACE_FOLDER_MESSAGE =
  "Open a Unity or Tuanjie project folder before attaching.";

export interface WorkspaceFolderLike {
  readonly uri: { readonly fsPath: string };
}

export interface AttachCommandDependencies {
  discover(
    workspaceRoot: string,
  ): Promise<readonly EditorCandidate[]>;
  displayBackgroundProcesses(): boolean;
  createQuickPick(): TargetPicker;
  startDebugging(
    folder: WorkspaceFolderLike,
    configuration: Readonly<Record<string, unknown>>,
  ): PromiseLike<boolean>;
  showError(message: string): Promise<void>;
}

/**
 * The reference's attach command, minus its streaming picker: discover the
 * targets for the open project, let the user pick one, start a single attach
 * session. The synthesized configuration carries `endPoint` and no process id,
 * exactly as the reference's picker does (`address + ":" + debuggerPort`), so
 * the provider validates the literal and does not discover a second time.
 */
export async function runAttachUnityDebugger(
  folder: WorkspaceFolderLike | undefined,
  dependencies: AttachCommandDependencies,
): Promise<void> {
  if (!folder) {
    await dependencies.showError(NO_WORKSPACE_FOLDER_MESSAGE);
    return;
  }

  const candidates = await dependencies.discover(folder.uri.fsPath);
  const candidate = await selectTarget(candidates, {
    displayBackgroundProcesses: dependencies.displayBackgroundProcesses(),
    createQuickPick: dependencies.createQuickPick,
  });
  if (!candidate) {
    return;
  }

  await dependencies.startDebugging(folder, {
    name: PRODUCT_IDENTITY.defaultConfigurationName,
    type: PRODUCT_IDENTITY.debugType,
    request: "attach",
    endPoint: `${candidate.host}:${candidate.port}`,
    projectPath: candidate.workspaceRoot,
  });
}

export function activate(
  context: vscode.ExtensionContext,
): UnityDebuggerPureApiV1 {
  const discovery = new EditorDiscovery();
  const attachRequests = new AttachRequestRegistry();
  const ui = createConfigurationUi();
  const provider = new DebugConfigurationProvider(
    discovery,
    ui,
    listWorkspaceRoots,
    attachRequests,
  );
  const launcher = new AdapterLauncher();

  const configurationRegistration =
    vscode.debug.registerDebugConfigurationProvider(
      PRODUCT_IDENTITY.debugType,
      provider,
    );
  const descriptorRegistration =
    vscode.debug.registerDebugAdapterDescriptorFactory(
      PRODUCT_IDENTITY.debugType,
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

  const attachRegistration = vscode.commands.registerCommand(
    ATTACH_UNITY_DEBUGGER_COMMAND,
    () =>
      runAttachUnityDebugger(
        (vscode.workspace.workspaceFolders ?? [])[0],
        {
          discover: (workspaceRoot) =>
            discovery.discover(listWorkspaceRoots(workspaceRoot)),
          displayBackgroundProcesses: () =>
            vscode.workspace
              .getConfiguration(PRODUCT_IDENTITY.extensionName)
              .get<boolean>(DISPLAY_BACKGROUND_PROCESSES_SETTING, false),
          createQuickPick: () =>
            vscode.window.createQuickPick<TargetPickerItem>(),
          startDebugging: (folder, configuration) =>
            vscode.debug.startDebugging(
              folder as unknown as vscode.WorkspaceFolder | undefined,
              configuration as vscode.DebugConfiguration,
            ),
          showError: (message) => ui.showError(message),
        },
      ),
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
      if (session.type !== PRODUCT_IDENTITY.debugType) {
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
    attachRegistration,
    refreshRegistration,
    openLogsRegistration,
    copyDiagnosticsRegistration,
    terminationRegistration,
  );

  return new UnityDebuggerPureApi(
    discovery,
    attachRequests,
    new VscodeDebugSessionStarter(),
    context.extension.packageJSON.version as string,
  );
}

export function deactivate(): void {}

export function createConfigurationUi(): ConfigurationUi {
  return {
    async selectCandidate(candidates) {
      const items = candidates.map((candidate) => ({
        // The reference shows one label format wherever a target is listed, so
        // the configuration-flow picker shares the attach command's. A Player
        // carries no process id and no Editor version, which is what the
        // previous ad-hoc `PID … — port …` label got wrong.
        label: formatTargetLabel(candidate),
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
