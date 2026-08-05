# Reference Evaluation and Variable Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task in the current checkout. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement reference-compatible Roslyn expression evaluation, lazy debugger properties, Getter and `ToString()` invocation, enum semantics, collection expansion, and Unity-specific debug values on the new direct Mono engine.

**Architecture:** Parse expressions into Roslyn syntax, evaluate them against a project-owned stack-frame environment, and represent target values through small runtime interfaces backed by `Mono.Debugger.Soft`. DAP Scopes registers one lazy frame property; Variables and Evaluate resolve only the requested layer with request-scoped cancellation and reference timeouts.

**Tech Stack:** C# 8; .NET Framework 4.8 x64; `Microsoft.CodeAnalysis.CSharp` 4.14.0; `Mono.Debugger.Soft`; xUnit 2.9.3.

## Global Constraints

- Execute this plan only after the foundation plan passes.
- Use the installed reference debugger as the behavior authority.
- Default Variables/Evaluate timeout is exactly 10,000 ms; `ToString()` waits exactly 2,000 ms.
- Target calls use `InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded`.
- Getter exceptions are property error values; they do not fail the containing Variables response.
- `ToString()` failure or timeout falls back to `{TypeName}` for that request and never updates in the background.
- Variables with an unavailable handle return an empty successful list.
- Do not add caches, retries, speculative prefetch, background refresh, or global evaluation cancellation.
- `unityDebuggerPure.enableImplicitEvaluation` defaults on and remains user/workspace/workspace-folder scoped.
- Do not install an evaluation-only build into Cursor.

---

## File Map

Runtime seams:

- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/IRuntimeType.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/IRuntimeValue.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/RuntimeMembers.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeType.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeValue.cs`

Expression engine:

- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/CSharpDebugParser.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ParsingResult.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/IFrameEvaluationEnvironment.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ExpressionEvaluator.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/RuntimeInvoker.cs`

Property/value model:

- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/DebugProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/FrameProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/ValueProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/FieldProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/AccessorProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/ComputedProperty.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/DebugValue.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/ValueFormatter.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/CollectionValueProviders.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/UnityValueProviders.cs`
- `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/EvaluationService.cs`

Tests:

- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CSharpDebugParserTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ExpressionEvaluatorTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ValueFormatterTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/PropertyEvaluationTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CollectionValueTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/UnityValueProviderTests.cs`
- `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs`

### Task 1: Add the exact Roslyn parser dependency and expression parser

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj`
- Modify: `adapter/src/UnityDebugger.Adapter/packages.lock.json`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/packages.lock.json`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/CSharpDebugParser.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ParsingResult.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CSharpDebugParserTests.cs`

**Interfaces:**
- Produces `ParsingResult CSharpDebugParser.ParseExpression(string text)`.
- `ParsingResult` contains `ExpressionSyntax? Expression` and `string? Error`; `CanEvaluate` is true only when Expression is present and Error is empty.

- [ ] **Step 1: Write failing parser tests**

```csharp
[Theory]
[InlineData("currentState == ButtonState.Normal", SyntaxKind.EqualsExpression)]
[InlineData("items[index]", SyntaxKind.ElementAccessExpression)]
[InlineData("(ButtonState)value", SyntaxKind.CastExpression)]
public void ParseExpressionReturnsTheRoslynExpressionRoot(
    string text,
    SyntaxKind expectedKind)
{
    var result = new CSharpDebugParser().ParseExpression(text);

    Assert.True(result.CanEvaluate, result.Error);
    Assert.Equal(expectedKind, result.Expression!.Kind());
}

[Fact]
public void InvalidExpressionReturnsTheFirstDiagnosticWithoutThrowing()
{
    var result = new CSharpDebugParser().ParseExpression("currentState ==");

    Assert.False(result.CanEvaluate);
    Assert.NotEmpty(result.Error!);
}
```

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CSharpDebugParserTests
```

Expected: FAIL because Roslyn 4.14.0 and the parser do not exist.

- [ ] **Step 3: Add Roslyn 4.14.0 and implement parsing**

