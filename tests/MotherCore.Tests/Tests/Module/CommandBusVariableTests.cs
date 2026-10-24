using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// Variables are resolved when each step runs, not when the routine is entered.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class CommandBusVariableTests : TestBase
    {
        /// <summary>
        /// Records every execution so tests can see the resolved command.
        /// </summary>
        class RecordingCommand : BaseModuleCommand
        {
            public readonly List<string> Calls = new List<string>();
            public readonly List<List<string>> Arguments = new List<List<string>>();

            public override string Name => "rec";

            public override string Execute(TerminalCommand command)
            {
                Calls.Add(command.CommandString);
                Arguments.Add(command.Arguments.ToList());
                return string.Empty;
            }
        }

        static Script<CoreTestProgram> Boot(RecordingCommand recorder, CustomDataComposer composer = null)
        {
            return ScriptFactory()
                .WithMother()
                .WithCustomData((composer ?? new CustomDataComposer()).Build())
                .WithCommands(recorder)
                .Boot();
        }

        static Script<CoreTestProgram> Run(Script<CoreTestProgram> script, string routine)
        {
            script.RunTerminal(routine);
            script.RunToIdle();
            return script;
        }

        static string Printed(Script<CoreTestProgram> script)
        {
            // The capture was created at boot and has seen every terminal refresh since
            return string.Join("\n", script.CaptureEcho().Lines);
        }

        // --- Set then use ---

        [Test]
        public void A_Variable_Set_Mid_Routine_Is_Seen_By_A_Later_Step_When_Already_Defined()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("X", "old"));

            Run(script, "var/set X new; rec $X");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec new" }));
        }

        [Test]
        public void A_Variable_Set_Mid_Routine_Is_Seen_By_A_Later_Step_When_Undefined()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec);

            Run(script, "rec $Y; var/set Y 5; rec $Y");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec $Y", "rec 5" }));
        }

        [Test]
        public void A_Variable_Set_Mid_Config_Command_Is_Seen_By_A_Later_Step_In_The_Body()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("X", "old")
                .WithCommand("Show", "var/set X 5; rec $X"));

            Run(script, "Show");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec 5" }));
        }

        [Test]
        public void A_Variable_Is_Resolved_After_A_Wait()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("X", "old"));

            script.RunTerminal("wait 5; var/set X new; rec $X");
            script.Run();
            script.Run();

            Assert.That(rec.Calls, Is.Empty);

            var runtime = script.Mother.Program.Runtime as FakeGridProgramRuntimeInfo;
            runtime.TimeSinceLastRun = TimeSpan.FromSeconds(5.1);
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec new" }));
        }

        // --- Argument shape ---

        [Test]
        public void A_Quoted_Value_With_Spaces_Stays_One_Argument()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("LCD", "Cockpit [LCD]"));

            Run(script, "rec \"$LCD\" next");

            Assert.That(rec.Arguments.Single(), Is.EqualTo(new[] { "Cockpit [LCD]", "next" }));
        }

        [Test]
        public void An_Undefined_Variable_Stays_Literal()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec);

            Run(script, "rec $NOPE");

            Assert.That(rec.Arguments.Single(), Is.EqualTo(new[] { "$NOPE" }));
        }

        [Test]
        public void A_Variable_In_An_Option_Value_Is_Resolved()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("LEVEL", "high"));

            Run(script, "rec a --level=$LEVEL");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec a --level=high" }));
        }

        [Test]
        public void A_Variable_Can_Be_The_Command_Name()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("CMD", "rec"));

            Run(script, "$CMD hello");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec hello" }));
        }

        [Test]
        public void A_Variable_Can_Be_The_Wait_Duration()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("DELAY", "2"));

            script.RunTerminal("wait $DELAY; rec after");
            script.Run();
            script.Run();

            Assert.That(rec.Calls, Is.Empty, "The wait should still be blocking.");

            var runtime = script.Mother.Program.Runtime as FakeGridProgramRuntimeInfo;
            runtime.TimeSinceLastRun = TimeSpan.FromSeconds(2.1);
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec after" }));
        }

        // --- Config command parameters ---

        [Test]
        public void A_Config_Parameter_Default_Can_Hold_A_Variable()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("VAR", "hi")
                .WithCommand("Say", "rec {{p:$VAR}}"));

            Run(script, "Say");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec hi" }));
        }

        [Test]
        public void A_Config_Parameter_Option_Overrides_The_Variable_Default()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("VAR", "hi")
                .WithCommand("Say", "rec {{p:$VAR}}"));

            Run(script, "Say --p=yo");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec yo" }));
        }

        // --- Parallel groups ---

        [Test]
        public void A_Parallel_Group_Resolves_Variables()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("X", "1"));

            Run(script, "{ rec $X; } { var/set Z 9; rec $Z; }");

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec 1", "rec 9" }));
        }

        // --- Blank and multi-step values ---

        [Test]
        public void A_Step_That_Resolves_To_Nothing_Is_A_Silent_No_Op()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("EMPTY", ""));

            Run(script, "$EMPTY; rec next");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec next" }));

            string output = Printed(script);
            Assert.That(output, Does.Not.Contain("Command not found"));
            Assert.That(output, Does.Not.Contain("Command failed"));
        }

        [Test]
        public void A_Step_With_A_Trailing_Separator_And_An_Empty_Variable_Is_A_No_Op()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("EMPTY", "")
                .WithCommand("Next", "$EMPTY;"));

            Run(script, "Next; rec after");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec after" }));
        }

        [Test]
        public void The_Maybe_Pattern_Runs_Or_Skips_A_Command_And_Switches_At_Runtime()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("X", "Yes")
                .WithCommand("Maybe", "$X;")
                .WithCommand("Yes", "rec ran")
                .WithCommand("No", ""));

            Run(script, "Maybe");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ran" }));

            Run(script, "var/set X No; Maybe");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ran" }), "X=No should do nothing.");

            Run(script, "var/set X Yes; Maybe");
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ran", "rec ran" }));
        }

        [Test]
        public void A_Value_Holding_Several_Steps_Runs_Each_Step()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("PAIR", "rec a; rec b"));

            Run(script, "$PAIR; rec c");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec a", "rec b", "rec c" }));
        }

        [Test]
        public void A_Semicolon_Inside_Quotes_Does_Not_Split()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("TEXT", "rec \"a; b\""));

            Run(script, "$TEXT");

            Assert.That(rec.Arguments.Single(), Is.EqualTo(new[] { "a; b" }));
        }

        [Test]
        public void A_Value_Holding_Parallel_Groups_Launches_Them()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("PAR", "{ rec a; } { rec b; }"));

            Run(script, "$PAR");

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec a", "rec b" }));
        }

        [Test]
        public void Steps_From_A_Value_Are_Not_Resolved_A_Second_Time()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("PAIR", "rec $$5; rec b"));

            Run(script, "$PAIR");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec $5", "rec b" }));
        }

        // --- Failures ---

        [Test]
        public void A_Duplicate_Option_Stops_The_Routine_And_Prints_The_Message()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("LCD", "Cockpit")
                .WithVariable("OPT", "--level=low"));

            Run(script, "rec first; rec $LCD $OPT --level=high; rec a; rec b");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec first" }), "Later steps must not run.");

            Assert.That(Printed(script),
                Does.Contain("Command failed: rec $LCD $OPT --level=high (duplicate option --level)"));
        }

        [Test]
        public void A_Failure_Is_Not_Thrown_Out_Of_The_Clock()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("OPT", "--level=low"));

            Assert.DoesNotThrow(() => Run(script, "rec $OPT --level=high"));
        }

        [Test]
        public void A_Failure_Inside_A_Config_Command_Stops_The_Caller()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("OPT", "--level=low")
                .WithCommand("Outer", "rec o1; Inner; rec o2")
                .WithCommand("Inner", "rec i1; rec $OPT --level=high; rec i2"));

            Run(script, "Outer; rec after");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec o1", "rec i1" }));
            Assert.That(Printed(script), Does.Contain("Command failed: rec $OPT --level=high"));
        }

        [Test]
        public void A_Failure_In_One_Parallel_Group_Does_Not_Stop_A_Sibling_Group()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("OPT", "--level=low"));

            Run(script, "{ rec $OPT --level=high; rec lost; } { rec s1; rec s2; }");

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec s1", "rec s2" }));
            Assert.That(Printed(script), Does.Contain("Command failed: rec $OPT --level=high"));
        }

        [Test]
        public void A_Group_Launched_From_A_Config_Command_Fails_Independently()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("OPT", "--level=low")
                .WithCommand("Both", "{ rec $OPT --level=high; } { rec ok; }"));

            Run(script, "Both");

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ok" }));
            Assert.That(Printed(script), Does.Contain("Command failed: rec $OPT --level=high"));
        }

        [Test]
        public void Variable_Nesting_Too_Deep_Names_The_Chain()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer()
                .WithVariable("A", "$B")
                .WithVariable("B", "$A"));

            Run(script, "rec $A; rec after");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("variable nesting deeper than 5: A > B > A > B > A"));
        }

        [Test]
        public void A_Value_Cannot_Redirect_A_Step_To_Another_Grid()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, new CustomDataComposer().WithVariable("REMOTE", "@grid rec a"));

            Run(script, "$REMOTE; rec after");

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Contain("a variable cannot send a step to another grid"));
        }

        // --- Remote routines ---

        [Test]
        public void A_Remote_Routine_Is_Sent_With_The_Typed_Text_Resolved_And_Config_Bodies_Left_Raw()
        {
            var world = WorldFactory().Boot();
            var recA = new RecordingCommand();
            var recB = new RecordingCommand();

            var shipA = world.CreateScript("ShipA")
                .WithMother()
                .OnNetwork()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("TARGET", "ShipB")
                    .WithVariable("X", "fromA")
                    .WithCommand("Do", "rec body $X")
                    .Build())
                .WithCommands(recA)
                .Boot();

            world.CreateScript("ShipB")
                .WithMother()
                .OnNetwork()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("X", "fromB")
                    .Build())
                .WithCommands(recB)
                .Boot();

            shipA.RunTerminal("@$TARGET rec typed $X");
            world.DeliverMessages();
            world.RunMany(5, Sandbox.ModAPI.Ingame.UpdateType.Update10);

            Assert.That(recB.Calls, Is.EqualTo(new[] { "rec typed fromA" }));

            shipA.RunTerminal("@ShipB Do");
            world.DeliverMessages();
            world.RunMany(5, Sandbox.ModAPI.Ingame.UpdateType.Update10);

            Assert.That(recB.Calls, Is.EqualTo(new[] { "rec typed fromA", "rec body fromB" }));
            Assert.That(recA.Calls, Is.Empty);
        }
    }
}
