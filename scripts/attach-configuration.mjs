// The reviewed initial Attach configuration, and the single source of truth for
// the gates that assert it.
//
// The same fact is asserted against two different inputs:
// `scripts/verify-vsix.mjs` reads the built VSIX, and
// `tests/build/scaffold.test.mjs` (`expectedAttachConfiguration`) reads the
// source manifest. That test still carries its own copy of this object;
// importing this module there is the one-line change that would make the two
// gates agree by construction instead of by duplication.
//
// `projectPath` is part of the reviewed shape, not an addition to it. `ea0db8e`
// added it so Attach resolves the Unity project root, and the snippet body and
// the initial configuration carry the same key on purpose: a launch.json
// created from the snippet and one taken from the initial configuration must
// not differ for the same product. The expectation below therefore moves with
// the manifest, never back to the pre-Task-7 three-key shape.
export const attachConfiguration = {
  name: "Attach to Unity Debugger Pure",
  type: "unity-debugger-pure",
  request: "attach",
  projectPath: "${workspaceFolder}",
};
