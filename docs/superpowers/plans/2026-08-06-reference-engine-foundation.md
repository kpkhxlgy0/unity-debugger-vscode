# Reference Engine Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task in the current checkout. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace UnityDebuggerPure's duplicated DAP/Mono execution state with a reference-compatible event dispatcher, step manager, and suspended-handle lifecycle built directly on `Mono.Debugger.Soft`.

**Architecture:** Add a project-owned Unity engine beside the old `Mono.Debugging.Soft` backend. The new foundation translates Mono events once, owns one active step request, and exposes typed backend events to a thin DAP adapter. Keep the old backend as the production factory until the evaluation and breakpoint plans complete, so no incomplete build is installed.

**Tech Stack:** C# 8 on .NET Framework 4.8 x64; `Mono.Debugger.Soft`; vendored MIT `VSCodeDebug`; xUnit 2.9.3; Newtonsoft.Json 13.0.4.

## Global Constraints

- The installed reference debugger is the only behavioral authority.
- Do not load, copy, link, or redistribute `SyntaxTree.*` or Microsoft AD7 binaries.
- Do not add rapid-step queues, duplicate-step coalescing, optimistic running state, cancellation waits, retries, or arbitrary timeouts.
- Mono breaking events are the ordinary source of DAP `stopped`; reference-compatible Pause and successful Goto are the only control-path exceptions.
- Suspended handles reset on the next real breaking event or Pause stop, not when resume is requested; successful Goto preserves them.
- Preserve existing implicit-evaluation setting behavior; evaluation itself is implemented in the next plan.
- Do not install a foundation-only build into Cursor.
- Work in the current checkout; do not create or switch branches or worktrees.
- Use `apply_patch` for source edits and path-scoped Git commits.

---

## File Map

New engine files:

- `adapter/src/UnityDebugger.Adapter/Engine/Events/EngineEventModels.cs` — project-owned event kinds and event-set model.
- `adapter/src/UnityDebugger.Adapter/Engine/Events/IEngineEventSource.cs` — event-source and receiver seams.
- `adapter/src/UnityDebugger.Adapter/Engine/Events/EngineEventDispatcher.cs` — single event loop and breaking-event ordering.
- `adapter/src/UnityDebugger.Adapter/Engine/Control/StepModels.cs` — line-step depths and filters.
- `adapter/src/UnityDebugger.Adapter/Engine/Control/IStepRuntime.cs` — step-request/runtime seam.
- `adapter/src/UnityDebugger.Adapter/Engine/Control/StepManager.cs` — one-request reference step lifecycle.
- `adapter/src/UnityDebugger.Adapter/Engine/State/SuspendedState.cs` — frame/property/code-path/code-context maps.
- `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoEventSource.cs` — Mono event-set translation.
- `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoStepRuntime.cs` — Mono step request creation and filters.
- `adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs` — connection/control/event coordinator implementing `IDebuggerBackend` incrementally.

Modified files:

- `adapter/vendor/vscode-mono-debug/DebugSession.cs` — reference DAP request/capability surface.
- `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs` — backend event/control models.
- `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs` — thin DAP-to-engine contract.
- `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs` — remove execution truth and translate typed engine events.
- `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj` — compile direct engine sources and retain current backend during migration.

Tests:

- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/EngineEventDispatcherTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/StepManagerTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/SuspendedStateTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceCapabilityTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceControlTranscriptTests.cs`

### Task 1: Extend the vendored DAP surface to the reference capability set

**Files:**
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceCapabilityTests.cs`
- Modify: `adapter/vendor/vscode-mono-debug/DebugSession.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`

**Interfaces:**
- Produces capability fields `supportsStepInTargetsRequest`, `supportsGotoTargetsRequest`, `supportsTerminateRequest`, `supportsExceptionOptions`, and their explicitly false peers.
- Produces request hooks `StepInTargets`, `GotoTargets`, `Goto`, and `ExceptionInfo` for later plans.

- [ ] **Step 1: Write the failing capability test**

Add a test that initializes `UnityDebugSession` with `FakeDebuggerBackend` and asserts these hand-written literals:

