import { describe, expect, it } from "vitest";
import { parsePlayerAdvertisement } from "../../extension/src/playerAdvertisement.js";

const packet =
  "[IP] 192.168.1.20 [Port] 55000 [Flags] 3 [Guid] 1234 " +
  "[EditorId] 9876 [Version] 1048832 [Id] WindowsEditor(Tuanjie) " +
  "[Debug] 1 [DebuggerPort] 56234 [ProjectName] FixtureProject";

describe("parsePlayerAdvertisement", () => {
  it("reads a PlayerConnection record without trusting its remote IP", () => {
    expect(parsePlayerAdvertisement(packet)).toMatchObject({
      ip: "192.168.1.20",
      debuggerPort: 56234,
      projectName: "FixtureProject",
      allowDebugging: true,
    });
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

export { packet };
