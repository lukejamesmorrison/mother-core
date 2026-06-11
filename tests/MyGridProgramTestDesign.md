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

These are not just constructor inputs. Together they are the programmable block test harness.

The harness should feel like a small game-world runtime:

- `World` owns shared test infrastructure and world-scoped utilities
- `Grid` is where scripts and blocks live
- `Script<TProgram>` is one programmable block instance mounted on a grid
- terminal blocks are world objects that can be registered, queried, mutated, and asserted against

MotherCore already injects most of this through `ProgramFactory.ProgramBuilder<T>`. The design question is not whether we need a test seam. It is where that seam should live.

The current answer is:

- use a test-only `partial Program` as the script seam
- use `Script<TProgram>` as one booted programmable block instance
- use a shared `World` as the environment for all script interaction, even when a test only boots one script

In the current MotherCore harness, that world model now includes a small world-owned grid handle:

- `TestWorld` owns shared environment state and topology orchestration
- `TestGrid` is a world-bound handle used to register blocks onto a specific grid before boot
- `MergePair` is an optional deferred topology descriptor for pre-registered merge-block pairs

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
- `OnNetwork()`
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

### 4. The harness should be explicit and complete

The main runtime-facing seams should be treated as first-class harness components, not as incidental bits of setup hidden inside factories:

- `GridTerminalSystem`
- `IGC`
- `Storage`
- `Echo`
- `Me`

Most of these already exist in some form today. The design direction should now be to make them explicit world and script services with stable fake implementations and developer-friendly helpers.

That matters because this layer is not business logic under test. It is orchestration infrastructure. It should therefore optimize for:

- intuitive setup
- representative behavior
- low-friction mutation during a test
- strong assertions without bespoke per-test wiring

The end goal is not merely to be able to fake Space Engineers. The goal is to make the common testing path feel obvious.

## Harness Composition

Think of the harness as a composition root for game-runtime test doubles.

### World-owned services

The world should own the shared services and default factories that make tests feel coherent:

- world-level topology and grid registry
- shared IGC transport and dispatch
- default world info
- default echo routing and optional output capture
- entity and block ID allocation
- block registration and later construct orchestration

### Script-owned projections

Each script should receive its own programmable-block-local view of that world:

- one `Me`
- one `Storage`
- one `Runtime`
- one script-facing `GridTerminalSystem` projection onto the world topology
- one `Echo` delegate that can be captured at script or world scope

This is the important split:

- the world owns the source of truth
- the script owns the programmable-block-local projection of that truth

That model matches the game more closely than a pile of isolated mocks.

## Ownership Rules

The goal is to mirror the game closely enough to make tests intuitive while still keeping setup cheap.

### Shared at the world level

These should default to being shared by all scripts in the same test world or at least originated there:

- world clock or time source
- IGC transport
- network topology
- `IMyGridProgramWorldInfo`
- optional default echo sink
- grid and block registries
- entity identity allocation

### Unique per script

These should remain unique per `Script` instance:

- `Me`
- `GridTerminalSystem`
- `Runtime`
- `Storage`
- `Echo` delegate binding
- script identity such as grid/program name

This is an important correction to the earlier design direction.

A shared `Runtime` object is not a good fit, because `IMyGridProgramRuntimeInfo` represents script-local execution state such as instruction count, update frequency, and time since last run. In the real game, those belong to one programmable block, not the entire world.

The better model is:

- a shared world clock
- a per-script runtime facade backed by that world clock
- a world-owned environment with per-script programmable-block projections

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
- terminal block simulation via dedicated harness block fakes and factories

## Current Implementation

MotherCore now has a working version of most of this model in the test utilities:

