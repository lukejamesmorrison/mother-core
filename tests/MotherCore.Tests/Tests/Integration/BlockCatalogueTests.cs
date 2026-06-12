using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using System.Linq;

namespace MotherCore.Tests.Integration
{
    public class BlockCatalogueTests : ScriptTestBase<CoreTestProgram>
    {
        [Test]
        public void RegisterBlockForStateMonitoring_Detects_A_Changed_State_On_Run()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");
            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var state = DoorStatus.Open;
            var handler = new MutableStateHandler(() => state);

            catalogue.RegisterBlockForStateMonitoring(door, handler);

            catalogue.Run();
            state = DoorStatus.Closed;

            catalogue.Run();

            Assert.That(handler.ChangeCount, Is.EqualTo(1));
        }

        [Test]
        public void SetBlockWithTag_Adds_Tag_Targeting_And_Updates_Block_Configuration()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");
            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            var taggedBlock = catalogue.SetBlockWithTag(door, "airlock");
            var blockConfig = catalogue.GetBlockConfiguration(door);

            Assert.That(taggedBlock, Is.SameAs(door));
            Assert.That(catalogue.GetBlocksByName<IMyDoor>("#airlock"), Is.EqualTo(new[] { door }));
            Assert.That(blockConfig.Get("general", "tags").ToString(), Is.EqualTo("airlock"));
        }

        [Test]
        public void GetBlocks_Returns_Filtered_Construct_Blocks_Of_A_Given_Type()
        {
            var primaryBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Primary Battery");
            var auxBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Aux Battery");
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

            var script = new Script()
                .WithBlocks(primaryBattery, auxBattery, door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var filteredBlocks = catalogue.GetBlocks<IMyBatteryBlock>(block => block.CustomName.Contains("Primary"));

            Assert.That(filteredBlocks, Is.EqualTo(new[] { primaryBattery }));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Primary Battery"), Is.EqualTo(new[] { primaryBattery }));
        }

        [Test]
        public void GetBlocks_Returns_All_Construct_Blocks_When_Booted_On_A_Two_Grid_Mechanical_Construct()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var primaryBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Primary Battery");
            var cargoBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            var script = new Script(primaryGrid, "Carrier");
            script.ConnectGrids(primaryGrid, cargoGrid);
            script.WithBlock(primaryBattery, primaryGrid);
            script.WithBlock(cargoBattery, cargoGrid);
            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var batteries = catalogue.GetBlocks<IMyBatteryBlock>();

            Assert.That(
                catalogue.ConstructGridIds,
                Is.EquivalentTo(new[] { primaryGrid.EntityId, cargoGrid.EntityId }));
            Assert.That(batteries, Is.EquivalentTo(new[] { primaryBattery, cargoBattery }));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.EqualTo(new[] { cargoBattery }));
        }

        [Test]
        public void GetBlocksByName_Returns_Block_Groups_When_Booted_On_A_Two_Grid_Mechanical_Construct()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var airlockGrid = GridFactory.Create("Airlock Pod");
            var leftDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Left Door");
            var rightDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Right Door");

            var script = new Script(primaryGrid, "Carrier");
            script.ConnectGrids(primaryGrid, airlockGrid);
            script.WithBlock(leftDoor, primaryGrid);
            script.WithBlock(rightDoor, airlockGrid);
            script.WithBlockGroup("Airlocks", leftDoor, rightDoor);
            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(
                catalogue.ConstructGridIds,
                Is.EquivalentTo(new[] { primaryGrid.EntityId, airlockGrid.EntityId }));
            Assert.That(
                catalogue.GetBlocksByName<IMyDoor>("Airlocks").Select(block => block.CustomName).ToList(),
                Is.EquivalentTo(new[] { "Left Door", "Right Door" }));
        }

        [Test]
        public void GetBlockConfiguration_Returns_An_Empty_Ini_For_An_Untracked_Block()
        {
            var script = new Script().Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var untrackedDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Loose Door");

            Assert.That(catalogue.GetBlockConfiguration(untrackedDoor).ToString(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetBlockConfiguration_Loads_Block_Custom_Data_And_Tags_On_Boot()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

            door.CustomData = new CustomDataComposer()
                .With("general", "tags", "airlock")
                .With("status", "mode", "sealed")
                .Build();

            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var blockConfig = catalogue.GetBlockConfiguration(door);

            Assert.That(blockConfig.Get("status", "mode").ToString(), Is.EqualTo("sealed"));
            Assert.That(blockConfig.Get("general", "tags").ToString(), Is.EqualTo("airlock"));
            Assert.That(catalogue.GetBlocksByName<IMyDoor>("#airlock"), Is.EqualTo(new[] { door }));
        }

        [Test]
        public void GetBlocksByName_Resolves_Each_Block_Tag_Individually_When_Multiple_Tags_Are_Configured()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

            door.CustomData = new CustomDataComposer()
                .With("general", "tags", "airlock, hangar, cargo")
                .Build();

            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(catalogue.GetBlocksByName<IMyDoor>("#airlock"), Is.EqualTo(new[] { door }));
            Assert.That(catalogue.GetBlocksByName<IMyDoor>("#hangar"), Is.EqualTo(new[] { door }));
            Assert.That(catalogue.GetBlocksByName<IMyDoor>("#cargo"), Is.EqualTo(new[] { door }));
        }

        [Test]
        public void HandleEvent_For_ConnectorLocked_Preserves_Block_Group_Targeting_Configured_At_Boot()
        {
            var leftDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Left Door");
            var rightDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Right Door");

            var script = new Script()
                .WithBlocks(leftDoor, rightDoor)
                .WithBlockGroup("Airlocks", leftDoor, rightDoor)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(
                catalogue.GetBlocksByName<IMyDoor>("Airlocks").Select(block => block.CustomName).ToList(),
                Is.EquivalentTo(new[] { "Left Door", "Right Door" }));

            catalogue.HandleEvent(new ConnectorLockedEvent(), null);

            Assert.That(
                catalogue.GetBlocksByName<IMyDoor>("Airlocks").Select(block => block.CustomName).ToList(),
                Is.EquivalentTo(new[] { "Left Door", "Right Door" }));
        }

        [Test]
        public void LoadBlockGroups_Reloads_Block_Groups_Added_After_Boot()
        {
            var leftDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Left Door");
            var rightDoor = TerminalBlockFactory.Create<IMyDoor>(customName: "Right Door");

            var script = new Script()
                .WithBlocks(leftDoor, rightDoor)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            script.WithBlockGroup("Airlocks", leftDoor, rightDoor);

            // The terminal system has a new block group, but Mother is still unaware of it
            Assert.That(catalogue.GetBlocksByName<IMyDoor>("Airlocks"), Is.Empty);
            // so we reload our block groups from the grid terminal system
            catalogue.LoadBlockGroups();

            Assert.That(
                catalogue.GetBlocksByName<IMyDoor>("Airlocks").Select(block => block.CustomName).ToList(),
                Is.EquivalentTo(new[] { "Left Door", "Right Door" }));
        }

        [Test]
        public void OnMechanicalBlockAttached_Adds_Newly_Connected_Grid_Blocks_To_The_Construct()
        {
            var script = new Script("Carrier").Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");
            script.ConnectGrids(script.PrimaryGrid, cargoGrid);
            script.WithBlock(battery, cargoGrid);

            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);

            catalogue.OnMechanicalBlockAttached(cargoGrid);
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));

            script.AssertEventEmitted<ConstructRefreshedEvent>();
        }

        [Test]
        public void OnMechanicalBlockDetached_Prunes_Disconnected_Grid_Blocks_From_The_Construct()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            var script = new Script(primaryGrid, "Carrier");

            script.ConnectGrids(primaryGrid, cargoGrid);
            script.WithBlock(battery, cargoGrid);
            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));

            script.DetachGrid(cargoGrid);
            catalogue.OnMechanicalBlockDetached();
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);
        }

        [Test]
        public void RefreshConstruct_Prunes_Disconnected_Grid_And_Emits_ConstructRefreshedEvent()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            var script = new Script(primaryGrid, "Carrier");
            script.ConnectGrids(primaryGrid, cargoGrid);
            script.WithBlock(battery, cargoGrid);
            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));

            script.DetachGrid(cargoGrid);

            catalogue.RefreshConstruct();

            script.Clock.RunToIdle();

            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);
            script.AssertEventEmitted<ConstructRefreshedEvent>();
        }

        [Test]
        public void RefreshConstruct_Adds_New_Connected_Grid_And_Emits_ConstructRefreshedEvent()
        {
            var script = new Script("Carrier").Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            var scoutGrid = GridFactory.Create("Scout Pod");
            var reactor = TerminalBlockFactory.Create<IMyReactor>(customName: "Scout Reactor");

            script.ConnectGrids(script.PrimaryGrid, scoutGrid);
            script.WithBlock(reactor, scoutGrid);

            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Is.Empty);

            catalogue.RefreshConstruct();
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(scoutGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Has.Count.EqualTo(1));
            script.AssertEventEmitted<ConstructRefreshedEvent>();
        }

        [Test]
        public void RunHook_Executes_A_Command_From_Block_Custom_Data_Hooks()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");
            door.CustomData = new CustomDataComposer()
                .With("hooks", "opened", "rename HangarOpen")
                .Build();

            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            catalogue.RunHook(door, "opened");
            script.Clock.RunToIdle();

            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("HangarOpen"));
        }

        [Test]
        public void HandleEvent_For_MergeBlockLocked_Refreshes_Construct_And_Emits_ConstructRefreshedEvent()
        {
            var script = new Script("Carrier").Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            var scoutGrid = GridFactory.Create("Scout Pod");
            var reactor = TerminalBlockFactory.Create<IMyReactor>(customName: "Scout Reactor");
            script.ConnectGrids(script.PrimaryGrid, scoutGrid);
            script.WithBlock(reactor, scoutGrid);

            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Is.Empty);

            catalogue.HandleEvent(new MergeBlockLockedEvent(), null);
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(scoutGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Has.Count.EqualTo(1));
            script.AssertEventEmitted<ConstructRefreshedEvent>();
        }

        [Test]
        public void HandleEvent_For_SystemConfigChanged_Reloads_Programmable_Block_Hooks()
        {
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

            var script = new Script()
                .WithBlock(door)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var configuration = script.Mother.GetModule<Configuration>();

            configuration.Ini.Set("hooks", "\"Hangar Door\".opened", "rename HangarConfigured");

            catalogue.HandleEvent(new SystemConfigChangedEvent(), null);
            catalogue.RunHook(door, "opened");
            script.Clock.RunToIdle();

            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("HangarConfigured"));
        }
    }
}