# MyGridProgram Test Design

## Goal

Create a reusable, low-friction test harness for Space Engineers scripts that inherit from `MyGridProgram`, so a user can test one script or multiple scripts such as `MotherOS.Program` and `MotherGUI.Program` with as little manual wiring as possible.

The `MyGridProgram` runtime contract that matters for tests is still the same:

- `GridTerminalSystem`
- `IGC`
- `Me`
- `Runtime`
- `Storage`
- `Echo`
- `World`

MotherCore already injects most of this through `Gateway.ProgramBuilder<T>`. The design question is not whether we need a test seam. It is where that seam should live.

The current answer is:

- use a test-only `partial Program` as the script seam
- use `Script<TProgram>` as one booted programmable block instance
- use a shared `World` as the environment for multi-script interaction

## Core Model

### 1. `partial Program` is the script seam

Both MotherOS and MotherGUI define `Program` as a partial class.

That matters because a test project that imports their source can contribute an additional test-only partial definition of the same `Program` type. In practice, that means:

- the script's production partials still define the real constructor and module registration
- the test partial can expose test-only hooks into the script
- the compiled result is still the real `Program` type, not a separate wrapper

This is a better seam for script-specific behavior than introducing a parallel host abstraction such as `TestGridProgramHost`.

Examples of what a test-only partial can do:

- expose the internal `mother` field through a property or method
- add script-specific helpers for common assertions
- register test-only modules in a controlled way
- implement a small interface the generic harness can rely on

The important point is that the script and the test partial together represent the full `Program` class in the test assembly.

### 2. `Script<TProgram>` is one programmable block instance

`Script<TProgram>` remains the generic test harness entry point.

Its responsibility is to boot one `MyGridProgram` instance with a programmable-block-shaped runtime.

In the current implementation, `Script<TProgram>` already provides:

- `WithIGC(...)`
- `OnNetwork(...)`
- `WithCustomData(...)`
- `WithCommands(...)`
- `Boot()`
- post-boot access to `Program`, `Mother`, `Bus`, `Config`, `Clock`, `IGC`, and `NetworkIGC`

That makes `Script<TProgram>` the right abstraction for:

- one programmable block
- one constructor run
- one `Mother` instance
- one script-local `Me`
- one script-local `GridTerminalSystem`
- one script-local `Runtime` facade

### 3. `World` is the shared environment

Multi-script tests should not be modeled as a bag of unrelated `Script` instances.

They should be modeled as multiple scripts running inside one shared `World`.

The world is the shared environment that owns:

- the common tick source or simulation clock
- shared IGC transport
- topology such as network membership and later same-construct membership
- optional shared services such as world information and default echo routing

A `World` should create or coordinate multiple `Script` instances. Each script then projects that shared environment into its own programmable block runtime.

## Ownership Rules

The goal is to mirror the game closely enough to make tests intuitive while still keeping setup cheap.

### Shared at the world level

These should default to being shared by all scripts in the same test world:

- world clock or time source
- IGC transport
- network topology
- `IMyGridProgramWorldInfo`
- optional default echo sink

### Unique per script

These should remain unique per `Script` instance:

- `Me`
- `GridTerminalSystem`
- `Runtime`
- `Storage`
- script identity such as grid/program name

This is an important correction to the earlier design direction.

A shared `Runtime` object is not a good fit, because `IMyGridProgramRuntimeInfo` represents script-local execution state such as instruction count, update frequency, and time since last run. In the real game, those belong to one programmable block, not the entire world.

The better model is:

- a shared world clock
- a per-script runtime facade backed by that world clock

## Why Partial Classes Beat a Separate Host Object

The earlier design leaned toward promoting the entire `MyGridProgram` runtime into a separate host object.

That approach is heavier than necessary for MotherOS and MotherGUI because their `Program` classes are already partial.

Using test-only partials has a few advantages:

- the seam is at the real script type, not beside it
- there is no extra wrapper the user has to learn first
- script-specific helpers can stay close to the script they belong to
- integration tests can stay focused on the real constructor and real module graph

This does not remove the need for shared infrastructure. It just changes where script-specific extension points should live.

The right split is:

- script-specific hooks via partial `Program`
- runtime/environment concerns via `Script` and `World`

## Current Implementation

MotherCore already has the beginning of this model:

