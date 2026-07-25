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
  const contents = await readFile(versionFile, "utf8");
  return parseProjectVersion(contents);
}
