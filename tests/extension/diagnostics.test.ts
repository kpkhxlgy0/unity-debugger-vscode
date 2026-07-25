import { describe, expect, it } from "vitest";
import { terminationMessageFromLog } from "../../extension/src/diagnostics.js";

describe("terminationMessageFromLog", () => {
  it("reports a non-zero adapter exit without exposing raw diagnostics", () => {
    const log = [
      "event=dap.command event=attach",
      "event=adapter.exit exitCode=1",
    ].join("\n");

    expect(terminationMessageFromLog(log)).toBe(
      "The Unity debug adapter exited with code 1. " +
        "Open sanitized diagnostics for details.",
    );
  });

  it("returns no message for a clean exit", () => {
    expect(
      terminationMessageFromLog("event=adapter.exit exitCode=0"),
    ).toBeUndefined();
  });
});
