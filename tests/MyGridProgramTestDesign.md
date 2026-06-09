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

MotherCore already injects most of this through `ProgramFactory.ProgramBuilder<T>`. The design question is not whether we need a test seam. It is where that seam should live.

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

MotherCore now has a working version of most of this model in the test utilities:

- `ProgramFactory.CreateProgram<T>()` builds a script with injected `MyGridProgram` state.
- `Script<TProgram>` boots a real script instance, exposes `Mother`, `Bus`, `Clock`, `Config`, `IGC`, and `Program`, and supports pre-boot customization.
- `Script` is a convenience alias over `Script<CoreTestProgram>` for MotherCore-focused tests.
- `FakeIgcNetwork` provides shared IGC transport, message capture, and automatic Almanac cross-registration for booted scripts.
- `TestWorld` is now present as the shared multi-script environment for remote-network tests.
- `ClockDriver` provides assertion-friendly tick control for coroutine-driven behavior.
- `PrintCapture` provides reusable output capture over `Program.Echo`.
- `FakeProgrammableBlock : IMyProgrammableBlock` provides a concrete mutable programmable block.

The main gap is no longer the absence of a world abstraction. The larger remaining gaps are construct-topology support, richer block registration helpers, and a few ergonomics items such as composer overloads and config reload helpers.

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

- `TestWorld`

Supporting transport:

- `FakeIgcNetwork` for remote communication, message capture, and Almanac synchronization

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

Today, `Script<TProgram>` exposes this fluent surface:

- `WithIGC(IMyIntergridCommunicationSystem igc)`
- `OnNetwork(FakeIgcNetwork network)`
- `WithCustomData(string customData)`
- `WithCommands(params BaseModuleCommand[] commands)`
- `Boot()`
- `Run(UpdateType updateType, string argument = "")`
- `CaptureEcho()`

And after boot:

- `Program`
- `Mother`
- `Bus` which is just a convenience accessor for `Mother.GetModule<CommandBus>()`
- `Config`
- `Clock`
- `IGC`
- `NetworkIGC`

`TestWorld` also exists today and provides:

- `CreateScript<TProgram>(string name = null)`
- `DispatchIgc()`
- `Run(UpdateType updateType, string argument = "")`
- `RunIGC()`
- `RunMany(int count, UpdateType updateType, string argument = "")`

### What should come next

The next wave of helpers should grow from the `Script` plus `World` model, not from a detached host abstraction.

Likely additions:

- construct-topology support such as `CreateConstruct()` and construct-local dispatch
- block-registration helpers such as `WithBlock(...)` and grid-terminal builders
- configuration ergonomics such as `WithCustomData(Action<CustomDataComposer>)` and `ReloadConfiguration()`

The core run and world-delivery helpers already exist; the remaining work is mostly in convenience APIs and broader topology modeling.

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

This is the world-based version of what `FakeIgcNetwork` already does in a narrower form.

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
- `CustomDataComposer` in `Utilities/Factories/`, treated as a factory for the full `Program.Me.CustomData` payload rather than a runtime object.

Likely next helpers:

- `WithCustomData(Action<CustomDataComposer>)`
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

Current assertion helpers:

- `PrintCapture.Contains(string)`
- `PrintCapture.ShouldHavePrinted(string)`
- `PrintCapture.Clear()`

Likely next helpers:

- `script.CaptureEcho()`
- `script.ShouldHavePrinted("...")`

### Event inspection

The harness should support both real event flow and event spying.

This likely belongs on a focused test helper or event recorder, not directly on the low-level world abstraction.

## Recommended Base Classes

To reduce barrier to entry further, provide a small number of obvious base classes.

### `ScriptTestBase<TProgram>`

Purpose:

- integration-style tests for commands, module behavior, and event flow inside one booted script

Defaults:

- boots one script per test
- exposes `Script`, `Mother`, `Program`, `Bus`, and `Clock`

### `ScriptFeatureTestBase<TProgram>`

Purpose:

- integration tests for one script instance

Defaults:

- boots one real script instance
- wires `Echo` capture automatically
- exposes `Script`, `Mother`, `Program`, `Bus`, `Clock`, and `Echo`

### `WorldTestBase`

Purpose:

- remote-network tests inside one shared world

Defaults:

- creates a shared world
- exposes `World`
- provides world-level delivery and run helpers

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
| `OnNetwork(network)` | ✅ Done | Joins `FakeIgcNetwork` before boot |
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

### FakeIgcNetwork

| Feature | Status | Notes |
|---|---|---|
| `AllocateEndpoint()` | ✅ Done | Creates `FakeIgc` on the network |
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
| `DispatchIgc()` | ✅ Done | Delegates to `FakeIgcNetwork.Deliver()` |
| `Run(UpdateType, string)` | ✅ Done | Runs all booted scripts one cycle |
| `RunIGC()` | ✅ Done | Convenience for `Run(UpdateType.IGC)` |
| `RunMany(count, UpdateType, string)` | ✅ Done | Advances multiple cycles |
| `CreateConstruct()` / same-construct messaging | ⬜ Pending | Requires construct topology design |

