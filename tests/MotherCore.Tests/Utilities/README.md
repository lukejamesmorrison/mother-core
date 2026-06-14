# Test Utilities

This folder is organized by role so the core test harness is easier to scan.

- `BaseClasses/`: reusable NUnit setup bases for script and world integration tests.
- `Factories/`: construction entry points for bootstrapped test programs, concrete harness blocks, fake runtime objects, lightweight text-surface fakes, and complete test artifacts such as `Program.Me.CustomData`.
- `Mocks/`: explicit fake implementations and spies used to simulate runtime dependencies in tests.
- `Harness/`: the executable test runtime surface (`Script`, `World`, clock, echo, and shared interfaces).

a## Recommended setup style

Prefer explicit per-test setup over shared implicit fixture setup:

```csharp
var script = ScriptFactory().WithMother().Boot();
var module = script.Mother.GetModule<SomeModule>();
```

For multi-script behavior, use world-first setup:

```csharp
var world = WorldFactory().Boot();
var scriptA = world.CreateScript("A").WithMother().OnNetwork().Boot();
var scriptB = world.CreateScript("B").WithMother().OnNetwork().Boot();
```

Unnamed `World.CreateScript()` calls are now auto-generated with unique names, but explicit names are still preferred in routing tests for readability.

Common utility examples:

- `FakeModule`: minimal module fake aligned with `BaseModule` defaults.
- `FakeModuleCommand`: reusable command fake for command-bus wiring tests and `BaseModuleCommand` helper behavior.

`World` is the preferred multi-script orchestration API. In world-based tests,
use `world.DeliverMessages()` for end-to-end remote-command progression and
`world.Tick(...)` for full world cycles. `Tick` advances full world cycles
(dispatch IGC + advance all script clocks), and `world.DispatchIgc()` remains
available when you intentionally need a transport-only phase.

The utilities root stays in `MotherCore.Tests.Utilities`; focused sub-areas such as `Factories` and `Mocks` use child namespaces when that makes the role clearer.

Timing model used by the harness follows the game loop target of 60 ticks per second:

- `Update1` corresponds to 1/60 second.
- `Update10` corresponds to 10/60 second.
- `Update100` corresponds to 100/60 second.

Execution and clock helpers are intentionally split:

- `Run(...)` methods execute script logic (`Program.Main`) using either the default runtime update type or an explicit `UpdateType` argument.
- `Tick()` advances clock time only and does not execute `Program.Main`.

Assertion guidance:

- Prefer behavior assertions (command executed, expected event emitted, expected message delivered).
- Avoid brittle assertions tied to global command-registration totals or transient coroutine counts.
- Use harness helpers first (`ShouldHaveExecuted`, `ShouldHaveDeliveredIgcMessage`, `ShouldHaveNoPendingMessages`) before low-level list inspection.

The current harness direction is explicit over generic: common game-facing interfaces
should be represented by concrete fake types, and unsupported families should fail
fast so new coverage gets added deliberately.