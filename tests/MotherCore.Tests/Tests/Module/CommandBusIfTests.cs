using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// Inline if / else / end steps handled by the CommandBus.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class CommandBusIfTests : TestBase
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

        static FakeBatteryBlock Battery(string name, double charge)
        {
            return new FakeBatteryBlock(customName: name)
            {
                CurrentStoredPower = (float)(charge * 100),
                MaxStoredPower = 100,
            };
        }

        static Script<CoreTestProgram> Boot(RecordingCommand rec, CustomDataComposer data, params IMyTerminalBlock[] blocks)
        {
            data.With("general", "ruleInterval", "100000");

            return ScriptFactory()
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(data.Build())
                .WithCommands(rec)
                .WithBlocks(blocks)
                .Boot();
        }

        static void Run(Script<CoreTestProgram> script, string routine)
        {
            script.RunTerminal(routine);
            script.RunToIdle();
        }

        static string Printed(Script<CoreTestProgram> script)
        {
            return string.Join("\n", script.Mother.GetModule<Log>().Records);
        }

        // --- Branches ---

        [Test]
        public void The_True_Branch_Runs_And_The_False_Branch_Is_Skipped()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            Run(script, "if \"Bat\" low; rec T; else; rec F; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T", "rec AFTER" }));
        }

        [Test]
        public void The_Else_Branch_Runs_When_The_Condition_Is_False()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; rec T; else; rec F; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec F", "rec AFTER" }));
        }

        [Test]
        public void Else_Is_Optional()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; rec T; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
        }

        [Test]
        public void Blocks_Nest_And_Each_If_Takes_One_End()
        {
            var rec = new RecordingCommand();
            var gear = new FakeLandingGear(customName: "Gear");
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1), gear);

            const string routine =
                "if \"Gear\" unlocked;" +
                "  if \"Bat\" low; rec LOW; else; rec OK; end;" +
                "  rec UNLOCKED;" +
                "else;" +
                "  rec LOCKED;" +
                "end;" +
                "rec DONE";

            Run(script, routine);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LOW", "rec UNLOCKED", "rec DONE" }));

            rec.Calls.Clear();
            gear.LockMode = LandingGearMode.Locked;
            Run(script, routine);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LOCKED", "rec DONE" }));
        }

        [Test]
        public void A_Skipped_Block_Skips_Its_Nested_Blocks_Without_Evaluating_Them()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            // The inner target does not exist, which would fail the routine if evaluated
            Run(script, "if \"Bat\" low; if \"Missing\" low; rec A; else; rec B; end; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
            Assert.That(Printed(script), Does.Not.Contain("Command failed"));
        }

        [Test]
        public void An_Else_Of_A_Skipped_Parent_Does_Not_Run()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; if \"Bat\" low; rec A; else; rec B; end; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
        }

        [Test]
        public void An_Open_Block_Is_Closed_At_The_End_Of_The_Routine()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; rec T");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Not.Contain("Command failed"));
        }

        [Test]
        public void An_Open_Block_In_A_Called_Command_Does_Not_Swallow_The_Callers_Steps()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer()
                .WithCommand("Inner", "if \"Bat\" low; rec IN")
                .WithCommand("Outer", "Inner; rec AFTER");
            var script = Boot(rec, data, Battery("Bat", 0.5));

            Run(script, "Outer");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
        }

        [Test]
        public void An_End_In_A_Called_Command_Cannot_Close_The_Callers_Block()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer().WithCommand("Closer", "end");
            var script = Boot(rec, data, Battery("Bat", 0.1));

            Run(script, "if \"Bat\" low; Closer; rec T; end");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("end without if"));
        }

        [Test]
        public void A_Command_Can_Contain_If_Blocks()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer().WithCommand("Guard", "if \"Bat\" low; rec LOW; else; rec OK; end");
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, data, battery);

            Run(script, "Guard");
            battery.CurrentStoredPower = 50;
            Run(script, "Guard");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LOW", "rec OK" }));
        }

        [Test]
        public void A_Wait_Inside_A_Branch_Blocks_The_Rest_Of_The_Branch()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            script.RunTerminal("if \"Bat\" low; wait 5; rec T; end; rec AFTER");
            script.Run();
            script.Run();

            Assert.That(rec.Calls, Is.Empty);

            var runtime = script.Mother.Program.Runtime as FakeGridProgramRuntimeInfo;
            runtime.TimeSinceLastRun = TimeSpan.FromSeconds(5.1);
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T", "rec AFTER" }));
        }

        [Test]
        public void Parallel_Groups_Each_Have_Their_Own_Blocks()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            Run(script, "{ if \"Bat\" low; rec A; else; rec B; } { rec C; }");

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec A", "rec C" }));
        }

        [Test]
        public void If_Works_Inside_A_Rule_Branch()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var gear = new FakeLandingGear(customName: "Gear");
            var data = new CustomDataComposer()
                .With("rules", "low", "when \"Bat\" low; if \"Gear\" locked; rec LOCKED; else; rec FLYING; end; rec TAIL; else; rec OK");
            var script = Boot(rec, data, battery, gear);

            // The rule loads one cycle after boot
            for (int i = 0; i < 20; i++)
                script.Mother.GetModule<Clock>().Run();

            script.Mother.GetModule<ConditionModule>().EvaluateRules();
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FLYING", "rec TAIL" }), "The else inside the if does not split the rule");

            battery.CurrentStoredPower = 50;
            script.Mother.GetModule<ConditionModule>().EvaluateRules();
            script.RunToIdle();

            Assert.That(rec.Calls.Last(), Is.EqualTo("rec OK"));
        }

        // --- Skipped steps ---

        [Test]
        public void Skipped_Steps_Do_Not_Run_Or_Resolve_Variables()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer()
                .WithVariable("A", "$B")
                .WithVariable("B", "$A");
            var battery = Battery("Bat", 0.5);
            var script = Boot(rec, data, battery);

            // $A is a cycle, which fails a step that resolves it
            Run(script, "if \"Bat\" low; rec $A; end; rec DONE");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec DONE" }));

            rec.Calls.Clear();
            battery.CurrentStoredPower = 10;
            Run(script, "if \"Bat\" low; rec $A; end; rec DONE");
            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("Command failed"));
        }

        [Test]
        public void A_Variable_In_The_Condition_Is_Resolved()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer().WithVariable("BAT", "Main Battery").WithVariable("LIMIT", "40%");
            var script = Boot(rec, data, Battery("Main Battery", 0.3));

            Run(script, "if \"$BAT\" charge < $LIMIT; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T" }));
        }

        // --- elseif ---

        [Test]
        [TestCase(0.05, "rec A")]
        [TestCase(0.15, "rec B")]
        [TestCase(0.5, "rec C")]
        public void The_First_True_Branch_Wins_And_Else_Runs_Only_When_Nothing_Matched(double charge, string expected)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", charge));

            Run(script, "if \"Bat\" charge < 10%; rec A; elseif \"Bat\" charge < 20%; rec B; else; rec C; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { expected, "rec AFTER" }));
        }

        [Test]
        public void An_Elseif_Chain_Takes_One_End_And_May_Have_Several_Elseifs()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" charge < 10%; rec A; elseif \"Bat\" charge < 20%; rec B; elseif \"Bat\" charge < 60%; rec C; elseif \"Bat\" charge < 90%; rec D; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec C", "rec AFTER" }));
        }

        [Test]
        public void Conditions_After_The_First_True_Branch_Are_Not_Evaluated()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            // The target does not exist, which would fail the routine if evaluated
            Run(script, "if \"Bat\" low; rec A; elseif \"Missing\" low; rec B; else; rec C; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec AFTER" }));
            Assert.That(Printed(script), Does.Not.Contain("Command failed"));
        }

        [Test]
        public void An_Elseif_In_A_Skipped_Parent_Is_Never_Evaluated_Or_Run()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; if \"Bat\" full; rec A; elseif \"Missing\" low; rec B; else; rec C; end; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
            Assert.That(Printed(script), Does.Not.Contain("Command failed"));
        }

        [Test]
        public void An_Elseif_Chain_Nests()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.15));

            Run(script, "if \"Bat\" critical; rec CRIT; elseif \"Bat\" low; if \"Bat\" charge < 18%; rec INNER; elseif \"Bat\" low; rec LOW; end; else; rec OK; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec INNER" }));
        }

        [Test]
        public void At_Overrides_A_Predicate_Threshold_On_If()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.3));

            Run(script, "if \"Bat\" low; rec DEFAULT; end; if \"Bat\" low --at=40%; rec AT; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AT" }));
        }

        // --- Text compare ---

        [Test]
        [TestCase("parked", "parked", "rec T")]
        [TestCase("parked", "takeoff", "rec F")]
        [TestCase("Parked", "PARKED", "rec T")]
        public void A_Text_Compare_With_Equals_Compares_Without_Case(string a, string b, string expected)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, $"if {a} == {b}; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { expected }));
        }

        [Test]
        [TestCase("parked", "parked", "rec F")]
        [TestCase("parked", "takeoff", "rec T")]
        public void A_Text_Compare_With_Not_Equals_Is_The_Opposite(string a, string b, string expected)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, $"if {a} != {b}; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void A_Text_Compare_Reads_A_Variable_Set_Earlier_In_The_Routine()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer().WithVariable("MODE", "off");
            var script = Boot(rec, data, Battery("Bat", 0.5));

            Run(script, "var/set MODE parked; if $MODE == parked; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T" }));
        }

        [Test]
        public void A_Text_Compare_Works_In_An_Elseif_Chain()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer().WithVariable("MODE", "landing");
            var script = Boot(rec, data, Battery("Bat", 0.5));

            Run(script, "if $MODE == parked; rec A; elseif $MODE == landing; rec B; elseif $MODE == off; rec C; else; rec D; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec B", "rec AFTER" }));
        }

        [Test]
        public void A_Text_Compare_Of_Quoted_Values_With_Spaces_Compares_The_Text()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"cruise atmo\" == \"Cruise Atmo\"; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T" }));
        }

        [Test]
        public void An_Undefined_Variable_In_A_Text_Compare_Stays_Literal_And_Does_Not_Match()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if $NOPE == parked; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec F" }));
        }

        [Test]
        public void A_Text_Compare_In_A_Skipped_Block_Is_Not_Evaluated()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.5));

            Run(script, "if \"Bat\" low; if a == a; rec A; else; rec B; end; end; rec AFTER");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
        }

        [Test]
        public void A_Numeric_Compare_With_A_Block_Still_Uses_The_Property_Form()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            Run(script, "if \"Bat\" charge < 20%; rec T; else; rec F; end");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec T" }));
        }

        // --- Failures ---

        [Test]
        [TestCase("if a == b --for=3; rec X; end", "bad syntax")]
        [TestCase("if a ~ b; rec X; end", "bad syntax")]
        [TestCase("if a < b; rec X; end", "bad syntax")]
        public void A_Three_Argument_If_That_Is_Not_A_Text_Compare_Still_Fails(string routine, string reason)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            Run(script, routine + "; rec AFTER");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("Command failed").And.Contain(reason));
        }

        [Test]
        [TestCase("else; rec X", "else without if")]
        [TestCase("end; rec X", "end without if")]
        [TestCase("if \"Bat\" low; else; else; rec X; end", "else after else")]
        [TestCase("if \"Bat\" bogus; rec X; end", "unknown predicate: bogus")]
        [TestCase("if \"Bat\" bogus < 1; rec X; end", "unknown property: bogus")]
        [TestCase("if \"Nope\" low; rec X; end", "no target: Nope")]
        [TestCase("if \"Bat\" charge ~ 1; rec X; end", "unknown operator: ~")]
        [TestCase("if \"Bat\" low --for=3; rec X; end", "--for only works on a when rule")]
        [TestCase("if \"Bat\" low --buffer=3; rec X; end", "--buffer only works on a when rule")]
        [TestCase("if \"Bat\" low --cooldown=3; rec X; end", "--cooldown only works on a when rule")]
        [TestCase("if \"Bat\" low --every=3; rec X; end", "--every only works on a when rule")]
        [TestCase("if \"Bat\" low --bogus=3; rec X; end", "bad syntax")]
        [TestCase("if \"Bat\" charge < 20% --at=25%; rec X; end", "--at needs a predicate")]
        [TestCase("if \"Bat\" low --at=lots; rec X; end", "bad option: --at")]
        [TestCase("elseif \"Bat\" low; rec X; end", "elseif without if")]
        [TestCase("if \"Bat\" low; else; elseif \"Bat\" low; rec X; end", "elseif after else")]
        [TestCase("if \"Bat\" full; elseif \"Bat\" bogus; rec X; end", "unknown predicate: bogus")]
        [TestCase("if; rec X; end", "bad syntax")]
        public void A_Bad_Step_Fails_And_Stops_The_Routine(string routine, string reason)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", 0.1));

            Run(script, routine + "; rec AFTER");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("Command failed").And.Contain(reason));
        }

        // --- Shared with when ---

        [Test]
        [TestCase(0.1)]
        [TestCase(0.5)]
        public void If_And_When_Give_The_Same_Result_For_The_Same_Condition(double charge)
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer(), Battery("Bat", charge));
            var module = script.Mother.GetModule<ConditionModule>();
            string error;

            foreach (var condition in new[] { "\"Bat\" low", "\"Bat\" charge < 20%", "\"Bat\" critical" })
            {
                bool asIf = module.Evaluate(new TerminalCommand("if " + condition), out error);
                Assert.That(error, Is.Null);

                bool asWhen = module.Evaluate(new TerminalCommand("when " + condition), out error);
                Assert.That(error, Is.Null);

                Assert.That(asIf, Is.EqualTo(asWhen), condition);
            }
        }

        // --- Examples from the spec ---

        [Test]
        public void Toggle_Flips_The_Block()
        {
            var rec = new RecordingCommand();
            var light = TerminalBlockFactory.Create<IMyLightingBlock>(customName: "Light");
            var data = new CustomDataComposer().WithCommand("Toggle", "if \"Light\" on; rec OFF; else; rec ON; end");
            var script = Boot(rec, data, light);

            Run(script, "Toggle");
            ((FakeTerminalBlock)light).Enabled = false;
            Run(script, "Toggle");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec OFF", "rec ON" }));
        }

        [Test]
        public void Shutdown_Refuses_Unless_The_Gear_Is_Locked()
        {
            var rec = new RecordingCommand();
            var gear = new FakeLandingGear(customName: "Gear");
            var data = new CustomDataComposer().WithCommand(
                "shutdown",
                "if \"Gear\" locked; rec STOCKPILE; rec OFF; rec DONE; else; rec REFUSED; end");
            var script = Boot(rec, data, gear);

            Run(script, "shutdown");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec REFUSED" }));

            rec.Calls.Clear();
            gear.LockMode = LandingGearMode.Locked;
            Run(script, "shutdown");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec STOCKPILE", "rec OFF", "rec DONE" }));
        }

        [Test]
        public void Start_Undocks_First_Only_When_Connected()
        {
            var rec = new RecordingCommand();
            var connector = new FakeShipConnector(customName: "Connector");
            var status = MyShipConnectorStatus.Connected;
            connector.Configure(() => status, null, () => { }, () => { }, () => { });
            var data = new CustomDataComposer().WithCommand(
                "start",
                "if \"Connector\" connected; rec UNDOCK; wait 1; end; rec BATTERIES; rec STARTED");
            var script = Boot(rec, data, connector);

            Run(script, "start");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec UNDOCK", "rec BATTERIES", "rec STARTED" }));

            rec.Calls.Clear();
            status = MyShipConnectorStatus.Unconnected;
            Run(script, "start");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec BATTERIES", "rec STARTED" }));
        }

        [Test]
        public void Land_Walks_The_Nested_Outcomes()
        {
            var rec = new RecordingCommand();
            var gear = new FakeLandingGear(customName: "Gear");
            var cockpit = TerminalBlockFactory.Create<IMyCockpit>(customName: "Cockpit");
            var tank = new FakeGasTank(customName: "Tank") { FilledRatio = 0.5 };
            var data = new CustomDataComposer().WithCommand(
                "Land",
                "if \"Gear\" unlocked;" +
                "  if grid altitude < 100;" +
                "    rec LIGHTS;" +
                "    if \"Tank\" low; rec H2_LOW; end;" +
                "  else;" +
                "    rec DESCEND;" +
                "  end;" +
                "else;" +
                "  rec GEAR_LOCKED;" +
                "end");
            var script = Boot(rec, data, gear, cockpit, tank);

            ((FakeCockpit)cockpit).PlanetElevation = 50;
            Run(script, "Land");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LIGHTS" }));

            rec.Calls.Clear();
            tank.FilledRatio = 0.05;
            Run(script, "Land");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec LIGHTS", "rec H2_LOW" }));

            rec.Calls.Clear();
            ((FakeCockpit)cockpit).PlanetElevation = 500;
            Run(script, "Land");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec DESCEND" }));

            rec.Calls.Clear();
            gear.LockMode = LandingGearMode.Locked;
            Run(script, "Land");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec GEAR_LOCKED" }));
        }
    }
}