### Base test classes

| Class | Status | Notes |
|---|---|---|
| `ScriptTestBase<TProgram>` | ✅ Done | Exposes `Script`, `Mother`, `Program`, `Bus`, `Clock` |
| `ScriptFeatureTestBase<TProgram>` | ✅ Done | Adds `Echo` capture; `SetUp` wires it automatically |
| `WorldTestBase` | ✅ Done | Exposes `World`; `SetUp` creates a fresh `TestWorld` |

### Script partial layer (script-specific seams)

| Feature | Status | Notes |
|---|---|---|
| `partial Program` in MotherOS tests | ⬜ Pending | Requires MotherOS test project |
| `partial Program` in MotherGUI tests | ⬜ Pending | Requires MotherGUI test project |

### Customization surfaces

| Feature | Status | Notes |
|---|---|---|
| `WithCustomData(string)` | ✅ Done | |
| `WithCustomData(Action<CustomDataComposer>)` | ⬜ Pending | Fluent composer overload |
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

---

## Test Coverage Plan

This plan reflects the current MotherCore source and the executable suite as it exists now. The current baseline is green, but coverage is concentrated around parsers, configuration, command routing, clock behavior, Almanac, and a small number of harness and terminal flows. The next steps should close the highest-risk gaps in core logic first.

### Phase 1: correctness-critical gaps

- [x] Expand `Unit/SerializerTests.cs` to cover list-heavy payloads.
    Added round-trips for flat lists, nested lists, list-of-dictionaries, dictionary-with-list payloads, escaping for quotes and backslashes, empty collections, and malformed input fallback. The serializer now has the Phase 1 baseline coverage planned here.

- [x] Add focused tests for intergrid message envelope parsing.
    Cover `IntergridMessageObject`, `Request`, `Response`, and `Router` with missing-tag, empty-tag, malformed-envelope, unmatched-route, and response-code cases. This closes an important gap in IGC message handling.

- [x] Extend `Unit/SecurityTests.cs` with edge cases.
    Covered empty-string round-trips, empty-passcode encrypt behavior, empty-passcode decrypt failure on encrypted payloads, unencrypted-input checks, and wrong-passcode decrypt behavior so current behavior is pinned explicitly.

### Phase 2: untested stateful core modules

- [x] Add integration coverage for `Modules/LocalStorage`.
    Covered set, get, clear, boot from `Program.Storage`, save-data serialization, and the `set` and `get` commands through the script harness.

- [ ] Add module boot and event coverage for `ConnectorModule`, `MechanicalBlockModule`, and `MergeBlockModule`.
    Reuse the test patterns documented in `Tests/README.md`: verify command registration, event subscription, state-transition behavior, and deferred hook behavior after construct refresh.

- [ ] Add focused integration coverage for `BlockCatalogue`.
    Cover state-monitor registration, change detection, block-group reload behavior, and construct refresh handling because multiple modules depend on it as a coordination point.

- [ ] Add tests for `ActivityMonitor`.
    Cover block registration, terminal-condition satisfaction, one-time callback execution, and automatic unregister after completion.

- [ ] Add targeted coverage for `DisplayModule`, `Display`, and `SpriteFactory`.
    Keep this focused on surface registration, source filtering, viewport math, and render-scale calculations. Parsing of display configuration is already covered separately.

### Phase 3: lower-risk pure utilities and migrations

- [ ] Add unit coverage for `ColorHelper`, `Geometry`, `NumberHelper`, and `MessageFormatter`.
    These are low-cost tests that improve confidence and prevent regressions in shared helper logic.

- [ ] Add migration coverage for `Configuration/VersionManager.cs`.
    Cover the `Commands` to `commands` rewrite and the security-to-channels migration path so legacy config upgrades are pinned.

- [ ] Revisit parser tests that currently document broken behavior as a baseline.
    Where appropriate, convert those into desired-behavior tests once the underlying implementation is ready to change.

### Recommended execution order

1. Serializer and intergrid message parsing
2. LocalStorage
3. Merge, Mechanical, Connector, and BlockCatalogue
4. ActivityMonitor and DisplayModule
5. Remaining pure utilities and migration helpers
6. PID if that utility becomes active again

### Notes for implementation

- Prefer `Unit/` for pure helpers and deterministic parsing behavior.
- Prefer `Integration/` for module boot, event, command, and cross-module behavior inside one booted `Script`.
- Prefer `WorldTestBase` and `TestWorld` for remote IGC scenarios that require more than one script.
- Reuse the examples in `Tests/README.md` as the canonical style guide for new tests.

