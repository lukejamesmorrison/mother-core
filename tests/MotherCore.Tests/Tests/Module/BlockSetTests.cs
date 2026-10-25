using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// The <c>Block&lt;T&gt;()</c> registration helpers: Command, Number, Flag, Status, Monitor and On.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class BlockSetTests : TestBase
    {
        class RecordingCommand : BaseModuleCommand
        {
            public readonly List<string> Calls = new List<string>();

            public override string Name => "rec";

            public override string Execute(TerminalCommand command)
            {
                Calls.Add(command.CommandString);
                return string.Empty;
            }
        }

        /// <summary>
        /// Boots a module whose Boot() is supplied by the test.
        /// </summary>
        class ProbeModule : BaseExtensionModule
        {
            readonly Action<ProbeModule> _boot;

            public ProbeModule(Mother mother, Action<ProbeModule> boot) : base(mother)
            {
                _boot = boot;
            }

            public override void Boot()
            {
                _boot(this);
            }
        }

        class OpenedEvent : IEvent { }
        class ClosedEvent : IEvent { }
        class DetectedEvent : IEvent { }
        class ClearedEvent : IEvent { }

        static Script<CoreTestProgram> Boot(RecordingCommand rec, Action<ProbeModule> register, params IMyTerminalBlock[] blocks)
        {
            return ScriptFactory()
                .WithMother()
                .WithModules(m => new ProbeModule(m, register))
                .WithCommands(rec)
                .WithBlocks(blocks)
                .Boot();
        }

        static IMyDoor Door(string name, DoorStatus status = DoorStatus.Closed, string hooks = null)
        {
            var customData = hooks == null
                ? ""
                : new CustomDataComposer().With("hooks", hooks.Split('=')[0], hooks.Split('=')[1]).Build();

            return TerminalBlockFactory.Create<IMyDoor>(
                customName: name,
                customData: customData,
                configure: d => ((FakeDoor)d).Status = status);
        }

        static void Poll(Script<CoreTestProgram> script)
        {
            script.Mother.GetModule<BlockCatalogue>().Run();
            script.RunToIdle();
        }

        // --- Block ---

        [Test]
        public void Block_Returns_A_New_Set_Each_Time()
        {
            BlockSet<IMyDoor> first = null, second = null;

            Boot(new RecordingCommand(), m =>
            {
                first = m.Block<IMyDoor>("door");
                second = m.Block<IMyDoor>("door");
            });

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first));
        }

        // --- Command ---

        [Test]
        public void Command_Registers_The_Prefixed_Name_And_Applies_The_Action_To_Matching_Blocks_Only()
        {
            var airlock = Door("Airlock");
            var hangar = Door("Hangar");
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>("door").Command("open", d => d.OpenDoor(), "Opened {0}"),
                airlock, hangar);

            script.RunTerminal("door/open Airlock");

            Assert.That(airlock.Status, Is.EqualTo(DoorStatus.Open));
            Assert.That(hangar.Status, Is.EqualTo(DoorStatus.Closed));
            script.AssertCommandExecuted("door/open");
        }

        [Test]
        public void Command_Responds_With_The_Message_Formatted_With_The_Block_Name()
        {
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>("door").Command("open", d => d.OpenDoor(), "Opened {0}"),
                Door("Airlock"));

            script.RunTerminal("door/open Airlock");

            script.AssertPrinted("Opened Airlock");
        }

        [Test]
        public void Command_Without_A_Prefix_Uses_The_Verb_As_The_Whole_Name()
        {
            var airlock = Door("Airlock");
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>().Command("opendoor", d => d.OpenDoor(), "Opened {0}"),
                airlock);

            script.RunTerminal("opendoor Airlock");

            Assert.That(airlock.Status, Is.EqualTo(DoorStatus.Open));
            script.AssertCommandExecuted("opendoor");
        }

        [Test]
        public void Command_Registers_Every_Verb_Chained_On_The_Set()
        {
            var airlock = Door("Airlock");
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>("door")
                    .Command("open", d => d.OpenDoor(), "Opened {0}")
                    .Command("close", d => d.CloseDoor(), "Closed {0}"),
                airlock);

            script.RunTerminal("door/open Airlock");
            Assert.That(airlock.Status, Is.EqualTo(DoorStatus.Open));

            script.RunTerminal("door/close Airlock");
            Assert.That(airlock.Status, Is.EqualTo(DoorStatus.Closed));
        }

        [Test]
        public void Command_Reports_A_Block_That_Was_Not_Found()
        {
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>("door").Command("open", d => d.OpenDoor(), "Opened {0}"),
                Door("Airlock"));

            script.RunTerminal("door/open Nowhere");

            script.AssertPrinted("Nowhere");
        }

        [Test]
        public void Every_Method_Returns_The_Same_Set_For_Chaining()
        {
            var results = new List<object>();
            BlockSet<IMyDoor> set = null;

            Boot(new RecordingCommand(), m =>
            {
                set = m.Block<IMyDoor>("door");
                results.Add(set.Command("open", d => d.OpenDoor(), "Opened {0}"));
                results.Add(set.Number("ratio", d => d.OpenRatio));
                results.Add(set.Flag("isopen", d => d.Open));
                results.Add(set.Status("status", d => d.Status));
                results.Add(set.Monitor(d => d.Status));
                results.Add(set.Monitor(d => d.Status, (d, s) => { }));
                results.Add(set.On<OpenedEvent>(DoorStatus.Open));
            });

            Assert.That(results.Count, Is.EqualTo(7));
            Assert.That(results, Has.All.SameAs(set));
        }

        // --- Number, Flag and Status ---

        [Test]
        public void Number_Makes_A_Property_Readable_In_Conditions()
        {
            var rec = new RecordingCommand();
            var battery = new FakeBatteryBlock(customName: "Bat") { CurrentStoredPower = 10, MaxStoredPower = 100 };
            var script = Boot(rec,
                m => m.Block<IMyBatteryBlock>().Number("charge", b => b.CurrentStoredPower / b.MaxStoredPower),
                battery);

            script.RunTerminal("if \"Bat\" charge < 20%; rec LOW; else; rec OK; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LOW" }));

            rec.Calls.Clear();
            battery.CurrentStoredPower = 50;
            script.RunTerminal("if \"Bat\" charge < 20%; rec LOW; else; rec OK; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec OK" }));
        }

        [Test]
        public void Flag_Makes_A_Boolean_Property_Readable_And_Names_Its_Predicates()
        {
            var rec = new RecordingCommand();
            var sensor = TerminalBlockFactory.Create<IMySensorBlock>(customName: "Sensor");
            var script = Boot(rec,
                m => m.Block<IMySensorBlock>().Flag("detected", b => b.IsActive, "detected", "clear"),
                sensor);

            script.RunTerminal("if \"Sensor\" clear; rec CLEAR; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec CLEAR" }));

            rec.Calls.Clear();
            ((FakeSensorBlock)sensor).IsActive = true;
            script.RunTerminal("if \"Sensor\" detected; rec DETECTED; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec DETECTED" }));
        }

        [Test]
        public void Status_Makes_An_Enum_Readable_By_Its_Value_Names()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock");
            var script = Boot(rec,
                m => m.Block<IMyDoor>().Status("status", d => d.Status),
                airlock);

            script.RunTerminal("if \"Airlock\" status == open; rec OPEN; else; rec SHUT; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec SHUT" }));

            rec.Calls.Clear();
            airlock.OpenDoor();
            script.RunTerminal("if \"Airlock\" status == open; rec OPEN; else; rec SHUT; end");
            script.RunToIdle();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec OPEN" }));
        }

        // --- Monitor with a handler ---

        [Test]
        public void Monitor_Calls_The_Handler_With_The_Typed_Block_And_New_State_When_The_State_Changes()
        {
            var airlock = Door("Airlock");
            var calls = new List<KeyValuePair<IMyDoor, object>>();
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>().Monitor(d => d.Status, (d, state) => calls.Add(new KeyValuePair<IMyDoor, object>(d, state))),
                airlock);

            Poll(script);
            Assert.That(calls, Is.Empty, "The initial state is not a change");

            ((FakeDoor)airlock).Status = DoorStatus.Open;
            Poll(script);

            Assert.That(calls.Count, Is.EqualTo(1));
            Assert.That(calls[0].Key, Is.SameAs(airlock));
            Assert.That(calls[0].Value, Is.EqualTo(DoorStatus.Open));

            Poll(script);
            Assert.That(calls.Count, Is.EqualTo(1), "An unchanged state does not call the handler again");
        }

        [Test]
        public void Monitor_Watches_Every_Block_Of_The_Type()
        {
            var airlock = Door("Airlock");
            var hangar = Door("Hangar");
            var changed = new List<string>();
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>().Monitor(d => d.Status, (d, state) => changed.Add(d.CustomName)),
                airlock, hangar);

            ((FakeDoor)hangar).Status = DoorStatus.Open;
            ((FakeDoor)airlock).Status = DoorStatus.Open;
            Poll(script);

            Assert.That(changed, Is.EquivalentTo(new[] { "Airlock", "Hangar" }));
        }

        // --- Monitor and On ---

        [Test]
        public void On_Emits_The_Event_And_Runs_The_Hook_Named_After_The_State_By_Default()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock", hooks: "onOpen=rec OPENED");
            var script = Boot(rec,
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<OpenedEvent>(DoorStatus.Open),
                airlock);

            ((FakeDoor)airlock).Status = DoorStatus.Open;
            Poll(script);

            script.AssertEventEmitted<OpenedEvent>();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec OPENED" }));
        }

        [Test]
        public void On_Runs_The_Explicit_Hook_Name_Instead_Of_The_Default()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock", DoorStatus.Open, hooks: "onClose=rec CLOSED");
            var script = Boot(rec,
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<ClosedEvent>(DoorStatus.Closed, "onClose"),
                airlock);

            ((FakeDoor)airlock).Status = DoorStatus.Closed;
            Poll(script);

            script.AssertEventEmitted<ClosedEvent>();
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec CLOSED" }));
        }

        [Test]
        public void On_Dispatches_Each_Declared_State_To_Its_Own_Event()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock");
            var script = Boot(rec,
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<OpenedEvent>(DoorStatus.Open)
                    .On<ClosedEvent>(DoorStatus.Closed),
                airlock);

            ((FakeDoor)airlock).Status = DoorStatus.Open;
            Poll(script);
            script.AssertEventEmitted<OpenedEvent>(1);
            script.AssertEventEmitted<ClosedEvent>(0);

            ((FakeDoor)airlock).Status = DoorStatus.Closed;
            Poll(script);
            script.AssertEventEmitted<OpenedEvent>(1);
            script.AssertEventEmitted<ClosedEvent>(1);
        }

        [Test]
        public void On_Ignores_A_State_That_Was_Not_Declared()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock", hooks: "onOpening=rec OPENING");
            var script = Boot(rec,
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<OpenedEvent>(DoorStatus.Open),
                airlock);

            ((FakeDoor)airlock).Status = DoorStatus.Opening;
            Poll(script);

            script.AssertEventEmitted<OpenedEvent>(0);
            Assert.That(rec.Calls, Is.Empty);
        }

        [Test]
        public void On_Matches_Boolean_States()
        {
            var rec = new RecordingCommand();
            var sensor = TerminalBlockFactory.Create<IMySensorBlock>(
                customName: "Sensor",
                customData: new CustomDataComposer()
                    .With("hooks", "onDetect", "rec DETECT")
                    .With("hooks", "onClear", "rec CLEAR")
                    .Build());
            var script = Boot(rec,
                m => m.Block<IMySensorBlock>()
                    .Monitor(s => s.IsActive)
                    .On<DetectedEvent>(true, "onDetect")
                    .On<ClearedEvent>(false, "onClear"),
                sensor);

            ((FakeSensorBlock)sensor).IsActive = true;
            Poll(script);
            script.AssertEventEmitted<DetectedEvent>(1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec DETECT" }));

            ((FakeSensorBlock)sensor).IsActive = false;
            Poll(script);
            script.AssertEventEmitted<ClearedEvent>(1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec DETECT", "rec CLEAR" }));
        }

        [Test]
        public void On_Runs_The_Extra_Action_With_The_Block_After_The_Event()
        {
            var airlock = Door("Airlock");
            IMyDoor seen = null;
            var script = Boot(new RecordingCommand(),
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<OpenedEvent>(DoorStatus.Open, null, d => seen = d),
                airlock);

            ((FakeDoor)airlock).Status = DoorStatus.Open;
            Poll(script);

            Assert.That(seen, Is.SameAs(airlock));
            script.AssertEventEmitted<OpenedEvent>();
        }

        [Test]
        public void On_Does_Not_Fire_Until_The_State_Changes()
        {
            var rec = new RecordingCommand();
            var airlock = Door("Airlock", DoorStatus.Open, hooks: "onOpen=rec OPENED");
            var script = Boot(rec,
                m => m.Block<IMyDoor>()
                    .Monitor(d => d.Status)
                    .On<OpenedEvent>(DoorStatus.Open),
                airlock);

            Poll(script);

            script.AssertEventEmitted<OpenedEvent>(0);
            Assert.That(rec.Calls, Is.Empty);
        }
    }
}
