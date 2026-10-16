import dgram from "node:dgram";
import { readFile } from "node:fs/promises";
import os from "node:os";
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
import { isClassifiableVersion } from "./versionPolicy.js";

const PLAYER_CONNECTION_GROUP = "225.0.0.222";
const PLAYER_CONNECTION_PORTS = [54_997, 34_997, 57_997, 58_997] as const;
const DEFAULT_DISCOVERY_TIMEOUT_MS = 350;
const EDITOR_HOST = "127.0.0.1";

type SocketFactory = (
  options: dgram.SocketOptions,
) => dgram.Socket;

interface DiscoveryOutcome {
  readonly candidates: readonly EditorCandidate[];
  /**
   * The advertisement that supplied the discovered Editor's debugger port. It
   * describes that Editor, so it must not be offered a second time as a Player.
   */
  readonly consumedAdvertisements: readonly PlayerAdvertisement[];
}

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
    const outcomes = await Promise.all(
      normalizedRoots.map((workspaceRoot) =>
        this.discoverOne(workspaceRoot, advertisements),
      ),
    );

    const consumed = new Set(
      outcomes.flatMap((outcome) => outcome.consumedAdvertisements),
    );
    const players = advertisements
      .filter(
        (advertisement) =>
          !consumed.has(advertisement) &&
          advertisement.allowDebugging &&
          isValidPort(advertisement.debuggerPort),
      )
      .map((advertisement) =>
        toPlayerCandidate(advertisement, normalizedRoots[0]),
      );

    const candidates = [
      ...outcomes.flatMap((outcome) => outcome.candidates),
      ...players,
    ];
    const uniqueCandidates = new Map<string, EditorCandidate>();
    for (const candidate of candidates) {
      const key = `${candidate.host}:${candidate.port}`;
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
  ): Promise<DiscoveryOutcome> {
    try {
      const projectName = path.basename(workspaceRoot);
      const editorInstancePath = path.join(
        workspaceRoot,
        "Library",
        "EditorInstance.json",
      );
      const instanceJson = await this.dependencies.readFile(
        editorInstancePath,
        "utf8",
      );
      const instance = parseEditorInstance(instanceJson);

      if (!(await this.dependencies.isProcessAlive(instance.processId))) {
        return noDiscovery();
      }

      const projectVersion =
        (await this.dependencies.readProjectVersion(workspaceRoot)) ||
        editorVersionFromInstance(instanceJson);
      const matching = advertisements.filter(
        (item) => item.projectName === projectName,
      );
      if (
        matching.length > 0 &&
        !matching.some((item) => item.allowDebugging)
      ) {
        return noDiscovery();
      }

      const advertised = matching.find(
        (item) => item.allowDebugging && isValidPort(item.debuggerPort),
      );
      return {
        candidates: [
          {
            processId: instance.processId,
            projectName,
            workspaceRoot,
            host: EDITOR_HOST,
            port:
              advertised?.debuggerPort ??
              defaultEditorPort(instance.processId),
            projectVersion,
            machine: os.hostname(),
            source: advertised ? "advertisement" : "derived-port",
          },
        ],
        consumedAdvertisements: advertised ? [advertised] : [],
      };
    } catch {
      return noDiscovery();
    }

    return noDiscovery();
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

function noDiscovery(): DiscoveryOutcome {
  return { candidates: [], consumedAdvertisements: [] };
}

/**
 * A project may have no `ProjectSettings/ProjectVersion.txt` at all, while
 * `Library/EditorInstance.json` records the version the running Editor
 * reports. This fallback is deliberately narrow: it supplies a version only
 * when the ProjectVersion.txt read yielded nothing, it never invents one from
 * absent, unusable or malformed JSON, and it supplies only a version the
 * version policy can classify. Anything else — a revision-suffixed value such
 * as `2022.3.62t11 (b1a0a2c)`, for instance — degrades to `""`, which the
 * policy treats as *unknown* and attachable, rather than reaching the policy
 * as *malformed* and blocking an attach this fallback was meant to enable.
 * Player targets never consult it: a Player's Editor version is genuinely
 * unknown.
 */
function editorVersionFromInstance(json: string): string {
  try {
    const value: unknown = JSON.parse(json);
    if (typeof value !== "object" || value === null) {
      return "";
    }

    const version = (value as Record<string, unknown>).version;
    if (typeof version !== "string") {
      return "";
    }

    const extracted = version.trim();
    return isClassifiableVersion(extracted) ? extracted : "";
  } catch {
    return "";
  }
}

function toPlayerCandidate(
  advertisement: PlayerAdvertisement,
  workspaceRoot: string,
): EditorCandidate {
  return {
    processId: 0,
    projectName: advertisement.projectName,
    workspaceRoot,
    host: advertisement.ip,
    port: advertisement.debuggerPort,
    projectVersion: "",
    machine: advertisement.machine,
    source: "player-advertisement",
  };
}

function isValidPort(port: number): boolean {
  return Number.isInteger(port) && port > 0 && port <= 65_535;
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

async function isProcessAlive(processId: number): Promise<boolean> {
  try {
    process.kill(processId, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === "EPERM";
  }
}
