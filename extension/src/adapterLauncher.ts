import { existsSync } from "node:fs";
import path from "node:path";

export interface AdapterExecutableDescriptor {
  readonly command: string;
  readonly args: readonly string[];
  readonly options: { readonly cwd: string };
}

export class AdapterLauncher {
  public constructor(
    private readonly fileExists: (filePath: string) => boolean = existsSync,
    private readonly platform: string = process.platform,
    private readonly architecture: string = process.arch,
  ) {}

  public createDescriptor(
    extensionPath: string,
  ): AdapterExecutableDescriptor {
    if (this.platform !== "win32" || this.architecture !== "x64") {
      throw new Error(
        "The Unity debug adapter supports Windows x64 only.",
      );
    }

    const command = path.join(
      extensionPath,
      "adapter",
      "win32-x64",
      "UnityCommunityDebug.exe",
    );
    if (!this.fileExists(command)) {
      throw new Error(
        "The packaged Unity debug adapter is missing. " +
          "Reinstall the extension.",
      );
    }

    return {
      command,
      args: [],
      options: { cwd: path.dirname(command) },
    };
  }
}
