import { describe, expect, it, vi } from "vitest";
import {
  DebugConfigurationProvider,
  type ConfigurationUi,
} from "../../extension/src/debugConfigurationProvider.js";
import type { EditorCandidate } from "../../extension/src/model.js";

const candidate: EditorCandidate = {
  processId: 1234,
  projectName: "fixture",
  workspaceRoot: "H:\\fixture",
  host: "127.0.0.1",
  port: 56234,
  projectVersion: "2022.3.62t11",
  source: "derived-port",
};

const folder = { uri: { fsPath: "H:\\fixture" } };
const attach = {
  name: "Attach",
  type: "unity-debugger-pure",
  request: "attach",
};

function ui(overrides: Partial<ConfigurationUi> = {}): ConfigurationUi {
  return {
    selectCandidate: vi.fn(async (candidates) => candidates[0]),
    showNoEditor: vi.fn(async () => undefined),
    showError: vi.fn(async () => undefined),
    ...overrides,
  };
}

function provider(
  discover: (
    roots: readonly string[],
  ) => Promise<readonly EditorCandidate[]>,
  configurationUi = ui(),
): DebugConfigurationProvider {
  return new DebugConfigurationProvider(
    { discover },
    configurationUi,
    (currentFolder) => [currentFolder],
  );
}

describe("DebugConfigurationProvider", () => {
  it("resolves a single local target without copying unknown fields", async () => {
    const configurationProvider = provider(async () => [candidate]);

    const resolved =
      await configurationProvider.resolveDebugConfiguration(folder, {
        ...attach,
        executable: "H:\\untrusted.exe",
      });

    expect(resolved).toMatchObject({
      type: "unity-debugger-pure",
      request: "attach",
      __processId: 1234,
      __host: "127.0.0.1",
      __port: 56234,
      __projectVersion: "2022.3.62t11",
    });
    expect(resolved).not.toHaveProperty("executable");
  });

  it("requires an open workspace folder", async () => {
    const configurationUi = ui();
    const configurationProvider = provider(
      async () => [candidate],
      configurationUi,
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(undefined, attach),
    ).resolves.toBeUndefined();
    expect(configurationUi.showError).toHaveBeenCalledOnce();
  });

  it("rejects every request other than unity-debugger-pure attach", async () => {
    const configurationUi = ui();
    const configurationProvider = provider(
      async () => [candidate],
      configurationUi,
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, {
        ...attach,
        request: "launch",
      }),
    ).resolves.toBeUndefined();
    expect(configurationUi.showError).toHaveBeenCalledWith(
      "unity-debugger-pure supports only request: attach.",
    );
  });

  it("offers one refresh when no Editor is discovered", async () => {
    const configurationUi = ui();
    const discover = vi.fn(async () => []);
    const configurationProvider = provider(discover, configurationUi);

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toBeUndefined();
    expect(configurationUi.showNoEditor).toHaveBeenCalledOnce();
    expect(discover).toHaveBeenCalledOnce();
  });

  it("performs exactly one additional discovery after Refresh", async () => {
    const configurationUi = ui({
      showNoEditor: vi.fn(async () => "refresh" as const),
    });
    const discover = vi
      .fn<() => Promise<readonly EditorCandidate[]>>()
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([candidate]);
    const configurationProvider = provider(discover, configurationUi);

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toMatchObject({ __processId: 1234 });
    expect(discover).toHaveBeenCalledTimes(2);
  });

  it("stops after a second empty result without recursion", async () => {
    const configurationUi = ui({
      showNoEditor: vi.fn(async () => "refresh" as const),
    });
    const discover = vi.fn(async () => []);
    const configurationProvider = provider(discover, configurationUi);

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toBeUndefined();
    expect(discover).toHaveBeenCalledTimes(2);
    expect(configurationUi.showError).toHaveBeenCalledWith(
      "No local debug-enabled Editor was found after refresh.",
    );
  });

  it("asks once when multiple candidates are available", async () => {
    const other = { ...candidate, processId: 4321, port: 56321 };
    const selectCandidate = vi.fn(async () => other);
    const configurationProvider = provider(
      async () => [candidate, other],
      ui({ selectCandidate }),
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toMatchObject({ __processId: 4321 });
    expect(selectCandidate).toHaveBeenCalledOnce();
  });

  it("returns undefined when target selection is cancelled", async () => {
    const selectCandidate = vi.fn(async () => undefined);
    const configurationProvider = provider(
      async () => [candidate, { ...candidate, processId: 4321 }],
      ui({ selectCandidate }),
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toBeUndefined();
    expect(selectCandidate).toHaveBeenCalledOnce();
  });

  it("allows an unverified compatible version for Adapter warning", async () => {
    const configurationProvider = provider(async () => [
      { ...candidate, projectVersion: "6000.0.50f1" },
    ]);

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toMatchObject({ __projectVersion: "6000.0.50f1" });
  });

  it("rejects an unsupported Editor version", async () => {
    const configurationUi = ui();
    const configurationProvider = provider(
      async () => [{ ...candidate, projectVersion: "2021.3.45f1" }],
      configurationUi,
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, attach),
    ).resolves.toBeUndefined();
    expect(configurationUi.showError).toHaveBeenCalledWith(
      expect.stringContaining("outside the version 0.1.1"),
    );
  });
});
