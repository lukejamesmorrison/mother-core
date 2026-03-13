using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;

//using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class TerminalRoutineTests : BaseModuleTests
    {
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
    }
}
