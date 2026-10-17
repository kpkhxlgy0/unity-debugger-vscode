import fs from "node:fs";
import { isIPv4 } from "node:net";
import { describe, expect, it, vi } from "vitest";
import {
  DebugConfigurationProvider,
  type ConfigurationUi,
} from "../../extension/src/debugConfigurationProvider.js";
import type { EditorCandidate } from "../../extension/src/model.js";

/**
 * The attach configuration is a two-layer contract: `DebugConfigurationProvider`
 * produces it on this side, and the C# adapter's `AttachArguments.Parse`
 * consumes it on the other. The defect this file is the net for was a
 * two-copies-disagree class — the two layers disagreed about what an empty
 * `logFile` means, so the provider put a value on the wire that the adapter
 * silently dropped — and the net is therefore anchored to the adapter's own
 * source rather than to a second copy of its rules typed out here.
 *
 * What it proves and what it does not, stated rather than implied: it never
 * executes `AttachArguments.Parse`, which needs .NET. It fails when a key the
 * adapter reads is renamed on either side, and when a value's shape stops
 * satisfying the rules the adapter states; it cannot fail on a semantic change
 * inside the adapter's parser.
 */
const ADAPTER_ATTACH_ARGUMENTS_SOURCE =
  "adapter/src/UnityDebugger.Adapter/Dap/AttachArguments.cs";

/**
 * The private keys `AttachArguments.Parse` reads, taken from its source. A list
 * typed here would be a third copy of the contract and would agree with the
 * other two only until one of them moved.
 */
function adapterAttachKeys(): readonly string[] {
  const source = fs.readFileSync(ADAPTER_ATTACH_ARGUMENTS_SOURCE, "utf8");
  return [
    ...new Set(
      [...source.matchAll(/"(__[A-Za-z]+)"/g)].map((match) => match[1]),
    ),
  ];
}

/**
 * The one emitted key the adapter does not read, named rather than tolerated:
 * `__apiAttachRequestId` is how the extension's own session starter matches the
 * session VS Code started (`publicApi.ts`), so it never reaches the parser.
 * Naming it keeps "the adapter ignores this one" distinguishable from "nobody
 * checked".
 */
const EXTENSION_ONLY_KEYS = ["__apiAttachRequestId"];

const editor: EditorCandidate = {
  processId: 1234,
  projectName: "fixture",
  workspaceRoot: "H:\\fixture",
  host: "127.0.0.1",
  port: 56234,
  projectVersion: "2022.3.62t11",
  machine: "fixture-host",
  isBackground: false,
  source: "derived-port",
};

/** A Player carries no process id and no Editor version, by design. */
const player: EditorCandidate = {
  ...editor,
  processId: 0,
  projectVersion: "",
  host: "192.168.1.77",
  source: "player-advertisement",
};

const folder = { uri: { fsPath: "H:\\fixture" } };
const attach = {
  name: "Attach",
  type: "unity-debugger-pure",
  request: "attach",
};

interface GoldenRow {
  readonly label: string;
  readonly configuration: Readonly<Record<string, unknown>>;
  readonly candidates: readonly EditorCandidate[];
  readonly apiRequest: boolean;
  readonly keys: readonly string[];
}

const baseKeys = [
  "__host",
  "__port",
  "__processId",
  "__projectVersion",
  "__workspaceRoot",
];

const goldenRows: readonly GoldenRow[] = [
  {
    label: "a discovered Editor",
    configuration: attach,
    candidates: [editor],
    apiRequest: false,
    keys: baseKeys,
  },
  {
    label: "a discovered Player",
    configuration: attach,
    candidates: [player],
    apiRequest: false,
    keys: baseKeys,
  },
  {
    label: "an explicit endPoint",
    configuration: { ...attach, endPoint: "192.168.1.50:56234" },
    candidates: [],
    apiRequest: false,
    keys: baseKeys,
  },
  {
    label: "a log file",
    configuration: { ...attach, logFile: "H:\\logs\\unity.log" },
    candidates: [editor],
    apiRequest: false,
    keys: [...baseKeys, "__logFile"],
  },
  {
    label: "a trusted API attach request",
    configuration: { ...attach, __apiAttachRequestId: "request-1" },
    candidates: [editor],
    apiRequest: true,
    keys: [...baseKeys, "__apiAttachRequestId"],
  },
];

function ui(): ConfigurationUi {
  return {
    selectCandidate: vi.fn(async (candidates) => candidates[0]),
    showNoEditor: vi.fn(async () => undefined),
    showError: vi.fn(async () => undefined),
  };
}

/** The fields that cross to the adapter, as opposed to VS Code's own three. */
function privateKeys(emitted: Readonly<Record<string, unknown>>): string[] {
  return Object.keys(emitted)
    .filter((key) => key.startsWith("__"))
    .sort();
}

