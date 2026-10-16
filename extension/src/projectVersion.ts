import { readFile } from "node:fs/promises";
import path from "node:path";

export function parseProjectVersion(contents: string): string {
  const match = /^m_EditorVersion:\s*(\S+)\s*$/m.exec(contents);
  if (!match) {
    throw new Error("ProjectVersion.txt does not contain m_EditorVersion");
  }

  return match[1];
}

export async function readProjectVersion(
  workspaceRoot: string,
): Promise<string> {
  const versionFile = path.join(
    workspaceRoot,
    "ProjectSettings",
    "ProjectVersion.txt",
  );

  try {
    const contents = await readFile(versionFile, "utf8");
    return parseProjectVersion(contents);
  } catch {
    // A missing ProjectVersion.txt — or one without m_EditorVersion — is not
    // fatal to attaching: the Editor is already known to be alive and its
    // debugger port is known, so discovery reports the target and the caller
    // treats "" as an unknown version.
    return "";
  }
}
