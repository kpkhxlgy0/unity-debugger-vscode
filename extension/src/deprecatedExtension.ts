import { PRODUCT_IDENTITY } from "./productIdentity.js";

/**
 * The deprecated `unity.unity-debug` extension — the historical "Debugger for
 * Unity". It is a **third** extension, distinct from both this one and
 * `local.vstuc`: `vstuc` is the behavioural reference this work item aligns to
 * and is expected to coexist with us, while `unity.unity-debug` is the one the
 * reference warns about, and `docs/release-checklist.md` names a further id for
 * uninstall during migration.
 *
 * Addressed in the lowercase spelling the reference uses; VS Code matches
 * extension ids case-insensitively.
 */
export const DEPRECATED_EXTENSION_ID = "unity.unity-debug";

/**
 * The reference's deprecated-extension warning, copied verbatim from its
 * English bundle (`out/nls.bundle.json`) and confirmed character-for-character
 * there this round. Do not paraphrase it: the wording is the user-visible
 * contract this alignment exists to keep.
 *
 * The placeholders are the reference's own `{0}`/`{1}` convention, substituted
 * by `formatDeprecatedExtensionWarning` below.
 */
export const DEPRECATED_EXTENSION_WARNING =
  "The {0} extension is deprecated and incompatible with the {1} " +
  "extension. Please uninstall or disable it.";

/**
 * The fields of a `vscode.Extension` this warning reads — exactly those, the
 * way the other `...Like` interfaces here narrow a host type.
 *
 * `packageJSON` is `any` in the API, so `displayName` is declared required here
 * so that a caller or a test double which omits it fails to compile. The
 * run-time read stays as unguarded as the reference's, so a manifest that
 * declares no `displayName` would put the literal word `undefined` inside the
 * sentence. That is the reference's own behaviour rather than a case this
 * warning invents, and it is left alone rather than silently improved: the
 * deprecation target declares a display name, and a fallback would be a
 * divergence no decision covers. Retained as a residual risk in the report.
 */
export interface DeprecatedExtensionLike {
  readonly isActive: boolean;
  readonly packageJSON: { readonly displayName: string };
}

/** `vscode.extensions`, narrowed to the one lookup this warning makes. */
export interface ExtensionRegistryLike {
  getExtension(extensionId: string): DeprecatedExtensionLike | undefined;
}

export interface DeprecatedExtensionWarningDependencies {
  /**
   * The extension registry — `vscode.extensions` in production. Injected, so
   * the absent, inactive and active branches are all reachable without a host.
   */
  readonly extensions: ExtensionRegistryLike;
  /**
   * The warning sink — `vscode.window.showWarningMessage` in production.
   * Injected for the same reason, and typed `void`: the reference fires the
   * message at activation without awaiting it, so a warning can never be in the
   * way of activation.
   */
  showWarningMessage(message: string): void;
}

/**
 * The reference's deprecated-extension check, which is: look
 * `unity.unity-debug` up in the extension registry; **return silently if it is
 * absent or not active**; otherwise warn once.
 *
 * The `isActive` half is not an optimisation. Warning on installation alone
 * would fire for every user who ever installed the extension and never enabled
 * it, and the deprecated extension being *loaded* is what makes the conflict
 * real.
 *
 * Two deliberate differences from the reference's implementation, both to be
 * recorded in the alignment matrix rather than fixed silently:
 *
 * - **`{0}` is the deprecated extension's plain `displayName`, not a markdown
 *   link.** The reference builds
 *   `[<displayName>](command:extension.open?["<id>"])` and then passes
 *   `MarkdownString.value` — the markdown *source* — into a plain-text format,
 *   so what the user actually sees is the literal
 *   `[Debugger for Unity](command:extension.open?["unity.unity-debug"])`. That
 *   is a defect, not behaviour to preserve. The link was **dropped rather than
 *   repaired**, because repairing it means handing `showWarningMessage` a
 *   `MarkdownString` with `isTrusted: true` for a `command:` link to work at
 *   all — and that document would interpolate an installed third-party
 *   extension's `displayName` into a trusted, command-capable context, buying a
 *   convenience shortcut at the price of a markup-injection surface. The
 *   sentence already says what to do ("Please uninstall or disable it."), so
 *   the affordance is the only thing lost.
 * - **`{1}` is our own display name, `Unity Debugger Pure`.** The reference's
 *   resolves to `Unity`. This descends from the coexistence decision — the two
 *   extensions can be installed together, so a message naming only `Unity`
 *   would be ambiguous about which one to keep — and the template is unchanged,
 *   which is why this is a difference in the *arguments* rather than in the
 *   string.
 */
export function warnAboutDeprecatedExtensions(
  dependencies: DeprecatedExtensionWarningDependencies,
): void {
  const deprecated = dependencies.extensions.getExtension(
    DEPRECATED_EXTENSION_ID,
  );
  if (!deprecated || !deprecated.isActive) {
    return;
  }

  dependencies.showWarningMessage(
    formatDeprecatedExtensionWarning(
      deprecated.packageJSON.displayName,
      PRODUCT_IDENTITY.displayName,
    ),
  );
}

/**
 * `{0}`/`{1}` substitution, the placeholder convention the reference's own
 * generated formatter uses.
 *
 * The scan is single-pass and the replacement is a function, so a display name
 * that happens to contain `$&` or the text `{1}` is inserted literally: a
 * string-pattern `String.replace` would reinterpret the first, and replacing
 * `{0}` before `{1}` would let the second substitute inside the value it was
 * given.
 */
function formatDeprecatedExtensionWarning(
  deprecatedDisplayName: string,
  ownDisplayName: string,
): string {
  return DEPRECATED_EXTENSION_WARNING.replace(
    /\{([01])\}/g,
    (_placeholder, index: string) =>
      index === "0" ? deprecatedDisplayName : ownDisplayName,
  );
}
