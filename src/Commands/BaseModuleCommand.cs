using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Base class for all module commands.
    /// </summary>
    public abstract class BaseModuleCommand : IModuleCommand
    {
        /// <summary>
        /// The name of the command. This should be unique for each command.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Execute the a Terminal Command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public abstract string Execute(TerminalCommand command);

        /// <summary>
        /// Get the command name.
        /// </summary>
        /// <returns></returns>
        public string GetCommandName() => Name;

        /// <summary>
        /// Parses a string representing an incremental value. Forwards to
        /// <see cref="TerminalCommand.GetIncrement(string)"/>; lambda commands
        /// should call that directly. Remove once no command class uses it.
        /// </summary>
        /// <param name="valueString"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected float GetIncrementalValue(string valueString, Dictionary<string, string> options)
            => TerminalCommand.GetIncrement(valueString, options);

        /// <summary>
        /// Calculates the distributed value for cumulative operations across multiple blocks.
        /// When cumulative mode is enabled, the total value is divided evenly among all blocks.
        /// </summary>
        /// <param name="totalValue">The total value to distribute.</param>
        /// <param name="blockCount">The number of blocks to distribute across.</param>
        /// <param name="isCumulative">Whether cumulative mode is enabled.</param>
        /// <returns>The value per block (distributed if cumulative, otherwise the original value).</returns>
        protected float GetDistributedValue(float totalValue, int blockCount, bool isCumulative)
            => TerminalCommand.GetDistributedValue(totalValue, blockCount, isCumulative);

        /// <summary>
        /// Determines if shared mode is enabled based on command options. When enabled,
        /// values are distributed evenly across all blocks in the group.
        /// </summary>
        /// <param name="options">The command options dictionary.</param>
        /// <returns>True if shared mode is enabled.</returns>
        protected bool IsSharedMode(Dictionary<string, string> options) => options.ContainsKey("share");
    }
}
