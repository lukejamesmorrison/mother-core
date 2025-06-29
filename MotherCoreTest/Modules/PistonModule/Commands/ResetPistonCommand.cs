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
    partial class Program
    {
        /// <summary>
        /// Command to reset the piston.
        /// </summary>
        public class ResetPistonCommand : BaseModuleCommand
        {
            /// <summary>
            /// The PistonModule extension module.
            /// </summary>
            readonly PistonModule Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "piston/reset";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public ResetPistonCommand(PistonModule module)
            {
                Module = module;
            }

            /// <summary>
            /// Executes the command.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            public override string Execute(TerminalCommand command)
            {
                if (command.Arguments.Count == 0)
                    return CommandBus.Messages.NoArgumentsProvided;
           
                string pistonName = command.Arguments[0];

                List<IMyPistonBase> pistons = Module.GetBlocksByName<IMyPistonBase>(pistonName);

                if (pistons.Count == 0)
                    return MessageFormatter.Format(BlockMessages.BlockNotFound, pistonName);

                pistons.ForEach(piston => Module.Reset(piston));

                return MessageFormatter.Format(BlockMessages.BlockResetting, pistonName);

            }
        }
    }
}
