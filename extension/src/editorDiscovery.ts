import dgram from "node:dgram";
import { readFile } from "node:fs/promises";
import net from "node:net";
import path from "node:path";
import {
  defaultEditorPort,
  parseEditorInstance,
} from "./editorInstance.js";
import type { EditorCandidate } from "./model.js";
import {
  parsePlayerAdvertisement,
  type PlayerAdvertisement,
} from "./playerAdvertisement.js";
import { readProjectVersion } from "./projectVersion.js";

const PLAYER_CONNECTION_GROUP = "225.0.0.222";
const PLAYER_CONNECTION_PORTS = [54_997, 34_997, 57_997, 58_997] as const;
const DEFAULT_DISCOVERY_TIMEOUT_MS = 350;
const DEFAULT_PROBE_TIMEOUT_MS = 250;

type SocketFactory = (
  options: dgram.SocketOptions,
) => dgram.Socket;

export interface EditorDiscoveryDependencies {
  readonly readFile: (
    filePath: string,
    encoding: BufferEncoding,
  ) => Promise<string>;
  readonly readProjectVersion: (
    workspaceRoot: string,
  ) => Promise<string>;
  readonly isProcessAlive: (processId: number) => Promise<boolean>;
  readonly collectAdvertisements: (
    timeoutMs: number,
  ) => Promise<readonly PlayerAdvertisement[]>;
  readonly probeLoopbackPort: (port: number) => Promise<boolean>;
}

export class EditorDiscovery {
  private readonly activeSockets = new Set<dgram.Socket>();
  private readonly dependencies: EditorDiscoveryDependencies;

  public constructor(
    dependencies: Partial<EditorDiscoveryDependencies> = {},
    private readonly socketFactory: SocketFactory = dgram.createSocket,
  ) {
    this.dependencies = {
      readFile: async (filePath, encoding) => readFile(filePath, encoding),
      readProjectVersion,
      isProcessAlive: isProcessAlive,
      collectAdvertisements: async (timeoutMs) =>
        this.collectAdvertisements(timeoutMs),
      probeLoopbackPort: probeLoopbackPort,
      ...dependencies,
    };
  }

  public async discover(
    workspaceRoots: readonly string[],
    timeoutMs = DEFAULT_DISCOVERY_TIMEOUT_MS,
  ): Promise<readonly EditorCandidate[]> {
    const normalizedRoots = uniqueRoots(workspaceRoots);
    if (normalizedRoots.length === 0) {
      return [];
    }

    const advertisements =
      await this.dependencies.collectAdvertisements(timeoutMs);
    const discovered = await Promise.all(
      normalizedRoots.map((workspaceRoot) =>
        this.discoverOne(workspaceRoot, advertisements),
      ),
    );

    const candidates = discovered.flat();
    const uniqueCandidates = new Map<string, EditorCandidate>();
    for (const candidate of candidates) {
      const key = `${candidate.processId}:${candidate.port}`;
      if (!uniqueCandidates.has(key)) {
        uniqueCandidates.set(key, candidate);
      }
    }
    return [...uniqueCandidates.values()];
  }

  public dispose(): void {
    for (const socket of [...this.activeSockets]) {
      this.closeSocket(socket);
    }
  }

