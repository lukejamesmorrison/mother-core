using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies the <see cref="Script{TProgram}"/> test harness: boot lifecycle,
    /// fluent configuration, <see cref="Script{TProgram}.Run"/>, and
    /// <see cref="Script{TProgram}.CaptureEcho"/> / <see cref="PrintCapture.ShouldHavePrinted"/>.
    /// </summary>
    public class ScriptTests
    {
        class CountingCommand : BaseModuleCommand
        {
            readonly string _name;

            public override string Name => _name;
            public int ExecutionCount { get; private set; }

            public CountingCommand(string name)
            {
                _name = name;
            }

            public override string Execute(TerminalCommand command)
            {
                ExecutionCount++;
                return string.Empty;
            }
        }

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
        public void Boot_Exposes_A_Non_Null_GridTerminalSystem()
        {
            var script = new Script().Boot();

            Assert.That(script.GridTerminalSystem, Is.Not.Null);
        }

        [Test]
        public void Boot_Binds_The_Program_To_The_Harness_GridTerminalSystem()
        {
            var script = new Script().Boot();

            Assert.That(script.Program.GridTerminalSystem, Is.SameAs(script.GridTerminalSystem));
        }

        [Test]
        public void Boot_Defaults_Runtime_UpdateFrequency_To_Update10()
        {
            var script = new Script().Boot();

            Assert.That(script.Program.Runtime.UpdateFrequency, Is.EqualTo(UpdateFrequency.Update10));
        }

        [Test]
        public void WithUpdateFrequency_Overrides_Runtime_UpdateFrequency_After_Boot()
        {
            var script = new Script()
                .WithUpdateFrequency(UpdateFrequency.Update1)
                .Boot();

            Assert.That(script.Program.Runtime.UpdateFrequency, Is.EqualTo(UpdateFrequency.Update1));
        }

        [Test]
        public void Boot_Can_Assign_The_Programmable_Block_To_A_Supplied_Primary_Grid()
        {
            var grid = GridFactory.Create("Carrier Grid");
            var script = new Script(grid).Boot();

            Assert.That(script.PrimaryGrid, Is.SameAs(grid));
            Assert.That(script.Program.Me.CubeGrid, Is.SameAs(grid));
            Assert.That(script.Program.GridTerminalSystem, Is.SameAs(script.GridTerminalSystem));
        }

        // =====================================================================
        // WithCustomData
        // =====================================================================

        [Test]
        public void WithCustomData_Assigns_CustomData_To_The_Programmable_Block_Before_Boot()
        {
            var customData = new CustomDataComposer()
                .WithCommand("openDoor", "track")
                .Build();

            var script = new Script()
                .WithCustomData(customData)
                .Boot();

            Assert.That(script.Program.Me.CustomData, Does.Contain("[commands]"));
            Assert.That(script.Program.Me.CustomData, Does.Contain("openDoor=track"));
        }

        // =====================================================================
        // WithCommands
        // =====================================================================

        [Test]
        public void WithCommands_Registers_Command_With_Bus()
        {
            var tracker = new CountingCommand("myCmd");
            var script = new Script().WithCommands(tracker).Boot();

            script.Bus.RunTerminalCommand("myCmd");
            script.Clock.Tick(2);

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void WithCommands_Accepts_Multiple_Commands()
        {
            var trackerA = new CountingCommand("cmdA");
            var trackerB = new CountingCommand("cmdB");
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
        public void WithBlock_Registers_Block_In_GridTerminalSystem()
        {
            var script = new Script();
            var connector = script.WithBlock<IMyShipConnector>("Dock A");

            script.Boot();

            Assert.That(script.GridTerminalSystem.GetBlockWithName("Dock A"), Is.SameAs(connector));
        }

        [Test]
        public void WithBlock_GenericOverload_Creates_And_Registers_A_Block_By_Type_And_Name()
        {
            var script = new Script();

            script.WithBlock<IMyDoor>("Hangar Door");
            script.Boot();

            var door = script.GetBlock<IMyDoor>("Hangar Door");

            Assert.That(door, Is.Not.Null);
            Assert.That(door.CustomName, Is.EqualTo("Hangar Door"));
            script.AssertHasBlock("Hangar Door");
        }

        [Test]
        public void WithBlock_GenericOverload_Can_Configure_Interface_Specific_State()
        {
            var script = new Script();

            var battery = script.WithBlock<IMyBatteryBlock>(
                "Reserve Battery",
                configure: block => block.Enabled = false);

            script.Boot();

            Assert.That(battery.Enabled, Is.False);
            script.AssertHasBlock("Reserve Battery");
        }

        [Test]
        public void WithBlock_When_CustomName_Is_Not_Provided_AutoGenerates_Name_From_Block_Type()
        {
            var light = new FakeLightingBlock();

            Assert.That(light.CustomName, Is.EqualTo("Lighting Block 1"));
        }

        [Test]
        public void WithBlock_When_Multiple_Unnamed_Blocks_Of_Same_Type_Are_Added_Increments_Suffix()
        {
            var first = new FakeLightingBlock();
            var second = new FakeLightingBlock();

            Assert.That(first.CustomName, Is.EqualTo("Lighting Block 1"));
            Assert.That(second.CustomName, Is.EqualTo("Lighting Block 2"));
        }

        [Test]
        public void CreateGrid_And_WithBlock_Make_A_Subgrid_Block_Reachable_And_SameConstruct()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var carrierBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Carrier Battery");
            var script = new Script("Carrier")
                .WithGrid(cargoGrid);
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            script.WithBlock(carrierBattery)
                .WithBlock(battery, cargoGrid)
                .Boot();

            var reachableBatteries = new System.Collections.Generic.List<IMyBatteryBlock>();
            script.GridTerminalSystem.GetBlocksOfType(reachableBatteries);

            Assert.That(reachableBatteries, Contains.Item(carrierBattery));
            Assert.That(reachableBatteries, Contains.Item(battery));
            Assert.That(battery.IsSameConstructAs(carrierBattery), Is.True);
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

            Assert.That(piston, Is.Not.Null);
            Assert.That(piston.TopGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(script.GridTerminalSystem.GetBlockWithName("Cargo Battery"), Is.SameAs(battery));
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
            script.GridTerminalSystem.GetBlocksOfType(mechanicalBlocks);

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
        public void ConnectGridsViaConnector_Keeps_The_Connected_Grid_Out_Of_The_Construct()
        {
            var shuttleGrid = GridFactory.Create("Shuttle");
            var carrierBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Carrier Battery");
            var shuttleBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Shuttle Battery");

            var script = new Script("Carrier");
            var dock = script.ConnectGridsViaConnector(
                script.PrimaryGrid,
                shuttleGrid,
                baseConnectorName: "Carrier Dock",
                otherConnectorName: "Shuttle Dock");

            script.WithBlock(carrierBattery)
                .WithBlock(shuttleBattery, shuttleGrid)
                .Boot();

            var mechanicalBlocks = new System.Collections.Generic.List<IMyMechanicalConnectionBlock>();
            script.GridTerminalSystem.GetBlocksOfType(mechanicalBlocks);

            Assert.That(dock.OtherConnector, Is.Not.Null);
            script.ShouldHaveConnectorStatus("Carrier Dock", MyShipConnectorStatus.Connected);
            Assert.That(mechanicalBlocks, Is.Empty);
            script.ShouldBeSameConstruct("Carrier Dock", "Carrier Battery");
            script.ShouldNotBeSameConstruct("Shuttle Dock", "Carrier Battery");
            script.ShouldNotBeSameConstruct("Shuttle Battery", "Carrier Battery");
        }

        [Test]
        public void ConnectGridsViaMergeBlock_When_Locked_Rewrites_Blocks_Onto_One_Grid()
        {
            var world = new TestWorld();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");
            var carrierMerge = carrierGrid.AddBlock<IMyShipMergeBlock>("Carrier Merge");
            var cargoMerge = cargoGrid.AddBlock<IMyShipMergeBlock>("Cargo Merge");

            var script = world.CreateScript(carrierGrid, "Carrier").Boot();

            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(cargoGrid.Grid.EntityId));
            Assert.That(cargoMerge.IsSameConstructAs(carrierMerge), Is.False);

            world.Merge(carrierMerge, cargoMerge);

            script.ShouldHaveMergeState("Carrier Merge", MergeState.Locked);
            script.ShouldHaveMergeState("Cargo Merge", MergeState.Locked);
            Assert.That(carrierMerge.Enabled, Is.True);
            Assert.That(cargoMerge.Enabled, Is.True);
            Assert.That(cargoMerge.IsSameConstructAs(carrierMerge), Is.True);
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(carrierMerge.CubeGrid.EntityId));
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.Not.EqualTo(cargoGrid.Grid.EntityId));
            Assert.That(script.PrimaryGrid.EntityId, Is.EqualTo(script.Program.Me.CubeGrid.EntityId));
        }

        [Test]
        public void ConnectGridsViaMergeBlock_When_Unlocked_Restores_Separate_Grid_References()
        {
            var world = new TestWorld();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            var carrierMerge = carrierGrid.AddBlock<IMyShipMergeBlock>("Carrier Merge");
            var cargoMerge = cargoGrid.AddBlock<IMyShipMergeBlock>("Cargo Merge");

            var script = world.CreateScript(carrierGrid, "Carrier").Boot();
            world.Merge(carrierMerge, cargoMerge);

            // assert grids are connected in a single grid
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(carrierMerge.CubeGrid.EntityId));

            world.Unmerge(carrierMerge);

            // assert one of the two merge blocks has forced an unmerge
            Assert.That(carrierMerge.State, Is.EqualTo(MergeState.None));
            Assert.That(cargoMerge.State, Is.EqualTo(MergeState.Working));
            Assert.That(carrierMerge.Enabled, Is.False);
            Assert.That(cargoMerge.Enabled, Is.True);

            // assert that grids are not completely separate in game world.
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(cargoGrid.Grid.EntityId));
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.Not.EqualTo(carrierMerge.CubeGrid.EntityId));
            Assert.That(cargoMerge.CubeGrid.EntityId, Is.EqualTo(cargoGrid.Grid.EntityId));
            Assert.That(script.Program.Me.CubeGrid.EntityId, Is.EqualTo(script.PrimaryGrid.EntityId));
        }

        [Test]
        public void WithBlockGroup_Registers_The_Group_In_GridTerminalSystem()
        {
            var leftDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Left Door");
            var rightDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Right Door");

            var script = new Script()
                .WithBlocks(leftDoor, rightDoor)
                .WithBlockGroup("Airlocks", leftDoor, rightDoor)
                .Boot();

            var group = script.GridTerminalSystem.GetBlockGroupWithName("Airlocks");
            var blocks = new System.Collections.Generic.List<IMyDoor>();
            group.GetBlocksOfType(blocks);

            script.ShouldContainGroup("Airlocks");
            script.ShouldGroupContainBlocks("Airlocks", "Left Door", "Right Door");
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
            var script = new Script().Boot();

            script.Run(UpdateType.Terminal, "help");
            script.Clock.RunToIdle();

            script.AssertCommandExecuted("help");
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
            var script = new Script().Boot();

            script.Run(UpdateType.Terminal, "help");
            script.Clock.RunToIdle();
            script.Run(UpdateType.Terminal, "help");
            script.Clock.RunToIdle();

            script.AssertCommandExecuted("help", 2);
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

            script.Program.Echo("hello from test");

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
            
            script.Program.Echo("expected output");

            Assert.DoesNotThrow(() => script.ShouldHavePrinted("expected output"));
        }

        [Test]
        public void AssertPrinted_Throws_When_Fragment_Is_Absent()
        {
            var script = new Script().Boot();

            Assert.Throws<AssertionException>(() => script.ShouldHavePrinted("was never printed"));
        }

        [Test]
        public void ShouldNotHavePrinted_Throws_When_Fragment_Is_Present()
        {
            var script = new Script().Boot();

            script.Program.Echo("present line");

            Assert.Throws<AssertionException>(() => script.ShouldNotHavePrinted("present line"));
        }
    }
}