```csharp
[Fact]
public void InitializeMatchesReferenceDebuggerCapabilities()
{
    var messages = DapTestProtocol.Run(
        new UnityDebugSession(() => new FakeDebuggerBackend()),
        DapTestProtocol.Request(
            "initialize",
            new
            {
                adapterID = "unity-debugger-pure",
                linesStartAt1 = true,
                columnsStartAt1 = true,
                pathFormat = "path",
            }));

    var body = DapTestProtocol.Response(messages, "initialize")["body"]!;
    Assert.True(body["supportsConditionalBreakpoints"]!.Value<bool>());
    Assert.True(body["supportsEvaluateForHovers"]!.Value<bool>());
    Assert.True(body["supportsFunctionBreakpoints"]!.Value<bool>());
    Assert.True(body["supportsLogPoints"]!.Value<bool>());
    Assert.True(body["supportsSetVariable"]!.Value<bool>());
    Assert.True(body["supportsStepInTargetsRequest"]!.Value<bool>());
    Assert.True(body["supportsGotoTargetsRequest"]!.Value<bool>());
    Assert.True(body["supportsTerminateRequest"]!.Value<bool>());
    Assert.False(body["supportsConfigurationDoneRequest"]!.Value<bool>());
    Assert.False(body["supportsDataBreakpoints"]!.Value<bool>());
    Assert.False(body["supportsDisassembleRequest"]!.Value<bool>());
    Assert.False(body["supportsInstructionBreakpoints"]!.Value<bool>());
    Assert.False(body["supportsHitConditionalBreakpoints"]!.Value<bool>());
    Assert.False(body["supportsSingleThreadExecutionRequests"]!.Value<bool>());
    Assert.False(body["supportsSteppingGranularity"]!.Value<bool>());
    Assert.False(body["supportsStepBack"]!.Value<bool>());
    Assert.False(body["supportsReadMemoryRequest"]!.Value<bool>());
    Assert.False(body["supportsRestartRequest"]!.Value<bool>());
    Assert.False(body["supportsWriteMemoryRequest"]!.Value<bool>());
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReferenceCapabilityTests
```

Expected: FAIL because the current capability type omits the reference fields and currently reports function breakpoints and Set Variable as unsupported.

- [ ] **Step 3: Add exact DAP models and dispatch hooks**

Extend `Capabilities` with the named booleans. Add response bodies with these exact shapes:

```csharp
public sealed class StepInTarget
{
    public int id;
    public string label;
}

public sealed class StepInTargetsResponseBody : ResponseBody
{
    public StepInTarget[] targets;
}

public sealed class GotoTarget
{
    public int id;
    public string label;
    public int line;
    public int column;
    public int endLine;
    public int endColumn;
}

public sealed class GotoTargetsResponseBody : ResponseBody
{
    public GotoTarget[] targets;
}
```

Dispatch `stepInTargets`, `gotoTargets`, `goto`, and `exceptionInfo` to new abstract methods. In `UnityDebugSession`, return empty successful bodies until the corresponding engine plan supplies data; do not advertise an unsupported capability before its request hook exists.

- [ ] **Step 4: Run the capability and existing lifecycle tests**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ReferenceCapabilityTests|FullyQualifiedName~UnityDebugSessionLifecycleTests"
```

Expected: PASS with one response per request and no protocol errors.

- [ ] **Step 5: Commit the DAP surface**

```powershell
git add -- adapter/vendor/vscode-mono-debug/DebugSession.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceCapabilityTests.cs
git commit -m "feat: match reference debugger DAP surface"
```

### Task 2: Implement reference suspended-handle lifetime

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/State/SuspendedState.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/SuspendedStateTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`

**Interfaces:**
- Produces `int RegisterFrame(object frame)`, `bool TryGetFrame<T>(int id, out T value)`.
- Produces equivalent property, code-path, and code-context methods.
- Produces `void Reset()` that clears all maps and resets each map's ID producer.

- [ ] **Step 1: Write failing lifetime tests**

