import { describe, expect, it, vi } from "vitest";

vi.mock("vscode", () => ({
  Uri: { file: (value: string) => ({ fsPath: value }) },
  commands: { registerCommand: vi.fn() },
  debug: {
    onDidTerminateDebugSession: vi.fn(),
    registerDebugAdapterDescriptorFactory: vi.fn(),
    registerDebugConfigurationProvider: vi.fn(),
    startDebugging: vi.fn(async () => true),
  },
  env: {
    clipboard: { writeText: vi.fn() },
    openExternal: vi.fn(),
  },
  window: {
    createQuickPick: vi.fn(),
    showErrorMessage: vi.fn(),
    showInformationMessage: vi.fn(),
    showQuickPick: vi.fn(),
    showWarningMessage: vi.fn(),
  },
  workspace: {
    getConfiguration: vi.fn(),
    getWorkspaceFolder: vi.fn(),
    workspaceFolders: undefined,
  },
}));

import * as vscode from "vscode";
import {
  createConfigurationUi,
  runAttachUnityDebugger,
  type AttachCommandDependencies,
} from "../../extension/src/extension.js";
import type {
  TargetPicker,
  TargetPickerItem,
} from "../../extension/src/attachTargetPicker.js";
import type { EditorCandidate } from "../../extension/src/model.js";

type Mocked = ReturnType<typeof vi.fn>;

const showQuickPick = vscode.window.showQuickPick as unknown as Mocked;

const editor: EditorCandidate = {
  processId: 1234,
  projectName: "sgproj",
  workspaceRoot: "H:\\sgproj",
  host: "127.0.0.1",
  port: 56234,
  projectVersion: "2022.3.62t11",
  machine: "xiaolonghan",
  isBackground: false,
  source: "derived-port",
};

const player: EditorCandidate = {
  processId: 0,
  projectName: "头号禁区",
  workspaceRoot: "H:\\sgproj-remote",
  host: "10.30.154.73",
  port: 56322,
  projectVersion: "",
  machine: "samsung_SM-S9260@10.30.154.73",
  isBackground: false,
  source: "player-advertisement",
};

const background: EditorCandidate = {
  ...editor,
  processId: 4321,
  projectName: "sgproj-background",
  port: 56400,
  isBackground: true,
};

const folder = { uri: { fsPath: "H:\\sgproj" } };

function labels(items: readonly TargetPickerItem[]): string[] {
  return items.map((item) => item.label);
}

describe("configuration-flow picker", () => {
  it("labels a Player target with the reference format, not PID 0", async () => {
    // A Player candidate carries processId 0 and no Editor version, so the old
    // ad-hoc `PID 0 —  — port N` label rendered two empty fields. The
    // configuration-flow picker now shares the attach command's label format.
    showQuickPick.mockResolvedValue(undefined);
    const ui = createConfigurationUi();

    await ui.selectCandidate([editor, player]);

    const calls = showQuickPick.mock.calls as unknown as Array<
      [readonly TargetPickerItem[], { placeHolder: string }]
    >;
    expect(labels(calls[0][0])).toEqual([
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
      "Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, " +
        "Port:56322 (Mono)",
    ]);
    // The placeholder has to describe the list it sits above, and a Player row
    // can appear in it. The exact label array above is what pins the Player's
    // label, so the previous `not.toContain("PID 0")` here was subsumed by it
    // and has been dropped rather than counted as coverage.
    expect(calls[0][1].placeHolder).toBe(
      "Select a Unity or Tuanjie Editor or Player",
    );
  });
});

