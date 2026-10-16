import { PRODUCT_IDENTITY } from "./productIdentity.js";

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
  readonly host: string;
  readonly port: number;
  readonly projectVersion: string;
  readonly machine: string;
  /**
   * The reference's probe reports whether a target is a background process
   * created by the main Unity process, and its picker hides those unless the
   * `displayBackgroundProcesses` setting is on. Neither our UDP advertisement
   * nor `Library/EditorInstance.json` carries a parent-process relationship,
   * so this release cannot identify one and every candidate is `false`: the
   * filter and sort are implemented and tested against synthetic candidates
   * but stay inert against real discovery.
   */
  readonly isBackground: boolean;
  readonly source:
    | "advertisement"
    | "derived-port"
    | "player-advertisement";
}

export interface UnityAttachConfiguration {
  readonly name: string;
  readonly type: typeof PRODUCT_IDENTITY.debugType;
  readonly request: "attach";
  readonly __processId: number;
  readonly __host: string;
  readonly __port: number;
  readonly __workspaceRoot: string;
  readonly __projectVersion: string;
  readonly __apiAttachRequestId?: string;
  readonly __logFile?: string;
}
