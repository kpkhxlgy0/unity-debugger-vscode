import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { LogOutputChannel } from "vscode";

/**
 * The home directory the module under test reads. `os.homedir()` is mocked
 * rather than injected: what a user path looks like is the boundary's own
 * business, and pinning it here keeps the leak assertions the same on every
 * machine instead of depending on who runs the suite.
 */
const mocks = vi.hoisted(() => ({
  homeDirectory: "C:\\Users\\Admin",
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

vi.mock("node:os", async (importOriginal) => {
  const actual = await importOriginal<typeof import("node:os")>();
  const homedir = (): string => mocks.homeDirectory;
  // Everything else the extension reaches for (`hostname`, `tmpdir`) still
  // comes from the real module.
  return { ...actual, default: { ...actual, homedir }, homedir };
});

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
import {
  COPY_DIAGNOSTICS_COMMAND,
  NO_SANITIZED_DIAGNOSTICS,
  OPEN_LOGS_COMMAND,
} from "../../extension/src/diagnostics.js";
import { activate } from "../../extension/src/extension.js";
import {
  createOutputChannel,
  type SanitizedOutputChannel,
} from "../../extension/src/outputChannel.js";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";

type Mocked = ReturnType<typeof vi.fn>;

const temporaryDirectories: string[] = [];

beforeEach(() => {
  // `restoreMocks` alone does not reset these mocks' call history, so a failure
  // in one test would leak into the next one's call counts.
  vi.clearAllMocks();
  mocks.homeDirectory = "C:\\Users\\Admin";
  mocks.createOutputChannel.mockReturnValue(mocks.channel);
});

afterEach(() => {
  vi.unstubAllEnvs();
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { force: true, recursive: true });
  }
});

describe("createOutputChannel", () => {
  it("creates a log channel named after the product", () => {
    const factory = vi.fn((): LogOutputChannel => fakeLogChannel());

    createOutputChannel(factory);

    // `{ log: true }` is what makes this a LogOutputChannel with level methods
    // instead of a plain OutputChannel, and the name is the product's rather
    // than the reference's bare "Unity", so the two extensions' channels stay
    // distinguishable when both are installed.
    expect(factory).toHaveBeenCalledWith("Unity Debugger Pure", {
      log: true,
    });
    expect(PRODUCT_IDENTITY.displayName).toBe("Unity Debugger Pure");
  });

  it("returns the log channel so its level methods are reachable", () => {
    const channel = fakeLogChannel();

    expect(createOutputChannel(() => channel)).toBe(channel);
  });
});

describe("writeSanitized", () => {
  it("drops lines that would leak a user path", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("attached to C:\\Users\\Admin\\proj");

    // Refused, not redacted and not echoed: nothing about the offending line
    // reaches the channel at any level, because repeating it — even to say it
    // was refused — would leak the same path.
    expect(mocks.channel.info).not.toHaveBeenCalled();
    expect(mocks.channel.warn).not.toHaveBeenCalled();
    expect(mocks.channel.error).not.toHaveBeenCalled();
    expect(mocks.channel.debug).not.toHaveBeenCalled();
    expect(mocks.channel.trace).not.toHaveBeenCalled();
  });

  it("passes through a sanitized line", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("event=attach");

    expect(mocks.channel.info).toHaveBeenCalledWith("event=attach");
  });

  it("recognises the home directory the way the adapter's redactor does", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // `PathRedactor` matches the user profile with `[\\/]` and
    // `RegexOptions.IgnoreCase`, so the same path spelled with the other
    // separator or different casing is the same leak and must not slip past.
    channel.writeSanitized("opened c:/users/admin/appdata/local/logs");

    expect(mocks.channel.info).not.toHaveBeenCalled();
  });

  it("does not read a whole drive as the user's own directory", () => {
    // `C:` (or `/`) names no user. Matching it would refuse every line
    // containing a separator, which loses all diagnostics rather than one
    // leaking line.
    mocks.homeDirectory = "C:\\";
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("opened C:\\proj\\logs");

    expect(loggedLines()).toEqual(["opened C:\\proj\\logs"]);
  });

  it("takes the user path from the home directory rather than a literal", () => {
    mocks.homeDirectory = "D:\\profiles\\Somebody";
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("opened D:\\profiles\\Somebody\\AppData\\logs");
    // A path that is not this user's home is not this boundary's business: the
    // producer's redactor owns the workspace path, which a channel that knows
    // only its factory cannot see.
    channel.writeSanitized("attached to C:\\Users\\Admin\\proj");

    expect(loggedLines()).toEqual([
      "attached to C:\\Users\\Admin\\proj",
    ]);
  });
});

