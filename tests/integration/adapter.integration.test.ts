import { execFileSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, beforeAll, describe, expect, it } from "vitest";
import { DapClient } from "./dapClient.js";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
);
const testAdapterProject = path.join(
  repositoryRoot,
  "tests",
  "integration",
  "UnityDebugger.TestAdapter",
  "UnityDebugger.TestAdapter.csproj",
);
const testAdapterPath = path.join(
  repositoryRoot,
  "tests",
  "integration",
  "UnityDebugger.TestAdapter",
  "bin",
  "Release",
  "net48",
  "UnityDebugger.TestAdapter.exe",
);
const fixtureRoot = path.join(
  repositoryRoot,
  "tests",
  "integration",
  "fixture",
);
const fixtureSource = path.join(
  fixtureRoot,
  "Assets",
  "DebuggerFixture.cs",
);

const clients: DapClient[] = [];

beforeAll(() => {
  execFileSync(
    "dotnet",
    ["build", testAdapterProject, "-c", "Release", "--no-restore"],
    { cwd: repositoryRoot, stdio: "pipe" },
  );
});

afterEach(async () => {
  await Promise.all(clients.splice(0).map((client) => client.dispose()));
});

describe("Unity debug adapter process", () => {
  it("runs a complete attach, inspect, evaluate, and step session", async () => {
    const client = await start("normal");
    await initialize(client);
    await attach(client);
    const breakpoint = await client.request("setBreakpoints", {
      source: { path: fixtureSource },
      breakpoints: [{ line: 12, condition: "health <= 0" }],
      sourceModified: false,
    });
    expect(breakpoint.body.breakpoints[0]).toMatchObject({
      id: 1,
      verified: true,
      line: 12,
    });

    const stopped = await client.waitForEvent("stopped");
    expect(stopped.body.hitBreakpointIds).toEqual([1]);
    const threads = await client.request("threads", {});
    expect(threads.body.threads).toEqual([
      { id: stopped.body.threadId, name: "Main Thread" },
    ]);
    const stack = await client.request("stackTrace", {
      threadId: stopped.body.threadId,
      startFrame: 0,
      levels: 20,
    });
    expect(stack.body.stackFrames[0]).toMatchObject({
      name: "FixtureBehaviour.Update",
      line: 12,
    });
    const scopes = await client.request("scopes", {
      frameId: stack.body.stackFrames[0].id,
    });
    const variables = await client.request("variables", {
      variablesReference: scopes.body.scopes[0].variablesReference,
    });
    expect(variables.body.variables[0]).toMatchObject({
      name: "health",
      value: "0",
      type: "System.Int32",
    });
    const evaluation = await client.request("evaluate", {
      expression: "health",
      frameId: stack.body.stackFrames[0].id,
      context: "watch",
    });
    expect(evaluation.body.result).toBe("0");

    await client.request("next", {
      threadId: stopped.body.threadId,
    });
    await client.request("disconnect", { restart: false });
    await client.expectCleanExit(0);
  });

  it("preserves a logical breakpoint id across a domain reload rebind", async () => {
    const client = await start("reload");
    await initialize(client);
    await attach(client);
    await client.request("setBreakpoints", {
      source: { path: fixtureSource },
      breakpoints: [{ line: 12 }],
    });

    const stopped = await client.waitForEvent("stopped");
    expect(stopped.body).toMatchObject({
      reason: "breakpoint",
      allThreadsStopped: true,
    });

    const preserved = await client.request("setBreakpoints", {
      source: { path: fixtureSource },
      breakpoints: [{ line: 12 }],
    });
    expect(preserved.body.breakpoints[0]).toMatchObject({
      id: 1,
      verified: true,
    });

    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("reports exception stops and supports every step operation", async () => {
    const exceptionClient = await start("exception");
    await initialize(exceptionClient);
    await attach(exceptionClient);
    await exceptionClient.request("setExceptionBreakpoints", {
      filters: ["all"],
    });
    const exception = await exceptionClient.waitForEvent("stopped");
    expect(exception.body.reason).toBe("exception");
    await exceptionClient.request("disconnect", {});
    await exceptionClient.expectCleanExit(0);

    const stepClient = await start("normal");
    await initialize(stepClient);
    await attach(stepClient);
    await stepClient.request("setBreakpoints", {
      source: { path: fixtureSource },
      breakpoints: [{ line: 12 }],
    });
    let stopped = await stepClient.waitForEvent("stopped");
    for (const command of ["stepIn", "next", "stepOut"]) {
      await stepClient.request(command, {
        threadId: stopped.body.threadId,
      });
      stopped = await stepClient.waitForEvent("stopped");
      expect(stopped.body.reason).toBe("step");
    }
    await stepClient.request("disconnect", {});
    await stepClient.expectCleanExit(0);
  });

  it("rejects invalid protocol requests without duplicate responses", async () => {
    const client = await start("protocol-error");
    await initialize(client);
    await expect(
      client.request("launch", { program: "not-allowed" }),
    ).rejects.toThrow("attach only");
    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("reports a backend failure and keeps the DAP process alive", async () => {
    const client = await start("backend-crash");
    await initialize(client);
    await attach(client);
    await expect(client.request("threads", {})).rejects.toThrow(
      "Managed inspection failed",
    );
    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("preserves expression errors for hover and watch", async () => {
    const client = await start("evaluation-error");
    await initialize(client);
    await attach(client);

    await expect(
      client.request("evaluate", {
        expression: "DefinitelyMissingName",
        frameId: 1,
        context: "hover",
      }),
    ).rejects.toThrow(
      "The identifier `DefinitelyMissingName` is not in the scope",
    );
    await expect(
      client.request("evaluate", {
        expression: "DefinitelyMissingName",
        frameId: 1,
        context: "watch",
      }),
    ).rejects.toThrow(
      "The identifier `DefinitelyMissingName` is not in the scope",
    );

    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("returns an empty collection for a stale stopped-context handle", async () => {
    const client = await start("stale-handle");
    await initialize(client);
    await attach(client);
    await client.request("setBreakpoints", {
      source: { path: fixtureSource },
      breakpoints: [{ line: 12 }],
    });
    const stopped = await client.waitForEvent("stopped");
    const stack = await client.request("stackTrace", {
      threadId: stopped.body.threadId,
      startFrame: 0,
      levels: 20,
    });
    const scopes = await client.request("scopes", {
      frameId: stack.body.stackFrames[0].id,
    });
    const staleReference = scopes.body.scopes[0].variablesReference;

    await client.request("continue", { threadId: stopped.body.threadId });
    await client.waitForEvent("stopped");
    const stale = await client.request("variables", {
      variablesReference: staleReference,
    });

    expect(stale.body.variables).toEqual([]);
    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("omits unavailable pause sources and enables implicit evaluation by default", async () => {
    const client = await start("pause-source");
    const capabilities = await client.request("initialize", {
      adapterID: "unity-debugger-pure",
      linesStartAt1: true,
      columnsStartAt1: true,
      pathFormat: "path",
    });
    expect(capabilities.body.supportsEvaluateForHovers).toBe(true);
    await client.waitForEvent("initialized");
    await attach(client);

    const threads = await client.request("threads", {});
    const threadId = threads.body.threads[0].id;
    await client.request("pause", { threadId });
    const stopped = await client.waitForEvent("stopped");
    expect(stopped.body.reason).toBe("pause");
    expect(stopped.body).not.toHaveProperty("hitBreakpointIds");

    const stack = await client.request("stackTrace", {
      threadId,
      startFrame: 0,
      levels: 20,
    });
    expect(stack.body.stackFrames[0]).toMatchObject({
      name: "UnityEngine.PlayerLoop",
      presentationHint: "subtle",
    });
    expect(stack.body.stackFrames[0]).not.toHaveProperty("source");
    expect(stack.body.stackFrames[1].source.path).toBe(fixtureSource);

    const scopes = await client.request("scopes", {
      frameId: stack.body.stackFrames[1].id,
    });
    const variables = await client.request("variables", {
      variablesReference: scopes.body.scopes[0].variablesReference,
    });
    expect(variables.body.variables[0].value).toBe("implicit-enabled");

    const hover = await client.request("evaluate", {
      expression: "_isVisible",
      frameId: stack.body.stackFrames[1].id,
      context: "hover",
    });
    expect(hover.body.result).toBe("implicit-enabled");

    await client.request("continue", { threadId });
    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });

  it("keeps mature implicit evaluation enabled regardless of the attach flag", async () => {
    const client = await start("pause-source");
    await initialize(client);
    await attach(client, false);

    const threads = await client.request("threads", {});
    const threadId = threads.body.threads[0].id;
    await client.request("pause", { threadId });
    await client.waitForEvent("stopped");
    const stack = await client.request("stackTrace", {
      threadId,
      startFrame: 0,
      levels: 20,
    });
    const frameId = stack.body.stackFrames[1].id;
    const scopes = await client.request("scopes", { frameId });
    const variables = await client.request("variables", {
      variablesReference: scopes.body.scopes[0].variablesReference,
    });
    expect(variables.body.variables[0].value).toBe("implicit-enabled");

    const hover = await client.request("evaluate", {
      expression: "_isVisible",
      frameId,
      context: "hover",
    });
    expect(hover.body.result).toBe("implicit-enabled");

    const watch = await client.request("evaluate", {
      expression: "_isVisible",
      frameId,
      context: "watch",
    });
    expect(watch.body.result).toBe("implicit-enabled");

    await client.request("disconnect", {});
    await client.expectCleanExit(0);
  });
});

async function start(scenario: string): Promise<DapClient> {
  const client = await DapClient.start(testAdapterPath, [scenario]);
  clients.push(client);
  return client;
}

async function initialize(client: DapClient): Promise<void> {
  await client.request("initialize", {
    adapterID: "unity-debugger-pure",
    linesStartAt1: true,
    columnsStartAt1: true,
    pathFormat: "path",
  });
  await client.waitForEvent("initialized");
}

async function attach(
  client: DapClient,
  enableImplicitEvaluation?: boolean,
): Promise<void> {
  await client.request("attach", {
    __processId: 1234,
    __host: "127.0.0.1",
    __port: 56234,
    __workspaceRoot: fixtureRoot,
    __projectVersion: "2022.3.62t11",
    ...(enableImplicitEvaluation === undefined
      ? {}
      : { __enableImplicitEvaluation: enableImplicitEvaluation }),
  });
}
