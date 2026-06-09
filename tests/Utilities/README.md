# Test Utilities

This folder is organized by role so the core test harness is easier to scan.

- `BaseClasses/`: reusable NUnit setup bases for script and world integration tests.
- `Factories/`: construction entry points for bootstrapped test programs, fake runtime objects, and complete test artifacts such as `Program.Me.CustomData`.
- `Mocks/`: fake implementations and spies used to simulate runtime dependencies in tests.
- `Harness/`: the executable test runtime surface (`Script`, `TestWorld`, clock, echo, and shared interfaces).

The utilities root stays in `MotherCore.Tests.Utilities`; focused sub-areas such as `Factories` and `Mocks` use child namespaces when that makes the role clearer.