- `Gateway.CreateProgram<T>()` builds a script with injected `MyGridProgram` state
- `Script<TProgram>` boots a real script instance with very little setup
- `MockIGCNetwork` provides shared IGC transport for multiple scripts
- `ClockDriver` makes coroutine and scheduling tests easier
- `TestProgrammableBlock : IMyProgrammableBlock` provides a concrete mutable programmable block

The current implementation is still closer to "standalone scripts plus a network" than to a first-class `World`, but it already points in the right direction.

## Recommended Shape

## Test Layers

Use three primary layers.

### 1. Script partial layer

This is the script-specific seam.

Suggested role:

- expose test-only accessors or helpers on the real `Program`
- avoid reflection where the script can provide a direct seam
- keep script-specific setup in the script's own test project

Examples:

- `partial class Program` in MotherOS tests
- `partial class Program` in MotherGUI tests

### 2. Script layer

This is the single-programmable-block runtime wrapper.

Suggested role:

- boot an actual `Program : MyGridProgram`
- expose `Program`, `Mother`, `Config`, and `Clock`
- expose `Bus` as a convenience accessor for the `CommandBus` core module
- own one programmable block identity
- own one script-local grid terminal system and storage
- bind the script into a world or network

Current closest type:

- `Script<TProgram>`

### 3. World layer

This is the multi-script environment.

Suggested role:

- coordinate multiple `Script` instances
- provide a shared clock and shared world info
- provide common IGC transport
- support same-construct and remote-network topologies
- drive message delivery and later construct delivery

Current closest type:

- `MockIGCNetwork` for remote communication only

Suggested future type:

- `TestWorld`

## Default Mental Model

The easiest path should read like this:

```csharp
var script = new Script<Program>().Boot();
```

That should be the default single-script experience.

When a user wants multiple scripts, the mental model should become:

```csharp
var world = new TestWorld();

var os = world.CreateScript<MotherOS.Program>("ShipOS").Boot();
var gui = world.CreateScript<MotherGUI.Program>("ShipGUI").Boot();
```

The user should think in terms of scripts inside a world, not in terms of manually wiring runtime internals.

## Default Test API

### What exists today

Today, `Script<TProgram>` exposes this minimal fluent surface:

- `WithIGC(IMyIntergridCommunicationSystem igc)`
- `OnNetwork(MockIGCNetwork network)`
- `WithCustomData(string customData)`
- `WithCommands(params BaseModuleCommand[] commands)`
- `Boot()`

And after boot:

- `Program`
- `Mother`
- `Bus` which is just a convenience accessor for `Mother.GetModule<CommandBus>()`
- `Config`
- `Clock`
- `IGC`
- `NetworkIGC`

### What should come next

The next wave of helpers should grow from the `Script` plus `World` model, not from a detached host abstraction.

Likely additions:

- `Run(UpdateType updateType)` or `Run(string argument, UpdateType updateType)`
- transport-specific helpers such as `DispatchIgc()` or `DispatchConstructMessages()`

Not all of these exist yet.

## Generic Test Scenarios

The following scenarios should drive the API design.

### 1. Unit test: execute an `IModuleCommand` within one script

Intent:

- verify command parsing
- verify command return values or side effects
- avoid booting unrelated external scripts

Ideal shape:

```csharp
var script = new Script<Program>().Boot();
var commandBus = script.Bus;

commandBus.RunTerminalCommand("rename Frigate");
script.Clock.RunToIdle();
```

If the script test project adds a partial `Program` helper, the test can use that helper directly instead of reflection or broad generic helpers.

`Bus` is not a second routing abstraction layered on top of the system. It is simply the booted `CommandBus` module, exposed as a convenience property because command execution is a very common test entry point.

Default expectations:

- the script boots without custom setup
- command registration happens through the real constructor and module graph
- `ClockDriver` is available for coroutine-based execution

### 2. Unit test: call a method on an `IModule`

Intent:

- isolate one module API
- avoid command routing when parsing is not what is under test

Ideal shape:

```csharp
var script = new Script<Program>().Boot();
var module = script.Mother.GetModule<DisplayModule>();

module.SomePublicMethod(...);
```

If a script-specific partial exposes the needed module or service directly, that is preferable to adding a generic `GetModule<T>()` surface to every script test.

### 3. Integration test: one script, event emitted in one module, observed in another

Intent:

