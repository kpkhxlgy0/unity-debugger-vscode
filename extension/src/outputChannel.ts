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
): vscode.LogOutputChannel {
  return factory(PRODUCT_IDENTITY.displayName, { log: true });
}
