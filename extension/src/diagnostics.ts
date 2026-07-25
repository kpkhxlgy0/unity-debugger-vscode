export const REFRESH_TARGETS_COMMAND =
  "unity-community.refreshTargets";
export const OPEN_LOGS_COMMAND = "unity-community.openLogs";
export const COPY_DIAGNOSTICS_COMMAND =
  "unity-community.copyDiagnostics";

export const NO_SANITIZED_DIAGNOSTICS =
  "No sanitized Unity debugger diagnostics are available yet.";

const LOG_FILE_PATTERN = /^adapter-.*\.log$/;

export function getLogDirectory(
  localApplicationData = process.env.LOCALAPPDATA,
): string | undefined {
  if (!localApplicationData) {
    return undefined;
  }
  return path.resolve(
    localApplicationData,
    "unity-debugger-vscode",
    "logs",
  );
}

export async function readLatestSanitizedReport(
  logDirectory = getLogDirectory(),
): Promise<string | undefined> {
  if (!logDirectory) {
    return undefined;
  }
  try {
    const entries = await fs.readdir(logDirectory, {
      withFileTypes: true,
    });
    const files = entries
      .filter(
        (entry) => entry.isFile() && LOG_FILE_PATTERN.test(entry.name),
      )
      .map((entry) => path.resolve(logDirectory, entry.name))
      .filter((candidate) => path.dirname(candidate) === logDirectory);
    const withTimes = await Promise.all(
      files.map(async (file) => ({
        file,
        modified: (await fs.stat(file)).mtimeMs,
      })),
    );
    const latest = withTimes.sort(
      (left, right) => right.modified - left.modified,
    )[0];
    return latest
      ? await fs.readFile(latest.file, "utf8")
      : undefined;
  } catch {
    return undefined;
  }
}

export function terminationMessageFromLog(
  contents: string | undefined,
): string | undefined {
  if (!contents) {
    return undefined;
  }
  const lines = contents.trimEnd().split(/\r?\n/).reverse();
  const exitLine = lines.find((line) =>
    /\bevent=adapter\.exit\b/.test(line),
  );
  const match = exitLine?.match(/\bexitCode=(\d+)\b/);
  if (!match) {
    return undefined;
  }
  const exitCode = Number.parseInt(match[1], 10);
  return exitCode === 0
    ? undefined
    : `The Unity debug adapter exited with code ${exitCode}. ` +
        "Open sanitized diagnostics for details.";
}
import fs from "node:fs/promises";
import path from "node:path";