- verify subscription wiring
- verify event-driven interaction between real modules

Ideal shape:

```csharp
var script = new Script<Program>().Boot();
var eventBus = script.Mother.GetModule<EventBus>();
var source = script.Mother.GetModule<ConnectorModule>();
var target = script.Mother.GetModule<SomeSubscriberModule>();

source.SomeMethodThatEmitsAnEvent();

Assert.That(... target reacted ...);
```

This stays a single-script test.

Current `EventBus` behavior is important here: `EventBus.Emit(...)` is synchronous. When an event is emitted, the bus immediately enumerates subscribers and calls `module.HandleEvent(e, eventData)` on each one in the same call stack.

That means there are two different things a test may want to assert:

- that a module is subscribed to the event at all
- that a subscriber actually handled the emitted event

For subscription wiring, assert directly against the bus:

```csharp
Assert.That(eventBus.IsSubscribed<SomeEvent>(target), Is.True);
```

For handling, prefer asserting on an observable effect produced by `HandleEvent(...)` rather than only asserting that the method was called.

Examples:

- state changed on the target module
- a command was queued
- a record was updated
- output was printed

If you need a narrower assertion, subscribe a spy module and verify that its `HandleEvent(...)` method was invoked when the event is emitted.

One subtlety: if the event is emitted inside a command coroutine, the event dispatch itself is still synchronous at the moment of emission, but the test still needs to advance the script to the point where that command runs. That is why `script.Clock.RunToIdle()` or explicit tick control still matters for some event tests.

### 4. Integration test: one script, one module calls another through `Mother`

Intent:

- verify inter-module collaboration through the real registration graph

Ideal shape:

```csharp
var script = new Script<Program>().Boot();
var caller = script.Mother.GetModule<IntergridMessageService>();

caller.SomeMethod();

Assert.That(script.Mother.GetModule<Almanac>().GetRecord("ShipA"), Is.Not.Null);
```

### 5. Integration test: multiple scripts in one world on the same construct

Intent:

- verify construct messaging
- verify relay and same-construct behavior
- model multiple programmable blocks that share a physical environment

Ideal shape:

```csharp
var world = new TestWorld();
var construct = world.CreateConstruct();

var os = world.CreateScript<MotherOS.Program>("ShipOS")
    .OnConstruct(construct)
    .Boot();

var gui = world.CreateScript<MotherGUI.Program>("ShipGUI")
    .OnConstruct(construct)
    .Boot();

os.Mother.GetModule<IntergridMessageService>()
    .SendConstructCommand(gui.Mother.Id, "view/go status");

construct.DispatchMessages();
world.RunIGC();
```

Default expectations:

- scripts share the same world
- scripts share construct topology
- each script still has its own `Me`, `GridTerminalSystem`, `Runtime`, and `Storage`

### 6. Integration test: multiple scripts in one world over IGC

Intent:

- verify remote command routing
- verify almanac discovery and remote targeting

Ideal shape:

```csharp
var world = new TestWorld();

var shipA = world.CreateScript<MotherOS.Program>("ShipA").Boot();
var shipB = world.CreateScript<MotherGUI.Program>("ShipB").Boot();

shipA.Bus.RunTerminalCommand("@ShipB view/go status");
world.DispatchIgc();
world.RunIGC();
```

This is the world-based version of what `MockIGCNetwork` already does in a narrower form.

The naming matters here. `Deliver()` is too vague because it mixes at least two different concerns:

- moving pending messages through transport
- advancing one or more program cycles

Those should be separate concepts in the future API.

Suggested direction:

- `world.DispatchIgc()` moves queued IGC messages into recipient inboxes
- `construct.DispatchMessages()` moves queued same-construct messages
- `world.Run(UpdateType updateType, string argument = "")` runs one real program cycle for scripts in the world
- `world.RunIGC()` is a convenience helper for `world.Run(UpdateType.IGC)`
- `world.RunMany(int count, UpdateType updateType)` advances multiple cycles explicitly

That makes tests read more like Space Engineers itself:

- player/terminal input maps to `UpdateType.Terminal` or `UpdateType.Trigger`
- antenna processing maps to `UpdateType.IGC`
- periodic runtime work maps to `UpdateType.Update10`, `UpdateType.Update100`, or similar

## Common Customization Surfaces

These are the changes a user is most likely to make in tests.

