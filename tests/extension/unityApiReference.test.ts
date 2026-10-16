import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  activeTextEditor: undefined as unknown,
  openExternal: vi.fn(async () => true),
  registerCommand: vi.fn(() => ({ dispose: () => {} })),
}));

vi.mock("vscode", () => ({
  Uri: {
    file: (value: string) => ({ fsPath: value }),
    parse: (value: string) => ({ toString: () => value }),
  },
  commands: { registerCommand: mocks.registerCommand },
  debug: {
    onDidTerminateDebugSession: vi.fn(() => ({ dispose: () => {} })),
    registerDebugAdapterDescriptorFactory: vi.fn(() => ({
      dispose: () => {},
    })),
    registerDebugConfigurationProvider: vi.fn(() => ({
      dispose: () => {},
    })),
    startDebugging: vi.fn(async () => true),
  },
  env: {
    clipboard: { writeText: vi.fn() },
    openExternal: mocks.openExternal,
  },
  window: {
    // The handler reads the active editor when it runs, not when the extension
    // activates, so this has to be a live read of mutable state.
    get activeTextEditor() {
      return mocks.activeTextEditor;
    },
    // The extension builds this channel at activation and does not write to it
    // until a debug session needs one, so activation only needs it to exist.
    createOutputChannel: vi.fn(),
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
import { activate } from "../../extension/src/extension.js";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";
import { buildUnityReferenceUrl } from "../../extension/src/unityApiReference.js";

const INDEX_URL = "https://docs.unity3d.com/ScriptReference";

beforeEach(() => {
  vi.clearAllMocks();
  mocks.activeTextEditor = undefined;
});

describe("buildUnityReferenceUrl", () => {
  it("searches for a selected symbol", () => {
    expect(buildUnityReferenceUrl("GameObject")).toBe(
      `${INDEX_URL}/30_search.html?q=GameObject`,
    );
  });

  it("falls back to the index for a multi-line selection", () => {
    expect(buildUnityReferenceUrl("a\nb")).toBe(INDEX_URL);
  });

  it("falls back to the index for an empty selection", () => {
    expect(buildUnityReferenceUrl(undefined)).toBe(INDEX_URL);
    expect(buildUnityReferenceUrl("")).toBe(INDEX_URL);
  });

  it("yields the index when the selection is only whitespace", () => {
    expect(buildUnityReferenceUrl("   ")).toBe(INDEX_URL);
  });

  it("encodes symbols the reference would break on", () => {
    // The reference interpolates the text raw, so each of these would produce a
    // URL that searches for something else entirely.
    expect(buildUnityReferenceUrl("Foo&Bar")).toBe(
      `${INDEX_URL}/30_search.html?q=Foo%26Bar`,
    );
    expect(buildUnityReferenceUrl("Foo#Bar")).toBe(
      `${INDEX_URL}/30_search.html?q=Foo%23Bar`,
    );
    expect(buildUnityReferenceUrl("Foo Bar")).toBe(
      `${INDEX_URL}/30_search.html?q=Foo%20Bar`,
    );
  });
});

describe("the Unity API Reference command", () => {
  it("searches for the selected text", async () => {
    mocks.activeTextEditor = fakeEditor({ selection: "GameObject" });

    await invoke();

    expect(openExternalUrls()).toEqual([
      `${INDEX_URL}/30_search.html?q=GameObject`,
    ]);
  });

  it("searches the word at the cursor when the selection is empty", async () => {
    // The reference falls back to `getWordRangeAtPosition(selection.start)`,
    // which is what makes the command usable from a bare cursor.
    mocks.activeTextEditor = fakeEditor({ selection: "", word: "Transform" });

    await invoke();

    expect(openExternalUrls()).toEqual([
      `${INDEX_URL}/30_search.html?q=Transform`,
    ]);
  });

  it("opens the index for a multi-line selection", async () => {
    mocks.activeTextEditor = fakeEditor({ selection: "a\nb" });

    await invoke();

    expect(openExternalUrls()).toEqual([INDEX_URL]);
  });

  it("opens the index when the cursor is not on a word", async () => {
    // `getWordRangeAtPosition` is undefined off a word, and VS Code's
    // `getText(undefined)` then returns the whole document, so the reference
    // reads the file rather than a symbol — and the single-line guard sends
    // that to the index instead of searching for the entire document.
    mocks.activeTextEditor = fakeEditor({
      selection: "",
      documentText: "var first = 1;\nvar second = 2;",
    });

    await invoke();

    expect(openExternalUrls()).toEqual([INDEX_URL]);
  });

  it("does nothing, and says nothing, with no active editor", async () => {
    mocks.activeTextEditor = undefined;

    await invoke();

    expect(mocks.openExternal).not.toHaveBeenCalled();
    expect(vscode.window.showErrorMessage).not.toHaveBeenCalled();
    expect(vscode.window.showWarningMessage).not.toHaveBeenCalled();
    expect(vscode.window.showInformationMessage).not.toHaveBeenCalled();
  });
});

function extensionContext(): vscode.ExtensionContext {
  return {
    extension: { packageJSON: { version: "0.3.2" } },
    extensionPath: process.cwd(),
    subscriptions: [],
  } as unknown as vscode.ExtensionContext;
}

/** Runs the command the way the palette does: the handler activate registered. */
async function invoke(): Promise<void> {
  activate(extensionContext());
  await registeredHandler(PRODUCT_IDENTITY.commandIds.showUnityApiReference)();
}

function registeredHandler(commandId: string): () => Promise<void> {
  const calls = mocks.registerCommand.mock.calls as unknown as Array<
    [string, () => Promise<void>]
  >;
  const registrations = calls.filter(([id]) => id === commandId);
  // Neither zero nor two registrations can be told apart from a stale capture
  // later on, so fail here with the id in the message.
  if (registrations.length !== 1) {
    throw new Error(
      `expected one registration of ${commandId}, got ${registrations.length}`,
    );
  }
  return registrations[0][1];
}

function openExternalUrls(): string[] {
  const calls = mocks.openExternal.mock.calls as unknown as Array<[unknown]>;
  // `Uri.parse` is mocked as an object whose `toString` is the URL, so the
  // assertion reads the value the command actually handed to `openExternal`.
  return calls.map(([uri]) => String(uri));
}

interface FakePosition {
  readonly line: number;
  readonly character: number;
}

interface FakeRange {
  readonly start: FakePosition;
  readonly end: FakePosition;
}

interface FakeEditor {
  readonly selection: FakeRange;
  readonly document: {
    getText(range?: FakeRange): string;
    getWordRangeAtPosition(position: FakePosition): FakeRange | undefined;
  };
}

/**
 * Models the two document reads the command makes, with `vscode.TextDocument`
 * semantics: no range returns the whole document, which is what the reference's
 * `getText(wordRange)` reads when the cursor is not on a word.
 */
function fakeEditor(options: {
  readonly selection: string;
  readonly word?: string;
  readonly documentText?: string;
}): FakeEditor {
  const start: FakePosition = { line: 0, character: 0 };
  const selection: FakeRange = {
    start,
    end: { line: 0, character: options.selection.length },
  };
  const wordRange: FakeRange | undefined =
    options.word === undefined
      ? undefined
      : { start, end: { line: 0, character: options.word.length } };
  const texts = new Map<FakeRange, string>([[selection, options.selection]]);
  if (wordRange) {
    texts.set(wordRange, options.word ?? "");
  }

  return {
    selection,
    document: {
      getText: (range?: FakeRange) =>
        range ? (texts.get(range) ?? "") : (options.documentText ?? ""),
      // The word is returned only for the selection's own start, so a handler
      // that reads the word somewhere else falls through to the whole document.
      getWordRangeAtPosition: (position: FakePosition) =>
        position === start ? wordRange : undefined,
    },
  };
}
