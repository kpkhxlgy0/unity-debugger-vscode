import {
  spawn,
  type ChildProcessWithoutNullStreams,
} from "node:child_process";

type DapMessage = Record<string, any>;

interface PendingRequest {
  readonly command: string;
  readonly resolve: (message: DapMessage) => void;
  readonly reject: (error: Error) => void;
  readonly timer: NodeJS.Timeout;
}

interface EventWaiter {
  readonly resolve: (message: DapMessage) => void;
  readonly reject: (error: Error) => void;
  readonly timer: NodeJS.Timeout;
}

export class DapClient {
  private static readonly timeoutMilliseconds = 5_000;
  private readonly child: ChildProcessWithoutNullStreams;
  private readonly pending = new Map<number, PendingRequest>();
  private readonly queuedEvents = new Map<string, DapMessage[]>();
  private readonly eventWaiters = new Map<string, EventWaiter[]>();
  private readonly exitPromise: Promise<{
    code: number | null;
    signal: NodeJS.Signals | null;
  }>;
  private sequence = 1;
  private stdoutBuffer = Buffer.alloc(0);
  private expectedBodyLength: number | undefined;
  private stderr = "";
  private fatalError: Error | undefined;
  private exited = false;

  private constructor(child: ChildProcessWithoutNullStreams) {
    this.child = child;
    this.child.stdout.on("data", (chunk: Buffer) => {
      this.receive(chunk);
    });
    this.child.stderr.on("data", (chunk: Buffer) => {
      this.stderr += chunk.toString("utf8");
    });
    this.exitPromise = new Promise((resolve) => {
      this.child.once("exit", (code, signal) => {
        this.exited = true;
        if (this.pending.size > 0 && !this.fatalError) {
          this.fail(
            new Error(
              `Adapter exited with pending requests: ${[
                ...this.pending.values(),
              ]
                .map((item) => item.command)
                .join(", ")}`,
            ),
          );
        }
        resolve({ code, signal });
      });
    });
  }

  static async start(
    executable: string,
    args: readonly string[],
  ): Promise<DapClient> {
    const child = spawn(executable, [...args], {
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
    });
    const client = new DapClient(child);
    await new Promise<void>((resolve, reject) => {
      child.once("spawn", resolve);
      child.once("error", reject);
    });
    return client;
  }

