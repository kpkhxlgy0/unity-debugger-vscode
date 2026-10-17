import type dgram from "node:dgram";
import { EventEmitter } from "node:events";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";
import {
  EditorDiscovery,
  type EditorDiscoveryDependencies,
} from "../../extension/src/editorDiscovery.js";
import type { EditorCandidate } from "../../extension/src/model.js";
import { parsePlayerAdvertisement } from "../../extension/src/playerAdvertisement.js";
import { androidPlayerPacket, packet } from "./playerAdvertisement.test.js";

function dependencies(
  overrides: Partial<EditorDiscoveryDependencies> = {},
): EditorDiscoveryDependencies {
  return {
    readFile: async () => '{"process_id":1234}',
    readProjectVersion: async () => "2022.3.62t11",
    isProcessAlive: async (processId) => processId === 1234,
    collectAdvertisements: async () => [
      parsePlayerAdvertisement(packet),
    ],
    ...overrides,
  };
}

describe("EditorDiscovery", () => {
  it("leaves the single-use debugger port for the Adapter", async () => {
    let connectionPhase = "discovery";
    const acceptedPhases: string[] = [];
    let resolveAccepted: () => void = () => {};
    const accepted = new Promise<void>((resolve) => {
      resolveAccepted = resolve;
    });
    const server = net.createServer((socket) => {
      acceptedPhases.push(connectionPhase);
      socket.destroy();
      server.close();
      resolveAccepted();
    });
    await new Promise<void>((resolve, reject) => {
      server.once("error", reject);
      server.listen(0, "127.0.0.1", resolve);
    });

    try {
      const address = server.address();
      if (address === null || typeof address === "string") {
        throw new Error("Expected a TCP loopback address");
      }
      const discovery = new EditorDiscovery({
        readFile: async () => '{"process_id":1234}',
        readProjectVersion: async () => "2022.3.62t11",
        isProcessAlive: async () => true,
        collectAdvertisements: async () => [
          {
            ...parsePlayerAdvertisement(packet),
            debuggerPort: address.port,
          },
        ],
      });

      const [candidate] = await discovery.discover([
        "H:\\FixtureProject",
      ]);
      expect(candidate).toMatchObject({
        host: "127.0.0.1",
        port: address.port,
        source: "advertisement",
        machine: os.hostname(),
      });

      connectionPhase = "adapter";
      await Promise.all([
        connectToLoopback(address.port),
        accepted,
      ]);
      expect(acceptedPhases).toEqual(["adapter"]);
    } finally {
      if (server.listening) {
        await new Promise<void>((resolve) => server.close(() => resolve()));
      }
    }
  });

  it("returns a matching Editor on loopback only", async () => {
    const discovery = new EditorDiscovery(dependencies());

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.62t11",
        machine: os.hostname(),
        isBackground: false,
        source: "derived-port",
      },
    ]);
  });

  it("uses a matching advertised port but never its LAN address", async () => {
    const discovery = new EditorDiscovery(dependencies());

    const [candidate] = await discovery.discover([
      "H:\\FixtureProject",
    ]);

    expect(candidate).toMatchObject({
      host: "127.0.0.1",
      port: 56234,
      source: "advertisement",
      machine: os.hostname(),
    });
    expect(candidate?.host).not.toBe("192.168.1.20");
  });

  it("returns no candidate for a dead Editor process", async () => {
    const discovery = new EditorDiscovery(
      dependencies({ isProcessAlive: async () => false }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([]);
  });

  it("honors an explicit debug-disabled advertisement", async () => {
    const advertisement = parsePlayerAdvertisement(
      packet
        .replace("[Debug] 1", "[Debug] 0")
        .replace("[ProjectName] FixtureProject", "[ProjectName] fixture"),
    );
    const discovery = new EditorDiscovery(
      dependencies({ collectAdvertisements: async () => [advertisement] }),
    );

    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([]);
  });

  it.each([70_000, 56_234.5])(
    "ignores invalid advertised port %s and uses the derived port",
    async (debuggerPort) => {
    const advertisement = {
      ...parsePlayerAdvertisement(packet),
      projectName: "fixture",
      debuggerPort,
    };
    const discovery = new EditorDiscovery(
      dependencies({
        collectAdvertisements: async () => [advertisement],
      }),
    );

    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.62t11",
        machine: os.hostname(),
        isBackground: false,
        source: "derived-port",
      },
    ]);
    },
  );

  it("ignores malformed EditorInstance JSON", async () => {
    const discovery = new EditorDiscovery(
      dependencies({ readFile: async () => "not json" }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([]);
  });

  it("uses the derived Editor port without a matching advertisement", async () => {
    const discovery = new EditorDiscovery(dependencies());

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.62t11",
        machine: os.hostname(),
        isBackground: false,
        source: "derived-port",
      },
    ]);
  });

  it("falls back to the Editor version when ProjectVersion.txt is unknown", async () => {
    // A project may have no ProjectSettings/ProjectVersion.txt at all — the
    // acceptance project does not — while Library/EditorInstance.json records
    // the version the Editor actually runs.
    const discovery = new EditorDiscovery(
      dependencies({
        readFile: async () =>
          '{"process_id":1234,"version":"2022.3.23f1"}',
        readProjectVersion: async () => "",
      }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.23f1",
        machine: os.hostname(),
        isBackground: false,
        source: "derived-port",
      },
    ]);
  });

  it("prefers ProjectVersion.txt over the recorded Editor version", async () => {
    const discovery = new EditorDiscovery(
      dependencies({
        readFile: async () =>
          '{"process_id":1234,"version":"2022.3.23f1"}',
      }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.62t11",
        machine: os.hostname(),
        isBackground: false,
        source: "derived-port",
      },
    ]);
  });

  it.each([
    ["carries no version", '{"process_id":1234}'],
    ["carries a non-string version", '{"process_id":1234,"version":42}'],
  ])(
    "reports no version when EditorInstance.json %s and ProjectVersion.txt is unknown",
    async (_label, instanceJson) => {
      const discovery = new EditorDiscovery(
        dependencies({
          readFile: async () => instanceJson,
          readProjectVersion: async () => "",
        }),
      );

      const found = await discovery.discover(["H:\\fixture"]);

      expect(editorCandidates(found)).toMatchObject([
        { projectVersion: "" },
      ]);
    },
  );

  it("does not supply an EditorInstance version the policy cannot classify", async () => {
    // Library/EditorInstance.json may carry the value in the shape Unity uses
    // for m_EditorVersionWithRevision. The version policy cannot classify it,
    // and passing it through as the project version would block an attach that
    // an absent version allows, so the fallback degrades to "unknown".
    const discovery = new EditorDiscovery(
      dependencies({
        readFile: async () =>
          '{"process_id":1234,"version":"2022.3.62t11 (b1a0a2c)"}',
        readProjectVersion: async () => "",
      }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toMatchObject([
      { projectVersion: "" },
    ]);
  });

  it("keeps a ProjectVersion.txt version that carries a revision suffix", async () => {
    // The guard is a fallback-only contract. ProjectVersion.txt is the
    // authoritative declaration, so its value reaches the policy unchanged
    // and still blocks as malformed.
    const discovery = new EditorDiscovery(
      dependencies({
        readFile: async () => '{"process_id":1234}',
        readProjectVersion: async () => "2022.3.62t11 (b1a0a2c)",
      }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(editorCandidates(found)).toMatchObject([
      { projectVersion: "2022.3.62t11 (b1a0a2c)" },
    ]);
  });

  it("offers a player advertisement as its own target", async () => {
    const discovery = new EditorDiscovery({
      collectAdvertisements: async () => [
        {
          ip: "192.168.1.77",
          debuggerPort: 56_001,
          projectName: "Other",
          id: "p1",
          allowDebugging: true,
          machine: "samsung_SM-S9260@192.168.1.77",
        },
      ],
    });

    const found = await discovery.discover(["H:\\fixture"]);

    expect(found).toContainEqual(
      expect.objectContaining({
        processId: 0,
        projectVersion: "",
        host: "192.168.1.77",
        port: 56_001,
        isBackground: false,
        source: "player-advertisement",
        machine: "samsung_SM-S9260@192.168.1.77",
      }),
    );
  });

  it("does not offer a player that has not enabled debugging", async () => {
    const discovery = new EditorDiscovery(
      dependencies({
        collectAdvertisements: async () => [
          {
            ip: "192.168.1.77",
            debuggerPort: 56_001,
            projectName: "Other",
            id: "p1",
            allowDebugging: false,
            machine: "samsung_SM-S9260@192.168.1.77",
          },
        ],
      }),
    );

    const found = await discovery.discover(["H:\\fixture"]);

    expect(
      found.filter(
        (candidate) => candidate.source === "player-advertisement",
      ),
    ).toHaveLength(0);
    expect(found).toHaveLength(1);
  });

  it("keeps two players on different hosts that share a port", async () => {
    const discovery = new EditorDiscovery({
      collectAdvertisements: async () => [
        {
          ip: "192.168.1.77",
          debuggerPort: 56_001,
          projectName: "A",
          id: "p1",
          allowDebugging: true,
          machine: "samsung_SM-S9260@192.168.1.77",
        },
        {
          ip: "192.168.1.78",
          debuggerPort: 56_001,
          projectName: "B",
          id: "p2",
          allowDebugging: true,
          machine: "samsung_SM-S9260@192.168.1.78",
        },
      ],
    });

    const found = await discovery.discover(["H:\\fixture"]);

    const players = found.filter(
      (candidate) => candidate.source === "player-advertisement",
    );
    expect(players).toHaveLength(2);
    // The count alone would also pass if `host` were read from the wrong field
    // of the advertisement — two rows is two rows either way — so the hosts are
    // pinned and not only the number of them.
    expect(players.map((candidate) => candidate.host)).toEqual([
      "192.168.1.77",
      "192.168.1.78",
    ]);
  });

  it("does not offer an advertisement that carries no debugger port", async () => {
    // Recorded divergence: the reference attaches to this exact player by
    // obtaining a debugger port through the device (adb port forwarding,
    // /proc/net/tcp, usbmuxd), which the spec puts out of scope. The
    // advertisement carries [Port] 55000 — the player-connection port — and no
    // [DebuggerPort], so parsePlayerAdvertisement yields debuggerPort 0.
    // Offering a target we cannot attach to would be worse than omitting it;
    // the explicit endPoint path is the supported way to reach a player. The
    // omission is deliberate, not a bug.
    const discovery = new EditorDiscovery({
      collectAdvertisements: async () => [
        parsePlayerAdvertisement(androidPlayerPacket),
      ],
    });

    const found = await discovery.discover(["H:\\fixture"]);

    expect(
      found.filter(
        (candidate) => candidate.source === "player-advertisement",
      ),
    ).toHaveLength(0);
  });

  it("does not offer a debug-enabled advertisement with no debugger port", async () => {
    // The live record above also carries [Debug] 0, so it cannot show that the
    // missing debugger port alone excludes a target. Debugging is enabled here
    // to isolate the positive-port gate.
    const advertisement = {
      ...parsePlayerAdvertisement(androidPlayerPacket),
      allowDebugging: true,
    };
    const discovery = new EditorDiscovery({
      collectAdvertisements: async () => [advertisement],
    });

    const found = await discovery.discover(["H:\\fixture"]);

    expect(
      found.filter(
        (candidate) => candidate.source === "player-advertisement",
      ),
    ).toHaveLength(0);
  });

  it("collects advertisements once and preserves distinct roots", async () => {
    const collectAdvertisements = vi.fn(async () => [
      {
        ...parsePlayerAdvertisement(packet),
        projectName: "one",
        debuggerPort: 56101,
      },
      {
        ...parsePlayerAdvertisement(packet),
        projectName: "two",
        debuggerPort: 56202,
      },
    ]);
    const discovery = new EditorDiscovery(
      dependencies({
        collectAdvertisements,
        readFile: async (filePath) =>
          filePath.includes(`${path.sep}one${path.sep}`)
            ? '{"process_id":1101}'
            : '{"process_id":2202}',
        isProcessAlive: async () => true,
      }),
    );

    const candidates = await discovery.discover([
      "H:\\one",
      "H:\\two",
      "H:\\one",
    ]);

    expect(collectAdvertisements).toHaveBeenCalledTimes(1);
    expect(candidates.map((candidate) => candidate.workspaceRoot)).toEqual([
      path.resolve("H:\\one"),
      path.resolve("H:\\two"),
    ]);
  });

  it("closes every multicast socket after collection", async () => {
    const sockets: FakeDatagramSocket[] = [];
    const discovery = new EditorDiscovery(
      { readFile: async () => "not json" },
      () => {
        const socket = new FakeDatagramSocket();
        sockets.push(socket);
        return socket as unknown as dgram.Socket;
      },
    );

    await discovery.discover(["H:\\fixture"], 0);

    expect(sockets).toHaveLength(4);
    expect(sockets.every((socket) => socket.closed)).toBe(true);
  });

  it("dispose closes sockets and releases an active collection", async () => {
    const sockets: FakeDatagramSocket[] = [];
    const discovery = new EditorDiscovery(
      { readFile: async () => "not json" },
      () => {
        const socket = new FakeDatagramSocket();
        sockets.push(socket);
        return socket as unknown as dgram.Socket;
      },
    );

    const pending = discovery.discover(["H:\\fixture"], 10_000);
    expect(sockets).toHaveLength(4);
    discovery.dispose();

    await expect(pending).resolves.toEqual([]);
    expect(sockets.every((socket) => socket.closed)).toBe(true);
  });
});

/**
 * The Editor-path expectations are exact. Player targets are appended by
 * `EditorDiscovery.discover` and have their own tests above, so they are
 * filtered out here instead of being restated in every Editor expectation.
 */
function editorCandidates(
  candidates: readonly EditorCandidate[],
): readonly EditorCandidate[] {
  return candidates.filter(
    (candidate) => candidate.source !== "player-advertisement",
  );
}

async function connectToLoopback(port: number): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const socket = net.createConnection({
      host: "127.0.0.1",
      port,
    });
    socket.once("connect", () => {
      socket.destroy();
      resolve();
    });
    socket.once("error", reject);
  });
}

class FakeDatagramSocket extends EventEmitter {
  public closed = false;

  public bind(_port: number, callback: () => void): void {
    callback();
  }

  public addMembership(_group: string): void {}

  public close(): void {
    if (!this.closed) {
      this.closed = true;
      this.emit("close");
    }
  }
}
