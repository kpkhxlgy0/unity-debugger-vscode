import { PRODUCT_IDENTITY } from "./productIdentity.js";

export const SHOW_UNITY_API_REFERENCE_COMMAND =
  PRODUCT_IDENTITY.commandIds.showUnityApiReference;

/** Where the reference sends a lookup that has no symbol to search for. */
const UNITY_REFERENCE_INDEX_URL = "https://docs.unity3d.com/ScriptReference";

/** The two fields of a `vscode.Position` this command reads. */
export interface ReferencePositionLike {
  readonly line: number;
  readonly character: number;
}

export interface ReferenceRangeLike {
  readonly start: ReferencePositionLike;
}

export interface ReferenceDocumentLike {
  getText(range?: ReferenceRangeLike): string;
  getWordRangeAtPosition(
    position: ReferencePositionLike,
  ): ReferenceRangeLike | undefined;
}

export interface ReferenceEditorLike {
  readonly selection: ReferenceRangeLike;
  readonly document: ReferenceDocumentLike;
}

export interface UnityApiReferenceDependencies {
  readonly activeEditor: ReferenceEditorLike | undefined;
  openExternal(url: string): PromiseLike<unknown>;
}

/**
 * The reference's URL rule, read out of its handler: a search page for a
 * single-line, non-blank symbol, and the bare index otherwise. There is no
 * `<Symbol>.html` anywhere in that handler, so a per-symbol URL would be our
 * invention rather than its behaviour.
 *
 * The one deliberate difference is the encoding. The reference interpolates the
 * text raw (`encodeURIComponent` appears nowhere in it), so a symbol containing
 * `#`, `&` or a space produces a URL that looks something else up. Encoding
 * changes nothing for a plain C# identifier and fixes those.
 */
export function buildUnityReferenceUrl(
  selectedText: string | undefined,
): string {
  if (!isSearchable(selectedText)) {
    return UNITY_REFERENCE_INDEX_URL;
  }

  return (
    `${UNITY_REFERENCE_INDEX_URL}/30_search.html` +
    `?q=${encodeURIComponent(selectedText)}`
  );
}

/**
 * The reference's guard, which is
 * `!(!text || !text.trim()) && (text.match(/\n/g) ?? []).length === 0`: blank
 * text and multi-line text both fall back to the index.
 */
function isSearchable(text: string | undefined): text is string {
  if (!text || !text.trim()) {
    return false;
  }

  return (text.match(/\n/g) ?? []).length === 0;
}

/**
 * The reference's handler: with an active editor, look up the selection and
 * open the URL above; with no active editor, do nothing at all and say nothing.
 * There is no message on either path, which is why this command never reports
 * an error.
 *
 * `openExternal` is injected rather than reached for directly so the URL can be
 * asserted without a VS Code host. The reference uses
 * `commands.executeCommand("vscode.open", Uri.parse(url))`, which reaches the
 * same browser for an `https` URI; `vscode.env.openExternal` is the documented
 * API for opening one, and the extension already uses it for the log directory.
 */
export async function runShowUnityApiReference(
  dependencies: UnityApiReferenceDependencies,
): Promise<void> {
  const editor = dependencies.activeEditor;
  if (!editor) {
    return;
  }

  await dependencies.openExternal(
    buildUnityReferenceUrl(selectedText(editor)),
  );
}

function selectedText(editor: ReferenceEditorLike): string {
  const selection = editor.document.getText(editor.selection);
  if (selection) {
    return selection;
  }

  // `getWordRangeAtPosition` is undefined when the cursor is not on a word, and
  // VS Code's `getText(undefined)` then returns the whole document — which the
  // single-line guard above turns into the index. That is the outcome of the
  // reference's identical call chain, so it is not special-cased here.
  return editor.document.getText(
    editor.document.getWordRangeAtPosition(editor.selection.start),
  );
}
