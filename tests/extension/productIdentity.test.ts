import fs from "node:fs";
import { describe, expect, it } from "vitest";
import { PRODUCT_IDENTITY } from "../../extension/src/productIdentity.js";

describe("PRODUCT_IDENTITY", () => {
  it("matches the public manifest contract consumed by the extension", () => {
    const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
    const contribution = manifest.contributes.debuggers[0];

    expect(PRODUCT_IDENTITY.publisher).toBe(manifest.publisher);
    expect(PRODUCT_IDENTITY.extensionName).toBe(manifest.name);
    expect(PRODUCT_IDENTITY.displayName).toBe(manifest.displayName);
    expect(PRODUCT_IDENTITY.description).toBe(manifest.description);
    expect(PRODUCT_IDENTITY.vsixFileName).toBe(
      "unity-debugger-pure-0.1.1.vsix",
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
  });
});
