/**
 * The reference extension's function-breakpoint warning, copied verbatim from
 * its English bundle (`out/nls.bundle.json`). Do not paraphrase it: the wording
 * is the user-visible contract this alignment exists to keep.
 */
export const FUNCTION_BREAKPOINT_WARNING =
  "You are using function breakpoints. Even if they are fully supported, " +
  "they have a major negative impact on debugging performance. " +
  "Use them only when necessary.";

/**
 * `vscode.FunctionBreakpoint` is the only breakpoint kind that carries a
 * `functionName`, so its presence is the discriminator — an empty name is not a
 * usable function breakpoint and must not raise the warning on its own.
 * `instanceof vscode.FunctionBreakpoint` would be the other way to ask, but it
 * would make this untestable outside a VS Code host, which is why it is a
 * separate, pure module.
 */
export function shouldWarnAboutFunctionBreakpoints(
  breakpoints: readonly { functionName?: string }[],
): boolean {
  return breakpoints.some(
    (breakpoint) =>
      typeof breakpoint.functionName === "string" &&
      breakpoint.functionName.length > 0,
  );
}