describe("attach command", () => {
  it("starts one attach session at the chosen target's endpoint", async () => {
    // The Player is first and therefore the item the picker accepts, so every
    // value in the started configuration has to come from that candidate: a
    // hard-coded loopback endpoint or workspace root fails here.
    const picker = new AutoAcceptPicker();
    const startDebugging = vi.fn(async () => true);
    const displayBackgroundProcesses = vi.fn(() => false);
    const dependencies = attachDependencies({
      picker,
      candidates: [player, editor],
      startDebugging,
      displayBackgroundProcesses,
    });

    await runAttachUnityDebugger(folder, dependencies);

    expect(picker.title).toBe("Select Unity Instance");
    expect(labels(picker.items)).toEqual([
      "Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, " +
        "Port:56322 (Mono)",
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
    ]);
    expect(displayBackgroundProcesses).toHaveBeenCalledTimes(1);
    expect(startDebugging).toHaveBeenCalledTimes(1);
    const [startedFolder, configuration] = startDebugging.mock
      .calls[0] as unknown as [unknown, Record<string, unknown>];
    expect(startedFolder).toBe(folder);
    // The reference attaches from its picker by endpoint and with no process
    // id, so `endPoint` is what type-checks against the address literal and
    // skips a second discovery inside the provider.
    expect(configuration).toEqual({
      name: "Attach to Unity Debugger Pure",
      type: "unity-debugger-pure",
      request: "attach",
      endPoint: "10.30.154.73:56322",
      projectPath: "H:\\sgproj-remote",
    });
  });

  it("hides a background target unless the setting is on", async () => {
    const hidden = new AutoAcceptPicker();
    await runAttachUnityDebugger(
      folder,
      attachDependencies({
        picker: hidden,
        candidates: [background, editor],
        displayBackgroundProcesses: () => false,
      }),
    );
    expect(labels(hidden.items)).toEqual([
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
    ]);

    const shown = new AutoAcceptPicker();
    await runAttachUnityDebugger(
      folder,
      attachDependencies({
        picker: shown,
        candidates: [background, editor],
        displayBackgroundProcesses: () => true,
      }),
    );
    expect(labels(shown.items)).toEqual([
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
      "Unity Editor, sgproj-background, xiaolonghan, Port:56400 (Mono)",
    ]);
  });

  it("starts no session when the picker is dismissed", async () => {
    const startDebugging = vi.fn(async () => true);

    await runAttachUnityDebugger(
      folder,
      attachDependencies({
        picker: new DismissedPicker(),
        candidates: [editor],
        startDebugging,
      }),
    );

    expect(startDebugging).not.toHaveBeenCalled();
  });

  it("asks for a project folder without discovering when none is open", async () => {
    const discover = vi.fn(async () => [editor]);
    const showError = vi.fn(async () => undefined);

    await runAttachUnityDebugger(undefined, {
      discover,
      displayBackgroundProcesses: () => false,
      createQuickPick: () => new AutoAcceptPicker(),
      startDebugging: vi.fn(async () => true),
      showError,
    });

    expect(showError).toHaveBeenCalledWith(
      "Open a Unity or Tuanjie project folder before attaching.",
    );
    expect(discover).not.toHaveBeenCalled();
  });

  it("reports a refused start instead of doing nothing", async () => {
    // `startDebugging` refuses by resolving `false`, not by rejecting, so the
    // command would otherwise end with no output at all — indistinguishable
    // from one that never ran.
    const startDebugging = vi.fn(async () => false);
    const showError = vi.fn(async () => undefined);

    await runAttachUnityDebugger(
      folder,
      attachDependencies({
        picker: new AutoAcceptPicker(),
        candidates: [editor],
        startDebugging,
        showError,
      }),
    );

    expect(startDebugging).toHaveBeenCalledTimes(1);
    expect(showError).toHaveBeenCalledWith(
      "The Unity debug session could not be started.",
    );
  });

  it("reports a discovery rejection instead of doing nothing", async () => {
    const discover = vi.fn(async () => {
      throw new Error("discovery failed");
    });
    const picker = new AutoAcceptPicker();
    const startDebugging = vi.fn(async () => true);
    const showError = vi.fn(async () => undefined);

    await runAttachUnityDebugger(
      folder,
      attachDependencies({
        picker,
        candidates: [editor],
        discover,
        startDebugging,
        showError,
      }),
    );

    expect(showError).toHaveBeenCalledWith(
      "The Unity debug session could not be started.",
    );
    expect(picker.items).toEqual([]);
    expect(startDebugging).not.toHaveBeenCalled();
  });
});

function attachDependencies(overrides: {
  readonly picker: TargetPicker;
  readonly candidates: readonly EditorCandidate[];
  readonly startDebugging?: AttachCommandDependencies["startDebugging"];
  readonly displayBackgroundProcesses?: () => boolean;
  readonly discover?: AttachCommandDependencies["discover"];
  readonly showError?: AttachCommandDependencies["showError"];
}): AttachCommandDependencies {
  return {
    discover: overrides.discover ?? vi.fn(async () => overrides.candidates),
    displayBackgroundProcesses:
      overrides.displayBackgroundProcesses ?? (() => false),
    createQuickPick: () => overrides.picker,
    startDebugging: overrides.startDebugging ?? vi.fn(async () => true),
    showError: overrides.showError ?? vi.fn(async () => undefined),
  };
}

class AutoAcceptPicker implements TargetPicker {
  public title: string | undefined;
  public placeholder: string | undefined;
  public items: readonly TargetPickerItem[] = [];
  public selectedItems: readonly TargetPickerItem[] = [];
  readonly #acceptListeners: Array<() => void> = [];

  public show(): void {
    this.selectedItems = this.items.slice(0, 1);
    for (const listener of this.#acceptListeners) {
      listener();
    }
  }

  public dispose(): void {}

  public onDidAccept(listener: () => void): { dispose(): void } {
    this.#acceptListeners.push(listener);
    return { dispose: () => {} };
  }

  public onDidHide(_listener: () => void): { dispose(): void } {
    return { dispose: () => {} };
  }
}

class DismissedPicker implements TargetPicker {
  public title: string | undefined;
  public placeholder: string | undefined;
  public items: readonly TargetPickerItem[] = [];
  public selectedItems: readonly TargetPickerItem[] = [];
  readonly #hideListeners: Array<() => void> = [];

  public show(): void {
    for (const listener of this.#hideListeners) {
      listener();
    }
  }

  public dispose(): void {}

  public onDidAccept(_listener: () => void): { dispose(): void } {
    return { dispose: () => {} };
  }

  public onDidHide(listener: () => void): { dispose(): void } {
    this.#hideListeners.push(listener);
    return { dispose: () => {} };
  }
}
