import { NO_PROCESSES_MESSAGE } from "./diagnostics.js";
import type { EditorCandidate } from "./model.js";

/**
 * The reference's QuickPick title, verbatim from its English bundle
 * (`out/nls.bundle.json`, `debuggerStrings.selectUnityInstance`).
 */
export const ATTACH_PICKER_TITLE = "Select Unity Instance";

/**
 * The reference's debugger-engine token is `VSTU`; ours is Mono, which is the
 * one field of one label that differs. The rest of the format is the
 * reference's own.
 */
const DEBUGGER_ENGINE = "Mono";

export interface TargetPickerItem {
  readonly label: string;
  readonly candidate: EditorCandidate;
}

/**
 * The part of `vscode.QuickPick` this module uses, declared structurally so the
 * picker can be exercised without VS Code. A real QuickPick satisfies it.
 */
export interface TargetPicker {
  title: string | undefined;
  placeholder: string | undefined;
  items: readonly TargetPickerItem[];
  readonly selectedItems: readonly TargetPickerItem[];
  show(): void;
  dispose(): void;
  onDidAccept(listener: () => void): { dispose(): void };
  onDidHide(listener: () => void): { dispose(): void };
}

export interface SelectTargetOptions {
  readonly displayBackgroundProcesses: boolean;
  readonly createQuickPick: () => TargetPicker;
}

/**
 * The reference's label builder, exactly:
 * `["Unity " + displayType, projectName, machine, displayConnection]`
 * filtered to non-blank parts and joined with `", "`. Observed live as
 * `Unity Editor, sgproj, xiaolonghan, Port:56548 (VSTU)` for a local Editor and
 * `Unity Player, 头号禁区, samsung_SM-S9260@10.30.154.73, Port:56322 (VSTU)` for
 * a network Player.
 */
export function formatTargetLabel(candidate: EditorCandidate): string {
  return [
    `Unity ${targetType(candidate)}`,
    candidate.projectName,
    candidate.machine,
    `Port:${candidate.port} (${DEBUGGER_ENGINE})`,
  ]
    .filter((part) => part && part.trim().length > 0)
    .join(", ");
}

/**
 * The reference's picker: background processes are hidden unless the setting
 * says otherwise, and the rest sort non-background first
 * (`Number(isBackground)` ascending, so the order inside each group is the
 * discovery order).
 *
 * The reference opens its picker before discovery finishes and fills it as
 * results arrive, which is why its placeholder says `Detecting... press any key
 * to filter`. We collect first and open afterwards, so that placeholder has no
 * counterpart here and is deliberately not faked: the picker is empty only when
 * nothing is attachable, and then it says so.
 */
export async function selectTarget(
  candidates: readonly EditorCandidate[],
  options: SelectTargetOptions,
): Promise<EditorCandidate | undefined> {
  const items = candidates
    .filter(
      (candidate) =>
        !candidate.isBackground || options.displayBackgroundProcesses,
    )
    .sort(
      (left, right) =>
        Number(left.isBackground) - Number(right.isBackground),
    )
    .map((candidate) => ({
      label: formatTargetLabel(candidate),
      candidate,
    }));

  const picker = options.createQuickPick();
  picker.title = ATTACH_PICKER_TITLE;
  picker.items = items;
  picker.placeholder = items.length === 0 ? NO_PROCESSES_MESSAGE : "";

  return new Promise<EditorCandidate | undefined>((resolve) => {
    let settled = false;
    const settle = (candidate: EditorCandidate | undefined): void => {
      if (settled) {
        return;
      }
      settled = true;
      picker.dispose();
      resolve(candidate);
    };

    picker.onDidAccept(() => settle(picker.selectedItems[0]?.candidate));
    picker.onDidHide(() => settle(undefined));
    picker.show();
  });
}

function targetType(candidate: EditorCandidate): "Editor" | "Player" {
  return candidate.source === "player-advertisement" ? "Player" : "Editor";
}
