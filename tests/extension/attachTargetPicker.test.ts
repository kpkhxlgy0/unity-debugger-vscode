import { describe, expect, it } from "vitest";
import {
  ATTACH_PICKER_TITLE,
  formatTargetLabel,
  selectTarget,
  type SelectTargetOptions,
  type TargetPicker,
  type TargetPickerItem,
} from "../../extension/src/attachTargetPicker.js";
import { NO_PROCESSES_MESSAGE } from "../../extension/src/diagnostics.js";
import type { EditorCandidate } from "../../extension/src/model.js";

/**
 * The reference extension's own labels, observed live from its shipped probe:
 * `Unity Editor, sgproj, xiaolonghan, Port:56548 (VSTU)` for a local Editor and
 * `Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, Port:56322 (VSTU)` for
 * a network Player. Our debugger engine token is the one field that differs.
 */
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
  workspaceRoot: "H:\\sgproj",
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

function options(
  picker: FakeTargetPicker,
  displayBackgroundProcesses = false,
): SelectTargetOptions {
  return {
    displayBackgroundProcesses,
    createQuickPick: () => picker,
  };
}

describe("formatTargetLabel", () => {
  it("formats a label like the reference", () => {
    expect(formatTargetLabel(editor)).toBe(
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
    );
  });

  it("labels an advertised Editor target as an Editor", () => {
    expect(formatTargetLabel({ ...editor, source: "advertisement" })).toBe(
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
    );
  });

  it("labels a Player target as a Player", () => {
    expect(formatTargetLabel(player)).toBe(
      "Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, " +
        "Port:56322 (Mono)",
    );
  });

  it("omits absent and whitespace-only parts", () => {
    expect(
      formatTargetLabel({ ...player, projectName: "", machine: "   " }),
    ).toBe("Unity Player, Port:56322 (Mono)");
  });
});

describe("selectTarget", () => {
  it("uses the reference's QuickPick title and a reference-format label", async () => {
    const picker = new FakeTargetPicker();
    const selection = selectTarget([editor, player], options(picker));

    expect(picker.title).toBe(ATTACH_PICKER_TITLE);
    expect(ATTACH_PICKER_TITLE).toBe("Select Unity Instance");
    // We collect before opening the picker, so there is no in-progress state
    // to describe: the reference's `Detecting...` placeholder has no
    // counterpart here and must not be faked.
    expect(picker.placeholder).toBe("");
    expect(picker.items.map((item) => item.label)).toEqual([
      "Unity Editor, sgproj, xiaolonghan, Port:56234 (Mono)",
      "Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, " +
        "Port:56322 (Mono)",
    ]);

    picker.accept(picker.items[1]);
    await expect(selection).resolves.toEqual(player);
    expect(picker.disposed).toBe(true);
  });

  it("hides background processes unless the setting is on", async () => {
    const hidden = new FakeTargetPicker();
    const hiddenSelection = selectTarget(
      [editor, background],
      options(hidden),
    );
    expect(hidden.items.map((item) => item.candidate)).toEqual([editor]);
    hidden.hide();
    await expect(hiddenSelection).resolves.toBeUndefined();

    const shown = new FakeTargetPicker();
    const shownSelection = selectTarget(
      [editor, background],
      options(shown, true),
    );
    expect(shown.items.map((item) => item.candidate)).toEqual([
      editor,
      background,
    ]);
    shown.hide();
    await expect(shownSelection).resolves.toBeUndefined();
  });

  it("sorts non-background targets first and keeps the rest in order", async () => {
    const picker = new FakeTargetPicker();
    const selection = selectTarget(
      [background, player, editor],
      options(picker, true),
    );

    expect(picker.items.map((item) => item.candidate)).toEqual([
      player,
      editor,
      background,
    ]);

    picker.hide();
    await selection;
  });

  it("reports no processes to attach to when the list is empty", async () => {
    const picker = new FakeTargetPicker();
    const selection = selectTarget([], options(picker));

    expect(picker.items).toEqual([]);
    expect(picker.placeholder).toBe(NO_PROCESSES_MESSAGE);
    expect(NO_PROCESSES_MESSAGE).toBe("No Process to attach to");
    picker.show();

    picker.hide();
    await expect(selection).resolves.toBeUndefined();
  });

  it("reports no processes to attach to when the setting hides every target", async () => {
    const picker = new FakeTargetPicker();
    const selection = selectTarget([background], options(picker));

    expect(picker.items).toEqual([]);
    expect(picker.placeholder).toBe(NO_PROCESSES_MESSAGE);

    picker.hide();
    await expect(selection).resolves.toBeUndefined();
  });
});

class FakeTargetPicker implements TargetPicker {
  public title: string | undefined;
  public placeholder: string | undefined;
  public items: readonly TargetPickerItem[] = [];
  public selectedItems: readonly TargetPickerItem[] = [];
  public shown = false;
  public disposed = false;
  readonly #acceptListeners: Array<() => void> = [];
  readonly #hideListeners: Array<() => void> = [];

  public show(): void {
    this.shown = true;
  }

  public dispose(): void {
    this.disposed = true;
  }

  public onDidAccept(listener: () => void): { dispose(): void } {
    this.#acceptListeners.push(listener);
    return { dispose: () => {} };
  }

  public onDidHide(listener: () => void): { dispose(): void } {
    this.#hideListeners.push(listener);
    return { dispose: () => {} };
  }

  public accept(item?: TargetPickerItem): void {
    this.selectedItems = item ? [item] : [];
    for (const listener of this.#acceptListeners) {
      listener();
    }
  }

  public hide(): void {
    for (const listener of this.#hideListeners) {
      listener();
    }
  }
}
