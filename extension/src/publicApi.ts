import path from "node:path";
import * as vscode from "vscode";
import type { AttachRequestRegistry } from "./attachRequestRegistry.js";
import type { EditorDiscoveryLike } from "./debugConfigurationProvider.js";
import type { EditorCandidate } from "./model.js";
import { PRODUCT_IDENTITY } from "./productIdentity.js";
import {
  PublicApiError,
  type PublicEditorTarget,
  type StartedDebugSession,
  type UnityDebuggerPureApiV1,
} from "./publicApiTypes.js";

const SESSION_START_TIMEOUT_MS = 10_000;

export interface PublicApiDiscovery extends EditorDiscoveryLike {
  discover(
    workspaceRoots: readonly string[],
  ): Promise<readonly EditorCandidate[]>;
}

export interface DebugSessionStarter {
  start(request: {
    readonly workspaceRoot: string;
    readonly requestId: string;
  }): Promise<string>;
}

interface VscodeDebugSessionLike {
  readonly id: string;
  readonly type: string;
  readonly configuration: Readonly<Record<string, unknown>>;
}

interface VscodeDisposableLike {
  dispose(): void;
}

export interface VscodeDebugSessionStarterDependencies {
  getWorkspaceFolder(workspaceRoot: string): unknown;
  onDidStartDebugSession(
    listener: (session: VscodeDebugSessionLike) => void,
  ): VscodeDisposableLike;
  startDebugging(
    folder: unknown,
    configuration: Readonly<Record<string, unknown>>,
  ): PromiseLike<boolean>;
  setTimeout(callback: () => void, delay: number): unknown;
  clearTimeout(timer: unknown): void;
}

export class UnityDebuggerPureApi implements UnityDebuggerPureApiV1 {
  public readonly apiVersion = 1 as const;
  public readonly debugType = PRODUCT_IDENTITY.debugType;
  public readonly extensionVersion: string;
  readonly #discovery: PublicApiDiscovery;
  readonly #registry: AttachRequestRegistry;
  readonly #starter: DebugSessionStarter;

  public constructor(
    discovery: PublicApiDiscovery,
    registry: AttachRequestRegistry,
    starter: DebugSessionStarter,
    extensionVersion: string,
  ) {
    this.#discovery = discovery;
    this.#registry = registry;
    this.#starter = starter;
    this.extensionVersion = extensionVersion;
    Object.freeze(this);
  }

  public async discoverTargets(
    workspaceRoots: readonly string[],
  ): Promise<readonly PublicEditorTarget[]> {
    const candidates = await this.#discovery.discover(
      canonicalizeWorkspaceRoots(workspaceRoots),
    );
    return candidates.map((candidate) => this.#registry.issue(candidate));
  }

  public async startAttach(targetId: string): Promise<StartedDebugSession> {
    const prepared = this.#registry.prepare(targetId);
    const sessionId = await this.#starter.start({
      workspaceRoot: prepared.workspaceRoot,
      requestId: prepared.requestId,
    });
    return { sessionId, targetId };
  }
}

export class VscodeDebugSessionStarter implements DebugSessionStarter {
  readonly #dependencies: VscodeDebugSessionStarterDependencies;

  public constructor(
    dependencies: VscodeDebugSessionStarterDependencies = vscodeDependencies(),
  ) {
    this.#dependencies = dependencies;
  }

  public start(request: {
    readonly workspaceRoot: string;
    readonly requestId: string;
  }): Promise<string> {
    return new Promise((resolve, reject) => {
      let settled = false;
      let startAccepted = false;
      let matchingSessionId: string | undefined;
      let subscription: VscodeDisposableLike | undefined;
      let timer: unknown;
      const finish = (complete: () => void) => {
        if (settled) {
          return;
        }
        settled = true;
        subscription?.dispose();
        if (timer !== undefined) {
          this.#dependencies.clearTimeout(timer);
        }
        complete();
      };
      const resolveWhenReady = () => {
        if (startAccepted && matchingSessionId) {
          finish(() => resolve(matchingSessionId!));
        }
      };

      subscription = this.#dependencies.onDidStartDebugSession((session) => {
        if (
          session.type === PRODUCT_IDENTITY.debugType &&
          session.configuration.__apiAttachRequestId === request.requestId
        ) {
          matchingSessionId = session.id;
          resolveWhenReady();
        }
      });
      if (settled) {
        subscription.dispose();
        return;
      }

      timer = this.#dependencies.setTimeout(() => {
        finish(() =>
          reject(
            new PublicApiError(
              "ATTACH_FAILED",
              "Timed out waiting for VS Code to start the attach session.",
            ),
          ),
        );
      }, SESSION_START_TIMEOUT_MS);
      if (settled) {
        this.#dependencies.clearTimeout(timer);
        return;
      }

      let startDebugging: PromiseLike<boolean>;
      try {
        startDebugging = this.#dependencies.startDebugging(
          this.#dependencies.getWorkspaceFolder(request.workspaceRoot),
          {
            name: PRODUCT_IDENTITY.defaultConfigurationName,
            type: PRODUCT_IDENTITY.debugType,
            request: "attach",
            __apiAttachRequestId: request.requestId,
          },
        );
      } catch {
        finish(() => reject(startFailureError()));
        return;
      }

      void startDebugging
        .then(
          (accepted) => {
            if (!accepted) {
              finish(() => reject(startFailureError()));
              return;
            }
            startAccepted = true;
            resolveWhenReady();
          },
          () => finish(() => reject(startFailureError())),
        );
    });
  }
}

function startFailureError(): PublicApiError {
  return new PublicApiError(
    "ATTACH_FAILED",
    "VS Code failed to start the attach request.",
  );
}

function canonicalizeWorkspaceRoots(
  workspaceRoots: readonly string[],
): readonly string[] {
  const roots = new Map<string, string>();
  for (const workspaceRoot of workspaceRoots) {
    const normalized = path.resolve(workspaceRoot);
    const key = process.platform === "win32"
      ? normalized.toLowerCase()
      : normalized;
    if (!roots.has(key)) {
      roots.set(key, normalized);
    }
  }
  return [...roots.values()];
}

function vscodeDependencies(): VscodeDebugSessionStarterDependencies {
  return {
    getWorkspaceFolder(workspaceRoot) {
      return vscode.workspace.getWorkspaceFolder(vscode.Uri.file(workspaceRoot));
    },
    onDidStartDebugSession(listener) {
      return vscode.debug.onDidStartDebugSession((session) => listener({
        id: session.id,
        type: session.type,
        configuration: session.configuration,
      }));
    },
    startDebugging(folder, configuration) {
      return vscode.debug.startDebugging(
        folder as vscode.WorkspaceFolder | undefined,
        configuration as vscode.DebugConfiguration,
      );
    },
    setTimeout,
    clearTimeout(timer) {
      clearTimeout(timer as NodeJS.Timeout);
    },
  };
}
