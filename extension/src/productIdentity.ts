export const PRODUCT_IDENTITY = {
  publisher: "kpk",
  extensionName: "unity-debugger-pure",
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
      "unity-debugger-pure.displayBackgroundProcesses",
  },
  adapterExecutable: "UnityDebuggerPure.exe",
  diagnosticsDirectoryName: "unity-debugger-pure",
  vsixFileName: "unity-debugger-pure-0.3.2.vsix",
} as const;
