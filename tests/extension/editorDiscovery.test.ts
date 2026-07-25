import type dgram from "node:dgram";
import { EventEmitter } from "node:events";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";
import {
  EditorDiscovery,
  type EditorDiscoveryDependencies,
} from "../../extension/src/editorDiscovery.js";
import { parsePlayerAdvertisement } from "../../extension/src/playerAdvertisement.js";
import { packet } from "./playerAdvertisement.test.js";

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
    probeLoopbackPort: async (port) => port === 56234,
    ...overrides,
  };
}

describe("EditorDiscovery", () => {
  it("returns a matching Editor on loopback only", async () => {
    const discovery = new EditorDiscovery(dependencies());

    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([
      {
        processId: 1234,
        projectName: "fixture",
        workspaceRoot: path.resolve("H:\\fixture"),
        host: "127.0.0.1",
        port: 56234,
        projectVersion: "2022.3.62t11",
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
    });
    expect(candidate?.host).not.toBe("192.168.1.20");
  });

  it("returns no candidate for a dead Editor process", async () => {
    const discovery = new EditorDiscovery(
      dependencies({ isProcessAlive: async () => false }),
    );
    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([]);
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

  it("rejects invalid candidate ports before probing", async () => {
    const probe = vi.fn(async () => true);
    const advertisement = {
      ...parsePlayerAdvertisement(packet),
      projectName: "fixture",
      debuggerPort: 70000,
    };
    const discovery = new EditorDiscovery(
      dependencies({
        collectAdvertisements: async () => [advertisement],
        probeLoopbackPort: probe,
      }),
    );

    const candidates = await discovery.discover(["H:\\fixture"]);
    expect(candidates).toHaveLength(1);
    expect(probe).not.toHaveBeenCalledWith(70000);
  });

  it("ignores malformed EditorInstance JSON", async () => {
    const discovery = new EditorDiscovery(
      dependencies({ readFile: async () => "not json" }),
    );
    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([]);
  });

  it("returns no candidate when every loopback probe fails", async () => {
    const discovery = new EditorDiscovery(
      dependencies({ probeLoopbackPort: async () => false }),
    );
    await expect(discovery.discover(["H:\\fixture"])).resolves.toEqual([]);
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
        probeLoopbackPort: async () => true,
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
