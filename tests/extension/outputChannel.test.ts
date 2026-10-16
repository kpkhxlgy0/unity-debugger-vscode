import { describe, expect, it, vi } from "vitest";
import type { LogOutputChannel } from "vscode";
import { createOutputChannel } from "../../extension/src/outputChannel.js";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";

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
