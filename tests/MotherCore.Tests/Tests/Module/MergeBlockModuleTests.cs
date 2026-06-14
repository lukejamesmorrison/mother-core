using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    [Category("Layer:Module")]
    public class MergeBlockModuleTests
    {
        sealed class MergeScriptArrangement
        {
            public MergeScriptArrangement(Script script, IMyShipMergeBlock mergeBlock)
            {
                Script = script;
                MergeBlock = mergeBlock;
            }

            public Script Script { get; private set; }

            public IMyShipMergeBlock MergeBlock { get; private set; }
        }

        static MergeScriptArrangement BootHookedMergeScript(
            string hookName,
            string hookAction,
            IMyCubeGrid cargoGrid,
            IMyBatteryBlock cargoBattery)
        {
            var script = new Script("Carrier");
            var mergeBlock = script.ConnectGridsViaMergeBlock(script.PrimaryGrid, cargoGrid);

            mergeBlock.CustomData = new CustomDataComposer()
                .With("hooks", hookName, hookAction)
                .Build();

            script.WithBlock(cargoBattery, cargoGrid)
                .Boot();

            return new MergeScriptArrangement(script, mergeBlock);
        }

        [Test]
        public void Boot_Subscribes_To_ConstructRefreshedEvent()
        {
            var script = new Script().Boot();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();
            var eventBus = script.Mother.GetModule<EventBus>();

            Assert.That(eventBus.IsSubscribed<ConstructRefreshedEvent>(mergeModule), Is.True);
        }

        [Test]
        public void Run_When_A_Merge_Block_Locks_Emits_Event_Refreshes_Construct_And_Defers_OnMerge_Hook()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var cargoBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "CargoBattery");

            var arrangement = BootHookedMergeScript("onMerge", "rename CarrierMerged", cargoGrid, cargoBattery);

            var script = arrangement.Script;
            var mergeBlock = arrangement.MergeBlock;

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();

            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("CargoBattery"), Is.Empty);

            mergeModule.LockMergeBlock(mergeBlock);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MergeBlockLockedEvent>();
            script.AssertEventEmitted<ConstructRefreshedEvent>();
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierMerged"));
            Assert.That(catalogue.ConstructGridIds, Has.Count.EqualTo(1));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("CargoBattery"), Has.Count.EqualTo(1));
        }

        [Test]
        public void Run_When_A_Merge_Block_Turns_Off_Emits_Event_Prunes_The_Construct_And_Defers_OnUnmerge_Hook()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var cargoBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "CargoBattery");

            var arrangement = BootHookedMergeScript("onUnmerge", "rename CarrierDetached", cargoGrid, cargoBattery);
            var script = arrangement.Script;
            var mergeBlock = arrangement.MergeBlock;

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();

            mergeModule.LockMergeBlock(mergeBlock);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.Mother.GetModule<EventBus>().Emissions.Clear();

            mergeModule.UnlockMergeBlock(mergeBlock);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MergeBlockOffEvent>(2);
            script.AssertEventEmitted<ConstructRefreshedEvent>();
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierDetached"));
            Assert.That(cargoBattery.CubeGrid.EntityId, Is.EqualTo(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("CargoBattery"), Is.Empty);
        }

        [Test]
        public void Construct_Refresh_Registers_Newly_Discovered_Merge_Blocks_For_State_Monitoring()
        {
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var scoutGrid = GridFactory.Create("Scout Pod");
            var scoutBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "ScoutBattery");

            var script = new Script("Carrier");
            var firstMergeBlock = script.ConnectGridsViaMergeBlock(script.PrimaryGrid, cargoGrid);
            var secondMergeBlock = script.ConnectGridsViaMergeBlock(cargoGrid, scoutGrid);

            script.WithBlock(scoutBattery, scoutGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mergeModule = script.Mother.GetModule<MergeBlockModule>();

            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("ScoutBattery"), Is.Empty);

            mergeModule.LockMergeBlock(firstMergeBlock);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.Mother.GetModule<EventBus>().Emissions.Clear();

            mergeModule.LockMergeBlock(secondMergeBlock);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MergeBlockLockedEvent>();
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("ScoutBattery"), Has.Count.EqualTo(1));
        }
    }
}