import os from "node:os";
import type * as vscode from "vscode";
import { CHANNEL_REFUSED_LINE } from "./diagnostics.js";
import { PRODUCT_IDENTITY } from "./productIdentity.js";

/**
 * The VS Code channel factory, injected so unit tests need no VS Code host.
 */
type LogChannelFactory = (
  name: string,
  options: { log: true },
) => vscode.LogOutputChannel;

/**
 * The extension's channel: the reference's `LogOutputChannel`, plus the one
 * addition the sanitized-diagnostics rule needs at the boundary.
 */
export interface SanitizedOutputChannel extends vscode.LogOutputChannel {
  /**
   * Writes one diagnostics line, unless that line names the user's home
   * directory — in which case the line is dropped and a path-free
   * `event=diagnostics.channel.refused` marker is written in its place.
   *
   * The producers already write only allowlisted, redacted lines; this is the
   * backstop for the producer that forgets. It refuses rather than redacts
   * because redaction already has one implementation — the adapter's
   * `PathRedactor`, whose match of the user profile this mirrors — and a second
   * one here could disagree with it.
   *
   * The refused line is never repeated, not even to report the refusal: that
   * would leak the very path the refusal exists to keep out. The refusal
   * itself is recorded, as the marker, so a dropped diagnostic stays
   * observable without its path. The match ends at a path boundary, so
   * `C:\Users\Admin` refuses `C:\Users\Admin\logs` but not
   * `C:\Users\Administrator\logs`: unlike `PathRedactor`, which rewrites, this
   * guard drops, and an over-broad match would cost a diagnostic rather than
   * merely redact a path.
   *
   * A home that cannot be read (`os.homedir()` throwing) is treated as
   * "nothing to match", exactly like one that names no user (`/`, a drive
   * root): the line is written. The guard can only refuse a path it can
   * recognise, and refusing everything would silently drop the whole channel's
   * output — a worse outcome than the single line it would hold back, with the
   * producer's redaction still in place. Refusal is a backstop here, never the
   * only gate.
   *
   * Only the home directory is checked. A workspace path is redacted by the
   * adapter and is not visible to a channel built from nothing but a factory,
   * so it cannot be part of this check.
   */
  writeSanitized(line: string): void;
}

/**
 * Creates the extension's log channel.
 *
 * `{ log: true }` is what makes it a `LogOutputChannel`: without it there are no
 * level methods, so the function-breakpoint warning could not be emitted at warn
 * level the way the reference emits it. The name comes from
 * `PRODUCT_IDENTITY.displayName` rather than the reference's own `Unity`,
 * because both extensions can be installed at once and two identically named
 * channels would be indistinguishable.
 */
export function createOutputChannel(
  factory: LogChannelFactory,
): SanitizedOutputChannel {
  const channel = factory(PRODUCT_IDENTITY.displayName, { log: true });

  // Decorated in place rather than wrapped: VS Code hands back a live host
  // object whose level methods are bound to it, and both `dispose` and the
  // `context.subscriptions` entry must keep reaching that exact object — which
  // is also why `createOutputChannel` returns it rather than a copy.
  return Object.assign(channel, {
    writeSanitized: (line: string): void => {
      if (namesUserPath(line)) {
        // The marker names the event and no path, so the dropped diagnostic is
        // observable without repeating the line that caused the refusal.
        channel.info(CHANNEL_REFUSED_LINE);
        return;
      }
      channel.info(line);
    },
  });
}

/** A home of `C:` names a whole drive rather than a user's own directory. */
const DRIVE_ROOT_PATTERN = /^[a-z]:$/;

/**
 * Whether `line` names the user's home directory.
 *
 * The comparison mirrors the adapter's `PathRedactor`: the user profile with
 * either separator and no trailing one, matched case-insensitively and ending
 * at a path boundary.
 */
function namesUserPath(line: string): boolean {
  const home = normalizeUserPath(homeDirectory());
  // A home that names no user — `/`, a drive root, or a home the platform
  // could not read at all, which normalizes to `""` — is not matched: it would
  // match every line, or every line containing a separator, losing all
  // diagnostics rather than one leaking line.
  if (home.length === 0 || DRIVE_ROOT_PATTERN.test(home)) {
    return false;
  }
  return namesHomePath(normalizeUserPath(line), home);
}

/**
 * The user's home directory, or `""` when the platform cannot name one.
 *
 * `os.homedir()` reads the environment and can throw. It is called from inside
 * a command handler, and a throw escaping from here would suppress the
 * `NO_SANITIZED_DIAGNOSTICS` message and reject the command, so the failure is
 * caught and treated as "nothing to match" — see `writeSanitized`.
 */
function homeDirectory(): string {
  try {
    return os.homedir();
  } catch {
    return "";
  }
}

/**
 * Whether the normalized `line` names `home` as a whole path segment.
 *
 * A bare `includes` would also match a sibling whose name merely starts with
 * the user's — `C:\Users\Administrator` and `C:\Users\Admin2` both contain
 * `C:\Users\Admin` — and this guard drops lines where `PathRedactor` rewrites
 * them, so an over-broad match costs a diagnostic instead of a redaction. The
 * match therefore has to end where the segment does: at the end of the string
 * or at a separator. Normalization folds both separators to `/`, so only `/`
 * has to be considered.
 */
function namesHomePath(normalizedLine: string, home: string): boolean {
  let index = normalizedLine.indexOf(home);
  while (index >= 0) {
    const following = normalizedLine.charAt(index + home.length);
    if (following === "" || following === "/") {
      return true;
    }
    index = normalizedLine.indexOf(home, index + 1);
  }
  return false;
}

/** Forward slashes, no trailing separator, and the platform's case rules. */
function normalizeUserPath(value: string): string {
  return value
    .replace(/[\\/]+$/, "")
    .replace(/\\/g, "/")
    .toLowerCase();
}
