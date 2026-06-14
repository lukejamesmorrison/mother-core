using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Common test base that exposes fluent script factory helpers without per-file imports.
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// Creates a world builder for multi-script orchestration tests.
        /// </summary>
        protected static WorldFactory WorldFactory()
        {
            return new WorldFactory();
        }

        /// <summary>
        /// Creates a script builder for the default test program.
        /// </summary>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static ScriptBuilder<CoreTestProgram> ScriptFactory(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
        {
            return ScriptFactories.ScriptFactory(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a script builder for the default test program using constructor-style argument order.
        /// </summary>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        /// <param name="gridName">Optional primary grid name.</param>
        protected static ScriptBuilder<CoreTestProgram> ScriptFactory(
            IMyCubeGrid primaryGrid,
            string gridName = null)
        {
            return ScriptFactories.ScriptFactory(primaryGrid, gridName);
        }

        /// <summary>
        /// Creates a script builder for a specific program type.
        /// </summary>
        /// <typeparam name="TProgram">Program type to boot in the harness.</typeparam>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TProgram : MyGridProgram, new()
        {
            return ScriptFactories.ScriptFactory<TProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a script builder for a specific program type using constructor-style argument order.
        /// </summary>
        /// <typeparam name="TProgram">Program type to boot in the harness.</typeparam>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        /// <param name="gridName">Optional primary grid name.</param>
        protected static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            IMyCubeGrid primaryGrid,
            string gridName = null)
            where TProgram : MyGridProgram, new()
        {
            return ScriptFactories.ScriptFactory<TProgram>(primaryGrid, gridName);
        }

        /// <summary>
        /// Creates a command builder for the default test program.
        /// </summary>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static CommandBuilder<CoreTestProgram> CommandFactory(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
        {
            return CommandFactories.CommandFactory(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a command builder for the default test program using constructor-style argument order.
        /// </summary>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        /// <param name="gridName">Optional primary grid name.</param>
        protected static CommandBuilder<CoreTestProgram> CommandFactory(
            IMyCubeGrid primaryGrid,
            string gridName = null)
        {
            return CommandFactories.CommandFactory(primaryGrid, gridName);
        }

        /// <summary>
        /// Creates a command builder for a specific program type.
        /// </summary>
        /// <typeparam name="TProgram">Program type to boot in the harness.</typeparam>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static CommandBuilder<TProgram> CommandFactory<TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TProgram : MyGridProgram, new()
        {
            return CommandFactories.CommandFactory<TProgram>(gridName, primaryGrid);
        }

        /// <summary>
        /// Creates a command builder for a specific program type using constructor-style argument order.
        /// </summary>
        /// <typeparam name="TProgram">Program type to boot in the harness.</typeparam>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        /// <param name="gridName">Optional primary grid name.</param>
        protected static CommandBuilder<TProgram> CommandFactory<TProgram>(
            IMyCubeGrid primaryGrid,
            string gridName = null)
            where TProgram : MyGridProgram, new()
        {
            return CommandFactories.CommandFactory<TProgram>(primaryGrid, gridName);
        }

        /// <summary>
        /// Creates a module fixture for the default test program.
        /// </summary>
        /// <typeparam name="TModule">Concrete module type under test.</typeparam>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static Module<TModule> ModuleFactory<TModule>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TModule : BaseModule
        {
            return primaryGrid == null
                ? new Module<TModule>(gridName)
                : new Module<TModule>(primaryGrid, gridName);
        }

        /// <summary>
        /// Creates a module fixture for a specific program and module type.
        /// </summary>
        /// <typeparam name="TModule">Concrete module type under test.</typeparam>
        /// <typeparam name="TProgram">Program type to boot in the harness.</typeparam>
        /// <param name="gridName">Optional primary grid name.</param>
        /// <param name="primaryGrid">Optional pre-created primary grid.</param>
        protected static Module<TModule, TProgram> ModuleFactory<TModule, TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TModule : BaseModule
            where TProgram : MyGridProgram, new()
        {
            return primaryGrid == null
                ? new Module<TModule, TProgram>(gridName)
                : new Module<TModule, TProgram>(primaryGrid, gridName);
        }
    }

   
}
