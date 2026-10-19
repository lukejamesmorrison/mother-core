using IngameScript;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    internal interface IModuleFixture<TProgram>
        where TProgram : MyGridProgram, new()
    {
        Script<TProgram> Script { get; }

        BaseModule BootBaseModule();
    }

    /// <summary>
    /// Layered-test module fixture with default test program.
    /// </summary>
    public class Module<TModule> : Module<TModule, CoreTestProgram>
        where TModule : BaseModule
    {
        /// <inheritdoc cref="Module{TModule, TProgram}(string)"/>
        public Module(string gridName = null) : base(gridName) { }

        /// <inheritdoc cref="Module{TModule, TProgram}.Module(IMyCubeGrid, string)"/>
        public Module(IMyCubeGrid primaryGrid, string gridName = null) : base(primaryGrid, gridName) { }
    }

    /// <summary>
    /// Layered-test module fixture for a specific script program.
    /// </summary>
    public class Module<TModule, TProgram> : IModuleFixture<TProgram>
        where TModule : BaseModule
        where TProgram : MyGridProgram, new()
    {
        bool _booted;
        TModule _module;

        /// <summary>
        /// Underlying script fixture for advanced assertions.
        /// </summary>
        public Script<TProgram> Script { get; private set; }

        /// <summary>
        /// Creates a module fixture for a script on a generated grid.
        /// </summary>
        public Module(string gridName = null)
        {
            Script = new Script<TProgram>(gridName);
        }

        /// <summary>
        /// Creates a module fixture for a script bound to an existing grid.
        /// </summary>
        public Module(IMyCubeGrid primaryGrid, string gridName = null)
        {
            Script = new Script<TProgram>(primaryGrid, gridName);
        }

        /// <summary>
        /// Injects a pre-created IGC endpoint into the script fixture.
        /// </summary>
        public Module<TModule, TProgram> WithIGC(IMyIntergridCommunicationSystem igc)
        {
            Script.WithIGC(igc);
            return this;
        }

        /// <summary>
        /// Sets runtime update frequency before boot.
        /// </summary>
        public Module<TModule, TProgram> WithUpdateFrequency(UpdateFrequency updateFrequency)
        {
            Script.WithUpdateFrequency(updateFrequency);
            return this;
        }

        /// <summary>
        /// Joins a shared IGC network.
        /// </summary>
        public Module<TModule, TProgram> OnNetwork(FakeIgcNetwork network)
        {
            Script.OnNetwork(network);
            return this;
        }

        /// <summary>
        /// Joins the default/private IGC network.
        /// </summary>
        public Module<TModule, TProgram> OnNetwork()
        {
            Script.OnNetwork();
            return this;
        }

        /// <summary>
        /// Sets programmable block CustomData before boot.
        /// </summary>
        public Module<TModule, TProgram> WithCustomData(string customData)
        {
            Script.WithCustomData(customData);
            return this;
        }

        /// <summary>
        /// Sets programmable block Storage before boot.
        /// </summary>
        public Module<TModule, TProgram> WithStorage(string storage)
        {
            Script.WithStorage(storage);
            return this;
        }

        /// <summary>
        /// Adds a secondary grid by name.
        /// </summary>
        public Module<TModule, TProgram> WithGrid(
            string gridName,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            Script.WithGrid(gridName, entityId, connectionKind);
            return this;
        }

        /// <summary>
        /// Adds a secondary grid by instance.
        /// </summary>
        public Module<TModule, TProgram> WithGrid(
            IMyCubeGrid grid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            Script.WithGrid(grid, connectionKind);
            return this;
        }

        /// <summary>
        /// Registers a terminal block in the script grid terminal system.
        /// </summary>
        public Module<TModule, TProgram> WithBlock(IMyTerminalBlock block)
        {
            Script.WithBlock(block);
            return this;
        }

        /// <summary>
        /// Registers a terminal block on a specific grid.
        /// </summary>
        public Module<TModule, TProgram> WithBlock(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            Script.WithBlock(block, grid);
            return this;
        }

        /// <summary>
        /// Registers one or more terminal blocks.
        /// </summary>
        public Module<TModule, TProgram> WithBlocks(params IMyTerminalBlock[] blocks)
        {
            Script.WithBlocks(blocks);
            return this;
        }

        /// <summary>
        /// Registers a named block group.
        /// </summary>
        public Module<TModule, TProgram> WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)
        {
            Script.WithBlockGroup(groupName, blocks);
            return this;
        }

        /// <summary>
        /// Registers additional commands with the script command bus.
        /// </summary>
        public Module<TModule, TProgram> WithCommands(params BaseModuleCommand[] commands)
        {
            Script.WithCommands(commands);
            return this;
        }

        /// <summary>
        /// Boots the script and resolves the concrete module instance.
        /// </summary>
        public TModule Boot()
        {
            if (_booted)
                return _module;

            Script.Boot();
            _module = Script.Mother.GetModule<TModule>();

            if (_module == null)
            {
                throw new InvalidOperationException(
                    "Failed to resolve module '" + typeof(TModule).Name + "' from booted script.");
            }

            _booted = true;

            return _module;
        }

        /// <summary>
        /// Creates a module-context command fixture.
        /// </summary>
        public Command<TCommand, TProgram> Command<TCommand>()
            where TCommand : BaseModuleCommand
        {
            return new Command<TCommand, TProgram>(this);
        }

        BaseModule IModuleFixture<TProgram>.BootBaseModule()
        {
            return Boot();
        }
    }
}
