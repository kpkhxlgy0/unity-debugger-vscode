import { describe, expect, it } from "vitest";
import { classifyVersion } from "../../extension/src/versionPolicy.js";

describe("classifyVersion", () => {
  it("formally supports only the baseline", () => {
    expect(classifyVersion("2022.3.62t11")).toEqual({
      level: "supported",
      version: "2022.3.62t11",
      warning: undefined,
    });
  });

  it.each(["2022.3.61f1", "6000.0.50f1", "6000.1.4f1"])(
    "allows valid but unverified version %s",
    (version) => {
      expect(classifyVersion(version).level).toBe("compatible-unverified");
    },
  );

  it.each(["2021.3.45f1", "2023.2.20f1"])(
    "rejects version outside the 0.1.1 compatibility policy: %s",
    (version) => {
      const result = classifyVersion(version);
      expect(result.level).toBe("unsupported");
      expect(result.warning).toContain(
        "outside the version 0.1.1 compatibility policy",
      );
    },
  );

  it("rejects malformed version metadata", () => {
    expect(classifyVersion("latest").level).toBe("unsupported");
  });
});
