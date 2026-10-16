import type { VersionDecision } from "./model.js";

const VERSION_PATTERN =
  /^(?:\d{4}\.\d+\.\d+[abfpxt]\d+|\d{4}\.\d+\.\d+)$/;
const SUPPORTED_VERSION = "2022.3.62t11";

/**
 * True when this policy can classify `version` at all.
 *
 * A caller that may supply a version from a secondary source — the
 * `EditorInstance.json` fallback in particular — must check this first. A
 * value the policy cannot classify reaches `classifyVersion` as *malformed*
 * and blocks an attach that an *absent* version allows, which would make the
 * fallback less permissive than no fallback at all.
 */
export function isClassifiableVersion(version: string): boolean {
  return VERSION_PATTERN.test(version);
}

export function classifyVersion(projectVersion: string): VersionDecision {
  // An absent version is not a malformed one. A project may have no
  // ProjectSettings/ProjectVersion.txt at all, and a Player target carries no
  // Editor version by design, so an empty value means "unknown" and must not
  // block attaching. Only an exactly empty string is absent: a whitespace
  // value is a version that is wrong rather than one that is missing, and it
  // stays malformed below.
  if (projectVersion === "") {
    return {
      level: "compatible-unverified",
      version: "",
      warning:
        "The Editor version could not be determined; the supported " +
        `baseline is ${SUPPORTED_VERSION}.`,
    };
  }

  if (!isClassifiableVersion(projectVersion)) {
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
        `Editor ${projectVersion} is outside the version 0.3.2 ` +
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
