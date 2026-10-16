import { describe, expect, it } from "vitest";
import { parsePlayerAdvertisement } from "../../extension/src/playerAdvertisement.js";

const packet =
  "[IP] 192.168.1.20 [Port] 55000 [Flags] 3 [Guid] 1234 " +
  "[EditorId] 9876 [Version] 1048832 [Id] WindowsEditor(Tuanjie) " +
  "[Debug] 1 [DebuggerPort] 56234 [ProjectName] FixtureProject";

// Captured live from an Android player on the Unity multicast group.
const androidPlayerPacket =
  "[IP] 10.30.154.73 [Port] 55000 [Flags] 19 [Guid] 3598034322 [EditorId] 2737672203 " +
  "[Version] 1048832 [Id] AndroidPlayer(11,samsung_SM-S9260@10.30.154.73) [Debug] 0 " +
  "[PackageName] AndroidPlayer [ProjectName] 头号禁区";

describe("parsePlayerAdvertisement", () => {
  it("reads a PlayerConnection record without trusting its remote IP", () => {
    expect(parsePlayerAdvertisement(packet)).toMatchObject({
      ip: "192.168.1.20",
      debuggerPort: 56234,
      projectName: "FixtureProject",
      allowDebugging: true,
      machine: "192.168.1.20",
    });
  });

  it("parses the machine label out of the [Id] field", () => {
    const parsed = parsePlayerAdvertisement(androidPlayerPacket);

    expect(parsed.machine).toBe("samsung_SM-S9260@10.30.154.73");
    expect(parsed.allowDebugging).toBe(false);
  });

  it("falls back to the advertised IP when [Id] carries no machine", () => {
    const withoutMachine = packet.replace(
      "[Id] WindowsEditor(Tuanjie)",
      "[Id] WindowsEditor",
    );

    expect(parsePlayerAdvertisement(withoutMachine).machine).toBe(
      "192.168.1.20",
    );
  });

  it("rejects a missing required field", () => {
    expect(() =>
      parsePlayerAdvertisement(packet.replace("[Guid] 1234 ", "")),
    ).toThrow("Advertisement is missing guid");
  });

  it("records an explicit debug-disabled advertisement", () => {
    expect(
      parsePlayerAdvertisement(packet.replace("[Debug] 1", "[Debug] 0"))
        .allowDebugging,
    ).toBe(false);
  });

  it.each(["-1", "65536", "1.5", "not-a-port"])(
    "rejects invalid debugger port %s",
    (port) => {
      expect(() =>
        parsePlayerAdvertisement(
          packet.replace("[DebuggerPort] 56234", `[DebuggerPort] ${port}`),
        ),
      ).toThrow("Advertisement has an invalid debugger port");
    },
  );
});

export { androidPlayerPacket, packet };
