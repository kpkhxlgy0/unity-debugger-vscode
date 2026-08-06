import type { VersionDecision } from "./model.js";

const VERSION_PATTERN =
  /^(?:\d{4}\.\d+\.\d+[abfpxt]\d+|\d{4}\.\d+\.\d+)$/;
const SUPPORTED_VERSION = "2022.3.62t11";

export function classifyVersion(projectVersion: string): VersionDecision {
  if (!VERSION_PATTERN.test(projectVersion)) {
    return {
      level: "unsupported",
      version: projectVersion,
      warning: `Malformed or unsupported Editor version: ${projectVersion}`,
    };
  }

  if (projectVersion === SUPPORTED_VERSION) {
    return {
      level: "supported",
      version: projectVersion,
      warning: undefined,
    };
  }

  if (
    !projectVersion.startsWith("2022.3.") &&
    !projectVersion.startsWith("6000.")
  ) {
    return {
      level: "unsupported",
      version: projectVersion,
      warning:
        `Editor ${projectVersion} is outside the version 0.3.0 ` +
        "compatibility policy.",
    };
  }

  return {
    level: "compatible-unverified",
    version: projectVersion,
    warning:
      `Editor ${projectVersion} is unverified; the supported baseline is ` +
      `${SUPPORTED_VERSION}.`,
  };
}
