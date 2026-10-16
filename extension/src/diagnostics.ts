import fs from "node:fs/promises";
import path from "node:path";
import { PRODUCT_IDENTITY } from "./productIdentity.js";

export const REFRESH_TARGETS_COMMAND =
  PRODUCT_IDENTITY.commandIds.refreshTargets;
export const OPEN_LOGS_COMMAND =
  PRODUCT_IDENTITY.commandIds.openLogs;
export const COPY_DIAGNOSTICS_COMMAND =
  PRODUCT_IDENTITY.commandIds.copyDiagnostics;

export const NO_SANITIZED_DIAGNOSTICS =
  "No sanitized Unity debugger diagnostics are available yet.";

/**
 * The output-channel lines for the two diagnostics commands.
 *
 * They keep the adapter's allowlisted line shape — `event=<name>` as
 * `DiagnosticLog.FormatLine` writes it — and name no path, because both
 * commands handle absolute paths and a path is exactly what the channel's
 * `writeSanitized` refuses.
 */
export const OPEN_LOGS_OPENED_LINE = "event=diagnostics.openLogs";
export const OPEN_LOGS_UNAVAILABLE_LINE =
  "event=diagnostics.openLogs.unavailable";
export const COPY_DIAGNOSTICS_COPIED_LINE =
  "event=diagnostics.copyDiagnostics";
export const COPY_DIAGNOSTICS_UNAVAILABLE_LINE =
  "event=diagnostics.copyDiagnostics.unavailable";

/**
 * The channel's record that a line was refused for naming the user's home
 * directory.
 *
 * It names the event and no path, which is the whole point: the line it stands
 * in for is the one that must not be repeated, so the refusal cannot be
 * reported by echoing it. Without this marker a refused diagnostic leaves no
 * trace at all, and an over-broad refusal is invisible.
 */
export const CHANNEL_REFUSED_LINE = "event=diagnostics.channel.refused";

/**
 * The reference extension's attach-failure strings, copied verbatim from its
 * English bundle (`out/nls.bundle.json`). Do not paraphrase them; the wording
 * is the user-visible contract this alignment exists to keep.
 */
export const UNITY_NOT_RUNNING_MESSAGE =
  "Unity is not running for the current project";
export const NO_UNITY_ENDPOINT_MESSAGE = "No Unity endPoint to connect to";
export const NO_PROCESSES_MESSAGE = "No Process to attach to";

const LOG_FILE_PATTERN = /^adapter-.*\.log$/;

export function getLogDirectory(
  localApplicationData = process.env.LOCALAPPDATA,
): string | undefined {
  if (!localApplicationData) {
    return undefined;
  }
  return path.resolve(
    localApplicationData,
    PRODUCT_IDENTITY.diagnosticsDirectoryName,
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
