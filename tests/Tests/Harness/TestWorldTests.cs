using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies <see cref="TestWorld"/>: script creation, IGC dispatch,
    /// single-cycle and multi-cycle execution, and the convenience helpers
    /// <see cref="TestWorld.RunIGC"/> and <see cref="TestWorld.RunMany"/>.
    /// </summary>
    public class TestWorldTests
    {
        // =====================================================================
        // CreateScript
        // =====================================================================

        [Test]
        public void CreateScript_Returns_Booted_Script()
        {
            var world = new TestWorld();

            var script = world.CreateScript<CoreTestProgram>().Boot();

            Assert.That(script.Mother.SystemState, Is.EqualTo(Mother.SystemStates.WORKING));
        }

        [Test]
        public void CreateScript_With_Grid_Name_Sets_Mother_Name_After_Boot()
        {
            var world = new TestWorld();

            var script = world.CreateScript<CoreTestProgram>("Flagship").Boot();

            Assert.That(script.Mother.Name, Is.EqualTo("Flagship"));
        }

        [Test]
        public void CreateScript_On_Existing_World_Grid_Binds_The_Program_To_That_Grid()
        {
            var world = new TestWorld();
            var carrierGrid = world.CreateGrid("Carrier");
            var script = world.CreateScript<CoreTestProgram>(carrierGrid, "Carrier").Boot();

            Assert.That(script.PrimaryGrid, Is.SameAs(carrierGrid.Grid));
            Assert.That(script.Program.Me.CubeGrid, Is.SameAs(carrierGrid.Grid));
        }

        [Test]
        public void Merge_Rewrites_World_Blocks_When_Standalone_Merge_Blocks_Are_Merged()
        {
            var world = new TestWorld();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            var carrierMerge = carrierGrid.AddBlock(
                TerminalBlockFactory.Create<IMyShipMergeBlock>(customName: "Carrier Merge"));

            var cargoMerge = cargoGrid.AddBlock(
                TerminalBlockFactory.Create<IMyShipMergeBlock>(customName: "Cargo Merge"));

            // create script on one of the grids
            world.CreateScript<CoreTestProgram>(carrierGrid, "Carrier").Boot();

            world.Merge(carrierMerge, cargoMerge);

            Assert.That(cargoMerge.IsSameConstructAs(carrierMerge), Is.True);
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(carrierMerge.CubeGrid.EntityId));
        }

        [Test]
        public void Scripts_Created_Via_World_Cross_Register_In_Each_Others_Almanac()
        {
            var world = new TestWorld();

            var shipA = world.CreateScript<CoreTestProgram>("ShipA").Boot();
            var shipB = world.CreateScript<CoreTestProgram>("ShipB").Boot();

            var almanacA = shipA.Mother.GetModule<Almanac>();
            var almanacB = shipB.Mother.GetModule<Almanac>();

            Assert.That(almanacA.GetRecord("ShipB"), Is.Not.Null, "ShipA should know ShipB");
            Assert.That(almanacB.GetRecord("ShipA"), Is.Not.Null, "ShipB should know ShipA");
        }

        // =====================================================================
        // DispatchIgc
        // =====================================================================

        [Test]
        public void DispatchIgc_Delivers_Queued_Messages_To_Recipient()
        {
            var world = new TestWorld();

            var shipA = world.CreateScript<CoreTestProgram>("ShipA").Boot();
            var shipB = world.CreateScript<CoreTestProgram>("ShipB").Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(0),
                "Message should not be delivered before DispatchIgc().");

            world.DispatchIgc();
            shipB.Clock.RunToIdle();

            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(1));
        }

        [Test]
        public void DispatchIgc_Returns_World_For_Chaining()
        {
            var world = new TestWorld();

            Assert.That(world.DispatchIgc(), Is.SameAs(world));
        }

        // =====================================================================
        // Run
        // =====================================================================

        [Test]
        public void Run_Terminal_Dispatches_Command_On_All_Scripts()
        {
            var world = new TestWorld();

            var shipA = world.CreateScript<CoreTestProgram>("ShipA").Boot();
            var shipB = world.CreateScript<CoreTestProgram>("ShipB").Boot();

            world.Run(UpdateType.Terminal, "help");

            shipA.Clock.RunToIdle();
            shipB.Clock.RunToIdle();

            Assert.That(shipA.Bus.GetExecutionCount("help"), Is.EqualTo(1));
            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(1));
        }

        [Test]
        public void Run_Does_Not_Throw_For_Update10()
        {
            var world = new TestWorld();
            world.CreateScript<CoreTestProgram>("ShipA").Boot();

            Assert.DoesNotThrow(() => world.Run(UpdateType.Update10));
        }

        [Test]
        public void Run_Returns_World_For_Chaining()
        {
            var world = new TestWorld();

            Assert.That(world.Run(UpdateType.Update10), Is.SameAs(world));
        }

        // =====================================================================
        // RunIGC
        // =====================================================================

        [Test]
        public void RunIGC_Does_Not_Throw_After_DispatchIgc()
        {
            var world = new TestWorld();

            var shipA = world.CreateScript<CoreTestProgram>("ShipA").Boot();
            world.CreateScript<CoreTestProgram>("ShipB").Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");

            shipA.Clock.RunToIdle();
            world.DispatchIgc();

            Assert.DoesNotThrow(() => world.RunIGC());
        }

        // =====================================================================
        // RunMany
        // =====================================================================

        [Test]
        public void RunMany_Update10_Advances_Clock_And_Executes_Queued_Coroutines()
        {
            var world = new TestWorld();

            var shipA = world.CreateScript<CoreTestProgram>("ShipA").Boot();

            // Queue the command directly — this adds a coroutine to the clock.
            shipA.Bus.RunTerminalCommand("help");

            // world.Run(Update10) → mother.Run(Update10) → RunModules() → Clock.Run()
            // which advances coroutines. 5 ticks is sufficient for any simple command.
            world.RunMany(5, UpdateType.Update10);

            Assert.That(shipA.Bus.GetExecutionCount("help"), Is.EqualTo(1));

            //world.RunMany(20, UpdateType.Update10);
            //Assert.That(tracker.ExecutionCount, Is.EqualTo(2));
        }

        [Test]
        public void RunMany_Does_Not_Throw_For_Zero_Cycles()
        {
            var world = new TestWorld();
            world.CreateScript<CoreTestProgram>("ShipA").Boot();

            Assert.DoesNotThrow(() => world.RunMany(0, UpdateType.Update10));
        }

        [Test]
        public void RunMany_Returns_World_For_Chaining()
        {
            var world = new TestWorld();

            Assert.That(world.RunMany(1, UpdateType.Update10), Is.SameAs(world));
        }
    }
}