- `ProgramFactory.CreateProgram<T>()` builds a script with injected `MyGridProgram` state.
- `Script<TProgram>` boots a real script instance, exposes `Mother`, `Bus`, `Clock`, `Config`, `IGC`, `NetworkIGC`, `GridTerminalSystem`, `PrimaryGrid`, and `Program`, and supports pre-boot customization.
- `CommandBus` exposes compact execution counters through `GetExecutionCount(commandName, outcome)` so tests can assert command and routine processing against real commands without shared spy helpers.
- `Script` is a convenience alias over `Script<CoreTestProgram>` for MotherCore-focused tests.
- `FakeIgcNetwork` provides shared IGC transport, message capture, and automatic Almanac cross-registration for booted scripts.
- `TestWorld` is now present as the shared multi-script environment for remote-network and world-topology tests.
- `TestGrid` is the world-owned grid handle returned by `TestWorld.CreateGrid(...)`; it wraps an `IMyCubeGrid` and provides `AddBlock(...)` for world-owned block registration.
- `MergePair` is a deferred merge-topology descriptor returned by `TestWorld.AddMergeBlockPair(...)` when a test wants merge blocks pre-registered before boot.
- `ClockDriver` provides assertion-friendly tick control for coroutine-driven behavior.
- `PrintCapture` provides reusable output capture over `Program.Echo`.
- `TerminalBlockFactory` now creates explicit harness-backed terminal block fakes for the supported common interfaces and fails fast for unsupported block interfaces.
- `FakeTerminalBlock` already provides a concrete mutable base for richer fake terminal blocks.
- `TestGrid` now supports both registering an existing block instance and creating one directly by interface type and name, plus grid-local lookup and `ShouldContainBlock(...)` assertions.
- `Script<TProgram>` now mirrors that convenience with a generic `WithBlock<TBlock>(...)` overload plus script-local block lookup and `AssertHasBlock(...)`.
- `GridFactory` now creates concrete `FakeCubeGrid` instances rather than interface mocks.
- connector pairs now use concrete `FakeShipConnector` blocks under `ConnectorConnectionFactory`
- merge pairs now use concrete `FakeShipMergeBlock` blocks under `MergeConnectionFactory`, and merge-pair configuration now requires those explicit harness blocks.
- mechanical connections now use concrete `FakeMotorStator`, `FakePistonBase`, `FakeMotorRotor`, and `FakePistonTop` blocks under `MechanicalConnectionFactory`.
- `TextSurfaceFactory` provides lightweight `FakeTextSurface` and `FakeTextPanel` objects with captured `WriteText(...)`, `ContentType`, `SurfaceSize`, and `TextureSize` state for display-focused tests.
- `FakeProgrammableBlock : IMyProgrammableBlock` provides a concrete mutable programmable block.
- `FakeGridProgramRuntimeInfo : IMyGridProgramRuntimeInfo` provides the default runtime fallback used by `ProgramFactory`.

Display-related coverage is now also in place for the current harness shape:

- `DisplayModuleTests` covers surface registration, log-source filtering, and reload through the module's `BlockConfigChangedEvent` path.
- `DisplayTests` covers viewport and font-size math, scaling, text-line formatting, and the current public drawing surface of `Display`, including sprite primitives, text helpers, debug output, and the Mother badge renderer.
- `SpriteFactoryTests` pins the `MySprite` construction contract used by the display helpers.

The main gap is no longer the absence of a world abstraction. The larger remaining gaps are:

- making the world the obvious default entry point for harness setup
- promoting harness services (`Me`, `Storage`, `Echo`, `GridTerminalSystem`, `IGC`) into named first-class fake components
- unifying terminal block setup around a richer block model rather than ad hoc interface mocking
- adding assertion helpers scoped to world, script, grid, and block behavior
- a few ergonomics items such as composer overloads and config reload helpers

The explicit block-family rollout is now complete for the currently active harness surfaces:

- `FakeDoor` backs the common `IMyDoor` path
- `FakeBatteryBlock` backs the common `IMyBatteryBlock` path
- `FakeReactor` backs the common `IMyReactor` path
- `FakeShipConnector` and `FakeShipMergeBlock` back connector and merge topology helpers
- `FakeTextSurface` and `FakeTextPanel` back display-focused tests
- `FakeMotorRotor` and `FakePistonTop` now replace the old mechanical top-part interface-fake path

The current stop condition for this refactor has also changed:

- FakeItEasy is no longer the normal way to model game blocks or harness infrastructure in MotherCore tests
- generic interface fabrication has been removed from the harness; unsupported `TerminalBlockFactory.Create<TBlock>()` requests now fail fast so new block families stay explicit

The remaining package-removal checklist is now narrower and mostly project-level:

- replace any remaining direct fixture-local FakeItEasy setup where it still appears outside the harness guidance
- remove the `FakeItEasy` and `FakeItEasy.Analyzer.CSharp` package references from `MotherCore.Tests.csproj` once no active tests depend on them
- keep `Tests/README.md` and harness guidance aligned with harness-owned fake types and world/script helpers as the default path

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
- own one script-local projection of harness services such as `Me`, `Storage`, `Runtime`, and `Echo`
- bind the script into a world-owned grid and transport model

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