  request(
    command: string,
    argumentsValue: object,
  ): Promise<DapMessage> {
    if (this.fatalError) {
      return Promise.reject(this.withStderr(this.fatalError));
    }
    const seq = this.sequence++;
    const promise = new Promise<DapMessage>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.fail(
          new Error(`Timed out waiting for response to ${command}.`),
        );
      }, DapClient.timeoutMilliseconds);
      this.pending.set(seq, {
        command,
        resolve,
        reject,
        timer,
      });
    });
    this.send({
      seq,
      type: "request",
      command,
      arguments: argumentsValue,
    });
    return promise;
  }

  waitForEvent(eventName: string): Promise<DapMessage> {
    const queued = this.queuedEvents.get(eventName);
    if (queued && queued.length > 0) {
      return Promise.resolve(queued.shift()!);
    }
    if (this.fatalError) {
      return Promise.reject(this.withStderr(this.fatalError));
    }
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.fail(
          new Error(`Timed out waiting for event ${eventName}.`),
        );
      }, DapClient.timeoutMilliseconds);
      const waiters = this.eventWaiters.get(eventName) ?? [];
      waiters.push({ resolve, reject, timer });
      this.eventWaiters.set(eventName, waiters);
    });
  }

  async expectCleanExit(expectedCode: number): Promise<void> {
    let result: {
      code: number | null;
      signal: NodeJS.Signals | null;
    };
    try {
      result = await withTimeout(
        this.exitPromise,
        DapClient.timeoutMilliseconds,
        "Timed out waiting for Adapter exit.",
      );
    } catch (error) {
      this.fail(error as Error);
      throw this.withStderr(error as Error);
    }
    if (this.fatalError) {
      throw this.withStderr(this.fatalError);
    }
    if (result.signal !== null || result.code !== expectedCode) {
      throw this.withStderr(
        new Error(
          `Adapter exit was code=${String(result.code)} ` +
            `signal=${String(result.signal)}; expected ${expectedCode}.`,
        ),
      );
    }
    if (this.stdoutBuffer.length !== 0) {
      throw this.withStderr(
        new Error("Adapter exited with an incomplete DAP frame."),
      );
    }
  }

  async dispose(): Promise<void> {
    if (this.exited) {
      return;
    }
    this.child.stdin.end();
    const exited = await Promise.race([
      this.exitPromise.then(() => true),
      new Promise<boolean>((resolve) =>
        setTimeout(() => resolve(false), 500),
      ),
    ]);
    if (!exited && !this.exited) {
      this.child.kill();
      await Promise.race([
        this.exitPromise,
        new Promise((resolve) => setTimeout(resolve, 500)),
      ]);
    }
  }

  private send(message: object): void {
    const body = Buffer.from(JSON.stringify(message), "utf8");
    const header = Buffer.from(
      `Content-Length: ${body.length}\r\n\r\n`,
      "ascii",
    );
    this.child.stdin.write(Buffer.concat([header, body]));
  }

  private receive(chunk: Buffer): void {
    if (this.fatalError) {
      return;
    }
    this.stdoutBuffer = Buffer.concat([this.stdoutBuffer, chunk]);
    try {
      while (true) {
        if (this.expectedBodyLength === undefined) {
          const delimiter = this.stdoutBuffer.indexOf("\r\n\r\n");
          if (delimiter < 0) {
            if (this.stdoutBuffer.length > 8_192) {
              throw new Error("DAP header exceeded 8192 bytes.");
            }
            return;
          }
          const headerBytes = this.stdoutBuffer.subarray(0, delimiter);
          const header = headerBytes.toString("ascii");
          if (!Buffer.from(header, "ascii").equals(headerBytes)) {
            throw new Error("DAP header contained non-ASCII bytes.");
          }
          const headerLines = header.split("\r\n");
          const lengths = headerLines
            .map((line) => line.match(/^Content-Length: (\d+)$/i))
            .filter((match): match is RegExpMatchArray => match !== null);
          if (
            headerLines.length !== 1 ||
            lengths.length !== 1
          ) {
            throw new Error(
              "DAP frame requires exactly one Content-Length header.",
            );
          }
          const bodyLength = Number.parseInt(lengths[0][1], 10);
          if (
            !Number.isSafeInteger(bodyLength) ||
            bodyLength <= 0 ||
            bodyLength > 10 * 1024 * 1024
          ) {
            throw new Error("DAP Content-Length is invalid.");
          }
          this.expectedBodyLength = bodyLength;
          this.stdoutBuffer = this.stdoutBuffer.subarray(delimiter + 4);
        }

        if (this.stdoutBuffer.length < this.expectedBodyLength) {
          return;
        }
        const body = this.stdoutBuffer.subarray(
          0,
          this.expectedBodyLength,
        );
        this.stdoutBuffer = this.stdoutBuffer.subarray(
          this.expectedBodyLength,
        );
        this.expectedBodyLength = undefined;
        const message = JSON.parse(body.toString("utf8")) as DapMessage;
        this.dispatch(message);
      }
    } catch (error) {
      this.fail(error as Error);
    }
  }

  private dispatch(message: DapMessage): void {
    if (message.type === "response") {
      const requestSeq = message.request_seq;
      if (!Number.isInteger(requestSeq)) {
        throw new Error("DAP response has no integer request_seq.");
      }
      const pending = this.pending.get(requestSeq);
      if (!pending) {
        throw new Error(
          `Received response for unknown request_seq ${requestSeq}.`,
        );
      }
      this.pending.delete(requestSeq);
      clearTimeout(pending.timer);
      if (message.success === false) {
        pending.reject(
          this.withStderr(
            new Error(
              `DAP ${pending.command} failed: ${
                String(message.message ?? "unknown error")
              }`,
            ),
          ),
        );
      } else {
        pending.resolve(message);
      }
      return;
    }
    if (message.type === "event" && typeof message.event === "string") {
      const waiters = this.eventWaiters.get(message.event);
      const waiter = waiters?.shift();
      if (waiter) {
        clearTimeout(waiter.timer);
        waiter.resolve(message);
      } else {
        const queued = this.queuedEvents.get(message.event) ?? [];
        queued.push(message);
        this.queuedEvents.set(message.event, queued);
      }
      return;
    }
    throw new Error("Adapter stdout contained an unexpected DAP message.");
  }

  private fail(error: Error): void {
    if (this.fatalError) {
      return;
    }
    this.fatalError = error;
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(this.withStderr(error));
    }
    this.pending.clear();
    for (const waiters of this.eventWaiters.values()) {
      for (const waiter of waiters) {
        clearTimeout(waiter.timer);
        waiter.reject(this.withStderr(error));
      }
    }
    this.eventWaiters.clear();
    if (!this.exited) {
      this.child.kill();
    }
  }

  private withStderr(error: Error): Error {
    return new Error(
      this.stderr.length > 0
        ? `${error.message}\nAdapter stderr:\n${this.stderr}`
        : error.message,
    );
  }
}

async function withTimeout<T>(
  promise: Promise<T>,
  milliseconds: number,
  message: string,
): Promise<T> {
  let timer: NodeJS.Timeout | undefined;
  try {
    return await Promise.race([
      promise,
      new Promise<T>((_, reject) => {
        timer = setTimeout(() => reject(new Error(message)), milliseconds);
      }),
    ]);
  } finally {
    if (timer) {
      clearTimeout(timer);
    }
  }
}
