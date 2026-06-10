using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    public class ConnectorModuleTests
    {
        [Test]
        public void Run_When_A_Connector_Locks_Emits_Event_Runs_Hook_And_Does_Not_Add_The_Remote_Grid_To_The_Construct()
        {
            var tracker = new CommandSpy("probe");
            var primaryGrid = GridFactory.Create("Carrier");
            var dockedGrid = GridFactory.Create("Shuttle");
            var shuttleBattery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Shuttle Battery");

            var script = new Script(primaryGrid, "Carrier");
            var connector = script.ConnectGridsViaConnector(
                primaryGrid,
                dockedGrid,
                baseConnectorName: "CarrierDock",
                otherConnectorName: "ShuttleDock",
                initialStatus: MyShipConnectorStatus.Connectable);

            connector.CustomData = new CustomDataComposer()
                .With("hooks", "onLock", "probe")
                .Build();

            script
                .WithCommands(tracker)
                .WithBlock(shuttleBattery, dockedGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            connector.Connect();

            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<ConnectorLockedEvent>();
            script.AssertCommandExecuted(tracker);
            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(dockedGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Shuttle Battery"), Is.Empty);
        }

        [Test]
        public void Run_When_A_Connector_Unlocks_Emits_Event_Runs_Hook_And_Does_Not_Prune_The_Local_Construct()
        {
            var tracker = new CommandSpy("probe");
            var primaryGrid = GridFactory.Create("Carrier");
            var dockedGrid = GridFactory.Create("Shuttle");

            var script = new Script(primaryGrid, "Carrier");
            var connector = script.ConnectGridsViaConnector(
                primaryGrid,
                dockedGrid,
                baseConnectorName: "CarrierDock",
                otherConnectorName: "ShuttleDock");

            connector.CustomData = new CustomDataComposer()
                .With("hooks", "onUnlock", "probe")
                .Build();

            script.WithCommands(tracker)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            connector.Disconnect();
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<ConnectorUnlockedEvent>();
            script.AssertCommandExecuted(tracker);
            Assert.That(catalogue.ConstructGridIds, Is.EquivalentTo(new[] { primaryGrid.EntityId }));
        }

        [Test]
        public void Run_When_A_Connector_Becomes_Ready_Emits_Event_And_Runs_Hook()
        {
            var tracker = new CommandSpy("probe");
            var primaryGrid = GridFactory.Create("Carrier");
            var dockedGrid = GridFactory.Create("Shuttle");

            var script = new Script(primaryGrid, "Carrier");
            var connector = script.ConnectGridsViaConnector(
                primaryGrid,
                dockedGrid,
                baseConnectorName: "CarrierDock",
                otherConnectorName: "ShuttleDock",
                initialStatus: MyShipConnectorStatus.Unconnected);

            connector.CustomData = new CustomDataComposer()
                .With("hooks", "onReady", "probe")
                .Build();

            script.WithCommands(tracker)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            ConnectorConnectionFactory.SetStatus(connector, MyShipConnectorStatus.Connectable);
            catalogue.Run();
            script.Clock.RunToIdle(50);

            script.AssertEventEmitted<ConnectorReadyToLockEvent>();
            script.AssertCommandExecuted(tracker);
        }
    }
}