import { randomUUID } from "node:crypto";
import path from "node:path";
import type { EditorCandidate } from "./model.js";
import {
  PublicApiError,
  type PublicEditorTarget,
} from "./publicApiTypes.js";

const TOKEN_TTL_MS = 60_000;

interface TargetRecord {
  readonly candidate: EditorCandidate;
  readonly expiresAt: number;
}

interface RequestRecord extends TargetRecord {
  readonly targetId: string;
}

export interface PreparedAttachRequest {
  readonly requestId: string;
  readonly targetId: string;
  readonly workspaceRoot: string;
}

export class AttachRequestRegistry {
  private readonly targets = new Map<string, TargetRecord>();
  private readonly requests = new Map<string, RequestRecord>();

  public constructor(
    private readonly now: () => number = Date.now,
    private readonly createId: () => string = randomUUID,
  ) {}

  public issue(candidate: EditorCandidate): PublicEditorTarget {
    const scopedCandidate: EditorCandidate = {
      ...candidate,
      workspaceRoot: canonical(candidate.workspaceRoot),
    };
    const targetId = this.createId();
    this.targets.set(targetId, {
      candidate: scopedCandidate,
      expiresAt: this.now() + TOKEN_TTL_MS,
    });
    return {
      targetId,
      processId: scopedCandidate.processId,
      projectName: scopedCandidate.projectName,
      workspaceRoot: scopedCandidate.workspaceRoot,
      projectVersion: scopedCandidate.projectVersion,
      source: scopedCandidate.source,
    };
  }

  public prepare(targetId: string): PreparedAttachRequest {
    const record = this.takeLive(this.targets, targetId);
    const requestId = this.createId();
    this.requests.set(requestId, {
      candidate: record.candidate,
      expiresAt: this.now() + TOKEN_TTL_MS,
      targetId,
    });
    return {
      requestId,
      targetId,
      workspaceRoot: record.candidate.workspaceRoot,
    };
  }

  public consumeRequest(
    requestId: string,
    workspaceRoot: string,
  ): EditorCandidate {
    const record = this.takeLive(this.requests, requestId);
    if (canonical(record.candidate.workspaceRoot) !== canonical(workspaceRoot)) {
      throw new PublicApiError(
        "WORKSPACE_NOT_ALLOWED",
        "The attach request belongs to another workspace.",
      );
    }
    return record.candidate;
  }

  private takeLive<T extends TargetRecord>(
    source: Map<string, T>,
    id: string,
  ): T {
    const record = source.get(id);
    source.delete(id);
    if (!record || record.expiresAt <= this.now()) {
      throw new PublicApiError(
        "TARGET_EXPIRED",
        "The Editor target expired; discover targets again.",
      );
    }
    return record;
  }
}

function canonical(value: string): string {
  const normalized = path.resolve(value);
  return process.platform === "win32"
    ? normalized.toLowerCase()
    : normalized;
}
