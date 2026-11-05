using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using System;
using System.Linq;

namespace MotherCore.Tests.Tests.Unit
{
    [Category(TestCategories.LayerUnit)]
    public class LambdaCommandTests
    {
        [Test]
        public void Name_Is_The_Registered_Name()
        {
            var command = new LambdaCommand("view/up", 0, c => "");

            Assert.That(command.Name, Is.EqualTo("view/up"));
            Assert.That(command.GetCommandName(), Is.EqualTo("view/up"));
        }

        [Test]
        public void Execute_Returns_The_Delegate_Result()
        {
            var command = new LambdaCommand("echo", 1, c => c.Arguments[0] + "!");

            Assert.That(command.Execute(new TerminalCommand("echo hi")), Is.EqualTo("hi!"));
        }

        [Test]
        public void Execute_Runs_The_Delegate_When_MinArgs_Is_Zero_And_No_Arguments_Are_Given()
        {
            var command = new LambdaCommand("ping", 0, c => "pong");

            Assert.That(command.Execute(new TerminalCommand("ping")), Is.EqualTo("pong"));
        }

        [Test]
        public void Execute_Returns_NoArgumentsProvided_And_Skips_The_Delegate_When_Too_Few_Arguments()
        {
            bool ran = false;
            var command = new LambdaCommand("echo", 2, c => { ran = true; return ""; });

            string result = command.Execute(new TerminalCommand("echo one"));

            Assert.That(result, Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
            Assert.That(ran, Is.False);
        }

        [Test]
        public void Execute_Returns_The_Usage_Text_When_Too_Few_Arguments()
        {
            var command = new LambdaCommand("view/up", 1, c => "", "Usage: view/up <display name>");

            Assert.That(command.Execute(new TerminalCommand("view/up")), Is.EqualTo("Usage: view/up <display name>"));
        }

        [Test]
        public void Execute_Runs_The_Delegate_When_Exactly_MinArgs_Are_Given()
        {
            var command = new LambdaCommand("view/up", 1, c => "ok", "usage");

            Assert.That(command.Execute(new TerminalCommand("view/up Cockpit")), Is.EqualTo("ok"));
        }

        [Test]
        public void Options_Are_Not_Counted_As_Arguments()
        {
            var command = new LambdaCommand("echo", 1, c => "ran");

            Assert.That(
                command.Execute(new TerminalCommand("echo --flag")),
                Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void Delegate_Exceptions_Propagate_Like_A_Command_Class()
        {
            var command = new LambdaCommand("boom", 0, c => { throw new InvalidOperationException("boom"); });

            Assert.That(() => command.Execute(new TerminalCommand("boom")), Throws.InvalidOperationException);
        }
    }

    [Category(TestCategories.LayerUnit)]
    public class TerminalCommandHelperTests
    {
        [Test]
        public void GetIncrement_Throws_For_Invalid_Number()
        {
            var command = new TerminalCommand("x nope --add");

            Assert.That(
                () => command.GetIncrement("nope"),
                Throws.TypeOf<ArgumentException>().With.Message.EqualTo("Invalid numerical value provided."));
        }

        [Test]
        public void GetIncrement_Returns_Zero_Without_Add_Or_Sub()
        {
            Assert.That(new TerminalCommand("x 3.5").GetIncrement("3.5"), Is.EqualTo(0f));
        }

        [Test]
        public void GetIncrement_Returns_Value_With_Add()
        {
            Assert.That(new TerminalCommand("x 3.5 --add").GetIncrement("3.5"), Is.EqualTo(3.5f));
        }

        [Test]
        public void GetIncrement_Returns_Negated_Value_With_Sub()
        {
            Assert.That(new TerminalCommand("x 3.5 --sub").GetIncrement("3.5"), Is.EqualTo(-3.5f));
        }

        [Test]
        public void GetIncrement_Prefers_Add_When_Both_Are_Set()
        {
            Assert.That(new TerminalCommand("x 2 --add --sub").GetIncrement("2"), Is.EqualTo(2f));
        }

        [Test]
        public void IsShared_Reflects_The_Share_Option()
        {
            Assert.That(new TerminalCommand("x --share").IsShared(), Is.True);
            Assert.That(new TerminalCommand("x").IsShared(), Is.False);
        }

        [Test]
        public void GetDistributedValue_Divides_Only_When_Cumulative_And_Multiple_Blocks()
        {
            Assert.That(TerminalCommand.GetDistributedValue(10f, 4, true), Is.EqualTo(2.5f));
            Assert.That(TerminalCommand.GetDistributedValue(10f, 1, true), Is.EqualTo(10f));
            Assert.That(TerminalCommand.GetDistributedValue(10f, 4, false), Is.EqualTo(10f));
        }
    }
}

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// Core commands that were migrated from command classes to lambda
    /// registrations must keep their names, order, guards and responses.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class CoreLambdaCommandTests : TestBase
    {
        static string Run<TProgram>(Script<TProgram> script, string text) where TProgram : Sandbox.ModAPI.Ingame.MyGridProgram, new()
        {
            var terminal = new TerminalCommand(text);

            return script.Mother.GetModule<CommandBus>().ModuleCommands
                .First(c => c.GetCommandName() == terminal.Name)
                .Execute(terminal);
        }

        [TestCase("help")]
        [TestCase("halt")]
        [TestCase("clear")]
        [TestCase("print")]
        [TestCase("set")]
        [TestCase("get")]
        [TestCase("var/set")]
        [TestCase("ping")]
        [TestCase("boot")]
        [TestCase("purge")]
        [TestCase("rename")]
        public void Core_Command_Is_Registered_Once(string name)
        {
            var script = ScriptFactory().WithMother().Boot();

            int count = script.Mother.GetModule<CommandBus>().ModuleCommands
                .Count(c => c.GetCommandName() == name);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void Help_Is_Registered_Before_Halt()
        {
            var script = ScriptFactory().WithMother().Boot();
            var names = script.Mother.GetModule<CommandBus>().ModuleCommands.Select(c => c.GetCommandName()).ToList();

            Assert.That(names.IndexOf("help"), Is.LessThan(names.IndexOf("halt")));
        }

        [Test]
        public void Help_Lists_Every_Command_One_Per_Line()
        {
            var script = ScriptFactory().WithMother().Boot();
            var names = script.Mother.GetModule<CommandBus>().ModuleCommands.Select(c => c.GetCommandName());

            Assert.That(Run(script, "help"), Is.EqualTo(string.Concat(names.Select(n => n + "\n"))));
        }

        [Test]
        public void Halt_Responds_Halted()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "halt"), Is.EqualTo("Halted."));
        }

