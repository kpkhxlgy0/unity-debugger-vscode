export type PublicTargetSource = "advertisement" | "derived-port";

export interface PublicEditorTarget {
  readonly targetId: string;
  readonly processId: number;
  readonly projectName: string;
  readonly workspaceRoot: string;
  readonly projectVersion: string;
  readonly source: PublicTargetSource;
}

export interface StartedDebugSession {
  readonly sessionId: string;
  readonly targetId: string;
}

export interface UnityDebuggerPureApiV1 {
  readonly apiVersion: 1;
  readonly extensionVersion: string;
  readonly debugType: "unity-debugger-pure";
  discoverTargets(
    workspaceRoots: readonly string[],
  ): Promise<readonly PublicEditorTarget[]>;
  startAttach(targetId: string): Promise<StartedDebugSession>;
}

export type PublicApiErrorCode =
  | "TARGET_EXPIRED"
  | "WORKSPACE_NOT_ALLOWED"
  | "ATTACH_FAILED";

export class PublicApiError extends Error {
  public constructor(
    public readonly code: PublicApiErrorCode,
    message: string,
  ) {
    super(message);
    this.name = "PublicApiError";
  }
}