### Custom data and configuration

This should remain first-class because it is how users naturally configure Mother scripts.

Current helper:

- `WithCustomData(string)`

Likely next helpers:

- `WithCustomData(Action<CustomDataBuilder>)`
- `ReloadConfiguration()`

### Terminal block registration

The harness should provide a block factory plus registration helpers around each script's local `GridTerminalSystem`.

Expected pieces:

- `TerminalBlockFactory`
- `BlockGroupFactory`
- `GridTerminalSystemBuilder`

Likely helpers:

- `WithBlock(IMyTerminalBlock block)`
- `WithBlocks(params IMyTerminalBlock[] blocks)`
- `WithBlock<TBlock>(Action<TestBlockBuilder<TBlock>> configure)`

### Runtime control

The harness should treat runtime as a per-script facade over a shared world clock.

Current helper:

- `script.Clock.Tick()`
- `script.Clock.Tick(n)`
- `script.Clock.RunToIdle()`

Likely next helpers:

- `world.Run(UpdateType updateType, string argument = "")`
- `world.RunIGC()`
- `world.RunMany(int count, UpdateType updateType)`
- world-level time advance
- script runtime inspection helpers
- instruction-count control per script

`ClockDriver` is the right abstraction for coroutine progression inside an already-running script. It is not quite the same thing as driving real `Program.Run()` cycles. The future `World` API should make that distinction explicit.

### Echo and logs

The user should be able to assert on output without manually replacing `Echo`.

Current helper:

- `new PrintCapture(script)`

Likely next helpers:

- `script.CaptureEcho()`
- `script.ShouldHavePrinted("...")`

### Event inspection

The harness should support both real event flow and event spying.

This likely belongs on a focused test helper or event recorder, not directly on the low-level world abstraction.

## Recommended Base Classes

To reduce barrier to entry further, provide a small number of obvious base classes.

### `ModuleUnitTestBase<TProgram>`

Purpose:

- unit tests for commands and module methods inside one script

Defaults:

- boots one script per test
- exposes `Script`, `Mother`, and `Program`
- provides any repeated accessors that are truly common

### `ProgramFeatureTestBase<TProgram>`

Purpose:

- integration tests for one script instance

Defaults:

- boots one real script instance
- exposes clock, output capture, config helpers, and block registration helpers

### `MultiProgramFeatureTestBase`

Purpose:

- same-construct and remote-network tests inside one shared world

Defaults:

- creates a shared world
- provides named scripts
- provides delivery helpers for world, construct, and network traffic

## Proposed Mental Model For Users

Users should be able to think in these terms:

- if I want to test one command, I boot one `Script`
- if I want to test one module method, I boot one `Script`
- if I want to test script-specific details, I add a test-only partial `Program`
- if I want to test script-to-script communication, I create multiple scripts inside one `World`
- if I want to test behavior that unfolds across multiple game ticks or `Program.Run()` cycles, I drive explicit world cycles with `UpdateType`

The harness should hide `MyGridProgram` bootstrapping details by default while still letting advanced tests opt into topology and runtime control.

For longer-running behavior, the intended mental model is:

```csharp
var world = new TestWorld();
var script = world.CreateScript<Program>().Boot();

world.Run(UpdateType.Update10);
world.RunMany(30, UpdateType.Update10);
```

For command-driven behavior, the intended mental model is:

```csharp
var world = new TestWorld();
var script = world.CreateScript<Program>().Boot();

world.Run(UpdateType.Terminal, "rename Frigate");
world.RunMany(5, UpdateType.Update10);
```

This keeps tests aligned with the actual `Program.Main(...)` and `Mother.Run(...)` model rather than only with lower-level coroutine helpers.

## Boundary For Now

The current programmable block implementation remains intentionally light. Members that MotherCore does not use yet either return simple defaults or remain inert.

The same rule should apply to the rest of the design:

- implement stable defaults first
- add explicit helpers for common customization points
- grow the fake API surface only when a real test needs it

The goal is not to fully simulate Space Engineers. The goal is to make testing Mother scripts and modules easy, predictable, and cheap.

---

## Implementation Progress

> Last updated: 2026-06-09

### Script layer (`Script<TProgram>`)

