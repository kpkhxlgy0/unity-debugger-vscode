import { isIPv4 } from "node:net";
import { UNITY_NOT_RUNNING_MESSAGE } from "./diagnostics.js";
import type {
  EditorCandidate,
  UnityAttachConfiguration,
} from "./model.js";
import { PRODUCT_IDENTITY } from "./productIdentity.js";
import { classifyVersion } from "./versionPolicy.js";

/**
 * The reference extension accepts only an address literal plus a debugger
 * port, and answers `success` for an endpoint it cannot parse before
 * attaching to nothing. We reject it here instead, before discovery runs.
 */
const ENDPOINT_ERROR_MESSAGE =
  "endPoint must be an IP address literal and a port, such as " +
  "192.168.1.50:56234.";
const WORKSPACE_FOLDER_VARIABLE = "${workspaceFolder}";
const MAX_PORT = 65_535;

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

export interface ApiAttachRequestResolver {
  consumeRequest(
    requestId: string,
    workspaceRoot: string,
  ): EditorCandidate;
}

interface AttachOptions {
  readonly apiAttachRequestId?: string;
  readonly logFile?: string;
}

/**
 * Everything the adapter's attach request needs to be built. An `endPoint`
 * attach has no discovered candidate behind it, so the fields are named
 * individually instead of being faked into a candidate.
 */
interface AttachTarget {
  readonly processId: number;
  readonly host: string;
  readonly port: number;
  readonly workspaceRoot: string;
  readonly projectVersion: string;
}

interface ResolvedEndPoint {
  readonly host: string;
  readonly port: number;
}

