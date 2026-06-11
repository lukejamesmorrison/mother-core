# MotherCore Tests

Test files are grouped into three buckets:

- `Unit/`: narrow tests for isolated core behavior.
- `Integration/`: booted-script tests that exercise real module interaction.
- `Harness/`: tests for the reusable test runtime itself (`Script`, `TestWorld`, `FakeIgcNetwork`, and related helpers).

Examples for the three most common test scenarios, using real modules from
**MotherOS** and **MotherGUI**. All examples use the utilities in
`Utilities/` — no manual wiring of IGC, runtime, or grid terminal system
is required.

> **Test project setup**  
> MotherOS and MotherGUI tests live in their own test projects that include
> the script source via `<Import Project="..\src\MotherCore.projitems" />` (for
> MotherCore) and the script's own `.projitems` file. The `Script<Program>`
> generic picks up whichever `Program` type is in scope for that project.

---

## 1. Testing a CustomData alias

Players configure MotherOS by writing command aliases into the programmable
block's `CustomData` (e.g. `openAirlock = door/open AirlockDoor`). A useful
first test verifies that an alias resolves and is forwarded to the right
command. Because the test environment has no real blocks, `door/open` reports
`BlockNotFound` — but the block name appearing in the output confirms the
alias was parsed, resolved, and dispatched correctly.

```csharp
// MotherOS.Tests project

[Test]
public void OpenAirlock_Alias_Resolves_And_Dispatches_To_door_open()
{
    var session = new Script<Program>()
        .WithCustomData(new CustomDataComposer()
            .WithCommand("openAirlock", "door/open AirlockDoor")
            .Build())
        .Boot();

    var capture = new PrintCapture(session);

    session.Bus.RunTerminalCommand("openAirlock");
    session.Clock.RunToIdle();

    // "AirlockDoor" appears in the BlockNotFound message, confirming
    // the alias resolved and door/open was the command that ran.
    Assert.That(capture.Lines.Any(l => l.Contains("AirlockDoor")), Is.True);
}
```

---

## 2. Testing a module

Retrieve a module from the booted `Mother` instance via `GetModule<T>()`.
All extension modules registered in `Program()` are booted automatically.

### 2.1 Boot verification

Boot tests confirm the module is wired up correctly before any command runs.
At minimum, check that commands are registered and any EventBus subscriptions
are in place.

```csharp
// MotherOS.Tests project

[Test]
public void DoorModule_Registers_Commands_On_Boot()
{
    var session = new Script<Program>().Boot();

    var names = session.Bus.ModuleCommands.Select(c => c.Name).ToList();

    Assert.That(names, Contains.Item("door/open"));
    Assert.That(names, Contains.Item("door/close"));
    Assert.That(names, Contains.Item("door/toggle"));
}
```

For modules that subscribe to events during `Boot()`, use
`EventBus.IsSubscribed<T>()` to verify the subscription was registered:

```csharp
// MotherCore.Tests project

[Test]
public void MergeBlockModule_Is_Subscribed_To_ConstructRefreshedEvent_After_Boot()
{
    var session = new Script().Boot();
    var mergeModule = session.Mother.GetModule<MergeBlockModule>();
    var eventBus   = session.Mother.GetModule<EventBus>();

    Assert.That(eventBus.IsSubscribed<ConstructRefreshedEvent>(mergeModule), Is.True);
}
```

### 2.2 Calling a command defined within the module

Run the command via the bus and assert on its output. Because the test
environment has no real grid blocks, a command that targets a named block
will report `BlockNotFound` — this still confirms the command was routed,
parsed, and executed:

```csharp
[Test]
public void door_open_Reports_BlockNotFound_For_An_Unknown_Block()
{
    var session = new Script<Program>().Boot();
    var capture = new PrintCapture(session);

    session.Bus.RunTerminalCommand("door/open HangarDoor");
    session.Clock.RunToIdle();

    // The block name appears in the not-found message, confirming the
    // command ran and looked up "HangarDoor" in the catalogue.
    Assert.That(capture.Lines.Any(l => l.Contains("HangarDoor")), Is.True);
}
```

Commands that don't require blocks can be tested more directly. `door/open`
with no arguments returns a `NoArgumentsProvided` message without touching
the grid terminal system at all:

```csharp
[Test]
public void door_open_With_No_Arguments_Returns_NoArgumentsProvided()
{
    var session = new Script<Program>().Boot();
    var capture = new PrintCapture(session);

    session.Bus.RunTerminalCommand("door/open");
    session.Clock.RunToIdle();

    Assert.That(capture.Lines.Any(l => l.Contains("No arguments")), Is.True);
}
```

#### Why `RunToIdle()` is required

`RunTerminalCommand` does not execute commands synchronously — it adds a
coroutine to the Clock queue. Without at least one tick the command has been
scheduled but not yet run. `RunToIdle()` ticks until the queue is empty and
is the right choice for most tests.

