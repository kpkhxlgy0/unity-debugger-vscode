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
import { warnAboutDeprecatedExtensions } from "./deprecatedExtension.js";
import {
  COPY_DIAGNOSTICS_COPIED_LINE,
  COPY_DIAGNOSTICS_COMMAND,
  COPY_DIAGNOSTICS_UNAVAILABLE_LINE,
  getLogDirectory,
  NO_SANITIZED_DIAGNOSTICS,
  NO_WORKSPACE_FOLDER_MESSAGE,
  OPEN_LOGS_COMMAND,
  OPEN_LOGS_OPENED_LINE,
  OPEN_LOGS_UNAVAILABLE_LINE,
  readLatestSanitizedReport,
  REFRESH_TARGETS_COMMAND,
  terminationMessageFromLog,
} from "./diagnostics.js";
import { EditorDiscovery } from "./editorDiscovery.js";
import {
  FUNCTION_BREAKPOINT_WARNING,
  shouldWarnAboutFunctionBreakpoints,
} from "./functionBreakpointWarning.js";
import type { EditorCandidate } from "./model.js";
import { createOutputChannel } from "./outputChannel.js";
import {
  DISPLAY_BACKGROUND_PROCESSES_SETTING,
  PRODUCT_IDENTITY,
} from "./productIdentity.js";
import {
  UnityDebuggerPureApi,
  VscodeDebugSessionStarter,
} from "./publicApi.js";
import type { UnityDebuggerPureApiV1 } from "./publicApiTypes.js";
import {
  runShowUnityApiReference,
  SHOW_UNITY_API_REFERENCE_COMMAND,
} from "./unityApiReference.js";

const ATTACH_UNITY_DEBUGGER_COMMAND =
  PRODUCT_IDENTITY.commandIds.attachUnityDebugger;
/**
 * A command run from the palette that reports nothing at all is
 * indistinguishable from a command that never ran, and this is the one place
 * the attach path could fail in silence: `startDebugging` refuses by returning
 * `false` rather than by rejecting.
 *
 * The reference is silent here — it `await`s the same call and discards the
 * result, with no `catch` — so this message is a deliberate divergence in the
 * user's favour, not an alignment. Record it as one.
 */
