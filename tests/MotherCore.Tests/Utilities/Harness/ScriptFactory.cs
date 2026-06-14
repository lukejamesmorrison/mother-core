using IngameScript;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Entry points for fluent script harness construction.
    /// Import statically to use function-style calls:
    /// <code>
    /// using static MotherCore.Tests.Utilities.ScriptFactories;
    /// var script = ScriptFactory().WithCustomData(data).Boot();
    /// var script2 = ScriptFactory&lt;Program&gt;().Boot();
    /// </code>
    /// </summary>
    public static class ScriptFactories
    {
        /// <summary>
        /// Creates a builder for the default <see cref="CoreTestProgram"/> harness.
        /// </summary>
        public static ScriptBuilder<CoreTestProgram> ScriptFactory(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
        {
            return new ScriptBuilder<CoreTestProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a builder for the default harness using constructor-style argument order.
        /// </summary>
        public static ScriptBuilder<CoreTestProgram> ScriptFactory(
            IMyCubeGrid primaryGrid,
            string gridName = null)
        {
            return new ScriptBuilder<CoreTestProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a builder for a specific program type.
        /// </summary>
        public static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TProgram : MyGridProgram, new()
        {
            return new ScriptBuilder<TProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a builder for a specific program type using constructor-style argument order.
        /// </summary>
        public static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            IMyCubeGrid primaryGrid,
            string gridName = null)
            where TProgram : MyGridProgram, new()
        {
            return new ScriptBuilder<TProgram>(gridName, primaryGrid);
        }
    }

    /// <summary>
    /// Fluent builder for configuring and creating script harness instances.
    /// </summary>
    /// <typeparam name="TProgram">Program type used by the harness.</typeparam>
    public class ScriptBuilder<TProgram>
        where TProgram : MyGridProgram, new()
    {
        readonly string _gridName;
        readonly IMyCubeGrid _primaryGrid;
        readonly List<Action<Script<TProgram>>> _configure = new List<Action<Script<TProgram>>>();

        internal ScriptBuilder(string gridName = null, IMyCubeGrid primaryGrid = null)
        {
            _gridName = gridName;
            _primaryGrid = primaryGrid;
        }

        /// <summary>
        /// Appends custom configuration logic to the script before creation.
        /// </summary>
        public ScriptBuilder<TProgram> Configure(Action<Script<TProgram>> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            _configure.Add(configure);
            return this;
        }

        public ScriptBuilder<TProgram> WithIGC(IMyIntergridCommunicationSystem igc)
        {
            return Configure(script => script.WithIGC(igc));
        }

        public ScriptBuilder<TProgram> WithUpdateFrequency(UpdateFrequency updateFrequency)
        {
            return Configure(script => script.WithUpdateFrequency(updateFrequency));
        }

        public ScriptBuilder<TProgram> OnNetwork(FakeIgcNetwork network)
        {
            return Configure(script => script.OnNetwork(network));
        }

        public ScriptBuilder<TProgram> OnNetwork()
        {
            return Configure(script => script.OnNetwork());
        }

        public ScriptBuilder<TProgram> WithCustomData(string customData)
        {
            return Configure(script => script.WithCustomData(customData));
        }

        public ScriptBuilder<TProgram> WithStorage(string storage)
        {
            return Configure(script => script.WithStorage(storage));
        }

        public ScriptBuilder<TProgram> WithGrid(
            string gridName,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return Configure(script => script.WithGrid(gridName, entityId, connectionKind));
        }

        public ScriptBuilder<TProgram> WithGrid(
            IMyCubeGrid grid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return Configure(script => script.WithGrid(grid, connectionKind));
        }

        public ScriptBuilder<TProgram> WithBlock(IMyTerminalBlock block)
        {
            return Configure(script => script.WithBlock(block));
        }

        public ScriptBuilder<TProgram> WithBlock(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            return Configure(script => script.WithBlock(block, grid));
        }

        public ScriptBuilder<TProgram> WithBlocks(params IMyTerminalBlock[] blocks)
        {
            return Configure(script => script.WithBlocks(blocks));
        }

        public ScriptBuilder<TProgram> WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)
        {
            return Configure(script => script.WithBlockGroup(groupName, blocks));
        }

        public ScriptBuilder<TProgram> WithCommands(params BaseModuleCommand[] commands)
        {
            return Configure(script => script.WithCommands(commands));
        }

        /// <summary>
        /// Creates an unbooted script harness with all queued configuration applied.
        /// </summary>
        public Script<TProgram> Create()
        {
            Script<TProgram> script = _primaryGrid == null
                ? new Script<TProgram>(_gridName)
                : new Script<TProgram>(_primaryGrid, _gridName);

            foreach (var configure in _configure)
                configure(script);

            return script;
        }

        /// <summary>
        /// Creates and boots a script harness in one step.
        /// </summary>
        public Script<TProgram> Boot()
        {
            return Create().Boot();
        }
    }
}
