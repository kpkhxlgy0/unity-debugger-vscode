export type SupportLevel =
  | "supported"
  | "compatible-unverified"
  | "unsupported";

export interface VersionDecision {
  readonly level: SupportLevel;
  readonly version: string;
  readonly warning: string | undefined;
}

export interface EditorCandidate {
  readonly processId: number;
  readonly projectName: string;
  readonly workspaceRoot: string;
  readonly host: "127.0.0.1";
  readonly port: number;
  readonly projectVersion: string;
  readonly source: "advertisement" | "derived-port";
}

export interface UnityAttachConfiguration {
  readonly name: string;
  readonly type: "unity-community";
  readonly request: "attach";
  readonly __processId: number;
  readonly __host: "127.0.0.1";
  readonly __port: number;
  readonly __workspaceRoot: string;
  readonly __projectVersion: string;
}