Use `Tick(n)` when you need to assert state at a specific step within a
multi-command sequence. Each command in a semicolon-separated routine occupies
one tick, so you can stop at any boundary:

```csharp
// MotherCore.Tests project

[Test]
public void Multi_Step_Routine_Executes_Commands_One_Per_Tick()
{
    var lights = new MyCountingCommand("light/color");
    var blink  = new MyCountingCommand("light/blink");

    var session = new Script()
        .WithCustomData(new CustomDataComposer()
            .WithCommand("dockReady", "light/color DockLight 0,255,0; light/blink DockLight fast")
            .Build())
        .WithCommands(lights, blink)
        .Boot();

    session.Bus.RunTerminalCommand("dockReady");
    // Coroutine is queued but nothing has run yet.
    Assert.That(lights.ExecutionCount, Is.EqualTo(0));

    session.Clock.Tick();   // light/color runs
    Assert.That(lights.ExecutionCount, Is.EqualTo(1));
    Assert.That(blink.ExecutionCount,  Is.EqualTo(0));  // light/blink hasn't run yet

    session.Clock.Tick();   // light/blink runs
    Assert.That(blink.ExecutionCount, Is.EqualTo(1));
}
```

### 2.3 Calling a module method

Module methods are the API that other modules call internally. Retrieve the
module and call the method directly with a harness block fake. No command
dispatch or clock tick is needed:

```csharp
[Test]
public void BatteryModule_ChargeBattery_Sets_The_Block_To_Recharge_Mode()
{
    var session = new Script<Program>().Boot();
    var batteries = session.Mother.GetModule<BatteryModule>();

    var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Reserve Battery");
    batteries.ChargeBattery(battery);

    Assert.That(battery.ChargeMode, Is.EqualTo(ChargeMode.Recharge));
}
```

The same pattern applies to any method that takes a block and modifies it —
`DoorModule.OpenDoor`, `BatteryModule.ChargeBattery`, `CockpitModule.SetHandbrakes`,
etc. Reach for `TerminalBlockFactory.Create<TBlock>(...)` or a world/grid helper
first; for common families like plain terminal blocks, doors, batteries, reactors,
connectors, and merge blocks that now returns concrete harness-backed blocks.

If you hit an unsupported interface, the factory now throws instead of silently
fabricating a generic double. Add or extend a concrete harness fake for that
family rather than falling back to ad hoc mocking.

### 2.4 Testing display helpers

Display-focused tests usually need a fake `IMyTextSurface` or `IMyTextPanel`
plus a way to observe `WriteText(...)`, `ContentType`, and surface sizing.
Use `TextSurfaceFactory` for that instead of hand-building FakeItEasy setup in
each test:

```csharp
[Test]
public void Display_Uses_Viewport_Width_To_Calculate_Font_Size()
{
    var surface = TextSurfaceFactory.Create(
        surfaceSize: new Vector2(400f, 200f),
        textureSize: new Vector2(400f, 200f));

    var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Bridge LCD");
    var display = new Display(surface.Surface, block, new MyIni());

    Assert.That(display.IsWidescreen, Is.True);
    Assert.That(display.FontSize, Is.GreaterThan(0f));
}
```

For `DisplayModule` integration tests, `TextSurfaceFactory.CreatePanel(...)`
is the cheapest way to register a configurable LCD panel with `[surfaces]`
custom data and then assert on captured writes after boot.

### 2.5 Handling an event

`HandleEvent` is called by the EventBus on every module subscribed to a given
event. There are two complementary ways to test it.

**Call `HandleEvent` directly** when you want to exercise the handler logic
in isolation, without wiring up the full EventBus routing:

```csharp
// MergeBlockModule.HandleEvent(ConstructRefreshedEvent) re-registers merge
// block monitoring and flushes any deferred catalogue hooks. Calling it
// directly verifies the code path runs cleanly with no pending work queued.
[Test]
public void MergeBlockModule_HandleEvent_Does_Not_Throw_On_ConstructRefreshedEvent()
{
    var session    = new Script().Boot();
    var mergeModule = session.Mother.GetModule<MergeBlockModule>();

    Assert.DoesNotThrow(() =>
        mergeModule.HandleEvent(new ConstructRefreshedEvent(), null));
}
```

**Emit via EventBus** when you want to verify that the bus actually routes
the event to the correct subscribers. Prefer a small fixture-local recorder
module over a framework spy when you need to observe the call:

```csharp
private sealed class RecordingModule : FakeModule
{
    public int ReceivedCount { get; private set; }
    public object LastPayload { get; private set; }

    public RecordingModule(Mother mother) : base(mother) {}

    public override void HandleEvent<TEvent>(TEvent e, object eventData)
    {
        ReceivedCount++;
        LastPayload = eventData;
    }
}

[Test]
public void EventBus_Routes_DoorOpenedEvent_To_All_Subscribers()
{
    var session  = new Script<Program>().Boot();
    var eventBus = session.Mother.GetModule<EventBus>();

    var recorder = new RecordingModule(session.Mother);
    var fakeDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

    eventBus.Subscribe<DoorOpenedEvent>(recorder);
    eventBus.Emit<DoorOpenedEvent>(fakeDoor);

    Assert.That(recorder.ReceivedCount, Is.EqualTo(1));
    Assert.That(recorder.LastPayload, Is.SameAs(fakeDoor));
}
```

