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
  },
  adapterExecutable: "UnityDebuggerPure.exe",
  diagnosticsDirectoryName: "unity-debugger-pure",
  vsixFileName: "unity-debugger-pure-0.1.0.vsix",
} as const;
