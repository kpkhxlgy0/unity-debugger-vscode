import path from "node:path";
import { describe, expect, it, vi } from "vitest";
import { AttachRequestRegistry } from "../../extension/src/attachRequestRegistry.js";

vi.mock("vscode", () => ({
  Uri: { file: (value: string) => ({ fsPath: value }) },
  workspace: { getWorkspaceFolder: vi.fn() },
  debug: {
    onDidStartDebugSession: vi.fn(),
    startDebugging: vi.fn(),
  },
}));

import {
  UnityDebuggerPureApi,
  VscodeDebugSessionStarter,
  type VscodeDebugSessionStarterDependencies,
} from "../../extension/src/publicApi.js";
import type { EditorCandidate } from "../../extension/src/model.js";

const candidate: EditorCandidate = {
  processId: 1234,
  projectName: "MyGame",
  workspaceRoot: "H:\\workspace\\MyGame",
  host: "127.0.0.1",
  port: 56234,
  projectVersion: "2022.3.62t11",
  source: "derived-port",
};

function registry(): AttachRequestRegistry {
  return new AttachRequestRegistry(
    () => 1_000,
    (() => {
      const ids = ["target-1", "request-1"];
      return () => ids.shift()!;
    })(),
  );
}

interface StarterHarness {
  readonly dependencies: VscodeDebugSessionStarterDependencies;
  readonly listeners: Array<(session: {
    readonly id: string;
    readonly type: string;
    readonly configuration: Readonly<Record<string, unknown>>;
  }) => void>;
  readonly dispose: ReturnType<typeof vi.fn>;
  readonly clearTimeout: ReturnType<typeof vi.fn>;
  readonly timers: Array<{ readonly callback: () => void; readonly delay: number }>;
}

function starterHarness(
  startDebugging: VscodeDebugSessionStarterDependencies["startDebugging"],
): StarterHarness {
  const listeners: StarterHarness["listeners"] = [];
  const dispose = vi.fn();
  const clearTimeout = vi.fn();
  const timers: StarterHarness["timers"] = [];
  return {
    listeners,
    dispose,
    clearTimeout,
    timers,
    dependencies: {
      getWorkspaceFolder: vi.fn(() => undefined),
      onDidStartDebugSession: vi.fn((listener) => {
        listeners.push(listener);
        return { dispose };
      }),
      startDebugging,
      setTimeout: (callback, delay) => {
        timers.push({ callback, delay });
        return timers.length;
      },
      clearTimeout,
    },
  };
}

describe("UnityDebuggerPureApi", () => {
  it("discovers opaque targets and starts the selected target", async () => {
    const start = vi.fn(async () => "session-1");
    const api = new UnityDebuggerPureApi(
      { discover: vi.fn(async () => [candidate]) },
      registry(),
      { start },
      "0.2.0",
    );

    const [target] = await api.discoverTargets([candidate.workspaceRoot]);

    await expect(api.startAttach(target.targetId)).resolves.toEqual({
      sessionId: "session-1",
      targetId: "target-1",
    });
    expect(start).toHaveBeenCalledWith({
      workspaceRoot: target.workspaceRoot,
      requestId: "request-1",
    });
    expect(Object.isFrozen(api)).toBe(true);
  });

  it("does not expose its discovery, registry, or session starter at runtime", () => {
    const api = new UnityDebuggerPureApi(
      { discover: vi.fn(async () => []) },
      registry(),
      { start: vi.fn() },
      "0.2.0",
    );
    const untrusted = api as unknown as Record<string, unknown>;

    expect(Reflect.ownKeys(api).sort()).toEqual([
      "apiVersion",
      "debugType",
      "extensionVersion",
    ]);
    expect(Object.getOwnPropertyNames(Object.getPrototypeOf(api)).sort()).toEqual([
      "constructor",
      "discoverTargets",
      "startAttach",
    ]);
    expect(untrusted.discovery).toBeUndefined();
    expect(untrusted.registry).toBeUndefined();
    expect(untrusted.starter).toBeUndefined();
  });

  it("passes an empty, canonicalized, case-insensitively deduplicated root list to discovery", async () => {
    const discover = vi.fn(async () => []);
    const api = new UnityDebuggerPureApi({ discover }, registry(), { start: vi.fn() }, "0.2.0");

    await api.discoverTargets([]);
    await api.discoverTargets([
      "H:\\workspace\\MyGame",
      "h:\\WORKSPACE\\MYGAME\\.",
      "H:\\workspace\\OtherGame\\..\\OtherGame",
    ]);

    expect(discover).toHaveBeenNthCalledWith(1, []);
    expect(discover).toHaveBeenNthCalledWith(2, [
      path.resolve("H:\\workspace\\MyGame"),
      path.resolve("H:\\workspace\\OtherGame"),
    ]);
  });
});