Add:

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" />
```

Use `SyntaxFactory.ParseExpression(text, options: new CSharpParseOptions(LanguageVersion.Latest))`, collect diagnostics with severity Error, and return the first diagnostic message. Do not compile an assembly or execute host code.

Restore without floating versions:

```powershell
dotnet restore UnityDebugger.sln --force-evaluate
```

- [ ] **Step 4: Run parser tests and dependency verification**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CSharpDebugParserTests
dotnet list adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj package --include-transitive
```

Expected: parser PASS and the package list reports `Microsoft.CodeAnalysis.CSharp` at exactly 4.14.0. Runtime inventory and notice verification runs after the old engine assemblies are removed in the release plan.

- [ ] **Step 5: Commit parser and lock files**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/UnityDebugger.Adapter.csproj adapter/src/UnityDebugger.Adapter/packages.lock.json tests/adapter/UnityDebugger.Adapter.Tests/packages.lock.json adapter/src/UnityDebugger.Adapter/Engine/Evaluation/CSharpDebugParser.cs adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ParsingResult.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CSharpDebugParserTests.cs
git commit -m "feat: parse debugger expressions with Roslyn"
```

### Task 2: Add testable runtime value and type boundaries

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/IRuntimeType.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/IRuntimeValue.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime/RuntimeMembers.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeType.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeValue.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/FakeRuntimeValues.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/MonoRuntimeMappingTests.cs`

**Interfaces:**

```csharp
internal interface IRuntimeValue
{
    RuntimeValueKind Kind { get; }
    IRuntimeType Type { get; }
    object? Primitive { get; }
    string? String { get; }
    long? Address { get; }
    IRuntimeValue GetField(RuntimeField field);
    void SetField(RuntimeField field, IRuntimeValue value);
    IRuntimeValue GetElement(int index);
    int Length { get; }
    Task<IRuntimeValue> InvokeAsync(
        RuntimeMethod method,
        IReadOnlyList<IRuntimeValue> arguments,
        InvokeOptions options,
        CancellationToken cancellationToken);
}
```

`IRuntimeType` exposes name/full name, enum/primitive/value-type/array flags, base type, fields, properties, methods, assignability, and enum constants. `RuntimeField`, `RuntimeProperty`, and `RuntimeMethod` are immutable project-owned descriptors.

- [ ] **Step 1: Write failing mapping tests**

Test pure classification methods for null, PrimitiveValue, StringMirror, EnumMirror, ArrayMirror, StructMirror, ObjectMirror, and PointerValue kinds. Test that the invocation option is passed unchanged to a fake runtime value.

The fake value must mirror every `IRuntimeValue` member and throw a test failure when an unconfigured member is accessed.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~MonoRuntimeMappingTests
```

Expected: FAIL because the runtime boundary does not exist.

- [ ] **Step 3: Implement thin Mono wrappers**

`MonoRuntimeValue` stores one `Mono.Debugger.Soft.Value`; `MonoRuntimeType` stores one `TypeMirror`. Translate metadata without caching target values. `InvokeAsync` schedules the synchronous Mono invoke on `TaskScheduler.Default`, observes the request token, and always receives the caller-provided options.

Do not implement display formatting in the wrappers.

- [ ] **Step 4: Run mapping tests**

Run Step 2's command. Expected: PASS.

- [ ] **Step 5: Commit runtime boundaries**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Runtime adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeType.cs adapter/src/UnityDebugger.Adapter/Engine/Mono/MonoRuntimeValue.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/FakeRuntimeValues.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/MonoRuntimeMappingTests.cs
git commit -m "feat: wrap Mono debugger values for evaluation"
```

