# GitHub About Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Set the public repository description, Marketplace website, and approved discovery topics without changing any other repository setting.

**Architecture:** Treat GitHub repository metadata as the external configuration boundary. Capture the complete relevant state before the write, update only the About fields through authenticated GitHub APIs, then read the state back and compare every approved value and protected feature toggle.

**Tech Stack:** GitHub CLI 2.x, GitHub REST API, PowerShell

## Global Constraints

- Repository: `kpkhxlgy0/unity-debugger-vscode`.
- Only description, website, and topics may change.
- Issues, Projects, Wiki, Discussions, security settings, visibility, and branch configuration must remain unchanged.
- Description must equal `Pure managed C# debugging for local Unity 2022 and Tuanjie Editors.`.
- Website must equal `https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure`.
- Topics must equal the 11-topic set approved in the design specification.

---

### Task 1: Apply and Verify GitHub About Metadata

**Files:**
- Read: `docs/superpowers/specs/2026-07-26-github-about-design.md`
- Modify: GitHub repository About metadata for `kpkhxlgy0/unity-debugger-vscode`

**Interfaces:**
- Consumes: authenticated `gh` session with repository administration access
- Produces: repository metadata returned by `gh repo view` with the approved description, website, and topics

- [ ] **Step 1: Capture the current metadata and protected settings**

Run:

```powershell
gh repo view kpkhxlgy0/unity-debugger-vscode `
  --json description,homepageUrl,repositoryTopics,hasIssuesEnabled,hasProjectsEnabled,hasWikiEnabled,hasDiscussionsEnabled,visibility,defaultBranchRef
```

Expected before the write:

```text
description = Pure managed C# debugging for local Unity 2022 and Tuanjie Editors.
homepageUrl = empty
repositoryTopics = empty
hasIssuesEnabled = true
hasProjectsEnabled = true
hasWikiEnabled = true
hasDiscussionsEnabled = false
visibility = PUBLIC
defaultBranchRef.name = master
```

- [ ] **Step 2: Update only the description and website**

Run:

```powershell
gh repo edit kpkhxlgy0/unity-debugger-vscode `
  --description "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors." `
  --homepage "https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure"
```

Expected: exit code `0`.

- [ ] **Step 3: Replace the topic set atomically**

Run:

```powershell
gh api --method PUT `
  -H "Accept: application/vnd.github+json" `
  repos/kpkhxlgy0/unity-debugger-vscode/topics `
  -f "names[]=unity" `
  -f "names[]=unity3d" `
  -f "names[]=tuanjie" `
  -f "names[]=vscode" `
  -f "names[]=vscode-extension" `
  -f "names[]=debugger" `
  -f "names[]=unity-debugger" `
  -f "names[]=debug-adapter-protocol" `
  -f "names[]=csharp" `
  -f "names[]=mono" `
  -f "names[]=gamedev"
```

Expected: the returned `names` array contains all 11 approved topics.

- [ ] **Step 4: Verify the complete public metadata contract**

Run:

```powershell
$actual = gh repo view kpkhxlgy0/unity-debugger-vscode `
  --json description,homepageUrl,repositoryTopics,hasIssuesEnabled,hasProjectsEnabled,hasWikiEnabled,hasDiscussionsEnabled,visibility,defaultBranchRef |
  ConvertFrom-Json

$expectedTopics = @(
  "csharp",
  "debug-adapter-protocol",
  "debugger",
  "gamedev",
  "mono",
  "tuanjie",
  "unity",
  "unity-debugger",
  "unity3d",
  "vscode",
  "vscode-extension"
)
$actualTopics = @($actual.repositoryTopics.name | Sort-Object)

if ($actual.description -ne "Pure managed C# debugging for local Unity 2022 and Tuanjie Editors.") { throw "Description mismatch" }
if ($actual.homepageUrl -ne "https://marketplace.visualstudio.com/items?itemName=kpk.unity-debugger-pure") { throw "Homepage mismatch" }
if (Compare-Object $expectedTopics $actualTopics) { throw "Topics mismatch" }
if (-not $actual.hasIssuesEnabled) { throw "Issues setting changed" }
if (-not $actual.hasProjectsEnabled) { throw "Projects setting changed" }
if (-not $actual.hasWikiEnabled) { throw "Wiki setting changed" }
if ($actual.hasDiscussionsEnabled) { throw "Discussions setting changed" }
if ($actual.visibility -ne "PUBLIC") { throw "Visibility changed" }
if ($actual.defaultBranchRef.name -ne "master") { throw "Default branch changed" }
```

Expected: exit code `0` with no mismatch output.

- [ ] **Step 5: Push the reviewed documentation commits**

Run:

```powershell
git status --short --branch
git push origin master
```

Expected: the local `master` branch and `origin/master` point to the same commit.
