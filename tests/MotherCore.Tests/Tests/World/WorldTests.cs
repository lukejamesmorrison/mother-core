using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies <see cref="World"/>: script creation, IGC dispatch,
    /// single-cycle and multi-cycle execution, and the convenience helpers
    /// <see cref="World.RunIGC"/> and <see cref="World.RunMany"/>.
    /// </summary>
    [Category(TestCategories.LayerWorld)]
    public class WorldTests : TestBase
    {
        // =====================================================================
        // CreateScript
        // =====================================================================

        [Test]
        public void CreateScript_Returns_Booted_Script()
        {
            var world = WorldFactory().Boot();

            var script = world.CreateScript().Boot();

            script.ShouldBeWorking();
        }

        [Test]
        public void CreateScript_With_Grid_Name_Sets_Mother_Name_After_Boot()
        {
            var world = WorldFactory().Boot();

            var script = world.CreateScript("Flagship").Boot();

            script.ShouldHaveName("Flagship");
            world.ShouldHaveScript("Flagship");
        }

        [Test]
        public void CreateScript_On_Existing_World_Grid_Binds_The_Program_To_That_Grid()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var script = world.CreateScript(carrierGrid, "Carrier").Boot();

            Assert.That(script.PrimaryGrid, Is.SameAs(carrierGrid.Grid));
            Assert.That(script.Program.Me.CubeGrid, Is.SameAs(carrierGrid.Grid));
        }

        [Test]
        public void CreateScript_On_Different_World_Grids_Binds_Each_Script_To_Its_Own_Primary_Grid()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var escortGrid = world.CreateGrid("Escort");

            var carrier = world.CreateScript(carrierGrid, "Carrier").Boot();
            var escort = world.CreateScript(escortGrid, "Escort").Boot();

            Assert.That(carrier.PrimaryGrid, Is.SameAs(carrierGrid.Grid));
            Assert.That(carrier.Program.Me.CubeGrid, Is.SameAs(carrierGrid.Grid));

            Assert.That(escort.PrimaryGrid, Is.SameAs(escortGrid.Grid));
            Assert.That(escort.Program.Me.CubeGrid, Is.SameAs(escortGrid.Grid));
        }

        [Test]
        public void ConnectGrids_Binds_World_Grids_To_The_Same_Construct()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            world.ConnectGrids(carrierGrid, cargoGrid);

            Assert.That(world.AreSameConstruct(carrierGrid, cargoGrid), Is.True);
        }

        [Test]
        public void ShouldBeSameConstruct_Passes_For_Scripts_On_Connected_World_Grids()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            world.ConnectGrids(carrierGrid, cargoGrid);

            var shipA = world.CreateScript(carrierGrid, "ShipA").Boot();
            var shipB = world.CreateScript(cargoGrid, "ShipB").Boot();

            Assert.DoesNotThrow(() => world.ShouldBeSameConstruct(shipA, shipB));
        }

        [Test]
        public void ShouldBeSameConstruct_Fails_For_Scripts_On_Separate_Constructs()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            var shipA = world.CreateScript(carrierGrid, "ShipA").Boot();
            var shipB = world.CreateScript(cargoGrid, "ShipB").Boot();

            Assert.That(() => world.ShouldBeSameConstruct(shipA, shipB), Throws.Exception);
        }

        [Test]
        public void TestGrid_Can_Create_And_Register_A_Block_By_Type_And_Name()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");

            var door = carrierGrid.AddBlock<IMyDoor>("Hangar Door");

            Assert.That(door, Is.Not.Null);
            Assert.That(door.CustomName, Is.EqualTo("Hangar Door"));
            carrierGrid.ShouldContainBlock("Hangar Door");
            Assert.That(carrierGrid.GetBlock<IMyDoor>("Hangar Door"), Is.SameAs(door));
        }

        [Test]
        public void TestGrid_AddBlock_Configure_Can_Set_Interface_Specific_State()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");

            var battery = carrierGrid.AddBlock<IMyBatteryBlock>(
                "Reserve Battery",
                configure: block => block.Enabled = false);

            Assert.That(battery.Enabled, Is.False);
            carrierGrid.ShouldContainBlock("Reserve Battery");
        }

        [Test]
        public void Merge_Rewrites_World_Blocks_When_Standalone_Merge_Blocks_Are_Merged()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            var carrierMerge = carrierGrid.AddBlock<IMyShipMergeBlock>("Carrier Merge");

            var cargoMerge = cargoGrid.AddBlock<IMyShipMergeBlock>("Cargo Merge");

            // create script on one of the grids
            world.CreateScript(carrierGrid, "Carrier").Boot();

            world.Merge(carrierMerge, cargoMerge);

            Assert.That(cargoMerge.IsSameConstructAs(carrierMerge), Is.True);
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(carrierMerge.CubeGrid.EntityId));
        }

        [Test]
        public void Scripts_Created_Via_World_Cross_Register_In_Each_Others_Almanac()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();
            var shipB = world.CreateScript("ShipB").OnNetwork().Boot();

            world.ShouldHaveScriptCount(2);

            var almanacA = shipA.Mother.GetModule<Almanac>();
            var almanacB = shipB.Mother.GetModule<Almanac>();

            shipA.ShouldKnowGrid("ShipB");
            shipB.ShouldKnowGrid("ShipA");
        }

        [Test]
        public void Scripts_Created_Via_World_Do_Not_Cross_Register_When_Not_On_Network()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").Boot();
            var shipB = world.CreateScript("ShipB").Boot();

            var almanacA = shipA.Mother.GetModule<Almanac>();

            Assert.That(almanacA.GetRecord("ShipB"), Is.Null);
        }

        // =====================================================================
        // DispatchIgc
        // =====================================================================

        [Test]
        public void DispatchIgc_Delivers_Queued_Messages_To_Recipient()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();
            var shipB = world.CreateScript("ShipB").OnNetwork().Boot();

            shipA.RunTerminal("@ShipB help").RunToIdle();

            shipB.ShouldHaveExecuted("help", count: 0);

            world.DispatchIgc();
            shipB.RunToIdle();

            world.ShouldHaveDeliveredIgcMessage("ShipA", "ShipB", "*");
            shipB.ShouldHaveExecuted("help");
        }

        [Test]
        public void DispatchIgc_Returns_World_For_Chaining()
        {
            var world = WorldFactory().Boot();

            Assert.That(world.DispatchIgc(), Is.SameAs(world));
        }

        [Test]
        public void ShouldHaveBroadcast_Matches_World_Broadcast_Traffic()
        {
            var world = WorldFactory().Boot();
            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();

            shipA.Mother.GetModule<IntergridMessageService>().ConstructPing();

            world.ShouldHaveBroadcast(".construct", "ShipA");
        }

        // =====================================================================
        // Tick
        // =====================================================================

        [Test]
        public void TickMessages_Auto_Dispatches_Igc_Before_Advancing_Clocks()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();
            var shipB = world.CreateScript("ShipB").OnNetwork().Boot();

            shipA.RunTerminal("@ShipB help").RunToIdle();

            shipB.ShouldHaveExecuted("help", CommandExecutionOutcome.ModuleExecuted, count: 0);

            world.TickMessages();

            world.ShouldHaveDeliveredIgcMessage("ShipA", "ShipB", "*");
            shipB.ShouldHaveExecuted("help");
        }

        [Test]
        public void Tick_Returns_World_For_Chaining()
        {
            var world = WorldFactory().Boot();

            Assert.That(world.Tick(), Is.SameAs(world));
        }

        [Test]
        public void ShouldHaveNoPendingMessages_Passes_After_Idle_Tick()
        {
            var world = WorldFactory().Boot();

            world.CreateScript("ShipA")
                .OnNetwork()
                .Boot();

            world.Tick();

            Assert.DoesNotThrow(() => world.ShouldHaveNoPendingMessages());
        }

        [Test]
        public void TickMessages_Processes_Remote_Command_Flow()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();
            var shipB = world.CreateScript("ShipB").OnNetwork().Boot();

            shipA.RunTerminal("@ShipB help").RunToIdle();

            world.TickMessages();

            world.ShouldHaveDeliveredIgcMessage("ShipA", "ShipB", "*");
            shipB.ShouldHaveExecuted("help");
            world.ShouldHaveNoPendingMessages();
        }

        [Test]
        public void TickMessages_Returns_World_For_Chaining()
        {
            var world = WorldFactory().Boot();

            Assert.That(world.TickMessages(), Is.SameAs(world));
        }

        // =====================================================================
        // Run
        // =====================================================================

        [Test]
        public void Run_Terminal_Dispatches_Command_On_All_Scripts()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").Boot();
            var shipB = world.CreateScript("ShipB").Boot();

            world.Run(UpdateType.Terminal, "help");

            shipA.RunToIdle();
            shipB.RunToIdle();

            shipA.ShouldHaveExecuted("help");
            shipB.ShouldHaveExecuted("help");
        }

        [Test]
        public void RunTerminalAll_Dispatches_Command_On_All_Scripts()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").Boot();
            var shipB = world.CreateScript("ShipB").Boot();

            world.RunTerminalAll("help");

            world.TickMessages();

            shipA.ShouldHaveExecuted("help");
            shipB.ShouldHaveExecuted("help");
        }

        [Test]
        public void Run_Does_Not_Throw_For_Update10()
        {
            var world = WorldFactory().Boot();
            world.CreateScript("ShipA").Boot();

            Assert.DoesNotThrow(() => world.Run(UpdateType.Update10));
        }

        [Test]
        public void Run_Returns_World_For_Chaining()
        {
            var world = WorldFactory().Boot();

            Assert.That(world.Run(UpdateType.Update10), Is.SameAs(world));
        }

        // =====================================================================
        // RunIGC
        // =====================================================================

        [Test]
        public void RunIGC_Does_Not_Throw_After_DispatchIgc()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").OnNetwork().Boot();
            world.CreateScript("ShipB").OnNetwork().Boot();

            shipA.RunTerminal("@ShipB help");

            shipA.RunToIdle();
            world.DispatchIgc();

            Assert.DoesNotThrow(() => world.RunIGC());
        }

        // =====================================================================
        // RunMany
        // =====================================================================

        [Test]
        public void RunMany_Update10_Advances_Clock_And_Executes_Queued_Coroutines()
        {
            var world = WorldFactory().Boot();

            var shipA = world.CreateScript("ShipA").Boot();

            // Queue the command directly — this adds a coroutine to the clock.
            shipA.RunTerminal("help");

            // world.Run(Update10) → mother.Run(Update10) → RunModules() → Clock.Run()
            // which advances coroutines. 5 ticks is sufficient for any simple command.
            world.RunMany(5, UpdateType.Update10);

            Assert.DoesNotThrow(() => shipA.ShouldHaveExecuted("help"));

            //world.RunMany(20, UpdateType.Update10);
            //Assert.That(tracker.ExecutionCount, Is.EqualTo(2));
        }

        [Test]
        public void RunMany_Does_Not_Throw_For_Zero_Cycles()
        {
            var world = WorldFactory().Boot();
            world.CreateScript("ShipA").Boot();

            Assert.DoesNotThrow(() => world.RunMany(0, UpdateType.Update10));
        }

        [Test]
        public void RunMany_Returns_World_For_Chaining()
        {
            var world = WorldFactory().Boot();

            Assert.That(world.RunMany(1, UpdateType.Update10), Is.SameAs(world));
        }

        [Test]
        public void TickUntil_Stops_When_Condition_Becomes_True()
        {
            var world = WorldFactory().Boot();
            var shipA = world.CreateScript("ShipA").Boot();

            shipA.RunTerminal("rename ShipA-Renamed");

            Assert.DoesNotThrow(() => world.TickUntil(() => shipA.Mother.Name == "ShipA-Renamed", maxTicks: 10));

            shipA.ShouldHaveName("ShipA-Renamed");
        }
    }
}

