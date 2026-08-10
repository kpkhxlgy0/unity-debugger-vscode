import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";
import { load } from "js-yaml";

test("only Open VSX has an automated registry publishing workflow", () => {
  assert.equal(
    fs.existsSync(".github/workflows/publish-marketplace.yml"),
    false,
  );

  const openVsx = loadPublishJob(
    ".github/workflows/publish-open-vsx.yml",
  );

  assert.equal(openVsx.environment, "open-vsx");

  assert.equal(openVsx["runs-on"], "windows-latest");
  const commands = runBodies(openVsx);
  assert.match(commands, /gh release download/);
  assert.match(commands, /verify-release-artifact\.mjs/);
  assert.doesNotMatch(commands, /npm run package/);
  assert.doesNotMatch(
    commands,
    /dotnet (?:restore|build|test)/,
  );

  const openVsxValidation = namedStep(
    openVsx,
    "Confirm namespace ownership and credentials",
  );
  assert.deepEqual(openVsxValidation.env, {
    OVSX_PAT: "${{ secrets.OVSX_PAT }}",
    OVSX_NAMESPACE: "${{ vars.OVSX_NAMESPACE }}",
    OWNERSHIP_CONFIRMED:
      "${{ vars.OPEN_VSX_OWNERSHIP_CONFIRMED }}",
  });
});

test("release workflows use the 0.3.0 audited artifact", () => {
  const release = loadWorkflow(".github/workflows/release.yml");
  const publish = loadWorkflow(
    ".github/workflows/publish-open-vsx.yml",
  );

  for (const job of [release.jobs.release, publish.jobs.publish]) {
    const commands = runBodies(job);
    assert.match(commands, /unity-debugger-pure-0\.3\.0\.vsix/);
    assert.match(commands, /unity-debugger-pure-0\.3\.0\.vsix\.sha256/);
    assert.doesNotMatch(commands, /unity-debugger-pure-0\.2\.0/);
  }
});

test("registry CLIs are direct pinned development dependencies", () => {
  const manifest = JSON.parse(fs.readFileSync("package.json", "utf8"));
  assert.equal(manifest.devDependencies["@vscode/vsce"], "3.9.2");
  assert.equal(manifest.devDependencies.ovsx, "1.0.2");
  assert.equal(manifest.devDependencies["js-yaml"], "4.3.1");
});

function loadPublishJob(filePath) {
  const workflow = loadWorkflow(filePath);
  assert.deepEqual(workflow.permissions, { contents: "read" });
  assert.ok(workflow.jobs?.publish, `${filePath} has no publish job`);
  return workflow.jobs.publish;
}

function loadWorkflow(filePath) {
  return load(fs.readFileSync(filePath, "utf8"));
}

function runBodies(job) {
  return job.steps
    .filter((step) => typeof step.run === "string")
    .map((step) => step.run)
    .join("\n");
}

function namedStep(job, name) {
  const step = job.steps.find((candidate) => candidate.name === name);
  assert.ok(step, `Missing workflow step: ${name}`);
  return step;
}
