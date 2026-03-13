using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class TerminalRoutineTests : BaseModuleTests
    {
        // --- Basic sequential parsing ---

        [Test]
        public void It_Can_Be_Instantiated_With_A_Routine_String()
        {
            string routineString = "rotor/rotate -45; piston/distance Piston1 1.5;";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Commands.Count, Is.EqualTo(2));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("rotor/rotate"));
            Assert.That(routine.Commands[1].Name, Is.EqualTo("piston/distance"));
        }

        [Test]
        public void It_Can_Be_Instantiated_With_A_Command_String()
        {
            string commandString = "rotor/rotate -45 --speed=100 --delay=0.5 --force";

            TerminalRoutine routine = new TerminalRoutine(commandString);

            Assert.That(routine.Commands.Count, Is.EqualTo(1));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("rotor/rotate"));
        }

        [Test]
        public void It_Preserves_Command_Order_In_Sequential_Routines()
        {
            string routineString = "cmd1; cmd2; cmd3; cmd4; cmd5";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Commands.Count, Is.EqualTo(5));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("cmd1"));
            Assert.That(routine.Commands[1].Name, Is.EqualTo("cmd2"));
            Assert.That(routine.Commands[2].Name, Is.EqualTo("cmd3"));
            Assert.That(routine.Commands[3].Name, Is.EqualTo("cmd4"));
            Assert.That(routine.Commands[4].Name, Is.EqualTo("cmd5"));
        }

        [Test]
        public void It_Ignores_Empty_Commands_From_Trailing_Semicolons()
        {
            string routineString = "cmd1; cmd2;";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Commands.Count, Is.EqualTo(2));
        }

        [Test]
        public void It_Defaults_Target_To_Self()
        {
            TerminalRoutine routine = new TerminalRoutine("help");

            Assert.That(routine.Target, Is.EqualTo("self"));
        }

        // --- Remote targeting ---

        [Test]
        public void It_Can_Have_A_Remote_Target()
        {
            string routineString = "@Mothership rotor/rotate -45";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Target, Is.EqualTo("Mothership"));
        }

        [Test]
        public void It_Can_Have_All_Remote_Target()
        {
            string routineString = "@* rotor/rotate -45";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Target, Is.EqualTo("*"));
        }

        [Test]
        public void Remote_Target_Is_Stripped_From_Commands()
        {
            string routineString = "@Mothership rotor/rotate -45";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Commands.Count, Is.EqualTo(1));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("rotor/rotate"));
        }

        [Test]
        public void Remote_Target_Preserves_Multiple_Commands()
        {
            string routineString = "@Mothership cmd1; cmd2; cmd3";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.Target, Is.EqualTo("Mothership"));
            Assert.That(routine.Commands.Count, Is.EqualTo(3));
        }

        // --- Unpack ---

        [Test]
        public void It_Can_Unpack_A_Routine_Into_A_Command()
        {
            Dictionary<string, string> lookup = new Dictionary<string, string>
            {
                { "rotateRotor", "rotor/rotate -45 --speed=100 --delay=0.5 --force" },
                { "extendFuelBoom", "rotateRotor; piston/distance Piston1 1.5" }
            };

            string routineString = "extendFuelBoom";

            TerminalRoutine routine = new TerminalRoutine(routineString);
            routine.Unpack(lookup);

            Assert.That(
                routine.UnpackedRoutineString,
                Is.EqualTo(
                "rotor/rotate -45 --speed=100 --delay=0.5 --force; " +
                "piston/distance Piston1 1.5;"
                )
            );
        }

        [Test]
        public void Unpack_Returns_Self_For_Fluent_Chaining()
        {
            var lookup = new Dictionary<string, string> { { "test", "help" } };
            var routine = new TerminalRoutine("test");

            var result = routine.Unpack(lookup);

            Assert.That(result, Is.SameAs(routine));
        }

        // --- Parallel groups ---

        [Test]
        public void It_Can_Parse_Parallel_Groups()
        {
            string routineString = "{ rotor/rotate TurretRotor 45; block/on WarningLight; } { piston/distance LandingGearPiston 2; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.True);
            Assert.That(routine.ParallelGroups.Count, Is.EqualTo(2));
            Assert.That(routine.Commands.Count, Is.EqualTo(0));
        }

        [Test]
        public void It_Parses_Commands_Within_Parallel_Groups()
        {
            string routineString = "{ rotor/rotate TurretRotor 45; block/on WarningLight; } { piston/distance LandingGearPiston 2; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            // First group: 2 commands
            Assert.That(routine.ParallelGroups[0].Count, Is.EqualTo(2));
            Assert.That(routine.ParallelGroups[0][0].Name, Is.EqualTo("rotor/rotate"));
            Assert.That(routine.ParallelGroups[0][1].Name, Is.EqualTo("block/on"));

            // Second group: 1 command
            Assert.That(routine.ParallelGroups[1].Count, Is.EqualTo(1));
            Assert.That(routine.ParallelGroups[1][0].Name, Is.EqualTo("piston/distance"));
        }

        [Test]
        public void It_Does_Not_Have_Parallel_Groups_For_Sequential_Routines()
        {
            string routineString = "rotor/rotate TurretRotor 45; block/on WarningLight;";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.False);
            Assert.That(routine.ParallelGroups.Count, Is.EqualTo(0));
            Assert.That(routine.Commands.Count, Is.EqualTo(2));
        }

        [Test]
        public void It_Can_Parse_A_Single_Parallel_Group()
        {
            string routineString = "{ rotor/rotate TurretRotor 45; block/on WarningLight; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.True);
            Assert.That(routine.ParallelGroups.Count, Is.EqualTo(1));
            Assert.That(routine.ParallelGroups[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void It_Can_Parse_Parallel_Groups_With_Wait_Commands()
        {
            string routineString = "{ rotor/rotate TurretRotor 45; wait 2; block/on WarningLight; } { piston/distance LandingGearPiston 2; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.True);
            Assert.That(routine.ParallelGroups.Count, Is.EqualTo(2));
            Assert.That(routine.ParallelGroups[0].Count, Is.EqualTo(3));
            Assert.That(routine.ParallelGroups[0][1].Name, Is.EqualTo("wait"));
        }

        [Test]
        public void Parallel_Groups_Preserve_Command_Order_Within_Each_Group()
        {
            string routineString = "{ a; b; c; } { d; e; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.ParallelGroups[0][0].Name, Is.EqualTo("a"));
            Assert.That(routine.ParallelGroups[0][1].Name, Is.EqualTo("b"));
            Assert.That(routine.ParallelGroups[0][2].Name, Is.EqualTo("c"));
            Assert.That(routine.ParallelGroups[1][0].Name, Is.EqualTo("d"));
            Assert.That(routine.ParallelGroups[1][1].Name, Is.EqualTo("e"));
        }

        [Test]
        public void Mixed_Content_Outside_Braces_Is_Not_Treated_As_Parallel()
        {
            // Content outside braces means this is NOT a parallel group
            string routineString = "cmd1; { cmd2; cmd3; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.False);
            Assert.That(routine.Commands.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Empty_Parallel_Groups_Are_Ignored()
        {
            string routineString = "{ cmd1; } { } { cmd2; }";

            TerminalRoutine routine = new TerminalRoutine(routineString);

            Assert.That(routine.HasParallelGroups, Is.True);
            Assert.That(routine.ParallelGroups.Count, Is.EqualTo(2));
        }

        // --- Whitespace handling ---

        [Test]
        public void It_Trims_Whitespace_From_The_Routine_String()
        {
            TerminalRoutine routine = new TerminalRoutine("  help  ");

            Assert.That(routine.Commands.Count, Is.EqualTo(1));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("help"));
        }

        [Test]
        public void It_Handles_Extra_Whitespace_Between_Semicolons()
        {
            TerminalRoutine routine = new TerminalRoutine("cmd1 ;  cmd2 ;  cmd3");

            Assert.That(routine.Commands.Count, Is.EqualTo(3));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("cmd1"));
            Assert.That(routine.Commands[1].Name, Is.EqualTo("cmd2"));
            Assert.That(routine.Commands[2].Name, Is.EqualTo("cmd3"));
        }

        // --- Quoted strings in routines ---

        [Test]
        public void Semicolons_Inside_Quoted_Strings_Are_Not_Treated_As_Separators()
        {
            TerminalRoutine routine = new TerminalRoutine("print \"hello; world\"");

            Assert.That(routine.Commands.Count, Is.EqualTo(1));
            Assert.That(routine.Commands[0].Name, Is.EqualTo("print"));
            Assert.That(routine.Commands[0].Arguments[0], Is.EqualTo("hello; world"));
        }

        // --- Wait command recognition ---

        [Test]
        public void Wait_Commands_Are_Parsed_As_Normal_Commands_In_Routine()
        {
            TerminalRoutine routine = new TerminalRoutine("cmd1; wait 5; cmd2");

            Assert.That(routine.Commands.Count, Is.EqualTo(3));
            Assert.That(routine.Commands[1].Name, Is.EqualTo("wait"));
            Assert.That(routine.Commands[1].Arguments[0], Is.EqualTo("5"));
        }

        // --- Single command ---

        [Test]
        public void Single_Command_Creates_One_Entry_In_Commands_List()
        {
            TerminalRoutine routine = new TerminalRoutine("block/on Light1");

            Assert.That(routine.Commands.Count, Is.EqualTo(1));
            Assert.That(routine.HasParallelGroups, Is.False);
        }
    }
}
