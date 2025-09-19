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
    public class TerminalCommandTests : BaseModuleTests
    {

        [Test]
        public void It_Can_Be_Instantiated_With_A_Command_String()
        {
            string commandString = "rotor/rotate -45 --speed=100 --delay=0.5 --force";

            TerminalCommand command = new TerminalCommand(commandString);

            Assert.That(command.Name, Is.EqualTo("rotor/rotate"));
            Assert.That(command.Arguments, Is.EqualTo(new List<string> { "-45" }));
            Assert.That(command.Options, Is.EqualTo(new Dictionary<string, string>
            {
                { "speed", "100" },
                { "delay", "0.5" },
                { "force", "true" }
            }));
            Assert.That(command.CommandString, Is.EqualTo(commandString));
        }

        [Test]
        public void It_Can_Get_Options_By_Key()
        {
            string commandString = "rotor/rotate -45 --speed=100 --delay=0.5 --force";
            TerminalCommand command = new TerminalCommand(commandString);

            Assert.That(command.GetOption("speed"), Is.EqualTo("100"));
            Assert.That(command.GetOption("delay"), Is.EqualTo("0.5"));
            Assert.That(command.GetOption("force"), Is.EqualTo("true"));
            Assert.That(command.GetOption("nonexistent"), Is.EqualTo(""));
        }
    }
}