const ATTACH_START_FAILED_MESSAGE =
  "The Unity debug session could not be started.";

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

  // Discovery and the start are the command's two fallible steps. Neither is
  // allowed to end in silence: a rejection from either one would otherwise
  // leave a palette command that reports nothing at all.
  try {
    const candidates = await dependencies.discover(folder.uri.fsPath);
    const candidate = await selectTarget(candidates, {
      displayBackgroundProcesses: dependencies.displayBackgroundProcesses(),
      createQuickPick: dependencies.createQuickPick,
    });
    if (!candidate) {
      return;
    }

    const started = await dependencies.startDebugging(folder, {
      name: PRODUCT_IDENTITY.defaultConfigurationName,
      type: PRODUCT_IDENTITY.debugType,
      request: "attach",
      endPoint: `${candidate.host}:${candidate.port}`,
      projectPath: candidate.workspaceRoot,
    });
    if (!started) {
      await dependencies.showError(ATTACH_START_FAILED_MESSAGE);
    }
  } catch {
    await dependencies.showError(ATTACH_START_FAILED_MESSAGE);
  }
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
  const outputChannel = createOutputChannel((name, options) =>
    vscode.window.createOutputChannel(name, options),
  );

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
            // The reference raises this at the same point, and only once its
            // launcher has succeeded: a descriptor that throws already surfaces
            // its own error, so a performance warning on top of that failure
            // would land at the worst possible moment.
            //
            // `vscode.debug.breakpoints` is `readonly Breakpoint[]`, and only
            // `FunctionBreakpoint` carries a `functionName`, so TypeScript's
            // weak-type rule rejects the predicate's structural parameter
            // without this assertion; the predicate re-checks at run time.
            //
            // `readonly (vscode.Breakpoint | { functionName?: string })[]` is
            // the union that would remove the assertion — `Breakpoint`
            // satisfies one member, so the weak-type rule never fires and the
            // tests' `{ functionName: "Foo" }` / `{}` inputs still
            // type-check. It is not taken, and should not be re-derived as an
            // improvement: it buys nothing at run time and would put a VS Code
            // type inside a module that is deliberately host-free, which is
            // the property that makes the predicate testable without a host.
            const breakpoints = vscode.debug.breakpoints as readonly {
              functionName?: string;
            }[];
            if (shouldWarnAboutFunctionBreakpoints(breakpoints)) {
              // Emitted inside its own `try`, unlike the launcher above: this
              // is a diagnostics call, and the `catch` that owns the launch
              // must never be reachable from it. Without this, a `warn` that
              // threw — a disposed channel, a host fault — would be caught
              // below and reported as a failed launch with no adapter, so the
              // diagnostic path would break the thing it diagnoses.
              try {
                outputChannel.warn(FUNCTION_BREAKPOINT_WARNING);
              } catch {
                // Nothing to do: the warning is a courtesy, and the launch it
                // is attached to is still good.
              }
            }
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
              // A single assertion, not `as unknown as`: `WorkspaceFolderLike`
              // is the deliberately host-free shape of the folder this closure
              // is handed, and the value really is the `vscode.WorkspaceFolder`
              // the command read from `vscode.workspace.workspaceFolders`. The
              // single form still fails to compile if the two shapes drift
              // apart, which is the one thing the double cast could hide.
              folder as vscode.WorkspaceFolder | undefined,
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
        // The channel records the same outcome the user is told about, and
        // names no path: the directory the other branch knows is an absolute
        // user path, so it goes to the shell and never into a log line.
        outputChannel.writeSanitized(OPEN_LOGS_UNAVAILABLE_LINE);
        await vscode.window.showInformationMessage(
          NO_SANITIZED_DIAGNOSTICS,
        );
        return;
      }
      await vscode.env.openExternal(vscode.Uri.file(directory));
      outputChannel.writeSanitized(OPEN_LOGS_OPENED_LINE);
    },
  );
  const copyDiagnosticsRegistration = vscode.commands.registerCommand(
    COPY_DIAGNOSTICS_COMMAND,
    async () => {
      const report = await readLatestSanitizedReport();
      await vscode.env.clipboard.writeText(
        report ?? NO_SANITIZED_DIAGNOSTICS,
      );
      outputChannel.writeSanitized(
        report
          ? COPY_DIAGNOSTICS_COPIED_LINE
          : COPY_DIAGNOSTICS_UNAVAILABLE_LINE,
      );
      await vscode.window.showInformationMessage(
        report
          ? "Latest sanitized diagnostics copied."
          : "Sanitized diagnostics status copied.",
      );
    },
  );
  const unityApiReferenceRegistration = vscode.commands.registerCommand(
    SHOW_UNITY_API_REFERENCE_COMMAND,
    () =>
      runShowUnityApiReference({
        // Read when the command runs, not when the extension activates: the
        // editor with focus at palette time is the one being asked about.
        activeEditor: vscode.window.activeTextEditor,
        // `Uri.parse` keeps the URL opaque, so VS Code's own handler decides
        // whether it is a documentation page it can open externally.
        openExternal: (url) => vscode.env.openExternal(vscode.Uri.parse(url)),
      }),
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
    outputChannel,
    configurationRegistration,
    descriptorRegistration,
    attachRegistration,
    refreshRegistration,
    openLogsRegistration,
    copyDiagnosticsRegistration,
    unityApiReferenceRegistration,
    terminationRegistration,
  );

  // Fired last, and without awaiting it, exactly where the reference fires it:
  // it is a courtesy to a user who has a deprecated third-party Unity debugger
  // active, so it must never be in the way of the debugger activating. It reads
  // the registry only, which declares no dependency on that extension —
  // `extensionDependencies` stays empty.
  warnAboutDeprecatedExtensions({
    extensions: vscode.extensions,
    showWarningMessage: (message) => {
      void vscode.window.showWarningMessage(message);
    },
  });

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
        // Names both kinds of row: a Player advertisement is a candidate here
        // too, and the copy that said only "Editor" described a list it no
        // longer shows.
        placeHolder: "Select a Unity or Tuanjie Editor or Player",
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
