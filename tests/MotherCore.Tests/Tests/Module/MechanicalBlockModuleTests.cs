using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    [Category("Layer:Module")]
    public class MechanicalBlockModuleTests
    {
        [Test]
        public void Run_When_A_Mechanical_Block_Detaches_Emits_Event_Runs_Hook_And_Prunes_The_Construct()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            var script = new Script(primaryGrid, "Carrier");

            var connection = script.ConnectGrids(primaryGrid, cargoGrid);

            connection.CustomData = new CustomDataComposer()
                .With("hooks", "onDetach", "rename CarrierDetached")
                .Build();

            script
                .WithBlock(battery, cargoGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            connection.Detach();
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MechanicalBlockDetachedEvent>();
            script.AssertEventEmitted<MechanicalBlockDetachedEvent>(catalogue);
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierDetached"));
            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);
        }

        [Test]
        public void Run_When_A_Mechanical_Block_Attaches_Emits_Event_Runs_Hook_And_Adds_The_Grid_To_The_Construct()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            var script = new Script(primaryGrid, "Carrier");
            var connection = script.ConnectGrids(primaryGrid, cargoGrid);
            connection.CustomData = new CustomDataComposer()
                .With("hooks", "onAttach", "rename CarrierAttached")
                .Build();

            script
                .WithBlock(battery, cargoGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            connection.Detach();
            catalogue.Run();
            script.Clock.RunToIdle(50);

            connection.Attach();
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MechanicalBlockAttachedEvent>();
            script.AssertEventEmitted<MechanicalBlockAttachedEvent>(catalogue);
            script.AssertCommandExecuted("rename");
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierAttached"));
            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));
        }

        [Test]
        public void Construct_Refresh_Registers_Newly_Discovered_Mechanical_Blocks_For_State_Monitoring()
        {
            var primaryGrid = GridFactory.Create("Carrier");
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var scoutGrid = GridFactory.Create("Scout Pod");

            var droneGrid = GridFactory.Create("Drone Pod");
            var droneBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Drone Battery");

            var script = new Script(primaryGrid, "Carrier");
            script.ConnectGrids(primaryGrid, cargoGrid);
            script.Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            /// Attach the scout grid with a mechanical block, then attach the drone grid to the scout grid with another mechanical block. 
            var nestedConnection = script.ConnectGrids(scoutGrid, droneGrid);
            script.WithBlock(droneBattery, droneGrid);
            script.ConnectGrids(cargoGrid, scoutGrid);

            catalogue.OnMechanicalBlockAttached(scoutGrid);
            script.Clock.RunToIdle(50);

            /// The catalogue should discover both mechanical blocks and register them for state monitoring, resulting in the drone 
            /// battery being tracked as well. When the drone grid is detached, the catalogue should prune it from the 
            /// construct and stop tracking the battery.
            Assert.That(catalogue.ConstructGridIds, Contains.Item(scoutGrid.EntityId));
            Assert.That(catalogue.ConstructGridIds, Contains.Item(droneGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Drone Battery"), Has.Count.EqualTo(1));

            script.Mother.GetModule<EventBus>().Emissions.Clear();

            nestedConnection.Detach();
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<MechanicalBlockDetachedEvent>();
            Assert.That(catalogue.ConstructGridIds, Contains.Item(scoutGrid.EntityId));
            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(droneGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Drone Battery"), Is.Empty);
        }
    }
}