import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  COPY_DIAGNOSTICS_COMMAND,
  getLogDirectory,
  OPEN_LOGS_COMMAND,
  REFRESH_TARGETS_COMMAND,
  terminationMessageFromLog,
} from "../../extension/src/diagnostics.js";

describe("terminationMessageFromLog", () => {
  it("uses the final commands and diagnostics directory", () => {
    expect(REFRESH_TARGETS_COMMAND).toBe(
      "unity-debugger-pure.refreshTargets",
    );
    expect(OPEN_LOGS_COMMAND).toBe("unity-debugger-pure.openLogs");
    expect(COPY_DIAGNOSTICS_COMMAND).toBe(
      "unity-debugger-pure.copyDiagnostics",
    );
    expect(getLogDirectory("C:\\Local")).toBe(
      path.resolve("C:\\Local", "unity-debugger-pure", "logs"),
    );
  });

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
