# GitHub About Metadata Design

## Goal

Make the public repository easier to identify, discover, and install from
without changing repository features or source code.

## Scope

Only the GitHub repository About metadata is changed:

- description;
- website;
- topics.

Issues, Projects, Wiki, Discussions, security settings, visibility, and branch
configuration remain unchanged.

## Approved Metadata

Description:

```text
Pure managed C# debugging for local Unity 2022 and Tuanjie Editors.
```

Website:

```text
https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure
```

Topics:

```text
unity
unity3d
tuanjie
vscode
vscode-extension
debugger
unity-debugger
debug-adapter-protocol
csharp
mono
gamedev
```

## Validation

After applying the settings, read the repository metadata through the GitHub
API and confirm that the description, website, and complete topic set match
this specification. Confirm that no repository feature toggle changed.