  private async discoverOne(
    workspaceRoot: string,
    advertisements: readonly PlayerAdvertisement[],
  ): Promise<readonly EditorCandidate[]> {
    try {
      const projectName = path.basename(workspaceRoot);
      const editorInstancePath = path.join(
        workspaceRoot,
        "Library",
        "EditorInstance.json",
      );
      const instance = parseEditorInstance(
        await this.dependencies.readFile(editorInstancePath, "utf8"),
      );

      if (!(await this.dependencies.isProcessAlive(instance.processId))) {
        return [];
      }

      const projectVersion =
        await this.dependencies.readProjectVersion(workspaceRoot);
      const matching = advertisements.filter(
        (item) => item.projectName === projectName,
      );
      if (
        matching.length > 0 &&
        !matching.some((item) => item.allowDebugging)
      ) {
        return [];
      }

      const advertised = matching.filter(
        (item) =>
          item.allowDebugging &&
          item.debuggerPort > 0 &&
          item.debuggerPort <= 65_535,
      );
      const candidatePorts = uniqueNumbers([
        ...advertised.map((item) => item.debuggerPort),
        defaultEditorPort(instance.processId),
      ]);

      for (const port of candidatePorts) {
        if (await this.dependencies.probeLoopbackPort(port)) {
          return [
            {
              processId: instance.processId,
              projectName,
              workspaceRoot,
              host: "127.0.0.1",
              port,
              projectVersion,
              source: advertised.some(
                (item) => item.debuggerPort === port,
              )
                ? "advertisement"
                : "derived-port",
            },
          ];
        }
      }
    } catch {
      return [];
    }

    return [];
  }

  private async collectAdvertisements(
    timeoutMs: number,
  ): Promise<readonly PlayerAdvertisement[]> {
    const advertisements: PlayerAdvertisement[] = [];
    const sockets = PLAYER_CONNECTION_PORTS.map(() => {
      const socket = this.socketFactory({
        type: "udp4",
        reuseAddr: true,
      });
      this.activeSockets.add(socket);
      return socket;
    });

    try {
      await Promise.all(
        sockets.map(
          (socket, index) =>
            new Promise<void>((resolve) => {
              let settled = false;
              const timer = setTimeout(
                finish,
                Math.max(0, timeoutMs),
              );

              function finish(): void {
                if (settled) {
                  return;
                }
                settled = true;
                clearTimeout(timer);
                resolve();
              }

              socket.on("message", (message) => {
                try {
                  advertisements.push(
                    parsePlayerAdvertisement(message.toString("utf8")),
                  );
                } catch {
                  // Ignore malformed multicast traffic.
                }
              });
              socket.once("error", finish);
              socket.once("close", finish);

              try {
                socket.bind(
                  PLAYER_CONNECTION_PORTS[index],
                  () => {
                    try {
                      socket.addMembership(PLAYER_CONNECTION_GROUP);
                    } catch {
                      finish();
                    }
                  },
                );
              } catch {
                finish();
              }
            }),
        ),
      );
    } finally {
      for (const socket of sockets) {
        this.closeSocket(socket);
      }
    }

    return advertisements;
  }

  private closeSocket(socket: dgram.Socket): void {
    if (!this.activeSockets.delete(socket)) {
      return;
    }

    try {
      socket.close();
    } catch {
      // A bind failure can leave a datagram socket already closed.
    }
  }
}

function uniqueRoots(workspaceRoots: readonly string[]): string[] {
  const roots = new Map<string, string>();
  for (const workspaceRoot of workspaceRoots) {
    const normalized = path.resolve(workspaceRoot);
    const key =
      process.platform === "win32" ? normalized.toLowerCase() : normalized;
    if (!roots.has(key)) {
      roots.set(key, normalized);
    }
  }
  return [...roots.values()];
}

function uniqueNumbers(values: readonly number[]): number[] {
  return [...new Set(values)];
}

async function isProcessAlive(processId: number): Promise<boolean> {
  try {
    process.kill(processId, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === "EPERM";
  }
}

async function probeLoopbackPort(port: number): Promise<boolean> {
  if (!Number.isInteger(port) || port <= 0 || port > 65_535) {
    return false;
  }

  return new Promise<boolean>((resolve) => {
    const socket = net.createConnection({
      host: "127.0.0.1",
      port,
    });
    let settled = false;

    function finish(result: boolean): void {
      if (settled) {
        return;
      }
      settled = true;
      socket.destroy();
      resolve(result);
    }

    socket.setTimeout(DEFAULT_PROBE_TIMEOUT_MS);
    socket.once("connect", () => finish(true));
    socket.once("timeout", () => finish(false));
    socket.once("error", () => finish(false));
  });
}