| Feature | Status | Notes |
|---|---|---|
| `Boot()` | ✅ Done | Boots program, extracts Mother, runs module Boot |
| `WithIGC(igc)` | ✅ Done | Injects custom IGC before boot |
| `OnNetwork(network)` | ✅ Done | Joins `MockIGCNetwork` before boot |
| `WithCustomData(string)` | ✅ Done | Sets custom data before boot |
| `WithCommands(params ...)` | ✅ Done | Registers extra commands after boot |
| `OnBeforeBoot(mother)` hook | ✅ Done | Override to inject test-only modules |
| `Program`, `Mother`, `Bus`, `Config`, `Clock`, `IGC`, `NetworkIGC` | ✅ Done | Post-boot accessors |
| `Run(UpdateType, string)` | ✅ Done | Drives one real `Mother.Run` cycle |
| `CaptureEcho()` | ✅ Done | Returns `PrintCapture`; wires up Echo redirect |

### PrintCapture

| Feature | Status | Notes |
|---|---|---|
| `new PrintCapture(IScript)` | ✅ Done | Manual construction, existing pattern |
| `Contains(string)` | ✅ Done | Returns bool |
| `Clear()` | ✅ Done | Empties captured lines |
| `ShouldHavePrinted(string)` | ✅ Done | NUnit assertion helper |

### MockIGCNetwork

| Feature | Status | Notes |
|---|---|---|
| `AllocateEndpoint()` | ✅ Done | Creates `MockIGC` on the network |
| `RegisterSession(session, name)` | ✅ Done | Cross-populates Almanac on boot |
| `Deliver()` | ✅ Done | Routes pending messages + triggers IGC processing |
| `SentMessages` | ✅ Done | Capture list for lightweight assertions |
| `ClearSentMessages()` | ✅ Done | Empties the capture list |
| `Sessions` | ✅ Done | Exposes all registered `IScript` instances |
| `DispatchIgc()` | ✅ Done | Preferred alias for `Deliver()` |

### TestWorld (multi-script environment)

| Feature | Status | Notes |
|---|---|---|
| `CreateScript<T>(name)` | ✅ Done | Returns `Script<T>` joined to world network |
| `DispatchIgc()` | ✅ Done | Delegates to `MockIGCNetwork.Deliver()` |
| `Run(UpdateType, string)` | ✅ Done | Runs all booted scripts one cycle |
| `RunIGC()` | ✅ Done | Convenience for `Run(UpdateType.IGC)` |
| `RunMany(count, UpdateType, string)` | ✅ Done | Advances multiple cycles |
| `CreateConstruct()` / same-construct messaging | ⬜ Pending | Requires construct topology design |

### Base test classes

| Class | Status | Notes |
|---|---|---|
| `ModuleUnitTestBase<TProgram>` | ✅ Done | Exposes `Script`, `Mother`, `Program`, `Bus`, `Clock` |
| `ProgramFeatureTestBase<TProgram>` | ✅ Done | Adds `Echo` capture; `SetUp` wires it automatically |
| `MultiProgramFeatureTestBase` | ✅ Done | Exposes `World`; `SetUp` creates a fresh `TestWorld` |

### Script partial layer (script-specific seams)

| Feature | Status | Notes |
|---|---|---|
| `partial Program` in MotherOS tests | ⬜ Pending | Requires MotherOS test project |
| `partial Program` in MotherGUI tests | ⬜ Pending | Requires MotherGUI test project |

### Customization surfaces

| Feature | Status | Notes |
|---|---|---|
| `WithCustomData(string)` | ✅ Done | |
| `WithCustomData(Action<CustomDataBuilder>)` | ⬜ Pending | Fluent builder overload |
| `ReloadConfiguration()` | ⬜ Pending | Hot-reload config in test |
| `TerminalBlockFactory` / `GridTerminalSystemBuilder` | ⬜ Pending | Block registration helpers |
| `WithBlock(IMyTerminalBlock)` / `WithBlocks(...)` | ⬜ Pending | Convenience block injection |

### Future / long-term

| Feature | Status | Notes |
|---|---|---|
| World-level shared clock / time advance | ⬜ Pending | Per-script `ClockDriver` exists today |
| `world.CreateConstruct()` | ⬜ Pending | Same-construct messaging topology |
| Script runtime inspection helpers | ⬜ Pending | Instruction count, update frequency per script |
| Event recorder / spy module | ⬜ Pending | For narrower event assertion |

