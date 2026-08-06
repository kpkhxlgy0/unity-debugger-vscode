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
  it("does not emit a private implicit evaluation policy", async () => {
    const resolved = await provider(async () => [candidate])
      .resolveDebugConfiguration(folder, attach);

    expect(resolved).not.toHaveProperty("__enableImplicitEvaluation");
  });

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

  it("consumes a trusted API request without rediscovery", async () => {
    const consumeRequest = vi.fn(() => candidate);
    const discover = vi.fn(async () => []);
    const configurationProvider = new DebugConfigurationProvider(
      { discover },
      ui(),
      (currentFolder) => [currentFolder],
      { consumeRequest },
    );

    const resolved = await configurationProvider.resolveDebugConfiguration(
      folder,
      { ...attach, __apiAttachRequestId: "request-1" },
    );

    expect(consumeRequest).toHaveBeenCalledWith("request-1", "H:\\fixture");
    expect(discover).not.toHaveBeenCalled();
    expect(resolved).toMatchObject({
      __processId: 1234,
      __host: "127.0.0.1",
      __port: 56234,
      __apiAttachRequestId: "request-1",
    });
  });

  it.each([
    {
      label: "supported",
      projectVersion: "2022.3.62t11",
      expectedVersion: "2022.3.62t11",
      expectedError: undefined,
    },
    {
      label: "compatible but unverified",
      projectVersion: "6000.0.50f1",
      expectedVersion: "6000.0.50f1",
      expectedError: undefined,
    },
    {
      label: "unsupported",
      projectVersion: "2021.3.45f1",
      expectedVersion: undefined,
      expectedError: "outside the version 0.3.0",
    },
    {
      label: "malformed",
      projectVersion: "latest",
      expectedVersion: undefined,
      expectedError: "Malformed or unsupported Editor version: latest",
    },
  ])(
    "applies the ordinary $label version policy to a trusted API request",
    async ({ projectVersion, expectedVersion, expectedError }) => {
      const trustedCandidate = { ...candidate, projectVersion };
      const consumeRequest = vi.fn(() => trustedCandidate);
      const discover = vi.fn(async () => [candidate]);
      const configurationUi = ui();
      const configurationProvider = new DebugConfigurationProvider(
        { discover },
        configurationUi,
        (currentFolder) => [currentFolder],
        { consumeRequest },
      );

      const resolved = await configurationProvider.resolveDebugConfiguration(
        folder,
        { ...attach, __apiAttachRequestId: "request-1" },
      );

      expect(consumeRequest).toHaveBeenCalledOnce();
      expect(discover).not.toHaveBeenCalled();
      if (expectedVersion) {
        expect(resolved).toMatchObject({ __projectVersion: expectedVersion });
        expect(configurationUi.showError).not.toHaveBeenCalled();
      } else {
        expect(resolved).toBeUndefined();
        expect(configurationUi.showError).toHaveBeenCalledWith(
          expect.stringContaining(expectedError!),
        );
      }
    },
  );

  it.each([
    ["empty string", ""],
    ["undefined", undefined],
    ["null", null],
    ["number", 123],
    ["object", { requestId: "forged" }],
  ])(
    "rejects a present %s API request marker without discovery or resolution",
    async (_label, malformedRequestId) => {
      const consumeRequest = vi.fn(() => candidate);
      const discover = vi.fn(async () => [candidate]);
      const configurationUi = ui();
      const configurationProvider = new DebugConfigurationProvider(
        { discover },
        configurationUi,
        (currentFolder) => [currentFolder],
        { consumeRequest },
      );

      await expect(
        configurationProvider.resolveDebugConfiguration(folder, {
          ...attach,
          __apiAttachRequestId: malformedRequestId,
        }),
      ).resolves.toBeUndefined();
      expect(configurationUi.showError).toHaveBeenCalledWith(
        "The API attach request is invalid or expired. Discover targets again.",
      );
      expect(discover).not.toHaveBeenCalled();
      expect(consumeRequest).not.toHaveBeenCalled();
    },
  );

  it("rejects a forged API request instead of falling back to discovery", async () => {
    const configurationUi = ui();
    const configurationProvider = new DebugConfigurationProvider(
      { discover: vi.fn(async () => [candidate]) },
      configurationUi,
      (currentFolder) => [currentFolder],
      {
        consumeRequest: () => {
          throw new Error("expired");
        },
      },
    );

    await expect(
      configurationProvider.resolveDebugConfiguration(folder, {
        ...attach,
        __apiAttachRequestId: "forged",
      }),
    ).resolves.toBeUndefined();
    expect(configurationUi.showError).toHaveBeenCalledWith(
      "The API attach request is invalid or expired. Discover targets again.",
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
      expect.stringContaining("outside the version 0.3.0"),
    );
  });
});
