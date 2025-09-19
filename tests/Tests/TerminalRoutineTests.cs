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
    }
}
