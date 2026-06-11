# Test Utilities

This folder is organized by role so the core test harness is easier to scan.

- `BaseClasses/`: reusable NUnit setup bases for script and world integration tests.
- `Factories/`: construction entry points for bootstrapped test programs, concrete harness blocks, fake runtime objects, lightweight text-surface fakes, and complete test artifacts such as `Program.Me.CustomData`.
- `Mocks/`: explicit fake implementations and spies used to simulate runtime dependencies in tests.
- `Harness/`: the executable test runtime surface (`Script`, `TestWorld`, clock, echo, and shared interfaces).

The utilities root stays in `MotherCore.Tests.Utilities`; focused sub-areas such as `Factories` and `Mocks` use child namespaces when that makes the role clearer.

The current harness direction is explicit over generic: common game-facing interfaces
should be represented by concrete fake types, and unsupported families should fail
fast so new coverage gets added deliberately.