# MotherCore Test Suite

This suite provides the shared programmable-block harness for Mother projects.

Primary goals:

- explicit setup in each test (fresh boot per test, no hidden fixture state)
- realistic script/world behavior through harness fakes
- compatibility with extension-script baseline (`netframework48`, C# 6)

## Compatibility contract

When consuming this harness in extension-script projects (MotherOS, MotherGUI):

- `TargetFramework` = `netframework48`
- `LangVersion` = `6`

## Current setup conventions

Use explicit per-test boot via factory helpers.

CommandFactory/CommandBuilder were removed from the harness. For command-path
verification, run terminal input through a booted script and assert with
script-level helpers.

Single script tests:

```csharp
[Test]
public void Rename_Command_Executes_And_Updates_Name()
{
    var script = ScriptFactory().WithMother().Boot();

    script.RunTerminal("rename Frigate");
    script.RunToIdle();

    script.ShouldHaveExecuted("rename");
    script.ShouldHaveName("Frigate");
}
```

Command-path assertions (preferred over direct Bus count checks):

```csharp
script.RunTerminal("purge storage");
script.ShouldHaveExecuted("purge");
script.ShouldHavePrinted("Run command with --force to purge");
```

Module tests (direct module API):

```csharp
[Test]
public void OpenDoor_Opens_Target_Door()
{
    var script = ScriptFactory<Program>().WithMother().Boot();
    var module = script.Mother.GetModule<DoorModule>();

    module.OpenDoor(door);

    Assert.That(door.Status, Is.EqualTo(DoorStatus.Open));
}
```

    Merge-block topology setup:

    ```csharp
    // Default: starts merged (single construct)
    var merged = script.ConnectGridsViaMergeBlock(script.PrimaryGrid, cargoGrid);

    // Explicit split start: starts as two constructs, then merge in-test
    var mergePair = script.AddUnmergedMergeBlockPair(script.PrimaryGrid, cargoGrid);
    script.Mother.GetModule<MergeBlockModule>().LockMergeBlock(mergePair);
    ```

World tests (multi-script):

```csharp
[Test]
public void Remote_Command_Delivers_To_Target()
{
    var world = WorldFactory().Boot();
    var sender = world.CreateScript("Sender").WithMother().OnNetwork().Boot();
    var receiver = world.CreateScript("Receiver").WithMother().OnNetwork().Boot();

    sender.RunTerminal("@Receiver help");
    world.DeliverMessages();

    world.ShouldHaveDeliveredIgcMessage(sender, receiver, "*");
    receiver.ShouldHaveExecuted("help");
}
```

    World merge setup options:

    ```csharp
    // Explicit blocks already available
    world.MergeBlocks(firstMergeBlock, secondMergeBlock);

    // Grid-first setup: creates merge blocks and locks them immediately
    world.MergeGrids(gridA, gridB);

    // Explicitly split a merged pair
    world.UnmergeBlocks(firstMergeBlock, secondMergeBlock);
    ```

## Important behavior notes

- `World.CreateScript()` without a name now generates a unique random name to prevent collisions.
- For message-routing tests, still prefer explicit names (`Sender`, `ReceiverA`, `ReceiverB`) to keep intent obvious.
- Clock coroutines are removed immediately when they complete; avoid brittle assertions that depend on stale coroutine counts.
- `script.ConnectGridsViaMergeBlock(...)` now defaults to `MergeState.Locked` so merge-pair setups start as a unified construct.
- Use `script.AddUnmergedMergeBlockPair(...)` when a test needs two separate grids first and performs the merge transition in the test body.

## Assertion guidance from the refactor

- Prefer behavior assertions over brittle totals.
- For command bus and routing checks, assert presence and outcomes rather than global absolute counts.
- Use world/script helper assertions (`ShouldHaveDeliveredIgcMessage`, `ShouldHaveExecuted`, `ShouldHaveNoPendingMessages`) before inspecting low-level transport internals.
- For transition-specific event assertions, call `script.ClearEventEmissions()` before the transition under test.

Event-transition assertion example:

```csharp
SetDoorStatus(door, DoorStatus.Open);
script.RunToIdle();

script.ClearEventEmissions();

SetDoorStatus(door, DoorStatus.Closing);
script.RunToIdle();

script.AssertEventEmitted<DoorClosingEvent>();
script.AssertEventEmitted<DoorOpenedEvent>(0);
```

## Layer map

- `Tests/Unit`: pure value behavior, no script boot.
- `Tests/Module`: booted script, direct module calls.
- `Tests/Command`: reserved for commands with meaningful standalone logic.
    Module-owned command behavior should generally be covered in `Tests/Module`
    via `RunTerminal(...)` plus behavior assertions.
- `Tests/Script`: single-script lifecycle/wiring behavior.
- `Tests/World`: multi-script topology/network behavior.

## Running tests

From repo root:

```powershell
dotnet test .\MotherCore\tests\MotherCore.Tests\MotherCore.Tests.csproj
```

Focused slices:

```powershell
dotnet test .\MotherCore\tests\MotherCore.Tests\MotherCore.Tests.csproj --filter "FullyQualifiedName~WorldTests"
dotnet test .\MotherCore\tests\MotherCore.Tests\MotherCore.Tests.csproj --filter "FullyQualifiedName~IntergridMessageServiceTests"
```
