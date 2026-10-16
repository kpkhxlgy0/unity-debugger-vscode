import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  parseProjectVersion,
  readProjectVersion,
} from "../../extension/src/projectVersion.js";

describe("parseProjectVersion", () => {
  it("reads the exact Tuanjie baseline", () => {
    expect(parseProjectVersion("m_EditorVersion: 2022.3.62t11\r\n")).toBe(
      "2022.3.62t11",
    );
  });

  it("rejects a missing editor version", () => {
    expect(() =>
      parseProjectVersion("m_EditorVersionWithRevision: x"),
    ).toThrow("ProjectVersion.txt does not contain m_EditorVersion");
  });

  it("reads ProjectSettings/ProjectVersion.txt as UTF-8", async () => {
    const workspaceRoot = await mkdtemp(
      path.join(os.tmpdir(), "unity-debugger-version-"),
    );

    try {
      const projectSettings = path.join(workspaceRoot, "ProjectSettings");
      await mkdir(projectSettings);
      await writeFile(
        path.join(projectSettings, "ProjectVersion.txt"),
        "m_EditorVersion: 2022.3.62t11\n",
        "utf8",
      );

      await expect(readProjectVersion(workspaceRoot)).resolves.toBe(
        "2022.3.62t11",
      );
    } finally {
      await rm(workspaceRoot, { recursive: true, force: true });
    }
  });
});

describe("readProjectVersion", () => {
  it("survives a project with no ProjectVersion.txt", async () => {
    const workspaceRoot = await mkdtemp(
      path.join(os.tmpdir(), "unity-debugger-version-"),
    );

    try {
      await expect(readProjectVersion(workspaceRoot)).resolves.toBe("");
    } finally {
      await rm(workspaceRoot, { recursive: true, force: true });
    }
  });

  it("survives a project directory that does not exist", async () => {
    await expect(readProjectVersion("H:\\fixture")).resolves.toBe("");
  });

  it("survives a ProjectVersion.txt without m_EditorVersion", async () => {
    const workspaceRoot = await mkdtemp(
      path.join(os.tmpdir(), "unity-debugger-version-"),
    );

    try {
      const projectSettings = path.join(workspaceRoot, "ProjectSettings");
      await mkdir(projectSettings);
      await writeFile(
        path.join(projectSettings, "ProjectVersion.txt"),
        "m_EditorVersionWithRevision: 2022.3.62t11 (b1a0a2c)\n",
        "utf8",
      );

      await expect(readProjectVersion(workspaceRoot)).resolves.toBe("");
    } finally {
      await rm(workspaceRoot, { recursive: true, force: true });
    }
  });
});
