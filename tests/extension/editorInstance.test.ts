import { describe, expect, it } from "vitest";
import {
  defaultEditorPort,
  parseEditorInstance,
} from "../../extension/src/editorInstance.js";

describe("parseEditorInstance", () => {
  it("accepts a positive integer process ID", () => {
    expect(parseEditorInstance('{"process_id": 1234}')).toEqual({
      processId: 1234,
    });
  });

  it.each([
    "[]",
    "{}",
    '{"process_id": 0}',
    '{"process_id": 1.5}',
    '{"process_id": "1234"}',
    "not json",
  ])("rejects invalid EditorInstance metadata: %s", (json) => {
    expect(() => parseEditorInstance(json)).toThrow(
      "EditorInstance.json has an invalid process_id",
    );
  });
});

describe("defaultEditorPort", () => {
  it("derives the Unity debugger port from the process ID", () => {
    expect(defaultEditorPort(1234)).toBe(56234);
  });

  it.each([0, -1, 1.5, Number.NaN])(
    "rejects invalid process ID %s",
    (processId) => {
      expect(() => defaultEditorPort(processId)).toThrow(
        "Editor process ID must be a positive integer",
      );
    },
  );
});