        [Test]
        public void Clear_Responds_With_Empty_Text()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "clear"), Is.EqualTo(""));
        }

        [Test]
        public void Print_Returns_The_First_Argument()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "print hello"), Is.EqualTo("hello"));
        }

        [Test]
        public void Set_Stores_The_Value_And_Echoes_It()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "set ship Frigate"), Is.EqualTo("ship=Frigate"));
            Assert.That(script.Mother.GetModule<LocalStorage>().Get("ship"), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Set_Without_Arguments_Reports_NoArgumentsProvided()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "set"), Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void Set_With_Only_A_Key_Reports_InvalidCommandFormat()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "set ship"), Is.EqualTo(CommandBus.Messages.InvalidCommandFormat));
        }

        [Test]
        public void Get_Returns_The_Stored_Value()
        {
            var script = ScriptFactory().WithMother().Boot();
            script.Mother.GetModule<LocalStorage>().Set("ship", "Frigate");

            Assert.That(Run(script, "get ship"), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Get_Without_Arguments_Reports_NoArgumentsProvided()
        {
            var script = ScriptFactory().WithMother().Boot();

            Assert.That(Run(script, "get"), Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void Boot_Command_Sets_The_Pending_Boot_Flag()
        {
            var script = ScriptFactory().WithMother().Boot();
            script.Mother.PendingBoot = false;

            Assert.That(Run(script, "boot"), Is.EqualTo("Rebooting..."));
            Assert.That(script.Mother.PendingBoot, Is.True);
        }

        [Test]
        public void Mother_RegisterCommand_Adds_A_Lambda_Command_To_The_CommandBus()
        {
            var script = ScriptFactory().WithMother().Boot();

            script.Mother.RegisterCommand("custom/test", 1, c => "got " + c.Arguments[0], "usage");

            Assert.That(Run(script, "custom/test a"), Is.EqualTo("got a"));
            Assert.That(Run(script, "custom/test"), Is.EqualTo("usage"));
        }

        [Test]
        public void CommandBus_RegisterCommand_Adds_A_Lambda_Command_To_ModuleCommands()
        {
            var script = ScriptFactory().WithMother().Boot();
            var bus = script.Mother.GetModule<CommandBus>();

            bus.RegisterCommand("custom/bus", 0, c => "bus");

            Assert.That(bus.ModuleCommands.Any(c => c.GetCommandName() == "custom/bus"), Is.True);
        }
    }
}