```csharp
[Fact]
public void RegisteredHandlesRemainAvailableUntilReset()
{
    var state = new SuspendedState();
    var frame = new object();
    var id = state.RegisterFrame(frame);

    Assert.True(state.TryGetFrame<object>(id, out var mapped));
    Assert.Same(frame, mapped);
}

[Fact]
public void NextRealStopResetsEveryHandleKindAndRestartsIds()
{
    var state = new SuspendedState();
    var oldFrame = state.RegisterFrame(new object());
    var oldProperty = state.RegisterProperty(new object());
    state.RegisterCodePath(new object());
    state.RegisterCodeContext(new object());

    state.Reset();

    Assert.False(state.TryGetFrame<object>(oldFrame, out _));
    Assert.False(state.TryGetProperty<object>(oldProperty, out _));
    Assert.Equal(1, state.RegisterFrame(new object()));
    Assert.Equal(1, state.RegisterProperty(new object()));
}
```

Task 7 adds the engine-level Continue regression that proves Continue does not call `Reset`; this task proves the state itself has no time- or generation-based expiry.

- [ ] **Step 2: Run and verify RED**

Run:

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SuspendedStateTests
```

Expected: FAIL because `Engine.State.SuspendedState` does not exist.

- [ ] **Step 3: Implement four independent object maps**

Implement a private `ObjectMap` with an integer counter starting at zero, `Interlocked.Increment` registration, a `ConcurrentDictionary<int, object>`, typed `TryGet`, and `Reset` that clears the dictionary and exchanges the counter to zero. `SuspendedState.Reset()` calls all four maps.

Do not add generation numbers, resume hooks, expiry timers, or visible stale-handle exceptions.

- [ ] **Step 4: Run the state tests**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Commit suspended state**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/State/SuspendedState.cs adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj tests/adapter/UnityDebugger.Adapter.Tests/Engine/SuspendedStateTests.cs
git commit -m "feat: add reference suspended state lifetime"
```

### Task 3: Implement one-request StepManager

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Control/StepModels.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Control/IStepRuntime.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Control/StepManager.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/StepManagerTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`

**Interfaces:**
- Consumes `IStepRuntime.CreateStepRequest(long threadId, StepRequestOptions options)` and `IStepRuntime.Resume()`.
- Produces `void RequestStep(long threadId, EngineStepDepth depth)`, `void CancelStep()`, and `void ProcessStepEvent(long threadId)`.
- `StepRequestOptions` fixes `Size=Line` and filters `DebuggerHidden | DebuggerStepThrough | DebuggerNonUserCode | StaticCtor` unless the current frames are already in a static constructor.

- [ ] **Step 1: Write a failing replacement-order test**

Create a recording runtime and assert this literal operation sequence:

```csharp
[Fact]
public void NewStepDisablesOldRequestBeforeEnablingAndResumingNewRequest()
{
    var runtime = new RecordingStepRuntime();
    var manager = new StepManager(runtime);

    manager.RequestStep(7, EngineStepDepth.Into);
    manager.RequestStep(7, EngineStepDepth.Over);

    Assert.Equal(
        new[]
        {
            "create:7:Into",
            "enable:Into",
            "resume",
            "disable:Into",
            "create:7:Over",
            "enable:Over",
            "resume",
        },
        runtime.Operations);
}
```

Add separate tests proving a StepEvent disables the active request exactly once and Continue cancellation is idempotent.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~StepManagerTests
```

Expected: FAIL because `StepManager` and its seams do not exist.

- [ ] **Step 3: Implement StepManager under one lock**

Use one private lock and one nullable `IStepRequest`. `RequestStep` calls `CancelStepCore`, creates the new request with line size and reference filters, assigns it before enabling, enables, then resumes. If enabling throws, call `IStepRuntime.Resume()` exactly as the reference engine does and leave the assigned request for the next normal `CancelStep` call.

`ProcessStepEvent` cancels the request, then raises the supplied `Action<long>` step-complete callback. Do not inspect a DAP running flag.

- [ ] **Step 4: Run StepManager tests**

Run Step 2's command. Expected: PASS.

- [ ] **Step 5: Commit StepManager**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Control adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj tests/adapter/UnityDebugger.Adapter.Tests/Engine/StepManagerTests.cs
git commit -m "feat: add reference step request lifecycle"
```

### Task 4: Implement the single Mono event dispatcher

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Events/EngineEventModels.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Events/IEngineEventSource.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Events/EngineEventDispatcher.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/EngineEventDispatcherTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`