describe("the diagnostics commands", () => {
  it("opens the log directory and logs a path-free event line", async () => {
    const localApplicationData = temporaryDirectory();
    vi.stubEnv("LOCALAPPDATA", localApplicationData);
    activate(extensionContext());

    await invokeCommand(OPEN_LOGS_COMMAND);

    expect(vscode.env.openExternal).toHaveBeenCalledWith({
      fsPath: logDirectory(localApplicationData),
    });
    // The directory the command hands to the shell is an absolute user path,
    // so the only line it may contribute names the event and nothing else.
    expect(loggedLines()).toEqual(["event=diagnostics.openLogs"]);
  });

  it("records that there was nothing to open", async () => {
    vi.stubEnv("LOCALAPPDATA", "");
    activate(extensionContext());

    await invokeCommand(OPEN_LOGS_COMMAND);

    expect(vscode.env.openExternal).not.toHaveBeenCalled();
    expect(vscode.window.showInformationMessage).toHaveBeenCalledWith(
      NO_SANITIZED_DIAGNOSTICS,
    );
    expect(loggedLines()).toEqual(["event=diagnostics.openLogs.unavailable"]);
  });

  it("copies the latest sanitized report and logs the event", async () => {
    const localApplicationData = temporaryDirectory();
    vi.stubEnv("LOCALAPPDATA", localApplicationData);
    const report = "event=adapter.exit exitCode=0\n";
    writeReport(localApplicationData, report);
    activate(extensionContext());

    await invokeCommand(COPY_DIAGNOSTICS_COMMAND);

    expect(vscode.env.clipboard.writeText).toHaveBeenCalledWith(report);
    expect(loggedLines()).toEqual(["event=diagnostics.copyDiagnostics"]);
  });

  it("copies the status instead of a report and logs which happened", async () => {
    vi.stubEnv("LOCALAPPDATA", temporaryDirectory());
    activate(extensionContext());

    await invokeCommand(COPY_DIAGNOSTICS_COMMAND);

    expect(vscode.env.clipboard.writeText).toHaveBeenCalledWith(
      NO_SANITIZED_DIAGNOSTICS,
    );
    expect(loggedLines()).toEqual([
      "event=diagnostics.copyDiagnostics.unavailable",
    ]);
  });

  it("keeps the guard on the channel the activated extension holds", async () => {
    const localApplicationData = temporaryDirectory();
    vi.stubEnv("LOCALAPPDATA", localApplicationData);
    const context = extensionContext();
    activate(context);

    await invokeCommand(OPEN_LOGS_COMMAND);
    // Written through the channel `activate()` registered, not a fresh one, so
    // this also covers the subscription wiring the channel depends on.
    activatedChannel(context).writeSanitized(
      `opened ${logDirectory(localApplicationData)}`,
    );

    expect(loggedLines()).toEqual(["event=diagnostics.openLogs"]);
  });
});

/** A partial fake: the level methods a LogOutputChannel adds, plus its base. */
function fakeLogChannel(): LogOutputChannel {
  return {
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
  } as unknown as LogOutputChannel;
}

/** Every line the channel has written at info level, in order. */
function loggedLines(): unknown[] {
  const calls = mocks.channel.info.mock.calls as unknown as string[][];
  return calls.map((call) => call[0]);
}

function logDirectory(localApplicationData: string): string {
  return path.resolve(
    localApplicationData,
    PRODUCT_IDENTITY.diagnosticsDirectoryName,
    "logs",
  );
}

/** Writes one allowlisted report into the directory the commands read. */
function writeReport(localApplicationData: string, contents: string): void {
  const directory = logDirectory(localApplicationData);
  mkdirSync(directory, { recursive: true });
  writeFileSync(path.join(directory, "adapter-20261016T000000000Z-1.log"), contents);
}

function temporaryDirectory(): string {
  const directory = mkdtempSync(path.join(tmpdir(), "unity-debugger-pure-"));
  temporaryDirectories.push(directory);
  return directory;
}

function extensionContext(): vscode.ExtensionContext {
  return {
    extension: { packageJSON: { version: "0.3.2" } },
    extensionPath: temporaryDirectory(),
    subscriptions: [],
  } as unknown as vscode.ExtensionContext;
}

async function invokeCommand(id: string): Promise<void> {
  const calls = (vscode.commands.registerCommand as unknown as Mocked).mock
    .calls as unknown as Array<[string, () => Promise<void>]>;
  const registration = calls.find(([commandId]) => commandId === id);
  if (!registration) {
    throw new Error(`activate() did not register ${id}.`);
  }
  await registration[1]();
}

function activatedChannel(
  context: vscode.ExtensionContext,
): SanitizedOutputChannel {
  const channel = context.subscriptions.find(isSanitizedChannel);
  if (!channel) {
    throw new Error(
      "activate() did not register the sanitized output channel.",
    );
  }
  return channel;
}

function isSanitizedChannel(
  subscription: vscode.Disposable,
): subscription is SanitizedOutputChannel {
  return (
    typeof (subscription as Partial<SanitizedOutputChannel>)
      .writeSanitized === "function"
  );
}
