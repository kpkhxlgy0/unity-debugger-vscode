const REQUIRED_FIELDS = [
  "ip",
  "port",
  "guid",
  "editorid",
  "version",
  "id",
  "debug",
] as const;

export interface PlayerAdvertisement {
  readonly ip: string;
  readonly debuggerPort: number;
  readonly projectName: string;
  readonly id: string;
  readonly allowDebugging: boolean;
}

export function parsePlayerAdvertisement(
  record: string,
): PlayerAdvertisement {
  const fields = new Map<string, string>();

  for (const match of record
    .trimEnd()
    .matchAll(/\[([^\]]+)\]\s*([^\[]*)/g)) {
    const key = match[1].trim().toLowerCase();
    const value = match[2].trim().replace(/\0+$/, "");
    fields.set(key, value);
  }

  for (const key of REQUIRED_FIELDS) {
    if (!fields.has(key)) {
      throw new Error(`Advertisement is missing ${key}`);
    }
  }

  const debuggerPort = Number(fields.get("debuggerport") ?? 0);
  if (
    !Number.isInteger(debuggerPort) ||
    debuggerPort < 0 ||
    debuggerPort > 65_535
  ) {
    throw new Error("Advertisement has an invalid debugger port");
  }

  return {
    ip: fields.get("ip")!,
    debuggerPort,
    projectName: fields.get("projectname") ?? "",
    id: fields.get("id")!,
    allowDebugging: fields.get("debug") === "1",
  };
}
