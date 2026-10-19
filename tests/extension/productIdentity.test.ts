import fs from "node:fs";
import { describe, expect, it } from "vitest";
import {
  DISPLAY_BACKGROUND_PROCESSES_SETTING,
  PRODUCT_IDENTITY,
} from "../../extension/src/productIdentity.js";

describe("PRODUCT_IDENTITY", () => {
  it("matches the public manifest contract consumed by the extension", () => {
    const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
    const contribution = manifest.contributes.debuggers[0];

    expect(PRODUCT_IDENTITY.publisher).toBe(manifest.publisher);
    expect(PRODUCT_IDENTITY.extensionName).toBe(manifest.name);
    expect(PRODUCT_IDENTITY.displayName).toBe(manifest.displayName);
    expect(PRODUCT_IDENTITY.description).toBe(manifest.description);
    expect(PRODUCT_IDENTITY.vsixFileName).toBe(
      "unity-debugger-pure-0.3.3.vsix",
    );
    expect(PRODUCT_IDENTITY.debugType).toBe(contribution.type);
    expect(PRODUCT_IDENTITY.defaultConfigurationName).toBe(
      contribution.configurationSnippets[0].body.name,
    );
    expect(Object.values(PRODUCT_IDENTITY.commandIds)).toEqual(
      manifest.contributes.commands.map(
        (command: { command: string }) => command.command,
      ),
    );
    expect(PRODUCT_IDENTITY.settingIds.displayBackgroundProcesses).toBe(
      "unity-debugger-pure.displayBackgroundProcesses",
    );
    // The verbatim id above pins the user-visible contract; these two pin the
    // composition. The extension reads the setting with
    // `getConfiguration(section).get(key)`, so it needs the key while the
    // manifest contributes the whole address: if either half were renamed
    // alone, the lookup would return the declared default for ever — silently,
    // with no error and no failing test. It is asserted here because this is
    // the module that declares both halves.
    expect(PRODUCT_IDENTITY.settingIds.displayBackgroundProcesses).toBe(
      `${PRODUCT_IDENTITY.extensionName}.` +
        DISPLAY_BACKGROUND_PROCESSES_SETTING,
    );
    expect(
      Object.keys(manifest.contributes.configuration?.properties ?? {}),
    ).toEqual([
      `${PRODUCT_IDENTITY.extensionName}.` +
        DISPLAY_BACKGROUND_PROCESSES_SETTING,
    ]);
    expect(
      Object.keys(manifest.contributes.configuration?.properties ?? {}),
    ).toEqual(Object.values(PRODUCT_IDENTITY.settingIds));
    expect(
      manifest.contributes.configuration?.properties ?? {},
    ).not.toHaveProperty("unityDebuggerPure.enableImplicitEvaluation");
  });
});
