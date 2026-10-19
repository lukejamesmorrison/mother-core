using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    [Category(TestCategories.LayerModule)]
    public class MergeBlockModuleTests : TestBase
    {
        [Test]
        public void Boot_Subscribes_To_ConstructRefreshedEvent()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();
            var eventBus = script.Mother.GetModule<EventBus>();

            Assert.That(eventBus.IsSubscribed<ConstructRefreshedEvent>(mergeModule), Is.True);
        }

        [Test]
        public void Run_When_Separate_Grids_Merge_Emits_Event_Refreshes_Construct_And_Defers_OnMerge_Hook()
        {
            var world = WorldFactory().Boot();
            var carrierGrid = world.CreateGrid("Carrier");
            var cargoGrid = world.CreateGrid("Cargo Pod");

            var mergeBlockA = TerminalBlockFactory.Create<IMyShipMergeBlock>(
                customName: "MergeA",
                customData: new CustomDataComposer()
                    .With("hooks", "onMerge", "rename CarrierMerged")
                    .Build()
            );
            var mergeBlockB = TerminalBlockFactory.Create<IMyShipMergeBlock>(customName: "MergeB");

            carrierGrid.AddBlock(mergeBlockA);
            cargoGrid.AddBlock(mergeBlockB);

            var script = world.CreateScript(carrierGrid).WithMother().Boot();

            world.MergeBlocks(mergeBlockA, mergeBlockB);

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            
            script.RunToIdle();

            script.AssertEventEmitted<MergeBlockLockedEvent>();
            script.AssertEventEmitted<ConstructRefreshedEvent>();
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierMerged"));
            Assert.That(catalogue.ConstructGridIds, Has.Count.EqualTo(2));
            Assert.That(catalogue.GetBlocksByName<IMyShipMergeBlock>("MergeA"), Has.Count.EqualTo(1));
            Assert.That(catalogue.GetBlocksByName<IMyShipMergeBlock>("MergeB"), Has.Count.EqualTo(1));
        }

        [Test]
        public void Run_When_Merged_Grids_Unmerge_Emits_Event_Prunes_The_Construct_And_Defers_OnUnmerge_Hook()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var script = ScriptFactory().WithMother().Create();
            var primaryGrid = script.PrimaryGrid;
            var mergeBlock = script.ConnectGridsViaMergeBlock(script.PrimaryGrid, cargoGrid);

            mergeBlock.CustomData = new CustomDataComposer()
                .With("hooks", "onUnmerge", "rename CarrierDetached")
                .Build();

            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();

            script.ClearEventEmissions();

            mergeModule.UnlockMergeBlock(mergeBlock);
            catalogue.Run();
            script.RunToIdle();

            script.AssertEventEmitted<MergeBlockOffEvent>(2);
            script.AssertEventEmitted<ConstructRefreshedEvent>();
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierDetached"));
            Assert.That(primaryGrid.IsSameConstructAs(cargoGrid), Is.False);
            Assert.That(catalogue.ConstructGridIds, Has.Count.EqualTo(1));
        }

        [Test]
        public void Construct_Refresh_Registers_Newly_Discovered_Merge_Blocks_For_State_Monitoring()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var scoutGrid = GridFactory.Create("Scout Pod");
            var scoutBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "ScoutBattery");

            var script = ScriptFactory().WithMother().Create();
            var firstMergeBlock = script.AddUnmergedMergeBlockPair(script.PrimaryGrid, cargoGrid);
            var secondMergeBlock = script.AddUnmergedMergeBlockPair(cargoGrid, scoutGrid);

            script.WithBlock(scoutBattery, scoutGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();

            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("ScoutBattery"), Is.Empty);

            mergeModule.LockMergeBlock(firstMergeBlock);
            catalogue.Run();
            script.RunToIdle();

            script.ClearEventEmissions();

            mergeModule.LockMergeBlock(secondMergeBlock);
            catalogue.Run();
            script.RunToIdle();

            script.AssertEventEmitted<MergeBlockLockedEvent>();
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("ScoutBattery"), Has.Count.EqualTo(1));
        }
    }
}