### 4. Block layer

This is the missing ergonomic layer between world setup and module logic.

Suggested role:

- create representative `IMyTerminalBlock` test doubles without per-test mock wiring
- allow type-specific behavior to be layered in only when needed
- support world-owned block registration before and after script boot
- expose assertion-friendly state for tests that care about block mutation

Recommended split:

- use concrete fake block types for common, stateful, behavior-rich blocks
- keep interface-backed factories only as a thin escape hatch for rare or highly specific interfaces
- make the world and grid helpers the normal registration path so tests think in terms of game objects, not mocks

Current closest types:

- `FakeTerminalBlock`
- `TerminalBlockFactory`
- `TestGrid.AddBlock(...)`
- `FakeShipConnector`
- `FakeShipMergeBlock`
- `FakeMotorStator` / `FakePistonBase`

## Terminal Block Strategy

Terminal blocks are the main domain object after the script itself.

Mother is intentionally agnostic to exact block type until modules begin to care. That makes terminal blocks the most important extensibility seam for module tests.

The desired developer experience should be:

```csharp
var world = new TestWorld();
var grid = world.CreateGrid("Frigate");

var door = grid.AddBlock(BlockFactory.Door("Airlock", b =>
{
    b.Status = DoorStatus.Closed;
    b.CustomData = "[tags]\nrole=airlock";
}));

var script = world.CreateScript<Program>(grid, "ShipOS").Boot();
```

The important part is not the exact API spelling. The important part is that the user configures a block-shaped object with mutable state and sensible defaults, then places it onto a grid in the world.

### Recommended fake model

Use a tiered block strategy.

#### Tier 1: concrete base fake

Provide a concrete base fake for shared `IMyTerminalBlock` behavior and state:

- naming
- custom data
- grid ownership
- entity identity
- enabled / working / functional flags
- same-construct evaluation
- position and basic world metadata

This is already the role `FakeTerminalBlock` wants to play. That direction should be reinforced.

#### Tier 2: common typed fakes

Add concrete typed fakes for the block families Mother modules commonly manipulate.

Examples:

- `FakeDoor`
- `FakeShipConnector`
- `FakeShipMergeBlock`
- `FakeMotorStator` / `FakeMechanicalConnectionBlock`
- `FakeCockpit` or `FakeRemoteControl`
- `FakeTextPanel` / text-surface providers
- `FakeBatteryBlock`, `FakeReactor`, `FakeSensorBlock` as needed by active modules

These should expose mutable state and behavior that mirrors the game at the level tests actually care about.

This rollout is now in its next phase: common active families are explicit, so future growth should be demand-driven.

The current guidance is:

1. keep adding typed harness fakes only when a real test needs a new family
2. prefer world/grid registration helpers over low-level factory calls when the test is expressing game topology
3. let unsupported `TerminalBlockFactory.Create<TBlock>(...)` calls fail loudly so missing harness coverage is visible and intentional

#### Tier 3: interface escape hatch

There is no generic interface escape hatch in the harness anymore.

If a new block family is needed, add a concrete fake or extend an existing one.

The developer should not need to know FakeItEasy to write most Mother module tests.

## Assertions and Developer Experience

The harness should not stop at setup. A frictionless harness also needs scoped assertions.

Recommended assertion layers:

- world assertions for topology, messaging, and shared output
- script assertions for command execution, printed output, emitted events, and module access
- grid assertions for registered blocks, groups, and construct membership
- block assertions for state transitions and received commands

Examples of the intended style:

```csharp
script.ShouldHavePrinted("System online");
script.ShouldHaveExecutedCommand("rename");
world.ShouldHaveDeliveredIgcMessage("ShipA", "ShipB");
grid.ShouldContainBlock("Airlock");
door.ShouldBeOpen();
```

Again, the exact method names are less important than the experience:

- setup should read like world construction
- execution should read like game actions
- assertions should read like domain outcomes

## Recommended Direction

The default mental model should move one step further:

- every test starts conceptually in a `TestWorld`
- every script is mounted on a `TestGrid`
- every runtime seam comes from the harness, not from ad hoc test setup
- every block is either a concrete fake or a consciously chosen fallback factory fake

For convenience, single-script helpers can still exist:

```csharp
var script = new Script<Program>().Boot();
```

But that should be understood as shorthand for creating a tiny default world, not as a separate conceptual path.

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

They should also think in terms of blocks and grids as first-class test objects, not in terms of mocking interfaces.

