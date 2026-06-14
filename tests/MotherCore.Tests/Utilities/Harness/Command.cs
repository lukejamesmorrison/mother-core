using IngameScript;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Layered-test command fixture with default test program.
    /// </summary>
    public class Command<TCommand> : Command<TCommand, CoreTestProgram>
        where TCommand : BaseModuleCommand
    {
        /// <inheritdoc cref="Command{TCommand, TProgram}.Command()"/>
        public Command() : base() { }

        /// <inheritdoc cref="Command{TCommand, TProgram}.Command(Script{TProgram})"/>
        public Command(Script<CoreTestProgram> script) : base(script) { }

        /// <inheritdoc cref="Command{TCommand, TProgram}.Command(BaseModule)"/>
        public Command(BaseModule module) : base(module) { }
    }

    /// <summary>
    /// Layered-test command fixture for a specific script program.
    /// </summary>
    public class Command<TCommand, TProgram>
        where TCommand : BaseModuleCommand
        where TProgram : MyGridProgram, new()
    {
        readonly IModuleFixture<TProgram> _moduleFixture;

        BaseModule _module;

        /// <summary>
        /// Backing script fixture when command is created from script/program context.
        /// </summary>
        public Script<TProgram> Script { get; private set; }

        /// <summary>
        /// Creates a command-only fixture and boots a script on demand.
        /// </summary>
        public Command()
        {
            Script = new Script<TProgram>();
        }

        /// <summary>
        /// Creates a command fixture bound to an existing script context.
        /// </summary>
        public Command(Script<TProgram> script)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            Script = script;
        }

        /// <summary>
        /// Creates a module-context command fixture from a concrete module.
        /// </summary>
        public Command(BaseModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            _module = module;
        }

        internal Command(IModuleFixture<TProgram> moduleFixture)
        {
            if (moduleFixture == null)
                throw new ArgumentNullException(nameof(moduleFixture));

            _moduleFixture = moduleFixture;
        }

        Mother Mother
        {
            get
            {
                if (_module != null)
                    return _module.Mother;

                if (Script != null)
                    return Script.Mother;

                return null;
            }
        }

        /// <summary>
        /// Ensures the fixture context is booted and command type is available.
        /// </summary>
        public Command<TCommand, TProgram> Boot()
        {
            if (_module == null && _moduleFixture != null)
            {
                _module = _moduleFixture.BootBaseModule();
                Script = _moduleFixture.Script;
            }

            if (Script != null && Script.Mother == null)
                Script.Boot();

            var bus = Mother != null ? Mother.GetModule<CommandBus>() : null;

            Assert.That(bus, Is.Not.Null,
                "Expected a booted command fixture, but command bus was null.");

            Assert.That(bus.ModuleCommands.Any(command => command is TCommand), Is.True,
                "Expected command bus to contain command type '" + typeof(TCommand).Name + "', but it was not registered.");

            return this;
        }

        /// <summary>
        /// Runs a terminal command through the command bus.
        /// </summary>
        public Command<TCommand, TProgram> RunTerminal(string argument)
        {
            Boot();

            if (Script != null)
            {
                Script.RunTerminal(argument ?? string.Empty);
                return this;
            }

            Mother.Run(argument ?? string.Empty, UpdateType.Terminal);
            return this;
        }

        /// <summary>
        /// Runs one script update cycle.
        /// </summary>
        public Command<TCommand, TProgram> Run(UpdateType updateType, string argument = "")
        {
            Boot();

            if (Script != null)
            {
                Script.Run(updateType, argument ?? string.Empty);
                return this;
            }

            Mother.Run(argument ?? string.Empty, updateType);
            return this;
        }

        /// <summary>
        /// Runs update cycles until queued clock work is drained.
        /// </summary>
        public Command<TCommand, TProgram> RunToIdle(int maxTicks = 100)
        {
            Boot();

            if (Script != null)
            {
                Script.RunToIdle(maxTicks);
                return this;
            }

            var clock = Mother.GetModule<Clock>();

            for (int i = 0; i < maxTicks; i++)
            {
                if (clock.CoroutineCount == 0 && clock.QueuedTaskCount == 0)
                    break;

                Mother.Run(string.Empty, UpdateType.Update10);
            }

            return this;
        }

        /// <summary>
        /// Asserts command execution count for a command name.
        /// </summary>
        public void ShouldHaveExecuted(
            string commandName,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted,
            int count = 1)
        {
            Boot();

            var matchCount = Mother.GetModule<CommandBus>().GetExecutionCount(commandName, outcome);

            Assert.That(matchCount, Is.EqualTo(count),
                "Expected command '" + commandName + "' with outcome '" + outcome + "' to appear " + count + " time(s), but saw " + matchCount + ".");
        }

        /// <summary>
        /// Asserts command execution count for this fixture's target command type.
        /// </summary>
        public void ShouldHaveExecuted(
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted,
            int count = 1)
        {
            Boot();

            var command = Mother.GetModule<CommandBus>().ModuleCommands.OfType<TCommand>().FirstOrDefault();

            Assert.That(command, Is.Not.Null,
                "Expected command bus to contain command type '" + typeof(TCommand).Name + "', but it was not registered.");

            ShouldHaveExecuted(command.GetCommandName(), outcome, count);
        }
    }

    /// <summary>
    /// Module extensions for module-context command fixtures.
    /// </summary>
    public static class ModuleCommandExtensions
    {
        /// <summary>
        /// Creates a command fixture from an already booted module.
        /// </summary>
        public static Command<TCommand> Command<TCommand>(this BaseModule module)
            where TCommand : BaseModuleCommand
        {
            return new Command<TCommand>(module);
        }
    }
}
