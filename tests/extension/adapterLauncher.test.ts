import path from "node:path";
import { describe, expect, it } from "vitest";
import { AdapterLauncher } from "../../extension/src/adapterLauncher.js";

describe("AdapterLauncher", () => {
  it("resolves only the packaged Windows x64 executable", () => {
    const launcher = new AdapterLauncher(() => true, "win32", "x64");

    expect(launcher.createDescriptor("H:\\extension")).toEqual({
      command: path.join(
        "H:\\extension",
        "adapter",
        "win32-x64",
        "UnityDebuggerPure.exe",
      ),
      args: [],
      options: {
        cwd: path.join("H:\\extension", "adapter", "win32-x64"),
      },
    });
  });

  it("fails with an actionable error when the executable is absent", () => {
    const launcher = new AdapterLauncher(() => false, "win32", "x64");

    expect(() => launcher.createDescriptor("H:\\extension")).toThrow(
      "The packaged Unity debug adapter is missing. Reinstall the extension.",
    );
  });

  it.each([
    ["linux", "x64"],
    ["darwin", "x64"],
    ["win32", "arm64"],
  ])("rejects unsupported host %s %s", (platform, architecture) => {
    const launcher = new AdapterLauncher(
      () => true,
      platform,
      architecture,
    );

    expect(() => launcher.createDescriptor("H:\\extension")).toThrow(
      "The Unity debug adapter supports Windows x64 only.",
    );
  });
});
