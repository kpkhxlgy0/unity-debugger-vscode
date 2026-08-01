# Initial Debug Configuration Design

## Goal

Make VS Code create a usable Unity Debugger Pure attach entry when a user
creates `launch.json` from the Run and Debug UI. The generated entry must be
the same minimal attach configuration already offered by IntelliSense.

## Design

Add one `initialConfigurations` entry to the existing
`contributes.debuggers[0]` manifest contribution:

```json
{
  "name": "Attach to Unity Debugger Pure",
  "type": "unity-debugger-pure",
  "request": "attach"
}
```

Keep `configurationSnippets` unchanged and require the initial configuration
to remain byte-for-byte equivalent to its snippet body. Do not add host, port,
target, or executable fields: target discovery remains interactive at attach
time. No extension runtime code or version changes are required.

## Verification

- A build test reads the source manifest and requires exactly one initial
  configuration equal to the existing snippet body.
- The VSIX audit reads the packaged manifest and enforces the same contract.
- Existing type checks, extension tests, Adapter tests, integration tests, and
  VSIX packaging/audit remain green.

## Scope

This change belongs only to the Unity Debugger Pure repository. It does not
modify the MCP companion, launcher, debugger protocol, or Editor integration.