### Task 3: Implement primitive and enum expression semantics first

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/IFrameEvaluationEnvironment.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ExpressionEvaluator.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/RuntimeInvoker.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ExpressionEvaluatorTests.cs`

**Interfaces:**
- `IFrameEvaluationEnvironment.TryGetValue(string name, out IRuntimeValue value)` resolves locals/arguments/fields.
- `TryGetType(string fullOrSimpleName, out IRuntimeType type)` resolves enum and cast types in the stopped AppDomain.
- `ExpressionEvaluator.Evaluate(ExpressionSyntax expression, CancellationToken token)` returns `IRuntimeValue`.

- [ ] **Step 1: Write the failing reported enum regression**

```csharp
[Fact]
public void EnumMemberEqualityUsesTheEnumType()
{
    var stateType = FakeRuntimeType.Enum(
        "ButtonState",
        ("Normal", 0),
        ("Selected", 1));
    var environment = new FakeFrameEnvironment()
        .WithType(stateType)
        .WithValue("currentState", stateType.EnumValue("Normal"));
    var expression = Parse("currentState == ButtonState.Normal");

    var result = new ExpressionEvaluator(environment)
        .Evaluate(expression, CancellationToken.None);

    Assert.Equal(RuntimeValueKind.Primitive, result.Kind);
    Assert.Equal(true, result.Primitive);
}
```

Add literal, identifier, enum member, parenthesis, `==`, `!=`, `&&`, `||`, and unary `!` tests. Each test uses hand-derived expected primitive values.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ExpressionEvaluatorTests
```

Expected: FAIL because the evaluator does not exist.

- [ ] **Step 3: Implement the minimal enum-capable visitor**

Support only the syntax covered in Step 1. Enum member access resolves the left side as a type, reads its literal constant, and creates a value retaining that enum type. Equality first requires compatible enum types, then compares normalized underlying values.

Return a typed evaluation exception for unknown identifiers/types/members. Do not convert an enum into an untyped integer before comparison.

- [ ] **Step 4: Run enum evaluator tests**

Run Step 2's command. Expected: PASS.

- [ ] **Step 5: Commit enum evaluation**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/IFrameEvaluationEnvironment.cs adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ExpressionEvaluator.cs adapter/src/UnityDebugger.Adapter/Engine/Evaluation/RuntimeInvoker.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ExpressionEvaluatorTests.cs
git commit -m "feat: evaluate typed enum expressions"
```

### Task 4: Complete the reference expression syntax surface

**Files:**
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ExpressionEvaluator.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/RuntimeInvoker.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ExpressionEvaluatorTests.cs`

**Interfaces:**
- Adds member access, element access, invocation, casts, conditional expressions, arithmetic, comparison, bitwise, null, `this`, and `base` handling.

- [ ] **Step 1: Add one failing table per syntax family**

Use literal expectations for:

```csharp
[InlineData("count + 2", 5)]
[InlineData("count >= 3", true)]
[InlineData("flags & ButtonFlags.Pressed", 2)]
[InlineData("items[1]", 20)]
[InlineData("enabled ? count : 0", 3)]
```

Add separate real fake-runtime tests for `player.Health`, `player.GetHealth()`, `(int)state`, `this`, and `base`. Assert that method invocation receives exactly `DisableBreakpoints | SingleThreaded`.

- [ ] **Step 2: Run and verify RED**

Run the Task 3 test command. Expected: the new cases FAIL on unsupported syntax.

- [ ] **Step 3: Implement one syntax family at a time**

For each family, implement the smallest visitor branch, rerun only that test, then continue. Use checked numeric conversion rules and reference-compatible null/member errors. Invocation resolves overloads by argument count and assignability and delegates to `RuntimeInvoker` with the exact options.

- [ ] **Step 4: Run all expression tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CSharpDebugParserTests|FullyQualifiedName~ExpressionEvaluatorTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the expression surface**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/ExpressionEvaluator.cs adapter/src/UnityDebugger.Adapter/Engine/Evaluation/RuntimeInvoker.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ExpressionEvaluatorTests.cs
git commit -m "feat: complete debugger expression syntax"
```

### Task 5: Implement lazy frame and member properties

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/DebugProperty.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/FrameProperty.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/ValueProperty.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/FieldProperty.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/AccessorProperty.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/ComputedProperty.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/PropertyEvaluationTests.cs`

**Interfaces:**

```csharp
internal abstract class DebugProperty
{
    public abstract string Name { get; }
    public abstract string TypeName { get; }
    public abstract Task<IRuntimeValue> GetValueAsync(CancellationToken token);
    public abstract Task SetValueAsync(IRuntimeValue value, CancellationToken token);
    public virtual Task<IReadOnlyList<DebugProperty>> GetChildrenAsync(CancellationToken token);
}
```

`FrameProperty` builds locals only when `GetChildrenAsync` is called. It merges visible locals, arguments, constants, `this`, closure captures, async/iterator hoisted fields, and current exception in reference order.

