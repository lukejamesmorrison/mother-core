using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;
using System.Linq;

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

        [Test]
        public void It_Substitutes_Variables_In_Terminal_Input()
        {
            _mother.ConfigVariables["BLOCK"] = "Light1";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/on $BLOCK");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Substitutes_Multiple_Variables_In_Terminal_Input()
        {
            _mother.ConfigVariables["BLOCK"] = "Light1";
            _mother.ConfigVariables["COLOR"] = "red";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/color $BLOCK $COLOR");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Runs_Terminal_Command_Without_Variables_When_None_Defined()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        // --- RunTerminalCommand edge cases ---

        [Test]
        public void RunTerminalCommand_Returns_False_For_Empty_String()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RunTerminalCommand_Handles_Semicolon_Separated_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("help;help");

            Assert.That(result, Is.True);
        }

        // --- Boot behavior ---

        [Test]
        public void Boot_Registers_The_Help_Command()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.Boot();

            var helpCommand = commandBus.ModuleCommands
                .FirstOrDefault(c => c.GetCommandName() == "help");

            Assert.That(helpCommand, Is.Not.Null);
        }

        [Test]
        public void Boot_Clears_Construct_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.ConstructCommands[999] = new List<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ConstructCommands.Count, Is.EqualTo(0));
        }

        // --- Multiple module commands ---

        [Test]
        public void Multiple_Module_Commands_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(2));
        }

        // --- GetSelfCommandNames ---

        [Test]
        public void GetSelfCommandNames_Includes_Module_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Config_Commands()
        {
            _mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(_mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("myCommand"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Both_Module_And_Config_Commands()
        {
            _mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
            Assert.That(names, Contains.Item("myCommand"));
            Assert.That(names.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetSelfCommandNames_Returns_Empty_When_No_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names.Count, Is.EqualTo(0));
        }

        // --- RegisterRemoteCommands ---

        [Test]
        public void RegisterRemoteCommands_Stores_Commands_For_Remote_Script()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;
            var commands = new List<string> { "dock", "undock" };

            commandBus.RegisterRemoteCommands(remoteId, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(remoteId), Is.True);
            Assert.That(commandBus.ConstructCommands[remoteId], Is.EqualTo(commands));
        }

        [Test]
        public void RegisterRemoteCommands_Ignores_Self_Id()
        {
            CommandBus commandBus = new CommandBus(_mother);

            var commands = new List<string> { "dock" };

            commandBus.RegisterRemoteCommands(_mother.Id, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(_mother.Id), Is.False);
        }

        [Test]
        public void RegisterRemoteCommands_Overwrites_Existing_Entry()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;

            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "old" });
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "new" });

            Assert.That(commandBus.ConstructCommands[remoteId].Count, Is.EqualTo(1));
            Assert.That(commandBus.ConstructCommands[remoteId][0], Is.EqualTo("new"));
        }

        // --- FindInstanceWithCommand ---

        [Test]
        public void FindInstanceWithCommand_Returns_Script_Id_When_Found()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock", "undock" });

            long result = commandBus.FindInstanceWithCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long result = commandBus.FindInstanceWithCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_First_Match_From_Multiple_Scripts()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId1 = _mother.Id + 1;
            long remoteId2 = _mother.Id + 2;

            commandBus.RegisterRemoteCommands(remoteId1, new List<string> { "dock" });
            commandBus.RegisterRemoteCommands(remoteId2, new List<string> { "undock" });

            Assert.That(commandBus.FindInstanceWithCommand("dock"), Is.EqualTo(remoteId1));
            Assert.That(commandBus.FindInstanceWithCommand("undock"), Is.EqualTo(remoteId2));
        }

        // --- Config command expansion ---

        [Test]
        public void RunTerminalCommand_Expands_Config_Command()
        {
            _mother.ConfigCommands["myAction"] = "help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("myAction");

            Assert.That(result, Is.True);
        }

        [Test]
        public void RunTerminalCommand_Expands_Config_Command_With_Multiple_Steps()
        {
            _mother.ConfigCommands["sequence"] = "help;help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("sequence");

            Assert.That(result, Is.True);
        }

        // --- WaypointRoutineQueue ---

        [Test]
        public void WaypointRoutineQueue_Is_Initialized_On_Construction()
        {
            CommandBus commandBus = new CommandBus(_mother);

            Assert.That(commandBus.WaypointRoutineQueue, Is.Not.Null);
        }
    }
}