describe("VscodeDebugSessionStarter", () => {
  it("subscribes before starting and resolves only the exact API attach session", async () => {
    let harness!: StarterHarness;
    const startDebugging = vi.fn(async () => {
      expect(harness.listeners).toHaveLength(1);
      return true;
    });
    harness = starterHarness(startDebugging);
    const starter = new VscodeDebugSessionStarter(harness.dependencies);
    let settled = false;
    const started = starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" })
      .then((value) => {
        settled = true;
        return value;
      });

    await Promise.resolve();
    expect(startDebugging).toHaveBeenCalledWith(undefined, {
      name: "Attach to Unity Debugger Pure",
      type: "unity-debugger-pure",
      request: "attach",
      __apiAttachRequestId: "request-1",
    });
    harness.listeners[0]({
      id: "unrelated-id",
      type: "unity-debugger-pure",
      configuration: { __apiAttachRequestId: "other-request" },
    });
    harness.listeners[0]({
      id: "wrong-type",
      type: "node",
      configuration: { __apiAttachRequestId: "request-1" },
    });
    await Promise.resolve();
    expect(settled).toBe(false);

    harness.listeners[0]({
      id: "session-1",
      type: "unity-debugger-pure",
      configuration: { __apiAttachRequestId: "request-1" },
    });
    await expect(started).resolves.toBe("session-1");
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("rejects and cleans up when VS Code rejects the attach request", async () => {
    const harness = starterHarness(vi.fn(async () => false));
    const starter = new VscodeDebugSessionStarter(harness.dependencies);

    await expect(
      starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" }),
    ).rejects.toMatchObject({ code: "ATTACH_FAILED" });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("maps a synchronous startDebugging throw to a sanitized attach error", async () => {
    const rejected = new Error("private synchronous failure");
    const harness = starterHarness(vi.fn(() => { throw rejected; }));
    const starter = new VscodeDebugSessionStarter(harness.dependencies);

    await expect(
      starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" }),
    ).rejects.toMatchObject({
      code: "ATTACH_FAILED",
      message: "VS Code failed to start the attach request.",
    });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("maps an asynchronous startDebugging rejection to a sanitized attach error", async () => {
    const harness = starterHarness(
      vi.fn(async () => { throw new Error("private asynchronous failure"); }),
    );
    const starter = new VscodeDebugSessionStarter(harness.dependencies);

    await expect(
      starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" }),
    ).rejects.toMatchObject({
      code: "ATTACH_FAILED",
      message: "VS Code failed to start the attach request.",
    });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("rejects when a matching session starts before VS Code returns false", async () => {
    let harness!: StarterHarness;
    harness = starterHarness(vi.fn(async () => {
      harness.listeners[0]({
        id: "session-1",
        type: "unity-debugger-pure",
        configuration: { __apiAttachRequestId: "request-1" },
      });
      return false;
    }));
    const starter = new VscodeDebugSessionStarter(harness.dependencies);

    await expect(
      starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" }),
    ).rejects.toMatchObject({ code: "ATTACH_FAILED" });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("rejects when a matching session starts before VS Code rejects", async () => {
    let harness!: StarterHarness;
    harness = starterHarness(vi.fn(async () => {
      harness.listeners[0]({
        id: "session-1",
        type: "unity-debugger-pure",
        configuration: { __apiAttachRequestId: "request-1" },
      });
      throw new Error("private event-ordering failure");
    }));
    const starter = new VscodeDebugSessionStarter(harness.dependencies);

    await expect(
      starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" }),
    ).rejects.toMatchObject({
      code: "ATTACH_FAILED",
      message: "VS Code failed to start the attach request.",
    });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });

  it("rejects after ten seconds and cleans up when no matching session starts", async () => {
    const harness = starterHarness(vi.fn(async () => true));
    const starter = new VscodeDebugSessionStarter(harness.dependencies);
    const started = starter.start({ workspaceRoot: candidate.workspaceRoot, requestId: "request-1" });

    expect(harness.timers).toHaveLength(1);
    expect(harness.timers[0].delay).toBe(10_000);
    harness.timers[0].callback();

    await expect(started).rejects.toMatchObject({ code: "ATTACH_FAILED" });
    expect(harness.dispose).toHaveBeenCalledOnce();
    expect(harness.clearTimeout).toHaveBeenCalledOnce();
  });
});
