using IngameScript;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Entry points for fluent command harness construction.
    /// Import statically to use function-style calls:
    /// <code>
    /// using static MotherCore.Tests.Utilities.CommandFactories;
    /// var command = CommandFactory&lt;Program&gt;().WithBlock(door).Boot&lt;OpenDoorCommand&gt;();
    /// </code>
    /// </summary>
    public static class CommandFactories
    {
        /// <summary>
        /// Creates a command builder for the default <see cref="CoreTestProgram"/> harness.
        /// </summary>
        public static CommandBuilder<CoreTestProgram> CommandFactory(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
        {
            return new CommandBuilder<CoreTestProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a command builder for the default harness using constructor-style argument order.
        /// </summary>
        public static CommandBuilder<CoreTestProgram> CommandFactory(
            IMyCubeGrid primaryGrid,
            string gridName = null)
        {
            return new CommandBuilder<CoreTestProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a command builder for a specific program type.
        /// </summary>
        public static CommandBuilder<TProgram> CommandFactory<TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TProgram : MyGridProgram, new()
        {
            return new CommandBuilder<TProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a command builder for a specific program type using constructor-style argument order.
        /// </summary>
        public static CommandBuilder<TProgram> CommandFactory<TProgram>(
            IMyCubeGrid primaryGrid,
            string gridName = null)
            where TProgram : MyGridProgram, new()
        {
            return new CommandBuilder<TProgram>(gridName, primaryGrid);
        }
    }

    /// <summary>
    /// Fluent builder for command fixtures that always run inside a booted script context.
    /// </summary>
    /// <typeparam name="TProgram">Program type used by the harness.</typeparam>
    public class CommandBuilder<TProgram>
        where TProgram : MyGridProgram, new()
    {
        readonly ScriptBuilder<TProgram> _script;

        Func<Script<TProgram>, BaseModule> _moduleResolver;

        /// <summary>
        /// Creates a command builder rooted in script context with Mother pre-enabled.
        /// </summary>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        internal CommandBuilder(string gridName = null, IMyCubeGrid primaryGrid = null)
        {
            _script = ScriptFactories.ScriptFactory<TProgram>(gridName, primaryGrid)
                .WithMother();
        }

        /// <summary>
        /// Resolves command context against a concrete module from the booted script.
        /// </summary>
        /// <typeparam name="TModule">Concrete module type used for command context.</typeparam>
        public CommandBuilder<TProgram> FromModule<TModule>()
            where TModule : BaseModule
        {
            _moduleResolver = script => script.Mother.GetModule<TModule>();
            return this;
        }

        /// <summary>
        /// Appends custom script configuration logic to the command context.
        /// </summary>
        /// <param name="configure">Configuration callback executed before boot.</param>
        public CommandBuilder<TProgram> Configure(Action<Script<TProgram>> configure)
        {
            _script.Configure(configure);
            return this;
        }

        /// <summary>
        /// Injects an explicit IGC endpoint.
        /// </summary>
        /// <param name="igc">IGC endpoint to use.</param>
        public CommandBuilder<TProgram> WithIGC(IMyIntergridCommunicationSystem igc)
        {
            _script.WithIGC(igc);
            return this;
        }

        /// <summary>
        /// Sets runtime update frequency before boot.
        /// </summary>
        /// <param name="updateFrequency">Runtime update flags to apply.</param>
        public CommandBuilder<TProgram> WithUpdateFrequency(UpdateFrequency updateFrequency)
        {
            _script.WithUpdateFrequency(updateFrequency);
            return this;
        }

        /// <summary>
        /// Joins a shared fake IGC network.
        /// </summary>
        /// <param name="network">Network to join.</param>
        public CommandBuilder<TProgram> OnNetwork(FakeIgcNetwork network)
        {
            _script.OnNetwork(network);
            return this;
        }

        /// <summary>
        /// Joins a default private fake IGC network.
        /// </summary>
        public CommandBuilder<TProgram> OnNetwork()
        {
            _script.OnNetwork();
            return this;
        }

        /// <summary>
        /// Sets programmable block custom data before boot.
        /// </summary>
        /// <param name="customData">Custom data payload.</param>
        public CommandBuilder<TProgram> WithCustomData(string customData)
        {
            _script.WithCustomData(customData);
            return this;
        }

        /// <summary>
        /// Sets programmable block storage before boot.
        /// </summary>
        /// <param name="storage">Storage payload.</param>
        public CommandBuilder<TProgram> WithStorage(string storage)
        {
            _script.WithStorage(storage);
            return this;
        }

        /// <summary>
        /// Adds a secondary grid by name.
        /// </summary>
        /// <param name="gridName">Grid name.</param>
        /// <param name="entityId">Optional explicit entity id.</param>
        /// <param name="connectionKind">Mechanical connection kind used to link constructs.</param>
        public CommandBuilder<TProgram> WithGrid(
            string gridName,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            _script.WithGrid(gridName, entityId, connectionKind);
            return this;
        }

        /// <summary>
        /// Adds a secondary grid by instance.
        /// </summary>
        /// <param name="grid">Grid instance.</param>
        /// <param name="connectionKind">Mechanical connection kind used to link constructs.</param>
        public CommandBuilder<TProgram> WithGrid(
            IMyCubeGrid grid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            _script.WithGrid(grid, connectionKind);
            return this;
        }

        /// <summary>
        /// Registers a terminal block in the harness terminal system.
        /// </summary>
        /// <param name="block">Block instance to register.</param>
        public CommandBuilder<TProgram> WithBlock(IMyTerminalBlock block)
        {
            _script.WithBlock(block);
            return this;
        }

        /// <summary>
        /// Registers a terminal block on a specific grid.
        /// </summary>
        /// <param name="block">Block instance to register.</param>
        /// <param name="grid">Owning grid for the block.</param>
        public CommandBuilder<TProgram> WithBlock(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            _script.WithBlock(block, grid);
            return this;
        }

        /// <summary>
        /// Registers one or more terminal blocks.
        /// </summary>
        /// <param name="blocks">Blocks to register.</param>
        public CommandBuilder<TProgram> WithBlocks(params IMyTerminalBlock[] blocks)
        {
            _script.WithBlocks(blocks);
            return this;
        }

        /// <summary>
        /// Registers a named block group.
        /// </summary>
        /// <param name="groupName">Block group name.</param>
        /// <param name="blocks">Blocks belonging to the group.</param>
        public CommandBuilder<TProgram> WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)
        {
            _script.WithBlockGroup(groupName, blocks);
            return this;
        }

        /// <summary>
        /// Registers additional command instances before boot.
        /// </summary>
        /// <param name="commands">Commands to append to the command bus.</param>
        public CommandBuilder<TProgram> WithCommands(params BaseModuleCommand[] commands)
        {
            _script.WithCommands(commands);
            return this;
        }

        /// <summary>
        /// Creates and boots a typed command fixture from the configured script context.
        /// </summary>
        /// <typeparam name="TCommand">Command type under test.</typeparam>
        public Command<TCommand, TProgram> Boot<TCommand>()
            where TCommand : BaseModuleCommand
        {
            var script = _script.Boot();

            if (_moduleResolver != null)
            {
                var module = _moduleResolver(script);

                if (module == null)
                {
                    throw new InvalidOperationException(
                        "Failed to resolve module context from command builder for module-bound command fixture.");
                }

                return new Command<TCommand, TProgram>(module).Boot();
            }

            return new Command<TCommand, TProgram>(script).Boot();
        }
    }
}
