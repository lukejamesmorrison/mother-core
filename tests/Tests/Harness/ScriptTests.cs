using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System.Linq;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies the <see cref="Script{TProgram}"/> test harness: boot lifecycle,
    /// fluent configuration, <see cref="Script{TProgram}.Run"/>, and
    /// <see cref="Script{TProgram}.CaptureEcho"/> / <see cref="PrintCapture.ShouldHavePrinted"/>.
    /// </summary>
    public class ScriptTests
    {
        // =====================================================================
        // Boot lifecycle
        // =====================================================================

        [Test]
        public void Boot_Exposes_A_Non_Null_CommandBus()
        {
            var script = new Script().Boot();

            Assert.That(script.Bus, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_ClockDriver()
        {
            var script = new Script().Boot();

            Assert.That(script.Clock, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Configuration()
        {
            var script = new Script().Boot();

            Assert.That(script.Config, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Program()
        {
            var script = new Script().Boot();

            Assert.That(script.Program, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Mother()
        {
            var script = new Script().Boot();

            Assert.That(script.Mother, Is.Not.Null);
        }

        [Test]
        public void Boot_Reaches_Working_System_State()
        {
            var script = new Script().Boot();

            Assert.That(script.Mother.SystemState, Is.EqualTo(Mother.SystemStates.WORKING));
        }

        [Test]
        public void Boot_Can_Assign_The_Programmable_Block_To_A_Supplied_Primary_Grid()
        {
            var grid = GridFactory.Create("Carrier Grid");
            var script = new Script(grid).Boot();

            Assert.That(script.Mother.SystemState, Is.EqualTo(Mother.SystemStates.WORKING));
            Assert.That(script.PrimaryGrid, Is.SameAs(grid));
            Assert.That(script.Mother.ProgrammableBlock.CubeGrid, Is.SameAs(grid));
        }

        // =====================================================================
        // WithCustomData
        // =====================================================================

        [Test]
        public void WithCustomData_Makes_Config_Command_Available_After_Boot()
        {
            var script = new Script()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("openDoor", "track")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigCommands.Keys, Contains.Item("openDoor"));
        }

        // =====================================================================
        // WithCommands
        // =====================================================================

        [Test]
        public void WithCommands_Registers_Command_With_Bus()
        {
            var tracker = new CommandSpy("myCmd");
            var script = new Script().WithCommands(tracker).Boot();

            script.Bus.RunTerminalCommand("myCmd");
            script.Clock.Tick(2);

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void WithCommands_Accepts_Multiple_Commands()
        {
            var trackerA = new CommandSpy("cmdA");
            var trackerB = new CommandSpy("cmdB");
            var script = new Script().WithCommands(trackerA, trackerB).Boot();

            script.Bus.RunTerminalCommand("cmdA");
            script.Bus.RunTerminalCommand("cmdB");
            script.Clock.RunToIdle();

            Assert.That(trackerA.ExecutionCount, Is.EqualTo(1));
            Assert.That(trackerB.ExecutionCount, Is.EqualTo(1));
        }

        // =====================================================================
        // Block registration
        // =====================================================================

        [Test]
        public void WithBlock_Makes_Block_Available_To_BlockCatalogue()
        {
            var connector = TerminalBlockFactory.Create<IMyShipConnector>(customName: "Dock A");

            var script = new Script()
                .WithBlock(connector)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var blocks = catalogue.GetBlocksByName<IMyShipConnector>("Dock A");

            Assert.That(blocks, Has.Count.EqualTo(1));
            Assert.That(blocks[0], Is.SameAs(connector));
        }

        [Test]
        public void CreateGrid_And_WithBlock_Build_A_MultiGrid_Construct_For_BlockCatalogue()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var script = new Script("Carrier")
                .WithGrid(cargoGrid);
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            script.WithBlock(battery, cargoGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var blocks = catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery");

            Assert.That(catalogue.ConstructGridIds, Has.Count.EqualTo(2));
            Assert.That(catalogue.ConstructGridIds, Contains.Item(script.PrimaryGrid.EntityId));
            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(blocks, Has.Count.EqualTo(1));
            Assert.That(blocks[0], Is.SameAs(battery));
            Assert.That(battery.IsSameConstructAs(script.Mother.ProgrammableBlock), Is.True);
        }

        [Test]
        public void CreateGrid_Uses_RotorConnection_By_Default()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var script = new Script("Carrier")
                .WithGrid(cargoGrid)
                .Boot();

            var rotor = script.GetMechanicalConnectionTo(cargoGrid) as IMyMotorStator;

            Assert.That(rotor, Is.Not.Null);
            Assert.That(rotor.CubeGrid.EntityId, Is.EqualTo(script.PrimaryGrid.EntityId));
            Assert.That(rotor.TopGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(rotor.IsAttached, Is.True);
        }

        [Test]
        public void CreateGrid_Can_Use_A_PistonConnection_When_Requested()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var script = new Script("Carrier")
                .WithGrid(cargoGrid, connectionKind: MechanicalConnectionKind.Piston);
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            script.WithBlock(battery, cargoGrid)
                .Boot();

            var piston = script.GetMechanicalConnectionTo(cargoGrid) as IMyPistonBase;

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(piston, Is.Not.Null);
            Assert.That(piston.TopGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));
        }

        [Test]
        public void DetachGrid_Detaches_The_Mechanical_Connection_For_A_Subgrid()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");

            var script = new Script("Carrier")
                .WithGrid(cargoGrid)
                .Boot();

            var connection = script.GetMechanicalConnectionTo(cargoGrid);

            script.DetachGrid(cargoGrid);

            Assert.That(connection.IsAttached, Is.False);
        }

        [Test]
        public void WithGrid_Can_Create_And_Attach_A_Named_Subgrid_Without_Keeping_A_Reference()
        {
            var script = new Script("Carrier")
                .WithGrid("Cargo Pod")
                .Boot();

            var mechanicalBlocks = new System.Collections.Generic.List<IMyMechanicalConnectionBlock>();
            script.Mother.GridTerminalSystem.GetBlocksOfType(mechanicalBlocks);

            Assert.That(mechanicalBlocks, Has.Count.EqualTo(1));
            Assert.That(mechanicalBlocks[0].TopGrid.CustomName, Is.EqualTo("Cargo Pod"));
            Assert.That(mechanicalBlocks[0].IsAttached, Is.True);
        }

        [Test]
        public void ConnectGrids_Can_Create_A_HingeStyle_Connection_Explicitly()
        {
            var script = new Script("Carrier");
            var armGrid = GridFactory.Create("Arm Grid");
            var hinge = script.ConnectGrids(script.PrimaryGrid, armGrid, MechanicalConnectionKind.Hinge);

            script.Boot();

            Assert.That(hinge, Is.AssignableTo<IMyMotorStator>());
            Assert.That(hinge.CustomName, Does.Contain("Harness Hinge"));
            Assert.That(hinge.TopGrid.EntityId, Is.EqualTo(armGrid.EntityId));
        }

        [Test]
        public void WithBlockGroup_Makes_Group_Available_To_BlockCatalogue()
        {
            var leftDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Left Door");
            var rightDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Right Door");

            var script = new Script()
                .WithBlocks(leftDoor, rightDoor)
                .WithBlockGroup("Airlocks", leftDoor, rightDoor)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var blocks = catalogue.GetBlocksByName<IMyDoor>("Airlocks");

            Assert.That(blocks.Select(block => block.CustomName).ToList(),
                Is.EquivalentTo(new[] { "Left Door", "Right Door" }));
        }

        // =====================================================================
        // Run
        // =====================================================================

        [Test]
        public void Run_Returns_Script_For_Chaining()
        {
            var script = new Script().Boot();

            var returned = script.Run(UpdateType.Update10);

            Assert.That(returned, Is.SameAs(script));
        }

        [Test]
        public void Run_Terminal_Dispatches_Argument_To_CommandBus()
        {
            var tracker = new CommandSpy("go");
            var script = new Script().WithCommands(tracker).Boot();

            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void Run_Terminal_Does_Not_Throw_For_Unknown_Command()
        {
            var script = new Script().Boot();

            Assert.DoesNotThrow(() => script.Run(UpdateType.Terminal, "unknowncmd"));
        }

        [Test]
        public void Run_Update10_Does_Not_Throw()
        {
            var script = new Script().Boot();

            Assert.DoesNotThrow(() => script.Run(UpdateType.Update10));
        }

        [Test]
        public void Run_Can_Be_Called_Multiple_Times()
        {
            var tracker = new CommandSpy("go");
            var script = new Script().WithCommands(tracker).Boot();

            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();
            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2));
        }

        // =====================================================================
        // Echo capture and AssertPrinted
        // =====================================================================

        [Test]
        public void CaptureEcho_Returns_A_Non_Null_PrintCapture()
        {
            var script = new Script().Boot();

            Assert.That(script.CaptureEcho(), Is.Not.Null);
        }

        [Test]
        public void CaptureEcho_Called_Twice_Returns_Same_Instance()
        {
            var script = new Script().Boot();

            var first = script.CaptureEcho();
            var second = script.CaptureEcho();

            Assert.That(first, Is.SameAs(second));
        }

        [Test]
        public void CaptureEcho_Captures_Output_Written_Via_Terminal_Echo()
        {
            var script = new Script().Boot();

            // Terminal.Echo() calls Program.Echo() directly, which PrintCapture intercepts.
            // Mother.Print() routes through Terminal.Print() (a buffer), not through Echo.
            script.Mother.GetModule<Terminal>().Echo("hello from test");

            Assert.That(script.CaptureEcho().Contains("hello from test"), Is.True);
        }

        [Test]
        public void CaptureEcho_Starts_Empty_After_Boot()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            Assert.That(capture.Lines.Count, Is.EqualTo(0));
        }

        [Test]
        public void AssertPrinted_Passes_When_Fragment_Is_Present()
        {
            var script = new Script().Boot();
            
            script.Mother.GetModule<Terminal>().Echo("expected output");

            Assert.DoesNotThrow(() => script.AssertPrinted("expected output"));
        }

        [Test]
        public void AssertPrinted_Throws_When_Fragment_Is_Absent()
        {
            var script = new Script().Boot();

            Assert.Throws<AssertionException>(() => script.AssertPrinted("was never printed"));
        }
    }
}
