import type {
  EditorCandidate,
  UnityAttachConfiguration,
} from "./model.js";
import { PRODUCT_IDENTITY } from "./productIdentity.js";
import { classifyVersion } from "./versionPolicy.js";

export interface ConfigurationUi {
  selectCandidate(
    candidates: readonly EditorCandidate[],
  ): Promise<EditorCandidate | undefined>;
  showNoEditor(message: string): Promise<"refresh" | undefined>;
  showError(message: string): Promise<void>;
}

export interface EditorDiscoveryLike {
  discover(
    workspaceRoots: readonly string[],
  ): Promise<readonly EditorCandidate[]>;
}

export class DebugConfigurationProvider {
  public constructor(
    private readonly discovery: EditorDiscoveryLike,
    private readonly ui: ConfigurationUi,
    private readonly listWorkspaceRoots: (
      currentFolder: string,
    ) => readonly string[],
  ) {}

  public async resolveDebugConfiguration(
    folder:
      | { readonly uri: { readonly fsPath: string } }
      | undefined,
    configuration: Readonly<Record<string, unknown>>,
  ): Promise<UnityAttachConfiguration | undefined> {
    if (!folder) {
      await this.ui.showError(
        "Open a Unity or Tuanjie project folder before attaching.",
      );
      return undefined;
    }

    if (
      configuration.type !== PRODUCT_IDENTITY.debugType ||
      configuration.request !== "attach"
    ) {
      await this.ui.showError(
        `${PRODUCT_IDENTITY.debugType} supports only request: attach.`,
      );
      return undefined;
    }

    const roots = this.listWorkspaceRoots(folder.uri.fsPath);
    let candidates = await this.discovery.discover(roots);
    if (candidates.length === 0) {
      const action = await this.ui.showNoEditor(
        "No local debug-enabled Tuanjie or Unity Editor was found. " +
          "Switch Code Optimization to Debug, then refresh.",
      );
      if (action !== "refresh") {
        return undefined;
      }

      candidates = await this.discovery.discover(roots);
      if (candidates.length === 0) {
        await this.ui.showError(
          "No local debug-enabled Editor was found after refresh.",
        );
        return undefined;
      }
    }

    const candidate =
      candidates.length === 1
        ? candidates[0]
        : await this.ui.selectCandidate(candidates);
    if (!candidate) {
      return undefined;
    }

    const decision = classifyVersion(candidate.projectVersion);
    if (decision.level === "unsupported") {
      await this.ui.showError(decision.warning!);
      return undefined;
    }

    return {
      name: String(
        configuration.name ??
          PRODUCT_IDENTITY.defaultConfigurationName,
      ),
      type: PRODUCT_IDENTITY.debugType,
      request: "attach",
      __processId: candidate.processId,
      __host: "127.0.0.1",
      __port: candidate.port,
      __workspaceRoot: candidate.workspaceRoot,
      __projectVersion: candidate.projectVersion,
    };
  }
}