**Interfaces:**
- `IEngineEventSource.GetNextEventSet(CancellationToken token)` blocks until an event set arrives.
- `IEngineEventSource.Resume()` releases one Mono suspend count.
- `IEngineEventReceiver.Process(EngineEvent value)` handles one selected event.
- Produces `Start()`, `Task Completion`, `BeforeResuming()`, `AfterResuming()`, and `Stop()`.

- [ ] **Step 1: Write failing event-order tests**

Cover these independent behaviors with literal event sequences:

```csharp
[Fact]
public void NonBreakingSuspendedEventSetIsResumed()
{
    var source = ScriptedEventSource.From(
        new EngineEventSet(
            EngineSuspendPolicy.All,
            EngineEvent.ThreadStarted(2)),
        new EngineEventSet(
            EngineSuspendPolicy.None,
            EngineEvent.VmDeath()));
    var receiver = new RecordingEventReceiver();
    var dispatcher = new EngineEventDispatcher(source, receiver);

    dispatcher.Start();
    dispatcher.Completion.GetAwaiter().GetResult();

    Assert.Equal(new[] { "event:ThreadStarted:2", "resume" }, source.AndReceiverLog(receiver));
}

[Fact]
public void BreakingEventsFromOtherThreadsAreQueuedUntilResume()
{
    var resumeGate = new ManualResetEventSlim();
    var source = ScriptedEventSource.From(
        new EngineEventSet(
            EngineSuspendPolicy.All,
            EngineEvent.Breakpoint(3),
            EngineEvent.Step(4)),
        new EngineEventSet(
            EngineSuspendPolicy.None,
            EngineEvent.VmDeath()));
    var receiver = new RecordingEventReceiver();
    var dispatcher = new EngineEventDispatcher(source, receiver);

    receiver.OnEvent = value =>
    {
        if (value.Kind == EngineEventKind.Breakpoint)
            resumeGate.Set();
    };
    dispatcher.Start();
    Assert.True(resumeGate.Wait(TimeSpan.FromSeconds(1)));
    Assert.Equal(new[] { "Breakpoint:3" }, receiver.Events);

    dispatcher.BeforeResuming();
    dispatcher.AfterResuming();
    dispatcher.Completion.GetAwaiter().GetResult();
    Assert.Equal(new[] { "Breakpoint:3", "Step:4" }, receiver.Events);
}
```

Add the same-thread Exception case: it consumes one resume instead of queueing a second visible stop.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~EngineEventDispatcherTests
```

Expected: FAIL because the dispatcher does not exist.

- [ ] **Step 3: Implement reference suspend-count behavior**

Use a `ConcurrentQueue<EngineEvent>` for queued breaking events, one lock for the dispatched thread ID, and the reference resume-count initialization of one. Breaking kinds are exactly UserBreak, Breakpoint, Step, and Exception. Dispatch all other kinds immediately, and resume a suspended event set that contains no breaking event.

The long-running dispatch task catches VM disconnect separately from unexpected exceptions, emits termination once through the receiver, and never writes DAP directly.

- [ ] **Step 4: Run dispatcher and StepManager tests together**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EngineEventDispatcherTests|FullyQualifiedName~StepManagerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the dispatcher**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Events adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj tests/adapter/UnityDebugger.Adapter.Tests/Engine/EngineEventDispatcherTests.cs
git commit -m "feat: add ordered Mono event dispatcher"
```

### Task 5: Add Mono event and step adapters

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoEventSource.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoStepRuntime.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/MonoAdapterContractTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`

**Interfaces:**
- Consumes `Mono.Debugger.Soft.VirtualMachine`, `EventSet`, and `StepEventRequest`.
- Produces the engine event and step seams from Tasks 3 and 4.

- [ ] **Step 1: Write failing pure mapping tests**

Extract static mapping methods that can be tested without a live VM:

```csharp
[Theory]
[InlineData(EventType.UserBreak, EngineEventKind.UserBreak)]
[InlineData(EventType.Breakpoint, EngineEventKind.Breakpoint)]
[InlineData(EventType.Step, EngineEventKind.Step)]
[InlineData(EventType.Exception, EngineEventKind.Exception)]
[InlineData(EventType.TypeLoad, EngineEventKind.TypeLoaded)]
[InlineData(EventType.AppDomainUnload, EngineEventKind.DomainUnloaded)]
public void MonoEventKindsMapWithoutExecutionStateInference(
    EventType source,
    EngineEventKind expected)
{
    Assert.Equal(expected, MonoEventSource.MapEventKind(source));
}
```

Add step-depth and step-filter mapping tests with hand-written expected Mono enum values.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~MonoAdapterContractTests
```

