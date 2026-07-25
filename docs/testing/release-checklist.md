# Release 0.1.0 real-Editor acceptance record

Copy this template to
`docs/testing/results/YYYY-MM-DD-tuanjie-2022.3.62t11.md` and complete every
field. Use `N/A (not installed)` only for the optional unverified-Editor check.

## Candidate identity

| Field | Recorded value |
| --- | --- |
| Acceptance date and timezone | |
| Tester | |
| Windows version and architecture | |
| Tuanjie full version | |
| VS Code version | |
| Extension version | `0.1.0` |
| Fixture/procedure commit SHA | |
| VSIX filename | `unity-debugger-vscode-0.1.0.vsix` |
| VSIX SHA-256 | |

## Acceptance criteria

| # | Criterion | Result | Evidence or notes |
| ---: | --- | --- | --- |
| 1 | Clean build, tests, third-party verification, and VSIX audit | | |
| 2 | Isolated profile contains only the candidate extension | | |
| 3 | Tuanjie installation, fixture, PID, and loopback port preflight | | |
| 4 | Standard and `health == 1` conditional breakpoints | | |
| 5 | Threads, Call Stack, Locals, Watch, and Debug Console evaluation | | |
| 6 | Step In, Step Over, and Step Out through `Decrement` | | |
| 7 | All-exception and uncaught-exception modes | | |
| 8 | Domain Reload pending and rebound breakpoint behavior | | |
| 9 | Disconnect leaves no Adapter process and reattach succeeds | | |
| 10 | ReSharper coexistence and unchanged ordinary .NET configurations | | |
| 11 | Unverified Editor emits exactly one warning, when installed | | |
| 12 | Diagnostics omit source, expressions, values, and full private paths | | |

## Sign-off

- Overall result:
- Known limitations observed:
- Sanitized diagnostic filename, if retained:
- Fixture restored with no diff:
- Repository clean:

The release remains unreleased if any required criterion is not `PASS`.
