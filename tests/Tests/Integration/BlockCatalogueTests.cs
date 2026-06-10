using FakeItEasy;
using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Integration
{
    public class BlockCatalogueTests
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
        public void OnMechanicalBlockAttached_Adds_Newly_Connected_Grid_Blocks_To_The_Construct()
        {
            var script = new Script("Carrier").Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var eventBus = script.Mother.GetModule<EventBus>();
            var observer = A.Fake<IModule>();

            eventBus.Subscribe<ConstructRefreshedEvent>(observer);

            var cargoGrid = script.CreateGrid("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");
            script.WithBlock(battery, cargoGrid);

            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);

            catalogue.OnMechanicalBlockAttached(cargoGrid);
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));
            A.CallTo(() => observer.HandleEvent(A<ConstructRefreshedEvent>._, null))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public void OnMechanicalBlockDetached_Prunes_Disconnected_Grid_Blocks_From_The_Construct()
        {
            var script = new Script("Carrier");
            var cargoGrid = script.CreateGrid("Cargo Pod");
            var battery = TerminalBlockFactory.Create<IMyBatteryBlock>(customName: "Cargo Battery");

            script.WithBlock(battery, cargoGrid)
                .Boot();

            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var mechanicalBlocks = new List<IMyMechanicalConnectionBlock>();

            script.Mother.GridTerminalSystem.GetBlocksOfType(mechanicalBlocks);

            var connection = mechanicalBlocks.Single(block => block.TopGrid.EntityId == cargoGrid.EntityId);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Has.Count.EqualTo(1));

            connection.Detach();
            catalogue.OnMechanicalBlockDetached();
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Does.Not.Contain(cargoGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyBatteryBlock>("Cargo Battery"), Is.Empty);
        }

        [Test]
        public void RefreshConstruct_Adds_New_Connected_Grid_And_Emits_ConstructRefreshedEvent()
        {
            var script = new Script("Carrier").Boot();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();
            var eventBus = script.Mother.GetModule<EventBus>();
            var observer = A.Fake<IModule>();

            eventBus.Subscribe<ConstructRefreshedEvent>(observer);

            var scoutGrid = script.CreateGrid("Scout Pod");
            var reactor = TerminalBlockFactory.Create<IMyReactor>(customName: "Scout Reactor");
            script.WithBlock(reactor, scoutGrid);

            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Is.Empty);

            catalogue.RefreshConstruct();
            script.Clock.RunToIdle(50);

            Assert.That(catalogue.ConstructGridIds, Contains.Item(scoutGrid.EntityId));
            Assert.That(catalogue.GetBlocksByName<IMyReactor>("Scout Reactor"), Has.Count.EqualTo(1));
            A.CallTo(() => observer.HandleEvent(A<ConstructRefreshedEvent>._, null))
                .MustHaveHappenedOnceExactly();
        }
    }
}