Expected: FAIL because the Mono adapters do not exist.

- [ ] **Step 3: Implement thin adapters only**

`MonoEventSource` translates every supported Mono event kind and retains the native event as an internal payload for the receiver. It does not filter stops or alter suspend state.

`MonoStepRuntime` creates a `StepEventRequest` for the mapped thread, sets line size, depth, assembly filter, and debugger step filters, and wraps Enable/Disable. `Resume()` calls the dispatcher's `BeforeResuming`, `VirtualMachine.Resume`, then `AfterResuming`; ignore only the exact `InvalidOperationException` that reference Resume ignores.

- [ ] **Step 4: Run mapping and all engine foundation tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Engine
```

Expected: PASS.

- [ ] **Step 5: Commit Mono adapters**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Mono adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj tests/adapter/UnityDebugger.Adapter.Tests/Engine/MonoAdapterContractTests.cs
git commit -m "feat: adapt Mono soft debugger to Unity engine"
```

### Task 6: Make UnityDebugSession a thin ordered translator

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceControlTranscriptTests.cs`
- Remove after replacement: `adapter/src/UnityDebugger.Adapter/State/ExecutionState.cs`
- Remove after replacement: `tests/adapter/UnityDebugger.Adapter.Tests/State/ExecutionStateTests.cs`

**Interfaces:**
- Backend events are `Stopped`, `ThreadChanged`, `ModuleChanged`, `BreakpointChanged`, `Output`, and `Terminated`.
- Control methods return after engine acceptance and do not expose `Continued` as execution truth.
- `BackendStoppedEventArgs` carries reason, thread ID, description, and zero or more breakpoint IDs.

- [ ] **Step 1: Write failing transcript tests**

Add tests for these literal message rules:

```csharp
[Fact]
public void FirstStopDuringAttachIsNotDropped()
{
    var backend = new FakeDebuggerBackend { StopSynchronouslyDuringAttach = true };
    var messages = DapTestProtocol.Run(
        new UnityDebugSession(() => backend),
        InitializeRequest(),
        AttachRequest());

    Assert.Single(DapTestProtocol.Events(messages, "stopped"));
    Assert.True(
        DapTestProtocol.Events(messages, "stopped")[0]["body"]!["allThreadsStopped"]!.Value<bool>());
}

[Fact]
public void StepResponseCannotHideNewStoppedEvent()
{
    var backend = new FakeDebuggerBackend { StopSynchronouslyOnStep = true };
    var messages = RunAttachedSession(backend, DapTestProtocol.Request("next", new { threadId = 1 }));

    Assert.Single(DapTestProtocol.Responses(messages, "next"));
    Assert.Single(DapTestProtocol.Events(messages, "stopped"));
    Assert.Empty(DapTestProtocol.Events(messages, "continued"));
}
```

Add `Events` to `DapTestProtocol` as a real parsed-message query, not a mock assertion. Add rapid StepIn/Next requests and assert both responses succeed with no Output warning. Add a writer-integrity stress transcript in which synchronous backend events race request responses; parse every framed message and assert every request sequence receives exactly one intact response.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReferenceControlTranscriptTests
```

Expected: FAIL because current `ExecutionState`, `activeResumeOperation`, `awaitingContinuedEvent`, and continued-event logic alter the transcript.

- [ ] **Step 3: Remove DAP execution truth**

Delete the fields and branches for `ExecutionState`, `controlResponsePending`, `awaitingContinuedEvent`, `activeResumeOperation`, pending control events, rapid-step coalescing, and optimistic resume transitions.

