using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
//using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class CommandBusTests : BaseModuleTests
    {

        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_Of_Mother()
        {
            CommandBus commandBus = new CommandBus(_mother);

            Console.WriteLine(commandBus.Commands.Count);

            Assert.That(commandBus.Mother, Is.SameAs(_mother));
        }

        [Test]
        public void It_Can_Be_Booted()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.Boot();

            Assert.Pass();
        }

        [Test]
        public void A_Module_Command_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void A_Module_Command_Can_Be_Run_From_A_Terminal_Command()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }
    }
}