- [ ] **Step 1: Write failing lazy-expansion tests**

Test that creating Scopes/FrameProperty does not ask the environment for locals; the first children request asks once. Test reference order with literals `this`, local, argument. Test that child field properties are registered only when the parent is expanded.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~PropertyEvaluationTests
```

Expected: FAIL because the property model does not exist.

- [ ] **Step 3: Implement lazy property classes**

Property instances retain descriptors and a stopped-frame environment, not eagerly fetched child arrays. `FieldProperty` reads on demand. `AccessorProperty` defers invocation to Task 6. `ComputedProperty` holds a child factory for static/non-public/base/raw/results groups.

- [ ] **Step 4: Run property tests**

Run Step 2's command. Expected: PASS for field-only cases.

- [ ] **Step 5: Commit lazy properties**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/PropertyEvaluationTests.cs
git commit -m "feat: add lazy debugger property model"
```

### Task 6: Match Getter, implicit evaluation, and ToString behavior

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/DebugValue.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/ValueFormatter.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/AccessorProperty.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/PropertyEvaluationTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ValueFormatterTests.cs`

**Interfaces:**
- `ValueFormatter.FormatAsync(IRuntimeValue value, EvaluationPolicy policy, CancellationToken token)` returns the DAP display string.
- `EvaluationPolicy` carries `AllowTargetInvoke`, `AllowGetters`, and `AllowToString` derived from Safe or Explicit mode.

- [ ] **Step 1: Write failing Getter tests**

Cover successful getter invocation, exact invocation flags, disabled implicit getter, and an invocation exception converted to an error value. The exception test asserts the containing property list still has both the error property and the following normal property.

- [ ] **Step 2: Write failing ToString timing tests with a fake clock boundary**

Inject `Func<Task<IRuntimeValue>, TimeSpan, CancellationToken, Task<IRuntimeValue?>> waitForValue` into `ValueFormatter`. Assert:

```csharp
Assert.Equal("Player", await formatter.FormatAsync(overriddenToStringValue, explicitPolicy, token));
Assert.Equal("{Player}", await formatter.FormatAsync(timedOutValue, explicitPolicy, token));
Assert.Equal(TimeSpan.FromMilliseconds(2000), recordedTimeout);
```

Also assert Object/ValueType default `ToString()` returns `{TypeName}` without target invocation.

- [ ] **Step 3: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~PropertyEvaluationTests|FullyQualifiedName~ValueFormatterTests"
```

Expected: new Getter and formatter cases FAIL.

- [ ] **Step 4: Implement exact invocation and fallback semantics**

Getter invocation uses `DisableBreakpoints | SingleThreaded`. Convert target `InvocationException` into `DebugValue.Error` containing the target exception type/message. `ValueFormatter` quotes strings, formats decimals raw, formats runtime Type values in braces, waits exactly two seconds for overridden `ToString()`, and falls back once without background continuation.

Safe automatic mode returns field/primitive data without getter or `ToString()` invocation. Explicit Watch/REPL uses full invocation even when the setting is disabled.

- [ ] **Step 5: Run Getter/formatter tests and commit**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~PropertyEvaluationTests|FullyQualifiedName~ValueFormatterTests"
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Properties/AccessorProperty.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/PropertyEvaluationTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/ValueFormatterTests.cs
git commit -m "feat: match Getter and ToString evaluation"
```

### Task 7: Add reference collection and Unity debug-value providers

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/CollectionValueProviders.cs`
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/UnityValueProviders.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CollectionValueTests.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/UnityValueProviderTests.cs`

**Interfaces:**
- Produces ordered child providers selected by runtime type.
- Array provider buckets large arrays using reference ranges.
- List/Dictionary/Enumerable providers expose element, entry, and Results View properties.
- Unity provider exposes reference-compatible Object, Component/GameObject, Scene, active scene, and GameObject children only when the stopped thread is Unity's main thread.

- [ ] **Step 1: Write failing value-provider tables**

Use complete fake values to assert literal names and order for:

- array indices and a large-array bucket;
- List elements;
- Dictionary key/value entries;
- enumerable Results View;
- public fields, Non-Public Members, Static Members, base, and Raw View;
- DebuggerDisplay and proxy selection;
- Component `this.gameObject`, active scene, and GameObject children on the main thread;
- absence of Unity target invocations on a worker thread.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CollectionValueTests|FullyQualifiedName~UnityValueProviderTests"
```