Subscribe to backend events before calling `Attach`. Translate every backend Stop directly to one DAP stopped event. Let the protocol writer serialize responses and events. Control handlers validate the DAP thread mapping, call the backend once, and send a success response without waiting for a backend Continued callback.

Remove `ExecutionState.cs` and its tests only after `rg -n "ExecutionState" adapter tests` returns no production consumer.

- [ ] **Step 4: Run all DAP tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Dap
```

Expected: PASS. Update old expectations that asserted custom continued events only when the new expected transcript is explicitly covered by `ReferenceControlTranscriptTests`.

- [ ] **Step 5: Commit the thin DAP adapter**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs adapter/src/UnityDebugger.Adapter/State/ExecutionState.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs tests/adapter/UnityDebugger.Adapter.Tests/State/ExecutionStateTests.cs
git commit -m "refactor: make DAP session follow engine events"
```

### Task 7: Build the new engine coordinator without switching production

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/UnityDebuggerEngineControlTests.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`

**Interfaces:**
- Implements the control, attach, thread, and stack subset of `IDebuggerBackend`.
- Consumes `Func<AttachTarget, IMonoEngineConnection>` in tests and a production Mono connection factory in the final plan.
- Exposes no retry or reconnect policy beyond reference VM/domain event handling.

- [ ] **Step 1: Write failing coordinator tests**

Test that Attach constructs managers before starting event dispatch, a Step event resets `SuspendedState` before raising `Stopped`, Continue cancels the active Step request before Resume without resetting suspended handles, Pause suspends the VM and raises one Pause stop per mapped thread in enumeration order, and termination is raised once.

Use a single recording connection and assert literal operation order:

```csharp
Assert.Equal(
    new[] { "connect", "create-state", "create-managers", "start-dispatch" },
    connection.Operations);
```

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UnityDebuggerEngineControlTests
```

Expected: FAIL because `UnityDebuggerEngine` does not exist.

- [ ] **Step 3: Implement only the foundation subset**

Implement Attach, Disconnect, Continue, Pause, StepIn, StepOver, StepOut, Threads, and StackTrace. Scopes, Variables, Evaluate, breakpoints, and exceptions throw `NotSupportedException` in this migration-only coordinator and are not wired into `Program` yet.

On every real breaking event, call `SuspendedState.Reset()` before raising `Stopped`. Step processing first cancels the current step request. Pause calls VM Suspend, enumerates mapped threads in reference order, and resets `SuspendedState` before each Pause stop notification. Breakpoint payload IDs remain for the breakpoint plan.

- [ ] **Step 4: Run foundation tests and build**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Engine|FullyQualifiedName~ReferenceControlTranscriptTests"
dotnet build UnityDebugger.sln -c Release --no-restore
```

Expected: PASS and build success. `Program` still creates the old backend, so no incomplete release can be packaged accidentally.

- [ ] **Step 5: Commit the coordinator**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj tests/adapter/UnityDebugger.Adapter.Tests/Engine/UnityDebuggerEngineControlTests.cs
git commit -m "feat: coordinate reference Unity engine controls"
```

### Task 8: Foundation checkpoint

**Files:**
- Review only; no production mutation unless a failing test identifies a defect.

- [ ] **Step 1: Run focused foundation verification**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Engine|FullyQualifiedName~ReferenceCapabilityTests|FullyQualifiedName~ReferenceControlTranscriptTests"
```

Expected: all focused tests pass with no warnings.

- [ ] **Step 2: Run the existing adapter suite**

```powershell
dotnet test UnityDebugger.sln -c Release --no-restore
```

Expected: all adapter and test-adapter projects pass. No claim is made about real Unity yet.

- [ ] **Step 3: Check source and package dependency boundaries**

```powershell
rg -n "activeResumeOperation|awaitingContinuedEvent|controlResponsePending|rapidStep|ExecutionState" adapter/src tests/adapter
git diff --check
git status --short
```

Expected: no old DAP execution-state symbols, no whitespace errors, and only task-related changes.

- [ ] **Step 4: Record the checkpoint**

```powershell
git add -- docs/superpowers/plans/2026-08-06-reference-engine-foundation.md
git commit -m "docs: complete reference engine foundation plan"
```

Do not build, install, or ask the user to test a VSIX at this checkpoint.
