const REQUIRED_FIELDS = [
  "ip",
  "port",
  "guid",
  "editorid",
  "version",
  "id",
  "debug",
] as const;

const MACHINE_PATTERN = /^([^()]*)\(([^,]*),([^)]*)\)$/;

export interface PlayerAdvertisement {
  readonly ip: string;
  readonly debuggerPort: number;
  readonly projectName: string;
  readonly id: string;
  readonly allowDebugging: boolean;
  readonly machine: string;
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

  const ip = fields.get("ip")!;
  const id = fields.get("id")!;
  return {
    ip,
    debuggerPort,
    projectName: fields.get("projectname") ?? "",
    id,
    allowDebugging: fields.get("debug") === "1",
    machine: machineFromId(id, ip),
  };
}

/**
 * The machine label the reference reports for a target: the second
 * comma-separated component inside the `[Id]` parentheses, e.g.
 * `AndroidPlayer(11,samsung_SM-S9260@10.30.154.73)` yields
 * `samsung_SM-S9260@10.30.154.73`. An `[Id]` without that shape carries no
 * machine, so the advertised address stands in for it.
 */
function machineFromId(id: string, ip: string): string {
  const match = MACHINE_PATTERN.exec(id);
  return match ? match[3] : ip;
}