## Action Plan

The next implementation wave should focus on ergonomics before breadth.

### Phase 1: make the harness model explicit

- document `TestWorld` as the primary composition root even for single-script tests
- name and expose the harness-owned runtime components more directly
- make `Script<TProgram>` read as a projection of world state rather than an owner of global setup
- align docs and examples around world -> grid -> script -> blocks

### Phase 2: unify the block story

- standardize on `FakeTerminalBlock` as the concrete base for block state
- add typed fake blocks for the block families used most by active modules
- keep `TerminalBlockFactory` as an escape hatch, but stop teaching it as the default pattern
- ensure world and grid registration helpers can accept both concrete fakes and factory-created blocks

Immediate Phase 2 continuation order:

- add new typed harness blocks only for genuinely new module coverage
- migrate any remaining fixture-local FakeItEasy setup onto existing harness fakes when those tests are touched

Phase 2 exit criteria:

- active tests rely on harness-owned concrete blocks for the currently common families
- display tests rely on harness-owned fake surfaces and panels
- mechanical topology relies on concrete harness base and top-part blocks
- unsupported block families fail fast instead of falling back to a generic mock/proxy path

Package-removal exit criteria:

- `MotherCore.Tests.csproj` no longer references `FakeItEasy` or `FakeItEasy.Analyzer.CSharp`
- no harness factory or mock type still depends on `A.Fake(...)` or `A.CallTo(...)`
- no active tests rely on FakeItEasy for spies, runtime stubs, or terminal/grid infrastructure
- `Tests/README.md` and harness guidance describe only harness-owned fake types and assertions as the standard path

### Phase 3: add harness-scoped helpers and assertions

- add world-level helpers for output capture, message dispatch, and topology assertions
- add script-level helpers for common runtime seams like `Storage`, `Me`, and `Echo`
- add grid-level lookup and assertion helpers
- add block-level assertion helpers for common behaviors

### Phase 4: reduce framework leakage from tests

- minimize direct FakeItEasy setup in fixture code
- move repeated behavior into harness fakes and builders
- update test guidance so module tests default to harness block fakes rather than hand-written mocks

### Phase 5: grow only where real tests need it

- add more typed block fakes only when a real module test benefits from them
- expand construct support and world-level delivery only when specific scenarios demand it
- keep inert defaults for the vast remainder of the Space Engineers API surface

## Default Test API

### What exists today

Today, `Script<TProgram>` exposes this fluent surface:

- `WithIGC(IMyIntergridCommunicationSystem igc)`
- `OnNetwork()`
- `OnNetwork(FakeIgcNetwork network)`
- `WithCustomData(string customData)`
- `WithStorage(string storage)`
- `CreateGrid(string name = null, long? entityId = null, MechanicalConnectionKind connectionKind = Rotor)`
- `WithGrid(string name, long? entityId = null, MechanicalConnectionKind connectionKind = Rotor)`
- `WithGrid(IMyCubeGrid grid, MechanicalConnectionKind connectionKind = Rotor)`
- `ConnectGrids(IMyCubeGrid baseGrid, IMyCubeGrid topGrid, MechanicalConnectionKind connectionKind = Rotor)`
- `ConnectGridsViaConnector(IMyCubeGrid baseGrid, IMyCubeGrid otherGrid, string baseConnectorName = null, string otherConnectorName = null, MyShipConnectorStatus initialStatus = Connected)`
- `ConnectGridsViaMergeBlock(IMyCubeGrid baseGrid, IMyCubeGrid otherGrid, string baseMergeBlockName = null, string otherMergeBlockName = null, MergeState initialState = None)`
- `MergeBlocks(IMyShipMergeBlock mergeBlock)`
- `UnmergeBlocks(IMyShipMergeBlock mergeBlock)`
- `WithBlock(IMyTerminalBlock block, IMyCubeGrid grid = null)`
- `WithBlock<TBlock>(string customName = null, string customData = "", long? entityId = null, IMyCubeGrid grid = null, Action<TBlock> configure = null)`
- `WithBlocks(params IMyTerminalBlock[] blocks)`
- `WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)`
- `WithCommands(params BaseModuleCommand[] commands)`
- `Boot()`
- `Run(UpdateType updateType, string argument = "")`
- `CaptureEcho()`

And after boot:

