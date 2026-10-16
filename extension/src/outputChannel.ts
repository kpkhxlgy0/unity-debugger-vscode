import os from "node:os";
import type * as vscode from "vscode";
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
   * directory — in which case the line is dropped.
   *
   * The producers already write only allowlisted, redacted lines; this is the
   * backstop for the producer that forgets. It refuses rather than redacts
   * because redaction already has one implementation — the adapter's
   * `PathRedactor`, whose match of the user profile this mirrors — and a second
   * one here could disagree with it. The refusal is silent: repeating the line,
   * even to report that it was refused, would leak the same path.
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
 * either separator and no trailing one, matched case-insensitively.
 */
function namesUserPath(line: string): boolean {
  const home = normalizeUserPath(os.homedir());
  // A home that names no user — `/` or a drive root — is not matched: it would
  // match every line containing a separator, which loses all diagnostics rather
  // than one leaking line.
  if (home.length === 0 || DRIVE_ROOT_PATTERN.test(home)) {
    return false;
  }
  return normalizeUserPath(line).includes(home);
}

/** Forward slashes, no trailing separator, and the platform's case rules. */
function normalizeUserPath(value: string): string {
  return value
    .replace(/[\\/]+$/, "")
    .replace(/\\/g, "/")
    .toLowerCase();
}