Expected: FAIL because providers do not exist.

- [ ] **Step 3: Implement providers in reference selection order**

Each provider recognizes one type family and returns `null` when not applicable. The composite chooses the first reference-compatible specialized provider, then falls back to the standard instance-member provider. Use the same invocation policy and request token as the containing Variables request.

- [ ] **Step 4: Run provider and formatter tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CollectionValueTests|FullyQualifiedName~UnityValueProviderTests|FullyQualifiedName~ValueFormatterTests"
```

Expected: PASS.

- [ ] **Step 5: Commit value providers**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/CollectionValueProviders.cs adapter/src/UnityDebugger.Adapter/Engine/Evaluation/Values/UnityValueProviders.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/CollectionValueTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Engine/Evaluation/UnityValueProviderTests.cs
git commit -m "feat: add reference debugger value providers"
```

### Task 8: Wire Scopes, Variables, Evaluate, and Set Variable to SuspendedState

**Files:**
- Create: `adapter/src/UnityDebugger.Adapter/Engine/Evaluation/EvaluationService.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs`
- Modify: `adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs`
- Create: `tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs`
- Modify: `tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs`

**Interfaces:**
- Backend Scopes/Variables/Evaluate/SetVariable accept `int timeoutMilliseconds` and a request cancellation token.
- Scopes registers one `FrameProperty` named Locals.
- Variables maps child `DebugProperty` objects into `BackendVariable` and registers expandable children.

- [ ] **Step 1: Write failing DAP transcript tests**

Cover these literal outcomes:

```csharp
[Fact]
public void MissingPropertyHandleReturnsEmptyVariablesSuccess()
{
    var backend = new FakeDebuggerBackend();
    var messages = RunAttached(
        backend,
        DapTestProtocol.Request(
            "variables",
            new { variablesReference = 404 }));

    var response = DapTestProtocol.Response(messages, "variables");
    Assert.True(response["success"]!.Value<bool>());
    Assert.Empty(response["body"]!["variables"]!);
}

[Fact]
public void MissingFrameReturnsEmptyScopesAndEvaluateSuccess()
{
    var backend = new FakeDebuggerBackend();
    var messages = RunAttached(
        backend,
        DapTestProtocol.Request("scopes", new { frameId = 404 }),
        DapTestProtocol.Request(
            "evaluate",
            new { frameId = 404, expression = "value", context = "hover" }));

    var scopes = DapTestProtocol.Response(messages, "scopes");
    Assert.True(scopes["success"]!.Value<bool>());
    Assert.Empty(scopes["body"]!["scopes"]!);
    var evaluate = DapTestProtocol.Response(messages, "evaluate");
    Assert.True(evaluate["success"]!.Value<bool>());
    Assert.True(evaluate["body"] is null || !evaluate["body"]!.HasValues);
}

[Fact]
public void HoverEnumComparisonReturnsTrue()
{
    var backend = new FakeDebuggerBackend
    {
        EvaluationResult = new BackendEvaluationResult("true", "System.Boolean", 0),
    };
    var messages = RunAttached(
        backend,
        DapTestProtocol.Request(
            "evaluate",
            new
            {
                frameId = 1,
                expression = "currentState == ButtonState.Normal",
                context = "hover",
            }));

    var response = DapTestProtocol.Response(messages, "evaluate");
    Assert.Equal("true", response["body"]!["result"]!.Value<string>());
    Assert.Equal(BackendEvaluationMode.Explicit, backend.LastEvaluationMode);
}

[Fact]
public void VariablesTimeoutReturnsEmptyVariablesWithoutTerminatingSession()
{
    var backend = new FakeDebuggerBackend
    {
        VariablesException = new OperationCanceledException(),
    };
    backend.Threads.Add(new BackendThread(1, "Main Thread"));
    var messages = RunAttached(
        backend,
        DapTestProtocol.Request(
            "variables",
            new { variablesReference = 1, timeout = 1 }),
        DapTestProtocol.Request("threads", new { }));

    var variables = DapTestProtocol.Response(messages, "variables");
    Assert.True(variables["success"]!.Value<bool>());
    Assert.Empty(variables["body"]!["variables"]!);
    var threads = DapTestProtocol.Response(messages, "threads");
    Assert.True(threads["success"]!.Value<bool>());
    Assert.Equal("Main Thread", threads["body"]!["threads"]![0]!["name"]!.Value<string>());
}
```

