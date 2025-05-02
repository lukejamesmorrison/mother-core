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
        ///
        readonly CommandBus Module;

        public HelpCommand(CommandBus module)
        {
            Module = module;
        }

        public override string Name => "help";


        public override string Execute(TerminalCommand command)
        {
            //List<IModuleCommand> commands = Module.Commands;
            //string titleString = "Commands:\n";
            string commandsString = "";


            Module.Commands.ForEach(moduleCommand =>
            {
                commandsString += moduleCommand.GetCommandName() + "\n";
            });


            // print all Commands in list
            //foreach (var moduleCommand in Module.Commands)
            //{
            //    commandsString += moduleCommand.GetCommandName() + "\n";
            //}

            return 
                //titleString + 
                commandsString;
        }
    }
}
