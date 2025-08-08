using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{
    /// <summary>
    /// Command to print all available commands to the terminal window.
    /// </summary>
    public class HelpCommand : BaseModuleCommand
    {
        /// <summary>
        /// The CommandBus core module.
        /// </summary>
        readonly CommandBus Module;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public HelpCommand(CommandBus module)
        {
            Module = module;
        }

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "help";

        /// <summary>
        /// Executes the command to print all available commands.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            string commandsString = "";

            Module.Commands.ForEach(moduleCommand =>
            {
                commandsString += moduleCommand.GetCommandName() + "\n";
            });

            return commandsString;
        }
    }
}
