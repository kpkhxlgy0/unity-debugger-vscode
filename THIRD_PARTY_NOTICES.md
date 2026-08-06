# Third-Party Notices

This repository retains debugger dependency sources from the recorded
revisions below. The corresponding license text is retained verbatim.

- `Unity-Technologies/vscode-mono-debug`
  (`d233b366b0c67ae4d61488f7e974e2a5b9da2e3b`) —
  [MIT license](third-party/licenses/vscode-mono-debug-MIT.txt)
- `Unity-Technologies/debugger-libs`
  (`cd005e941d18c92ddf0c50084c59ddaae6bf4c5d`) —
  [MIT license](third-party/licenses/debugger-libs-MIT.txt)
- `icsharpcode/NRefactory`
  (`0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5`) —
  [MIT license](third-party/licenses/nrefactory-MIT.txt)

The release Adapter builds `Mono.Debugger.Soft`, `Mono.Debugging`, and
`Mono.Debugging.Soft` from the pinned `debugger-libs` source. It also builds
`ICSharpCode.NRefactory` and `ICSharpCode.NRefactory.CSharp` from the pinned
NRefactory source for mature C# expression evaluation. These source-built
assemblies are linked and shipped with the Adapter.

The locked build also restores packages including
`Microsoft.NETFramework.ReferenceAssemblies`, `Newtonsoft.Json`, `Mono.Cecil`,
`Mono.Posix-4.5`, `System.Buffers`, `System.Collections.Immutable`, and Roslyn
workspace/runtime dependencies used by the pinned mature debugger projects.
Their license metadata and exact versions are recorded by NuGet lock files;
the release inventory added before packaging records every shipped assembly.

No precompiled binary from the three source repositories is copied into the
extension package.

The release pipeline records the exact shipped runtime assemblies, SHA-256
digests, origins, versions, and license identifiers in
`third-party/runtime-assemblies.json`.

All three pinned source repositories above are provided under the MIT License.
Their exact retained license texts are shipped under `third-party/licenses/`.
