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
    "rejects version outside the 0.3.2 compatibility policy: %s",
    (version) => {
      const result = classifyVersion(version);
      expect(result.level).toBe("unsupported");
      expect(result.warning).toContain(
        "outside the version 0.3.2 compatibility policy",
      );
    },
  );

  it("rejects malformed version metadata", () => {
    expect(classifyVersion("latest").level).toBe("unsupported");
  });

  it("treats an absent version as unknown rather than malformed", () => {
    // A project may have no ProjectSettings/ProjectVersion.txt at all, and
    // every Player candidate carries no Editor version by design, so an
    // absent version must not block attaching.
    const result = classifyVersion("");

    expect(result.level).toBe("compatible-unverified");
    expect(result.version).toBe("");
    expect(result.warning).toContain("could not be determined");
    expect(result.warning).not.toContain("Malformed");
  });

  it("does not treat a whitespace-only version as absent", () => {
    // Only an exactly empty string is absent. A whitespace value is a version
    // that is wrong, not a version that is missing, so it stays malformed
    // rather than falling out of a trim() by accident.
    const result = classifyVersion("   ");

    expect(result.level).toBe("unsupported");
    expect(result.warning).toContain(
      "Malformed or unsupported Editor version:",
    );
  });

  it("still rejects a version carrying a revision suffix", () => {
    // The shape Unity uses for m_EditorVersionWithRevision must keep blocking
    // when it is supplied as the Editor version directly.
    expect(classifyVersion("2022.3.62t11 (b1a0a2c)").level).toBe(
      "unsupported",
    );
  });
});