- `Program`
- `Mother`
- `Bus` which is just a convenience accessor for `Mother.GetModule<CommandBus>()`
- compact command execution counters through `Bus.GetExecutionCount(commandName, outcome)`
- `Config`
- `Clock`
- `IGC`
- `NetworkIGC`
- `GridTerminalSystem`
- `PrimaryGrid`
- `GetBlock(string blockName)` / `GetBlock<TBlock>(string blockName)`
- `ContainsBlock(string blockName)` / `AssertHasBlock(string blockName)`

`TestWorld` also exists today and provides:

- `CreateScript<TProgram>(string name = null)`
- `CreateGrid(string name = null, long? entityId = null)`
- `CreateScript<TProgram>(TestGrid primaryGrid, string name = null)`
- `Network`
- `Merge(IMyShipMergeBlock firstBlock, IMyShipMergeBlock secondBlock)`
- `Unmerge(IMyShipMergeBlock mergeBlock)`
- `DispatchIgc()`
- `Tick(int count = 1)`
- `TickMessages(int count = 2)`
- `Run(UpdateType updateType, string argument = "")`
- `RunIGC()`
- `RunMany(int count, UpdateType updateType, string argument = "")`
- `SentMessages`

`TestGrid` currently provides:

- `Grid`
- `Name`
- `AddBlock<TBlock>(TBlock block)`
- `AddBlock<TBlock>(string customName = null, string customData = "", long? entityId = null, Action<TBlock> configure = null)`
- `GetBlock(string blockName)` / `GetBlock<TBlock>(string blockName)`
- `ContainsBlock(string blockName)` / `ShouldContainBlock(string blockName)`

`MergePair` currently provides:

- `BaseGrid`
- `OtherGrid`
- `BaseMergeBlockName`
- `OtherMergeBlockName`
- `InitialState`
- `BaseBlock`
- `OtherBlock`

### What should come next

The next wave of helpers should grow from the `Script` plus `World` model, not from a detached host abstraction.

Likely additions:

- broader same-construct assertion ergonomics on top of existing world/grid topology helpers
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

If you need a narrower assertion, prefer a purpose-built test module or recorder local to that fixture rather than a shared global spy helper.

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
var osGrid = world.CreateGrid("ShipOS Grid");
var guiGrid = world.CreateGrid("ShipGUI Grid");

world.ConnectGrids(osGrid, guiGrid);

var os = world.CreateScript<MotherOS.Program>(osGrid, "ShipOS").Boot();
var gui = world.CreateScript<MotherGUI.Program>(guiGrid, "ShipGUI").Boot();

Assert.That(world.AreSameConstruct(osGrid, guiGrid), Is.True);

os.Mother.GetModule<IntergridMessageService>()
    .SendConstructCommand(gui.Mother.Id, "view/go status");

world.Tick();
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

var shipA = world.CreateScript<MotherOS.Program>("ShipA").OnNetwork().Boot();
var shipB = world.CreateScript<MotherGUI.Program>("ShipB").OnNetwork().Boot();

shipA.Bus.RunTerminalCommand("@ShipB view/go status");
world.TickMessages();
```

For world-topology tests that need grids before boot, the intended mental model now also includes grid handles:

```csharp
var world = new TestWorld();
var carrierGrid = world.CreateGrid("Carrier");
var cargoGrid = world.CreateGrid("Cargo Pod");

var carrierMerge = carrierGrid.AddBlock<IMyShipMergeBlock>("Carrier Merge");
var cargoMerge = cargoGrid.AddBlock<IMyShipMergeBlock>("Cargo Merge");

