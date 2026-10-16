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
   * observable without its path.
   *
   * The match ends where the path segment does, and that boundary is drawn
   * from the continuation side: the character after the match must be one that
   * could carry the same segment name on (`[a-z0-9._+~$-]`, which is what a
   * normalized segment is spelled with). Anything else ends the segment —
   * `/`, but equally the end of the string and the `"`, space, `;`, `)` or
   * `\r` a log line can put there. So `C:\Users\Admin\logs` is refused while
   * `C:\Users\Administrator\logs`, `C:\Users\Admin2\logs` and
   * `C:\Users\Admin-old\logs` are not. Unlike `PathRedactor`, which rewrites,
   * this guard drops, so an over-broad match would cost a diagnostic rather
   * than merely redact a path; an under-broad one leaks, which is worse, so
   * the boundary is defined by what may continue a segment rather than by what
   * may end one.
   *
   * A home that cannot be read (`os.homedir()` throwing) is treated as
   * "nothing to match", exactly like one that names no user (`/`, a drive
   * root): the line is written. **This is a deliberate trade of strictness for
   * availability, not a strictly better outcome.** A fail-closed guard would
   * not cost the user their diagnostics — those reach them by the report file,
   * the clipboard and the message, not through this channel — it would cost
   * the channel its own event lines, and it would do so from inside a command
   * handler, where refusing everything also suppresses the message the user is
   * meant to see and rejects the command. What the trade costs is the check
   * itself: the guard can only refuse a path it can recognise. Three things
   * bound that cost — `PathRedactor` remains the authority for actual
   * redaction, so this backstop being off is not redaction being off; the
   * branch is effectively unreachable in a live host, where `os.homedir()`
   * reads an environment variable the platform has already set; and the same
   * rule already governs a home that names no user, which is an ordinary state
   * rather than a failure. Refusal is a backstop here, never the only gate.
   *
   * That state is silent: unlike a refusal it writes no marker. The marker
   * exists to make a *dropped* line observable, and nothing is dropped here —
   * every line is written, so there is no gap in the channel to signal. The
   * failure is a property of the host rather than of the line, so a marker
   * would also repeat on every `writeSanitized` call for the life of the
   * channel, adding lines to the very output this branch exists to preserve.
   * That the guard is off is recorded where a reader asks the question — here
   * and on `homeDirectory` — rather than in the channel.
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
        //
        // `info`, not `warn`: the four lines that legitimately reach this
        // method are written at `info`, so the marker sits in the same stream
        // as the event it stands in for, where a reader looking for the lost
        // diagnostic actually is. `warn` would be more prominent, and it is
        // also the level the function-breakpoint warning owns — a caller whose
        // visibility must not be diluted. Changing this level would also flip
        // the `warn`-untouched assertions in the tests, which prove the
        // refusal does not echo the line.
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
 * where the path segment ends — see `namesHomePath` for which characters end
 * it.
 */
function namesUserPath(line: string): boolean {
  const home = normalizeUserPath(homeDirectory());
  // A home that names no user — `/`, a drive root, or a home the platform
  // could not read at all, which normalizes to `""` — is not matched: an empty
  // home matches every line, which would cost every channel line rather than
  // the one leaking line this refusal exists for.
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
 * caught and treated as "nothing to match" — the guard switching itself off,
 * silently, rather than taking the caller down with it. `writeSanitized`
 * carries the reasoning for both halves of that: why the trade is made, and
 * why being off is not itself recorded in the channel.
 */
function homeDirectory(): string {
  try {
    return os.homedir();
  } catch {
    return "";
  }
}

/**
 * A character that can carry a normalized path segment on.
 *
 * Normalization lowercases, so the class needs no `A-Z`. It is the set a
 * segment name is spelled with — letters, digits, and the punctuation both
 * Windows and POSIX allow in a file name — and any character outside it ends
 * the segment.
 */
const SEGMENT_CONTINUATION_PATTERN = /[a-z0-9._+~$-]/;

/**
 * Whether the normalized `line` names `home` as a whole path segment.
 *
 * A bare `includes` would also match a sibling whose name merely starts with
 * the user's — `C:\Users\Administrator` and `C:\Users\Admin2` both contain
 * `C:\Users\Admin` — and this guard drops lines where `PathRedactor` rewrites
 * them, so an over-broad match costs a diagnostic instead of a redaction.
 *
 * The match therefore has to end where the segment does, and the test is put
 * from the continuation side: the character after the match must be one that
 * could carry the segment on. `Administrator` and `Admin2` continue it (`i`,
 * `2`) and `Admin-old` does (`-`), so none of them is refused. Testing the
 * terminator instead — only `/` or the end of the string — reads every other
 * boundary as "the path ended here", which writes `opened "C:\Users\Admin"`,
 * the adapter's own `home=C:\Users\Admin attempt=1`, and a line whose trailing
 * `\r` a file read left behind.
 *
 * The empty `following` is the end of the line, which no continuation
 * character matches either; it is named explicitly because end of line is the
 * boundary's plainest form.
 *
 * The class is the ASCII spelling, so a sibling that continues with a
 * non-ASCII letter — `C:\Users\Adminé` — is refused rather than written. That
 * costs a diagnostic and leaks nothing, which is the direction this guard
 * prefers to err in; refusing is only wrong here when the line it drops was
 * path-free, and this one names the home's whole prefix.
 */
function namesHomePath(normalizedLine: string, home: string): boolean {
  let index = normalizedLine.indexOf(home);
  while (index >= 0) {
    const following = normalizedLine.charAt(index + home.length);
    if (
      following === "" ||
      !SEGMENT_CONTINUATION_PATTERN.test(following)
    ) {
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
