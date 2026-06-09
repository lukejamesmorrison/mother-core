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
            var script = new Script("Carrier");
            var cargoGrid = script.CreateGrid("Cargo Pod");
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
            var script = new Script("Carrier");
            var cargoGrid = script.CreateGrid("Cargo Pod");

            script.Boot();

            var rotors = new System.Collections.Generic.List<IMyMotorStator>();
            script.Mother.GridTerminalSystem.GetBlocksOfType(rotors);

            Assert.That(rotors, Has.Count.EqualTo(1));
            Assert.That(rotors[0].CubeGrid.EntityId, Is.EqualTo(script.PrimaryGrid.EntityId));
            Assert.That(rotors[0].TopGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(rotors[0].IsAttached, Is.True);
        }

        [Test]
        public void CreateGrid_Can_Use_A_PistonConnection_When_Requested()
        {
            var script = new Script("Carrier");
            var cargoGrid = script.CreateGrid("Cargo Pod", connectionKind: MechanicalConnectionKind.Piston);
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            script.WithBlock(battery, cargoGrid)
                .Boot();

            var pistons = new System.Collections.Generic.List<IMyPistonBase>();
            script.Mother.GridTerminalSystem.GetBlocksOfType(pistons);

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(pistons, Has.Count.EqualTo(1));
            Assert.That(pistons[0].TopGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));
        }

        [Test]
        public void ConnectGrids_Can_Create_A_HingeStyle_Connection_Explicitly()
        {
            var script = new Script("Carrier");
            var armGrid = TerminalBlockFactory.CreateCubeGrid("Arm Grid");
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
        // CaptureEcho and ShouldHavePrinted
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
            var capture = script.CaptureEcho();

            // Terminal.Echo() calls Program.Echo() directly, which PrintCapture intercepts.
            // Mother.Print() routes through Terminal.Print() (a buffer), not through Echo.
            script.Mother.GetModule<Terminal>().Echo("hello from test");

            Assert.That(capture.Contains("hello from test"), Is.True);
        }

        [Test]
        public void CaptureEcho_Does_Not_Contain_Output_Written_Before_Capture_Was_Set_Up()
        {
            var script = new Script().Boot();

            script.Mother.GetModule<Terminal>().Echo("before capture");
            var capture = script.CaptureEcho();

            Assert.That(capture.Lines.Count, Is.EqualTo(0));
        }

        [Test]
        public void ShouldHavePrinted_Passes_When_Fragment_Is_Present()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            script.Mother.GetModule<Terminal>().Echo("expected output");

            Assert.DoesNotThrow(() => capture.ShouldHavePrinted("expected output"));
        }

        [Test]
        public void ShouldHavePrinted_Throws_When_Fragment_Is_Absent()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            Assert.Throws<AssertionException>(() => capture.ShouldHavePrinted("was never printed"));
        }
    }
}
