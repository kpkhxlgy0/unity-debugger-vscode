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
  homedirFailure: null as Error | null,
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
  const homedir = (): string => {
    // Real hosts can fail to name a home; the flag lets one test model that
    // without disturbing the rest.
    if (mocks.homedirFailure) {
      throw mocks.homedirFailure;
    }
    return mocks.homeDirectory;
  };
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
import {
  CHANNEL_REFUSED_LINE,
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
  mocks.homedirFailure = null;
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

    // Refused, not redacted and not echoed: the offending line reaches no level
    // method — and not `appendLine`, `append` or `replace` either, so a refusal
    // implemented with the channel's non-level writes cannot pass this — because
    // repeating the line, even to say it was refused, would leak the same path.
    // What does reach the channel is the path-free marker, which is what keeps
    // the refusal itself observable. The exact array is the assertion: the only
    // line written is the marker, and the path is nowhere in it.
    expect(loggedLines()).toEqual([CHANNEL_REFUSED_LINE]);
    expect(mocks.channel.warn).not.toHaveBeenCalled();
    expect(mocks.channel.error).not.toHaveBeenCalled();
    expect(mocks.channel.debug).not.toHaveBeenCalled();
    expect(mocks.channel.trace).not.toHaveBeenCalled();
    expect(mocks.channel.append).not.toHaveBeenCalled();
    expect(mocks.channel.appendLine).not.toHaveBeenCalled();
    expect(mocks.channel.replace).not.toHaveBeenCalled();
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
    // separator and a different case is the same leak and must not slip past:
    // the mocked home is `C:\Users\Admin` and this line is its lowercase
    // forward-slash spelling, so it needs both folds to be recognised.
    channel.writeSanitized("opened c:/users/admin/appdata/local/logs");

    expect(loggedLines()).toEqual([CHANNEL_REFUSED_LINE]);
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

  it("does not refuse every line when the home names no user", () => {
    // The POSIX spelling of the same degenerate home: `/` normalizes to the
    // empty string, and an empty home must match nothing rather than
    // everything. `""` reaches the same branch directly.
    const channel = createOutputChannel(mocks.createOutputChannel);

    mocks.homeDirectory = "/";
    channel.writeSanitized("opened /srv/proj/logs");
    mocks.homeDirectory = "";
    channel.writeSanitized("event=attach");

    expect(loggedLines()).toEqual([
      "opened /srv/proj/logs",
      "event=attach",
    ]);
  });

  it("writes the line when the home directory cannot be read", () => {
    // `os.homedir()` can throw, and this guard runs inside a command handler:
    // letting the throw escape would suppress the message the user is meant to
    // see and reject the command. An unreadable home is therefore "nothing to
    // match" — the line is written, with the producer's redaction still the
    // authority — rather than a reason to drop the whole channel's output.
    mocks.homedirFailure = new Error("no home directory");
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("opened C:\\Users\\Admin\\logs");

    expect(loggedLines()).toEqual(["opened C:\\Users\\Admin\\logs"]);
  });

  it("refuses at a path boundary, not a shared name prefix", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // `C:\Users\Administrator` and `C:\Users\Admin2` merely *contain*
    // `C:\Users\Admin`. `PathRedactor` may match them because it rewrites what
    // it matches; this guard drops, so an over-broad match would cost a
    // diagnostic that leaks nothing. `Admin-old` is here for the same reason
    // from the other side: `-` continues a segment name, so this line is
    // written too, and the boundary is not "a path, then punctuation".
    channel.writeSanitized("opened C:\\Users\\Administrator\\logs");
    channel.writeSanitized("opened C:\\Users\\Admin2\\logs");
    channel.writeSanitized("opened C:\\Users\\Admin-old\\logs");
    channel.writeSanitized("opened C:\\Users\\Admin\\logs");

    expect(loggedLines()).toEqual([
      "opened C:\\Users\\Administrator\\logs",
      "opened C:\\Users\\Admin2\\logs",
      "opened C:\\Users\\Admin-old\\logs",
      CHANNEL_REFUSED_LINE,
    ]);
  });

  it("refuses a line whose home path ends at a quote", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // A path quoted inside a message is a leak the moment the quote closes it.
    // A guard that tests *terminators* — `/` or the end of the string — reads
    // `"` as "the path ended here" and writes this line; the path ends at the
    // quote, and the quote is not a character a segment name continues with.
    channel.writeSanitized('opened "C:\\Users\\Admin"');

    expect(loggedLines()).toEqual([CHANNEL_REFUSED_LINE]);
  });

  it("refuses a line whose home path ends at a field separator", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // `event=<name> key=value key=value` is the adapter's own line shape, so a
    // producer that prints the home as a value and carries on with the next
    // field is the forgetful producer this backstop is for. The space ends the
    // path exactly as a separator would; a terminator test calls the line
    // path-free and writes it.
    channel.writeSanitized("home=C:\\Users\\Admin attempt=1");

    expect(loggedLines()).toEqual([CHANNEL_REFUSED_LINE]);
  });

  it("refuses a home path ended by any punctuation or whitespace", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // The same boundary from the other end: `;` and `)` end a path inside a
    // sentence, and the `\r` a file read leaves behind ends one at the end of
    // a line. None of the three is `/`, so all three are written by a guard
    // that tests terminators instead of continuations.
    channel.writeSanitized("opened C:\\Users\\Admin;");
    channel.writeSanitized("(C:\\Users\\Admin)");
    channel.writeSanitized("opened C:\\Users\\Admin\r");

    expect(loggedLines()).toEqual([
      CHANNEL_REFUSED_LINE,
      CHANNEL_REFUSED_LINE,
      CHANNEL_REFUSED_LINE,
    ]);
  });

  it("refuses a home path that ends the line", () => {
    const channel = createOutputChannel(mocks.createOutputChannel);

    // The plainest boundary of all, and the only shape the suite did not
    // already put on the wire: every other refused line continues after the
    // home with a separator. End of line has to refuse on its own, because the
    // alternative spelling of this predicate — testing the terminator
    // characters that appear *in* a line — is easy to write in a way that
    // forgets the empty string, and a line that ends at the home leaks exactly
    // as much as one that continues with `/logs`.
    channel.writeSanitized("event=attach home=C:\\Users\\Admin");

    expect(loggedLines()).toEqual([CHANNEL_REFUSED_LINE]);
  });

  it("takes the user path from the home directory rather than a literal", () => {
    mocks.homeDirectory = "D:\\profiles\\Somebody";
    const channel = createOutputChannel(mocks.createOutputChannel);

    channel.writeSanitized("opened D:\\profiles\\Somebody\\AppData\\logs");
    // A path that is not this user's home is not this boundary's business: the
    // producer's redactor owns the workspace path, which a channel that knows
    // only its factory cannot see. Re-pointing the mock moved the refusal with
    // it — the first line is refused, the second is not — which is what shows
    // the comparison reads the home directory rather than a literal.
    channel.writeSanitized("attached to C:\\Users\\Admin\\proj");

    expect(loggedLines()).toEqual([
      CHANNEL_REFUSED_LINE,
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
    // this also covers the subscription wiring the channel depends on. The
    // leaking line is built from the *mocked* home — `opened
    // C:\Users\Admin\logs` — and not from the real log directory above: the
    // guard compares against that mocked home, and a real temp path contains it
    // only on a machine whose profile happens to be the mocked name. Built from
    // `temporaryDirectory()` this assertion passed locally and failed CI, where
    // `%TEMP%` is under `runneradmin`; the temp path stays where it belongs, in
    // the `LOCALAPPDATA` the command must really resolve.
    activatedChannel(context).writeSanitized(
      `opened ${mocks.homeDirectory}\\logs`,
    );

    expect(loggedLines()).toEqual([
      "event=diagnostics.openLogs",
      CHANNEL_REFUSED_LINE,
    ]);
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
