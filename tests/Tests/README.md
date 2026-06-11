# MotherCore Test Suite

This test suite is a reusable harness for scripts that inherit from `MyGridProgram`
(for example MotherCore, MotherOS, and MotherGUI).

Primary goals:

- keep setup friction low (`new Script<Program>().Boot()` for single-script tests)
- keep world behavior realistic (`TestWorld` for multi-script integration)
- keep compatibility with extension-script baselines (`netframework48`, C# 6)

## Compatibility contract

When consuming this harness in other script projects (like MotherOS/MotherGUI),
keep these contracts aligned:

- `TargetFramework` = `netframework48`
- `LangVersion` = `6`

The MotherCore harness and tests are intentionally maintained against that baseline.

## Quick start for extension-script projects

Create a test project that imports MotherCore shared source plus your script source. You can do this using Mother CLI, by important Mother Core as a shared project dependency:

**Script.proj**
```xml
<Import Project="..\MotherCore\src\MotherCore.projitems" Label="Shared" />
```

## Harness model in 30 seconds

- `Script<TProgram>`: one booted programmable block instance.
- `TestWorld`: shared environment for multiple scripts.
- `TestGrid`: world-owned grid handle for topology and block registration.

Use `Script<TProgram>` for single-script tests and `TestWorld` for multi-script tests.

## Most relevant scenarios

### 1. Test one module/command in a single `Script`

```csharp
[Test]
public void Rename_Command_Executes_Once()
{
    var script = new Script<Program>().Boot();

    script.RunTerminal("rename Frigate").RunToIdle();

    script.ShouldHaveExecuted("rename", count: 1);
    script.ShouldHaveName("Frigate");
}
```

### 2. Test multiple modules inside one script

This pattern validates interactions across real modules booted by your real `Program`.

```csharp
[Test]
public void Boot_Wires_CommandBus_And_EventBus_For_Merge_Module()
{
  var script = new Script<Program>().Boot();

  var mergeModule = script.Mother.GetModule<MergeBlockModule>();
  var eventBus = script.Mother.GetModule<EventBus>();

  Assert.That(eventBus.IsSubscribed<ConstructRefreshedEvent>(mergeModule), Is.True);

  script.RunTerminal("help").RunToIdle();
  script.ShouldHaveExecuted("help", count: 1);
}
```

### 3. Test multiple scripts on the same construct (local/construct cooperation)

Two common setup paths are useful here.

#### Variant A: both scripts on the exact same grid

```csharp
[Test]
public void SameConstruct_Scripts_Can_Share_One_Grid()
{
    var world = new TestWorld();
    var sharedGrid = world.CreateGrid("Carrier");

    var shipA = world.CreateScript<Program>(sharedGrid, "ShipA").OnNetwork().Boot();
    var shipB = world.CreateScript<Program>(sharedGrid, "ShipB").OnNetwork().Boot();

    Assert.That(shipA.PrimaryGrid.EntityId, Is.EqualTo(shipB.PrimaryGrid.EntityId));
    Assert.That(shipA.PrimaryGrid.IsSameConstructAs(shipB.PrimaryGrid), Is.True);
    shipA.ShouldKnowGrid("ShipB");
    shipB.ShouldKnowGrid("ShipA");
}
```

#### Variant B: separate grids connected by the world

Use this when each script should start on its own grid but still cooperate as one construct.

```csharp
[Test]
public void SameConstruct_Scripts_See_Shared_Topology()
{
    var world = new TestWorld();
    var carrier = world.CreateGrid("Carrier");
    var cargo = world.CreateGrid("Cargo Pod");

    world.ConnectGrids(carrier, cargo);

    var shipA = world.CreateScript<Program>(carrier, "ShipA").OnNetwork().Boot();
    var shipB = world.CreateScript<Program>(cargo, "ShipB").OnNetwork().Boot();

    world.ShouldBeSameConstruct(shipA, shipB);
    shipA.ShouldKnowGrid("ShipB");
    shipB.ShouldKnowGrid("ShipA");
}
```

### 4. Test multiple scripts on different constructs (remote cooperation)

Keep scripts on separate grids, opt into network, and progress with world ticks.

```csharp
[Test]
public void Remote_Command_Delivers_Between_Separate_Constructs()
{
    var world = new TestWorld();
    var senderGrid = world.CreateGrid("SenderGrid");
    var receiverGrid = world.CreateGrid("ReceiverGrid");

    var sender = world.CreateScript<Program>(senderGrid, "Sender").OnNetwork().Boot();
    var receiver = world.CreateScript<Program>(receiverGrid, "Receiver").OnNetwork().Boot();

    sender.RunTerminal("@Receiver help");

    // force message distribution for world
    world.TickMessages();

    world.ShouldHaveDeliveredIgcMessage("Sender", "Receiver", "*");
    receiver.ShouldHaveExecuted("help");
    world.ShouldHaveNoPendingMessages();
}
```

## Practical guidance

- Prefer world-level progression in multi-script tests: `world.TickMessages(...)` and `world.Tick(...)`.
- Prefer script-level progression in single-script tests: `script.RunToIdle()` or `script.Tick()`.
- Use harness concrete fakes (`TerminalBlockFactory`, `TextSurfaceFactory`, typed fake blocks) instead of ad hoc mocking.
- Use `world.ShouldHaveDeliveredIgcMessage(...)` and related helpers before inspecting low-level transport lists.

## Folder map

- `Unit/`: focused tests for isolated behavior.
- `Integration/`: booted-script behavior and module collaboration.
- `Harness/`: tests for the harness itself (`Script`, `TestWorld`, topology helpers).

## Running the suite

From `MotherCore/tests`:

```powershell
dotnet test .\MotherCore.Tests.csproj
```

For a focused slice:

```powershell
dotnet test --filter "FullyQualifiedName~ScriptTests|FullyQualifiedName~TestWorldTests"
```
