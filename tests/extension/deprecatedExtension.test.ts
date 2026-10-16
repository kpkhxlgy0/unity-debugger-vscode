import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  /** The extension the registry finds for every lookup, or none. */
  deprecated: undefined as unknown,
  lookedUp: [] as string[],
  showWarningMessage: vi.fn(),
}));

vi.mock("vscode", () => ({
  Uri: { file: (value: string) => ({ fsPath: value }) },
  commands: { registerCommand: vi.fn(() => ({ dispose: () => {} })) },
  debug: {
    breakpoints: [],
    onDidTerminateDebugSession: vi.fn(() => ({ dispose: () => {} })),
    registerDebugAdapterDescriptorFactory: vi.fn(() => ({
      dispose: () => {},
    })),
    registerDebugConfigurationProvider: vi.fn(() => ({
      dispose: () => {},
    })),
    startDebugging: vi.fn(async () => true),
  },
  env: { clipboard: { writeText: vi.fn() }, openExternal: vi.fn() },
  extensions: {
    // The lookup happens when activation runs, so this has to be a live read of
    // mutable state rather than a value captured here.
    getExtension: (extensionId: string) => {
      mocks.lookedUp.push(extensionId);
      return mocks.deprecated;
    },
  },
  window: {
    createOutputChannel: vi.fn(() => ({ info: vi.fn(), warn: vi.fn() })),
    createQuickPick: vi.fn(),
    showErrorMessage: vi.fn(),
    showInformationMessage: vi.fn(),
    showQuickPick: vi.fn(),
    showWarningMessage: mocks.showWarningMessage,
  },
  workspace: {
    getConfiguration: vi.fn(),
    getWorkspaceFolder: vi.fn(),
    workspaceFolders: undefined,
  },
}));

import * as vscode from "vscode";
import {
  DEPRECATED_EXTENSION_WARNING,
  warnAboutDeprecatedExtensions,
  type DeprecatedExtensionLike,
} from "../../extension/src/deprecatedExtension.js";
import { activate } from "../../extension/src/extension.js";

/**
 * The deprecated extension's display name is read out of its manifest and
 * interpolated into a user-visible sentence, so the fake carries a real one:
 * without it the message reads "The undefined extension is deprecated …", and
 * a mocked sink would agree with that.
 */
const deprecatedExtension: DeprecatedExtensionLike = {
  isActive: true,
  packageJSON: { displayName: "Debugger for Unity" },
};

const WARNING_SENTENCE =
  "The Debugger for Unity extension is deprecated and incompatible " +
  "with the Unity Debugger Pure extension. Please uninstall or disable it.";

beforeEach(() => {
  // `restoreMocks` alone does not reset these mocks' call history, so a failure
  // in one test would leak into the next one's call counts.
  vi.clearAllMocks();
  mocks.deprecated = undefined;
  mocks.lookedUp.length = 0;
});

describe("warnAboutDeprecatedExtensions", () => {
  it("asks for the deprecated extension by its marketplace id", () => {
    const extensions = registryOf(undefined);

    warnAboutDeprecatedExtensions({
      extensions,
      showWarningMessage: vi.fn(),
    });

    // `unity.unity-debug` is the deprecated "Debugger for Unity" — a third
    // extension, distinct from `local.vstuc`, which this work item treats as
    // the reference and expects to coexist with.
    expect(extensions.getExtension).toHaveBeenCalledWith(
      "unity.unity-debug",
    );
  });

  it("stays silent when the deprecated extension is not installed", () => {
    const showWarningMessage = vi.fn();

    warnAboutDeprecatedExtensions({
      extensions: registryOf(undefined),
      showWarningMessage,
    });

    expect(showWarningMessage).not.toHaveBeenCalled();
  });

  it("stays silent while it is installed but not active", () => {
    // The reference returns on `!t.isActive` as well as on an absent lookup, so
    // a merely installed extension raises nothing. Warning on install alone
    // would fire for every user who ever had it and never enabled it.
    const showWarningMessage = vi.fn();

    warnAboutDeprecatedExtensions({
      extensions: registryOf({ ...deprecatedExtension, isActive: false }),
      showWarningMessage,
    });

    expect(showWarningMessage).not.toHaveBeenCalled();
  });

  it("names both extensions once when it is active", () => {
    const showWarningMessage = vi.fn();

    warnAboutDeprecatedExtensions({
      extensions: registryOf(deprecatedExtension),
      showWarningMessage,
    });

    expect(showWarningMessage).toHaveBeenCalledTimes(1);
    // `{0}` is the deprecated extension's own `displayName` and `{1}` is ours.
    // Ours reads "Unity Debugger Pure" where the reference's reads "Unity":
    // the two can be installed together, so a message naming only "Unity"
    // would not say which one to keep.
    expect(showWarningMessage).toHaveBeenCalledWith(WARNING_SENTENCE);
  });

  it("sends plain text, not the reference's markdown source", () => {
    // The reference interpolates `MarkdownString.value` — the markdown *source*
    // — into a plain-text format, so what the user sees is the literal
    // `[Debugger for Unity](command:extension.open?["unity.unity-debug"])`.
    // See the module for why the link was dropped rather than made to work.
    const showWarningMessage = vi.fn();

    warnAboutDeprecatedExtensions({
      extensions: registryOf(deprecatedExtension),
      showWarningMessage,
    });

    const [message] = showWarningMessage.mock.calls[0] as [unknown];
    expect(typeof message).toBe("string");
    expect(message).not.toContain("command:extension.open");
    expect((message as string).startsWith("[")).toBe(false);
  });
});

describe("the message template", () => {
  it("holds the reference's text verbatim", () => {
    // Split at a different point from the production literal, so a typo in
    // either one is caught instead of both drifting together.
    expect(DEPRECATED_EXTENSION_WARNING).toBe(
      "The {0} extension is deprecated and incompatible with the " +
        "{1} extension. Please uninstall or disable it.",
    );
  });
});

describe("activation", () => {
  it("warns when the deprecated extension is already active", () => {
    mocks.deprecated = deprecatedExtension;

    activate(extensionContext());

    expect(mocks.lookedUp).toEqual(["unity.unity-debug"]);
    expect(mocks.showWarningMessage).toHaveBeenCalledWith(WARNING_SENTENCE);
  });

  it("stays silent when the registry has no such extension", () => {
    // The branch every other double in this repository exercises: activation
    // must look the id up exactly once and warn about nothing.
    mocks.deprecated = undefined;

    activate(extensionContext());

    expect(mocks.lookedUp).toEqual(["unity.unity-debug"]);
    expect(mocks.showWarningMessage).not.toHaveBeenCalled();
  });
});

/**
 * A registry that answers every lookup with `extension`, plus its spy so the
 * requested id can be asserted — the id is the whole contract.
 *
 * The return type is left to inference: `ReturnType<typeof vi.fn>` erases the
 * call signature, and this object has to stay assignable to
 * `ExtensionRegistryLike`.
 */
function registryOf(extension: DeprecatedExtensionLike | undefined) {
  return { getExtension: vi.fn((_extensionId: string) => extension) };
}

function extensionContext(): vscode.ExtensionContext {
  return {
    extension: { packageJSON: { version: "0.3.2" } },
    extensionPath: process.cwd(),
    subscriptions: [],
  } as unknown as vscode.ExtensionContext;
}
