import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  breakpoints: [] as Array<{ functionName?: string }>,
  channel: {
    append: vi.fn(),
    appendLine: vi.fn(),
    clear: vi.fn(),
    debug: vi.fn(),
    dispose: vi.fn(),
    error: vi.fn(),
    hide: vi.fn(),
    info: vi.fn(),
    replace: vi.fn(),
    show: vi.fn(),
    trace: vi.fn(),
    warn: vi.fn(),
  },
  createOutputChannel: vi.fn(),
}));

vi.mock("vscode", () => ({
  DebugAdapterExecutable: class DebugAdapterExecutable {
    public constructor(
      public readonly command: string,
      public readonly args: readonly string[],
      public readonly options: unknown,
    ) {}
  },
  Uri: { file: (value: string) => ({ fsPath: value }) },
  commands: {
    registerCommand: vi.fn(() => ({ dispose: () => {} })),
  },
  debug: {
    breakpoints: mocks.breakpoints,
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
    // `activate()` reads the registry for the deprecated-extension warning, so
    // every double that runs activation has to provide one. Absent here, which
    // is the branch that warns about nothing.
    getExtension: () => undefined,
  },
  window: {
    createOutputChannel: mocks.createOutputChannel,
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
import {
  FUNCTION_BREAKPOINT_WARNING,
  shouldWarnAboutFunctionBreakpoints,
} from "../../extension/src/functionBreakpointWarning.js";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";

type Mocked = ReturnType<typeof vi.fn>;

interface DescriptorFactory {
  createDebugAdapterDescriptor(): unknown;
}

/**
 * The packaged adapter exists only for Windows x64, which the launcher enforces
 * itself, so the two tests that need it to succeed cannot run elsewhere.
 */
const windowsX64 = process.platform === "win32" && process.arch === "x64";

const registerDescriptorFactory = vscode.debug
  .registerDebugAdapterDescriptorFactory as unknown as Mocked;
const temporaryDirectories: string[] = [];

// A SourceBreakpoint carries no `functionName`; through the structural view the
// predicate takes, it is a breakpoint whose function name is absent.
const sourceBreakpoint: { functionName?: string } = {};

beforeEach(() => {
  // `restoreMocks` alone does not reset these mocks' call history, so a failure
  // in one test would leak into the next one's call counts.
  vi.clearAllMocks();
  mocks.breakpoints.length = 0;
  mocks.createOutputChannel.mockReturnValue(mocks.channel);
});

afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { force: true, recursive: true });
  }
});

describe("shouldWarnAboutFunctionBreakpoints", () => {
  it("warns only when a function breakpoint exists", () => {
    expect(
      shouldWarnAboutFunctionBreakpoints([sourceBreakpoint]),
    ).toBe(false);
    expect(
      shouldWarnAboutFunctionBreakpoints([{ functionName: "Foo" }]),
    ).toBe(true);
  });

  it("ignores a function breakpoint with an empty name", () => {
    expect(
      shouldWarnAboutFunctionBreakpoints([{ functionName: "" }]),
    ).toBe(false);
  });

  it("warns when one of several breakpoints is a function breakpoint", () => {
    expect(
      shouldWarnAboutFunctionBreakpoints([
        sourceBreakpoint,
        { functionName: "Bar" },
      ]),
    ).toBe(true);
  });

  it("stays silent when there are no breakpoints", () => {
    expect(shouldWarnAboutFunctionBreakpoints([])).toBe(false);
  });

  it("holds the reference's text verbatim", () => {
    expect(FUNCTION_BREAKPOINT_WARNING).toBe(
      "You are using function breakpoints. Even if they are fully " +
        "supported, they have a major negative impact on debugging " +
        "performance. Use them only when necessary.",
    );
  });
});

describe("the descriptor factory", () => {
  it.skipIf(!windowsX64)(
    "warns through the log channel once the launcher succeeds",
    () => {
      mocks.breakpoints.push({ functionName: "Foo" });

      activate(extensionContext(packagedAdapterDirectory()));
      const descriptor =
        capturedDescriptorFactory().createDebugAdapterDescriptor();

      expect(mocks.channel.warn).toHaveBeenCalledWith(
        FUNCTION_BREAKPOINT_WARNING,
      );
      expect(descriptor).toBeInstanceOf(vscode.DebugAdapterExecutable);
    },
  );

  it.skipIf(!windowsX64)(
    "stays silent when every breakpoint is a source breakpoint",
    () => {
      mocks.breakpoints.push(sourceBreakpoint);

      activate(extensionContext(packagedAdapterDirectory()));
      capturedDescriptorFactory().createDebugAdapterDescriptor();

      expect(mocks.channel.warn).not.toHaveBeenCalled();
    },
  );

  it("stays silent when the launcher fails", () => {
    mocks.breakpoints.push({ functionName: "Foo" });

    activate(extensionContext(temporaryDirectory()));
    const descriptor =
      capturedDescriptorFactory().createDebugAdapterDescriptor();

    expect(descriptor).toBeUndefined();
    expect(vscode.window.showErrorMessage).toHaveBeenCalledTimes(1);
    expect(mocks.channel.warn).not.toHaveBeenCalled();
  });
});

function temporaryDirectory(): string {
  const directory = mkdtempSync(path.join(tmpdir(), "unity-debugger-pure-"));
  temporaryDirectories.push(directory);
  return directory;
}

/** A directory whose packaged Windows x64 adapter exists, so it launches. */
function packagedAdapterDirectory(): string {
  const directory = temporaryDirectory();
  const adapterDirectory = path.join(directory, "adapter", "win32-x64");
  mkdirSync(adapterDirectory, { recursive: true });
  writeFileSync(
    path.join(adapterDirectory, PRODUCT_IDENTITY.adapterExecutable),
    "",
  );
  return directory;
}

function extensionContext(extensionPath: string): vscode.ExtensionContext {
  return {
    extension: { packageJSON: { version: "0.3.2" } },
    extensionPath,
    subscriptions: [],
  } as unknown as vscode.ExtensionContext;
}

function capturedDescriptorFactory(): DescriptorFactory {
  const calls = registerDescriptorFactory.mock.calls as unknown as Array<
    [unknown, DescriptorFactory]
  >;
  // Each test activates once, so anything else means the capture is reading a
  // stale registration.
  expect(calls).toHaveLength(1);
  return calls[0][1];
}
