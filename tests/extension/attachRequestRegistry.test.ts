import { describe, expect, it } from "vitest";
import { AttachRequestRegistry } from "../../extension/src/attachRequestRegistry.js";
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

describe("AttachRequestRegistry", () => {
  it("publishes no host or port and consumes a target once", () => {
    let now = 1_000;
    const ids = ["target-1", "request-1"];
    const registry = new AttachRequestRegistry(
      () => now,
      () => ids.shift()!,
    );

    const target = registry.issue(candidate);
    expect(target).toEqual({
      targetId: "target-1",
      processId: 1234,
      projectName: "MyGame",
      workspaceRoot: "h:\\workspace\\mygame",
      projectVersion: "2022.3.62t11",
      source: "derived-port",
    });
    expect(target).not.toHaveProperty("host");
    expect(target).not.toHaveProperty("port");

    expect(registry.prepare("target-1")).toMatchObject({
      requestId: "request-1",
      workspaceRoot: "h:\\workspace\\mygame",
    });
    expect(() => registry.prepare("target-1")).toThrowError(
      expect.objectContaining({ code: "TARGET_EXPIRED" }),
    );
  });

  it("expires at 60 seconds and enforces the canonical workspace", () => {
    let now = 1_000;
    const ids = ["expired-target", "target-2", "request-2"];
    const registry = new AttachRequestRegistry(
      () => now,
      () => ids.shift()!,
    );
    registry.issue(candidate);
    now += 60_000;
    expect(() => registry.prepare("expired-target")).toThrowError(
      expect.objectContaining({ code: "TARGET_EXPIRED" }),
    );

    now = 1_000;
    const target = registry.issue(candidate);
    const prepared = registry.prepare(target.targetId);
    expect(() =>
      registry.consumeRequest(prepared.requestId, "H:\\other"),
    ).toThrowError(expect.objectContaining({ code: "WORKSPACE_NOT_ALLOWED" }));
  });

  it("gives a late-prepared request its own full 60-second lifetime", () => {
    let now = 1_000;
    const ids = ["target-1", "request-1"];
    const registry = new AttachRequestRegistry(
      () => now,
      () => ids.shift()!,
    );

    registry.issue(candidate);
    now += 59_999;
    const prepared = registry.prepare("target-1");
    now += 59_999;

    expect(registry.consumeRequest(prepared.requestId, candidate.workspaceRoot))
      .toMatchObject({ workspaceRoot: "h:\\workspace\\mygame" });
  });

  it("canonicalizes the workspace scope without mutating the caller candidate", () => {
    const suppliedCandidate: EditorCandidate = {
      ...candidate,
      workspaceRoot: "H:\\workspace\\MyGame\\..",
    };
    const ids = ["target-1", "request-1"];
    const registry = new AttachRequestRegistry(
      () => 1_000,
      () => ids.shift()!,
    );

    const target = registry.issue(suppliedCandidate);
    const prepared = registry.prepare(target.targetId);

    expect(suppliedCandidate.workspaceRoot).toBe("H:\\workspace\\MyGame\\..");
    expect(target.workspaceRoot).toBe("h:\\workspace");
    expect(prepared.workspaceRoot).toBe("h:\\workspace");
    expect(registry.consumeRequest(prepared.requestId, "h:\\WORKSPACE\\."))
      .toMatchObject({ workspaceRoot: "h:\\workspace" });
  });
});