---

## 3. MotherOS + MotherGUI in a shared world

In a typical ship build, MotherOS runs on one programmable block and
MotherGUI on another. For integration tests, prefer a shared `TestWorld`
so setup, progression, and assertions stay at one orchestration layer.

This example boots both scripts in one `TestWorld` and uses world-level
assertions for transport outcomes:

```csharp
// Multi-script test project that includes both MotherOS and MotherGUI source.
// Use type aliases to disambiguate the two Program classes:
//   using MotherOSProgram = IngameScript.Program;   (from MotherOS source)
//   using MotherGUIProgram = IngameScript.Program;  (from MotherGUI source)

[Test]
public void MotherOS_Can_Send_view_go_To_MotherGUI_Over_The_Network()
{
    var world = new TestWorld();

    // Boot MotherOS as the ship controller
    var ship = world.CreateScript<MotherOSProgram>("Ship")
        .OnNetwork()
        .Boot();

    // Boot MotherGUI as the display controller
    var gui = world.CreateScript<MotherGUIProgram>("GUI")
        .OnNetwork()
        .Boot();

    // MotherOS sends a view navigation command to MotherGUI
    ship.Bus.RunTerminalCommand("@GUI view/go \"Bridge LCD\" \"RotorView\"");
    world.TickMessages();

    world.ShouldHaveDeliveredIgcMessage("Ship", "GUI", "*");
    world.ShouldHaveNoPendingMessages();

    // Assert on whatever MotherGUI state the view navigation produces.
}
```

### Key points

- **Almanac is wired automatically.** Every session booted on the same
    world network is cross-registered in every other session's Almanac under its
    grid name, so `@GUI` resolves without any extra setup.

- **Network scripts are communication-ready by default.** Joining with
    `.OnNetwork()` ensures a default public channel entry (`[channels]`, `*=`)
    when no explicit channel config is provided.

- **`TickMessages()` is intent-first.** For remote-command flows, prefer
    `world.TickMessages()` so delivery and script execution advance together
    with readable test intent.

- **Use `DispatchIgc()` only for transport checkpoints.** Keep it for tests
    that intentionally assert pre-execution transport state before script clocks
    advance.

- **`TestWorld` network binding is explicit.** `world.CreateScript(...)` is local
    by default. For inter-script IGC flows, opt in with `.OnNetwork()`.

- **`TickMessages()` is the message-flow shorthand.** For world-level message
    scenarios, prefer `world.TickMessages(...)` over raw `world.Tick(...)` so the
    test intent stays obvious.

- **Use world assertion helpers first.** Prefer `world.ShouldHaveDeliveredIgcMessage(...)`,
    `world.ShouldHaveBroadcast(...)`, and `world.ShouldHaveNoPendingMessages()`
    before falling back to raw `SentMessages` inspection.

- **`SentMessages` remains available for lightweight transport checks.** If
    you only need to verify a message was emitted without asserting full flow:

  ```csharp
  ship.Bus.RunTerminalCommand("@GUI view/go \"Bridge LCD\" \"RotorView\"");
  ship.Clock.RunToIdle();

    Assert.That(world.SentMessages.Any(m => m.TargetId == gui.IGC.Me), Is.True);
  ```

- **More than two grids** work the same way — add more sessions with
    `.OnNetwork()` and they are all cross-registered with each other.

### When to use FakeIgcNetwork directly

Prefer direct `FakeIgcNetwork` setup only when testing transport behavior
itself (drop reasons, listener activation edge-cases, low-level tag routing)
instead of world-level integration outcomes.

### World-based remote flow (preferred in new integration tests)

When your test is already using `TestWorld`, keep progression at the world level:

```csharp
[Test]
public void Remote_Command_Executes_On_Receiver_Through_World_Ticks()
{
        var world = new TestWorld();

        var sender = world.CreateScript("Sender").OnNetwork(world.Network).Boot();
        var receiver = world.CreateScript("Receiver").OnNetwork(world.Network).Boot();

        sender.RunTerminal("@Receiver help");

        // Two world cycles for this flow:
        // 1) sender coroutine emits remote IGC message
        // 2) delivered message is processed and receiver coroutine runs
        world.TickMessages();

        Assert.That(receiver.Bus.GetExecutionCount("help"), Is.EqualTo(1));
}
```

`TickMessages()` here is not a global rule. Use the smallest count that matches the
stages in the behavior under test. For a terminal-driven remote command, two
cycles are commonly needed (emit, then execute). For other scenarios, `Tick(1)`
or more than two may be correct depending on coroutine depth and message hops.