var script = world.CreateScript<Program>(carrierGrid, "Carrier").Boot();
world.Merge(carrierMerge, cargoMerge);
```

This is the world-based version of what `FakeIgcNetwork` already does in a narrower form.

`Deliver()` remains useful at the transport layer, but world tests should usually advance via world cycles.

Current direction:

- `world.Tick(count)` advances full world cycles (dispatches IGC, then advances each script clock)
- `world.TickMessages(count)` is the intent-first helper for message-driven world progression (defaults to 2 cycles)
- `world.DispatchIgc()` remains available when tests need transport-only assertions before clocks advance
- `world.Run(UpdateType updateType, string argument = "")` runs one real program cycle for scripts in the world
- `world.RunIGC()` is a convenience helper for `world.Run(UpdateType.IGC)`
- `world.RunMany(int count, UpdateType updateType)` advances multiple `Mother.Run` cycles explicitly

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
- `TextSurfaceFactory` for display-oriented unit and integration tests that need lightweight text-surface fakes

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
- `world.Tick(int count = 1)`
- `world.RunIGC()`
- `world.RunMany(int count, UpdateType updateType)`
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

The harness should support real event flow by default.

If a narrower assertion is needed, prefer a purpose-built local recorder or test module for that fixture, not a shared cross-suite spy helper. This likely belongs on a focused test helper, not directly on the low-level world abstraction.

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

## Review Findings (2026-06-11)

The following findings come from reviewing harness tests under `Tests/Harness/`,
world/network-heavy integration tests (especially `IntergridMessageServiceTests`),
and the current harness helpers in `Utilities/Harness/` and `Utilities/Mocks/`.

### 1. Broadcast listener disable behavior is incomplete

`FakeIgc.DisableBroadcastListener(...)` toggles `FakeBroadcastListener.IsActive`,
but `FakeIgc.EnqueueBroadcast(...)` currently enqueues regardless of active state.

Impact:

- tests can observe messages delivered even after a listener is disabled
- this diverges from expected game-like behavior and can hide routing defects

### 2. `TestWorld` currently enforces one topology-backed primary grid

`TestWorld.EnsureWorldTopology(...)` throws when a second script attempts to bind
using a different primary world grid.

Impact:

- limits multi-script world orchestration in scenarios where scripts should be
  mounted on different pre-created world grids
- conflicts with the long-term world mental model where scripts are first-class
  world entities and world topology is not anchored to one primary script grid

### 3. Network delivery silently drops unresolved targets

`FakeIgcNetwork.Deliver()` skips deliveries when no endpoint is found and does
not expose drop telemetry.

Impact:

- sender-side assertions can pass while recipient-side non-delivery is hard to
  diagnose
- failures become "nothing happened" instead of explicit transport outcomes

### 4. Assertion helpers are useful but still too narrow for common flows

Current helpers (`Script.AssertPrinted`, `Script.AssertCommandExecuted`,
`Script.AssertEventEmitted`, `Script.AssertHasBlock`, `TestGrid.ShouldContainBlock`)
are solid foundations, but world/network tests still repeat low-level assertion
patterns directly against `SentMessages`, command counts, and lookup calls.

Impact:

- duplicated assertions across fixtures
- lower readability and weaker intention-revealing tests
- less consistent failure messages for orchestration scenarios

### 5. World-first guidance is present, but tests still mix styles heavily

The docs describe `TestWorld` as the preferred multi-script path and position
single-script `Script<TProgram>` as shorthand, but integration tests still use
both world-scoped and standalone network/script setup in mixed ways.

Impact:

- harder to establish one obvious orchestration style for new contributors
- increased fixture variance when expressing equivalent scenarios

## Implementation Plan: Orchestration and Assert Library Improvements

This plan prioritizes correctness and ergonomics before breadth.

### ✅ Phase A: Transport correctness and observability (short-term)

1. Fix `FakeBroadcastListener` enforcement.
    - Gate `FakeIgc.EnqueueBroadcast(...)` on `listener.IsActive`.
    - Add tests for disable/enable behavior boundaries.

2. Add explicit network delivery telemetry.
    - Track dropped deliveries (unknown endpoint, disabled listener, invalid tag).
    - Expose read-only dropped-message records for assertions.

3. Add first network assertion helpers.
    - `ShouldHaveUnicast(sourceId, targetId, tag)`
    - `ShouldHaveBroadcast(sourceId, tag)`
    - `ShouldHaveNoTraffic()`
    - `ShouldHaveDroppedMessage(...)`

Exit criteria:

- no silent transport loss in tests without an observable record
- network behavior and assertions are deterministic for common remote flows

### ✅ Phase B: Script and world assertion library (near-term)

1. Extend script-scoped assertions.
    - `ShouldBeWorking()`
    - `ShouldHaveName(expected)`
    - `ShouldHaveExecuted(commandName, outcome, count = 1)`
    - `ShouldHavePrinted(fragment)` / `ShouldNotHavePrinted(fragment)`
    - `ShouldKnowGrid(gridName)` (Almanac convenience)

2. Extend world-scoped assertions.
    - `ShouldHaveScript(name)`
    - `ShouldHaveScriptCount(count)`
    - `ShouldHaveDeliveredIgcMessage(sourceName, targetName, tag)`
    - `ShouldHaveBroadcast(tag, sourceName)`
    - `ShouldHaveNoPendingMessages()`

3. Add grid/block assertion helpers where repetition is high.
    - group membership assertions
    - same-construct assertions
    - merge/connector topology assertions

Exit criteria:

- repetitive direct `Assert.That(...)` transport/orchestration checks in harness
  tests are replaced by domain-level helper assertions
- failure messages describe world/script/network intent rather than raw fields

### ✅ Phase C: World topology orchestration evolution (medium-term)

1. Decouple world topology from a single primary script grid.
    - allow multiple scripts to bind using different existing `TestGrid` roots
    - preserve backward compatibility for existing single-root tests

2. Introduce explicit topology management APIs.
    - clear semantics for mechanical, merge, and connector connectivity

3. Add orchestration helpers for progression.
    - `RunTerminalAll(argument)`
    - `TickUntil(predicate, maxTicks)`
    - message-phase helpers that separate transport dispatch from script clocking

Exit criteria:

- multi-grid, multi-script world tests can be expressed without special-case
  setup constraints
- topology intent is explicit and assertion-friendly

### ✅ Phase D: Test style convergence and migration (ongoing)

1. Migrate high-value integration tests to world-first orchestration where it
    improves clarity.
2. Keep standalone `Script<TProgram>` tests for true single-script concerns.
3. Update `Tests/README.md` examples to prefer world->grid->script composition
    for multi-script scenarios and to demonstrate the new assertion helpers.

Exit criteria:

- new tests default to the intended mental model
- equivalent scenarios share consistent setup and assertion style

## Implementation Progress

> Last updated: 2026-06-11

### Script layer (`Script<TProgram>`)

| Feature | Status | Notes |
|---|---|---|
| `Boot()` | ✅ Done | Boots program, extracts Mother, runs module Boot |
| `WithIGC(igc)` | ✅ Done | Injects custom IGC before boot |
| `OnNetwork()` | ✅ Done | Joins default network context (or creates one when no default exists) |
| `OnNetwork(network)` | ✅ Done | Joins `FakeIgcNetwork` before boot |
| `WithCustomData(string)` | ✅ Done | Sets custom data before boot |
| `WithStorage(string)` | ✅ Done | Sets storage before boot |
| `CreateGrid(...)` / `WithGrid(...)` | ✅ Done | Adds construct-local grids through the script harness |
| `ConnectGrids(...)` / `ConnectGridsViaConnector(...)` / `ConnectGridsViaMergeBlock(...)` | ✅ Done | Supports mechanical, connector, and merge topology helpers |
| `MergeBlocks(...)` / `UnmergeBlocks(...)` | ✅ Done | Drives merge-block state transitions through the script harness |
| `WithBlock(...)` / `WithBlocks(...)` / `WithBlockGroup(...)` | ✅ Done | Registers blocks and groups into the script-local terminal system |
| `WithCommands(params ...)` | ✅ Done | Registers extra concrete commands after boot; useful for harness-specific tests |
| `OnBeforeBoot(mother)` hook | ✅ Done | Override to inject test-only modules |
| `Program`, `Mother`, `Bus`, `Config`, `Clock`, `IGC`, `NetworkIGC`, `GridTerminalSystem`, `PrimaryGrid` | ✅ Done | Post-boot accessors; `Bus` also exposes `GetExecutionCount(...)` for concrete command assertions |
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
| `CreateNetworkEndpoint()` | ✅ Done | Creates `FakeIgc` on the network |
| `RegisterSession(session, name)` | ✅ Done | Cross-populates Almanac on boot |
| `Deliver()` | ✅ Done | Routes pending messages + triggers IGC processing |
| `SentMessages` | ✅ Done | Capture list for lightweight assertions |
| `ClearSentMessages()` | ✅ Done | Empties the capture list |
| `Sessions` | ✅ Done | Exposes all registered `IScript` instances |
| `DispatchIgc()` | ✅ Done | Preferred alias for `Deliver()` |

### TestWorld (multi-script environment)

| Feature | Status | Notes |
|---|---|---|
| `CreateScript<T>(name)` | ✅ Done | Returns `Script<T>` in world context; network binding is explicit via `OnNetwork()`/`OnNetwork(world.Network)` |
| `CreateGrid(name, entityId)` | ✅ Done | Creates a world-owned grid handle before boot |
| `CreateScript<T>(primaryGrid, name)` | ✅ Done | Binds a script to an existing world grid and topology; different scripts can now bind to different world grids |
| `ConnectGrids(baseGrid, topGrid, connectionKind)` | ✅ Done | World-level mechanical construct orchestration across pre-created world grids |
| `Network` | ✅ Done | Exposes the world's shared `FakeIgcNetwork` for explicit script binding |
| `Merge(firstBlock, secondBlock)` / `Unmerge(mergeBlock)` | ✅ Done | Drives world-level merge topology using standalone or paired merge blocks |
| `TestGrid.AddBlock(...)` | ✅ Done | Registers world-owned blocks through a grid handle |
| `AddMergeBlockPair(...)` | ✅ Done | Optional deferred merge-pair descriptor for pre-registered topology |
| `DispatchIgc()` | ✅ Done | Delegates to `FakeIgcNetwork.Deliver()` |
| `Tick(count)` | ✅ Done | Advances full world cycles (IGC dispatch + all script clocks) |
| `TickMessages(count)` | ✅ Done | Message-focused world progression helper (defaults to two cycles) |
| `Run(UpdateType, string)` | ✅ Done | Runs all booted scripts one cycle |
| `RunTerminalAll(argument)` | ✅ Done | Runs one terminal update across all scripts with the same argument |
| `RunIGC()` | ✅ Done | Convenience for `Run(UpdateType.IGC)` |
| `RunMany(count, UpdateType, string)` | ✅ Done | Advances multiple cycles |
| `TickUntil(predicate, maxTicks)` | ✅ Done | Ticks until a condition is met (or fails within max ticks) |
| `SentMessages` | ✅ Done | Exposes world network sent-message capture for assertions |
| Construct helpers beyond same-construct assertions | ⏸ Deferred | Current world/grid assertions and `AreSameConstruct(...)` cover the needed topology intent for now |

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
| `TerminalBlockFactory` | ✅ Done | Lightweight block creation helpers for harness tests |
| `TextSurfaceFactory` | ✅ Done | Lightweight text-surface fakes for `DisplayModule` / `Display` tests |
| `GridTerminalSystemBuilder` | ⬜ Pending | A higher-level builder still does not exist |
| `WithBlock(IMyTerminalBlock)` / `WithBlocks(...)` | ✅ Done | Convenience block injection on the script harness |
| `TestGrid.AddBlock(...)` | ✅ Done | Convenience block injection on the world harness |

### Future / long-term

| Feature | Status | Notes |
|---|---|---|
| World-level shared clock / time advance | ✅ Done | `TestWorld.Tick(n)` advances full world cycles |
| `world.CreateConstruct()` | ⏸ Deferred | Not required at this stage; same-construct coverage is assertion-driven via existing world/grid helpers |
| Script runtime inspection helpers | ⬜ Pending | Instruction count, update frequency per script |
| Focused event recorder helper | ⬜ Pending | Only if fixture-local effects are not sufficient for narrower event assertions |

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

- [x] Add module boot and event coverage for [x] `ConnectorModule`, [x] `MechanicalBlockModule`, and [x] `MergeBlockModule`.
    Reuse the test patterns documented in `Tests/README.md`: verify command registration, event subscription, state-transition behavior, and deferred hook behavior after construct refresh.

- [x] Add focused integration coverage for `BlockCatalogue`.
    Covered state-monitor registration and change detection, tag and block-configuration loading, block-group lookup and reload behavior, multi-grid construct membership, mechanical attach and detach refresh flows, construct refresh events, and custom-data hook execution.

- [ ] Add tests for `ActivityMonitor`.
    Cover block registration, terminal-condition satisfaction, one-time callback execution, and automatic unregister after completion.

- [x] Add targeted coverage for `DisplayModule`, `Display`, and `SpriteFactory`.
    Added focused coverage for `DisplayModule` surface registration, source filtering, and reload behavior; `Display` viewport math, scaling, text-line formatting, and the current public drawing methods; and `SpriteFactory` sprite construction. Parsing of display configuration remains covered separately.

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
3. Merge, Mechanical, and Connector
4. ActivityMonitor
5. Remaining pure utilities and migration helpers
6. PID if that utility becomes active again

### Notes for implementation

- Prefer `Unit/` for pure helpers and deterministic parsing behavior.
- Prefer `Integration/` for module boot, event, command, and cross-module behavior inside one booted `Script`.
- Prefer `WorldTestBase` and `TestWorld` for remote IGC scenarios that require more than one script.
- Reuse the examples in `Tests/README.md` as the canonical style guide for new tests.