async function resolve(
  row: GoldenRow,
): Promise<Record<string, unknown> | undefined> {
  const provider = new DebugConfigurationProvider(
    { discover: async () => row.candidates },
    ui(),
    (currentFolder) => [currentFolder],
    row.apiRequest
      ? { consumeRequest: () => row.candidates[0] }
      : undefined,
  );

  const resolved = await provider.resolveDebugConfiguration(
    folder,
    row.configuration,
  );
  return resolved as unknown as Record<string, unknown> | undefined;
}

describe("the attach configuration the adapter receives", () => {
  it.each(goldenRows)(
    "emits the adapter's key set for $label",
    async (row) => {
      const emitted = (await resolve(row))!;

      expect(emitted).toBeDefined();
      // The three DAP keys VS Code itself requires, and then the private fields
      // the adapter reads. They are asserted apart because they answer to
      // different owners: VS Code validates the first three before the provider
      // ever runs, and the adapter is the only reader of the rest.
      expect(
        Object.keys(emitted)
          .filter((key) => !key.startsWith("__"))
          .sort(),
      ).toEqual(["name", "request", "type"]);
      expect(privateKeys(emitted)).toEqual([...row.keys].sort());
      for (const key of privateKeys(emitted)) {
        expect([...adapterAttachKeys(), ...EXTENSION_ONLY_KEYS]).toContain(
          key,
        );
      }
    },
  );

  it.each(goldenRows)(
    "emits values the adapter accepts for $label",
    async (row) => {
      const emitted = (await resolve(row))!;

      // Each rule below is one `AttachArguments.Parse` enforces, with the line
      // it enforces it at. The port is the one that matters most: the adapter
      // rejects anything outside 1..65535, and a candidate whose port cannot be
      // formed at all is the case the reference has a separate sentence for.
      expect(Number.isInteger(emitted.__processId)).toBe(true);
      expect(emitted.__processId as number).toBeGreaterThanOrEqual(0);
      expect(typeof emitted.__host).toBe("string");
      // `IPAddress.Parse` plus the canonical-form comparison: a short or
      // zero-padded literal is rejected rather than silently rewritten, and
      // `isIPv4` is strict about both.
      expect(isIPv4(emitted.__host as string)).toBe(true);
      expect(Number.isInteger(emitted.__port)).toBe(true);
      expect(emitted.__port as number).toBeGreaterThan(0);
      expect(emitted.__port as number).toBeLessThanOrEqual(65_535);
      expect(typeof emitted.__workspaceRoot).toBe("string");
      expect((emitted.__workspaceRoot as string).trim().length).toBeGreaterThan(
        0,
      );
      expect(typeof emitted.__projectVersion).toBe("string");
      if (emitted.__logFile !== undefined) {
        expect(typeof emitted.__logFile).toBe("string");
        expect((emitted.__logFile as string).trim().length).toBeGreaterThan(0);
      }
    },
  );

  it.each([
    ["absent", undefined, false],
    ["empty", "", false],
    ["blank", "   ", false],
    ["a tab", "\t", false],
    ["a path", "H:\\logs\\unity.log", true],
  ])(
    "agrees with the adapter about a %s logFile",
    async (_label, logFile, present) => {
      // `AttachArguments.ParseLogFile` takes a value only when it is not
      // `IsNullOrWhiteSpace`, and the provider drops it only when `trim()` is
      // empty. The two gates have to answer the same question the same way:
      // agreement is the assertion, in both directions, so a blank value that
      // starts being emitted fails here as loudly as a path that stops being
      // emitted.
      const emitted = (await resolve({
        label: "a log file row",
        configuration:
          logFile === undefined ? attach : { ...attach, logFile },
        candidates: [editor],
        apiRequest: false,
        keys: [],
      }))!;

      expect(Object.prototype.hasOwnProperty.call(emitted, "__logFile")).toBe(
        present,
      );
      if (present) {
        expect(emitted.__logFile).toBe(logFile);
      }
    },
  );

  it("never emits a version its own policy would not have accepted", async () => {
    // The provider's gate is the stricter of the two — it knows the version
    // policy, the adapter only knows the two accepted prefixes — so an
    // out-of-policy version is stopped here and never reaches the parser. What
    // the adapter can still see is the empty "unknown" sentinel, which its own
    // comment declares acceptable.
    const rows = await Promise.all(
      ["2022.3.62t11", "6000.0.50f1", ""].map((projectVersion) =>
        resolve({
          label: `version ${projectVersion}`,
          configuration: attach,
          candidates: [{ ...editor, projectVersion }],
          apiRequest: false,
          keys: [],
        }),
      ),
    );

    expect(rows.map((emitted) => emitted!.__projectVersion)).toEqual([
      "2022.3.62t11",
      "6000.0.50f1",
      "",
    ]);

    const rejected = await resolve({
      label: "out-of-policy version",
      configuration: attach,
      candidates: [{ ...editor, projectVersion: "2021.3.45f1" }],
      apiRequest: false,
      keys: [],
    });
    expect(rejected).toBeUndefined();
  });
});
