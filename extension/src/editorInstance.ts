export interface EditorInstance {
  readonly processId: number;
}

export function parseEditorInstance(json: string): EditorInstance {
  let value: unknown;
  try {
    value = JSON.parse(json);
  } catch {
    throw new Error("EditorInstance.json has an invalid process_id");
  }

  if (
    typeof value !== "object" ||
    value === null ||
    Array.isArray(value) ||
    !("process_id" in value) ||
    !Number.isInteger(value.process_id) ||
    (value.process_id as number) <= 0
  ) {
    throw new Error("EditorInstance.json has an invalid process_id");
  }

  return { processId: value.process_id as number };
}

export function defaultEditorPort(processId: number): number {
  if (!Number.isInteger(processId) || processId <= 0) {
    throw new Error("Editor process ID must be a positive integer");
  }

  return 56_000 + (processId % 1_000);
}
