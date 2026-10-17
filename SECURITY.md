# Security Policy

Report suspected vulnerabilities privately through GitHub Security Advisories
for this repository. Do not open a public issue until a fix or coordinated
disclosure plan is available.

Include the extension version, Editor version, Windows version, impact, and a
minimal reproduction description. Attach only diagnostics produced by the
extension's sanitized diagnostics command. Do not submit project source,
expressions, raw variable values, credentials, Unity debug ports exposed to the
public internet, or unsanitized logs.

Version `0.3.2` attaches to a discovered local Editor by default, and also
supports an explicit `endPoint` in `launch.json` naming any `ip:port`, including
a non-loopback one. Unity's managed debug transport has **no authentication**:
anyone who can reach the debugger port can attach, and an attached debugger can
execute code in the target Editor or player. Expose a remote endpoint only on a
network you trust and control. The extension adds no transport authentication.