Define `RunAttached` in the test file to prepend literal Initialize and Attach requests and then append the supplied requests. Extend `FakeDebuggerBackend` with `VariablesException`; `GetVariables` throws it before returning configured values. For implicit evaluation enabled by the Attach request, Hover must use Explicit/reference behavior. A separate disabled-setting test asserts Safe for Hover and Explicit for Watch.

Add tests proving Hover/Locals use Safe when implicit evaluation is disabled, while Watch/REPL use Explicit. Add Set Variable response value/type/reference assertions.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReferenceInspectionTranscriptTests
```

Expected: FAIL because the new engine evaluation path is not wired and the current DAP layer contains custom generation/Task.Run behavior.

- [ ] **Step 3: Implement request-scoped EvaluationService**

Use `CancellationTokenSource.CreateLinkedTokenSource` and `CancelAfter(timeoutMilliseconds)` for each request. Variables/Evaluate default to 10,000 ms when DAP omits timeout. Do not keep a global list of operations.

On missing handles, return empty result models. On Variables timeout, return an empty list. On Getter exception, return the error property. On expression parse/evaluation failure, return the reference-compatible DAP Evaluate error only for that request.

Remove inspection `Task.Run`, `inspectionGeneration`, immediate resume handle reset, and custom “Variable collection is no longer available” errors from `UnityDebugSession`.

- [ ] **Step 4: Run all evaluation and DAP inspection tests**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Evaluation|FullyQualifiedName~Inspection"
```

Expected: PASS.

- [ ] **Step 5: Commit evaluation wiring**

```powershell
git add -- adapter/src/UnityDebugger.Adapter/Engine/Evaluation/EvaluationService.cs adapter/src/UnityDebugger.Adapter/Engine/UnityDebuggerEngine.cs adapter/src/UnityDebugger.Adapter/Backend/IDebuggerBackend.cs adapter/src/UnityDebugger.Adapter/Backend/BackendModels.cs adapter/src/UnityDebugger.Adapter/Dap/UnityDebugSession.cs tests/adapter/UnityDebugger.Adapter.Tests/Dap/ReferenceInspectionTranscriptTests.cs tests/adapter/UnityDebugger.Adapter.Tests/Fakes/FakeDebuggerBackend.cs
git commit -m "feat: wire reference inspection semantics"
```

### Task 9: Evaluation checkpoint

**Files:**
- Review only unless a failing test identifies a defect.

- [ ] **Step 1: Run the complete evaluation slice**

```powershell
dotnet test tests/adapter/UnityDebugger.Adapter.Tests/UnityDebugger.Adapter.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Engine.Evaluation|FullyQualifiedName~ReferenceInspectionTranscriptTests"
```

Expected: PASS with no warnings.

- [ ] **Step 2: Run all adapter tests and integration tests**

```powershell
dotnet test UnityDebugger.sln -c Release --no-restore
npm run test:integration
```

Expected: PASS. The test adapter continues using `FakeDebuggerBackend`; production does not switch until the release-parity plan.

- [ ] **Step 3: Verify removal of global evaluation policies**

```powershell
rg -n "AsyncOperationManager|CancelAsyncEvaluations|WaitForAbortCompletion|inspectionGeneration|Variable collection is no longer available" adapter/src adapter/vendor tests/adapter
git diff --check
```

Expected: no active production reference to the removed evaluation/cancellation behavior. Vendor files are removed from the build in the final plan.

- [ ] **Step 4: Record the checkpoint**

```powershell
git add -- docs/superpowers/plans/2026-08-06-reference-evaluation-parity.md
git commit -m "docs: complete reference evaluation parity plan"
```

Do not package or install a VSIX at this checkpoint.