export class DebugConfigurationProvider {
  public constructor(
    private readonly discovery: EditorDiscoveryLike,
    private readonly ui: ConfigurationUi,
    private readonly listWorkspaceRoots: (
      currentFolder: string,
    ) => readonly string[],
    private readonly apiAttachRequests?: ApiAttachRequestResolver,
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

    const name = String(
      configuration.name ?? PRODUCT_IDENTITY.defaultConfigurationName,
    );
    // `projectPath` is a debug-configuration attribute, not a setting, and
    // `${workspaceFolder}` has not been substituted when this provider runs.
    // It is never required: the workspace folder is the default.
    const projectPath =
      expandWorkspaceFolder(
        configuration.projectPath,
        folder.uri.fsPath,
      ) ?? folder.uri.fsPath;
    const logFile = logFileFrom(configuration);
    const attachOptions: AttachOptions = { logFile };

    const hasApiRequestId = Object.prototype.hasOwnProperty.call(
      configuration,
      "__apiAttachRequestId",
    );
    if (hasApiRequestId) {
      const apiRequestId = configuration.__apiAttachRequestId;
      if (typeof apiRequestId !== "string" || apiRequestId.length === 0) {
        await this.showInvalidApiAttachRequest();
        return undefined;
      }

      let candidate: EditorCandidate;
      try {
        const consumed = this.apiAttachRequests?.consumeRequest(
          apiRequestId,
          folder.uri.fsPath,
        );
        if (!consumed) {
          throw new Error("No trusted API request resolver is active.");
        }
        candidate = consumed;
      } catch {
        await this.showInvalidApiAttachRequest();
        return undefined;
      }

      return this.applyCandidatePolicy(candidate, name, {
        ...attachOptions,
        apiAttachRequestId: apiRequestId,
      });
    }

    // A property of the wrong type is not an endpoint: VS Code's own
    // launch.json validation rejects it before this provider runs.
    const endPoint =
      typeof configuration.endPoint === "string" &&
      configuration.endPoint.length > 0
        ? configuration.endPoint
        : undefined;
    if (endPoint !== undefined) {
      const resolved = parseEndPoint(endPoint);
      if (!resolved) {
        await this.ui.showError(ENDPOINT_ERROR_MESSAGE);
        return undefined;
      }

      // An explicit endPoint skips discovery, so nothing supplies a process
      // id or an Editor version. 0 and "" are the "unknown" values the
      // adapter accepts; a fabricated positive process id would be
      // indistinguishable from a real one in a log.
      return toAttachConfiguration(
        {
          processId: 0,
          host: resolved.host,
          port: resolved.port,
          workspaceRoot: projectPath,
          projectVersion: "",
        },
        name,
        attachOptions,
      );
    }

    const roots = this.listWorkspaceRoots(projectPath);
    let candidates = await this.discovery.discover(roots);
    if (candidates.length === 0) {
      const action = await this.ui.showNoEditor(UNITY_NOT_RUNNING_MESSAGE);
      if (action !== "refresh") {
        return undefined;
      }

      candidates = await this.discovery.discover(roots);
      if (candidates.length === 0) {
        await this.ui.showError(UNITY_NOT_RUNNING_MESSAGE);
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

    return this.applyCandidatePolicy(candidate, name, attachOptions);
  }

  private async applyCandidatePolicy(
    candidate: EditorCandidate,
    name: string,
    options: AttachOptions,
  ): Promise<UnityAttachConfiguration | undefined> {
    const decision = classifyVersion(candidate.projectVersion);
    if (decision.level === "unsupported") {
      await this.ui.showError(decision.warning!);
      return undefined;
    }

    return toAttachConfiguration(
      {
        processId: candidate.processId,
        host: candidate.host,
        port: candidate.port,
        workspaceRoot: candidate.workspaceRoot,
        projectVersion: candidate.projectVersion,
      },
      name,
      options,
    );
  }

  private async showInvalidApiAttachRequest(): Promise<void> {
    await this.ui.showError(
      "The API attach request is invalid or expired. Discover targets again.",
    );
  }
}

function toAttachConfiguration(
  target: AttachTarget,
  name: string,
  options: AttachOptions,
): UnityAttachConfiguration {
  return {
    name,
    type: PRODUCT_IDENTITY.debugType,
    request: "attach",
    __processId: target.processId,
    // The target's own host, never a literal: an Editor candidate carries
    // loopback and a Player candidate carries its advertised LAN address, so
    // a literal would resolve a Player the user chose to whatever Editor
    // happens to listen on this machine.
    __host: target.host,
    __port: target.port,
    __workspaceRoot: target.workspaceRoot,
    __projectVersion: target.projectVersion,
    ...(options.apiAttachRequestId
      ? { __apiAttachRequestId: options.apiAttachRequestId }
      : {}),
    ...(options.logFile ? { __logFile: options.logFile } : {}),
  };
}

/**
 * `projectPath` is declared with the default `${workspaceFolder}`; a value
 * that still carries that variable is substituted against the open folder
 * here. A value that is absent, blank or not a string is not a project path
 * and falls back to the workspace folder, so the property is never required.
 */
function expandWorkspaceFolder(
  value: unknown,
  workspaceFolder: string,
): string | undefined {
  if (typeof value !== "string" || value.trim().length === 0) {
    return undefined;
  }

  return value.split(WORKSPACE_FOLDER_VARIABLE).join(workspaceFolder);
}

/**
 * `logFile` is an optional diagnostics mirror. An absent or empty value is
 * simply not a redirect target and must never block an attach.
 */
function logFileFrom(
  configuration: Readonly<Record<string, unknown>>,
): string | undefined {
  const value = configuration.logFile;
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

/**
 * `endPoint` is `ip:port` — the shape the reference extension itself produces
 * (`address + ":" + debuggerPort`) and the string it ships as the property
 * description. A host name is not resolved and a missing or out-of-range port
 * is not defaulted: .NET Framework's `IPAddress.Parse` silently rewrites an
 * abbreviated or zero-padded literal (`1.2.3` becomes `1.2.0.3`, `010.1.1.1`
 * becomes `8.1.1.1`), which would attach to a machine nobody asked for.
 *
 * Only an address literal is accepted, so a bracketed IPv6 literal is
 * rejected too: `ip:port` has no unambiguous IPv6 form and neither the
 * reference nor the manifest description defines one.
 */
function parseEndPoint(value: string): ResolvedEndPoint | undefined {
  const separator = value.lastIndexOf(":");
  if (separator === -1) {
    return undefined;
  }

  const host = value.slice(0, separator);
  const portText = value.slice(separator + 1);
  if (!isIPv4(host) || !/^\d+$/.test(portText)) {
    return undefined;
  }

  const port = Number.parseInt(portText, 10);
  if (port < 1 || port > MAX_PORT) {
    return undefined;
  }

  return { host, port };
}
