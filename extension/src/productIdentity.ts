const EXTENSION_NAME = "unity-debugger-pure";

/**
 * The key of the contributed `displayBackgroundProcesses` setting.
 *
 * VS Code addresses a contributed setting as `<extensionName>.<key>`, and the
 * two readers need different halves of that address: `package.json` contributes
 * the whole id, and `getConfiguration(section).get(key)` takes only the key.
 * The id below is built from this declaration so there is one copy of the key
 * rather than two that agree until one of them is renamed — a reader asking for
 * a key the manifest no longer contributes returns the default for ever, with
 * no error and no failing test.
 */
export const DISPLAY_BACKGROUND_PROCESSES_SETTING =
  "displayBackgroundProcesses";

export const PRODUCT_IDENTITY = {
  publisher: "kpk",
  extensionName: EXTENSION_NAME,
  displayName: "Unity Debugger Pure",
  description:
    "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors, " +
    "without C# Dev Kit or Microsoft's Unity extension.",
  debugType: "unity-debugger-pure",
  defaultConfigurationName: "Attach to Unity Debugger Pure",
  commandIds: {
    refreshTargets: "unity-debugger-pure.refreshTargets",
    openLogs: "unity-debugger-pure.openLogs",
    copyDiagnostics: "unity-debugger-pure.copyDiagnostics",
    attachUnityDebugger: "unity-debugger-pure.attachUnityDebugger",
    showUnityApiReference: "unity-debugger-pure.showUnityApiReference",
  },
  /**
   * A setting id is `<extensionName>.<settingName>`, which is why ours ends in
   * our own extension name where the reference's ends in `vstuc`.
   */
  settingIds: {
    displayBackgroundProcesses:
      `${EXTENSION_NAME}.${DISPLAY_BACKGROUND_PROCESSES_SETTING}`,
  },
  adapterExecutable: "UnityDebuggerPure.exe",
  diagnosticsDirectoryName: "unity-debugger-pure",
  vsixFileName: "unity-debugger-pure-0.3.3.vsix",
} as const;
