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
        /// Command to set the distance of a piston.
        /// </summary>
        public class SetPistonDistanceCommand : BaseModuleCommand
        {
            /// <summary>
            /// The PistonModule extension module.
            /// </summary>
            readonly PistonModule Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "piston/distance";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public SetPistonDistanceCommand(PistonModule module)
            {
                Module = module;
            }

            /// <summary>
            /// Executes the command. We check if this distance is absolute, 
            /// or being incremented for finer tuned control.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            public override string Execute(TerminalCommand command)
            {
                if (command.Arguments.Count == 0)
                {
                    return CommandBus.Messages.NoArgumentsProvided;
                }
                else if (command.Arguments.Count >= 2)
                {
                    string pistonName = command.Arguments[0];
                    string pistonDistanceString = command.Arguments[1];
                    string speedString = command.GetOption("speed");
                    float speed = !string.IsNullOrEmpty(speedString) ? float.Parse(speedString) : PistonModule.DEFAULT_SPEED;

                    List<IMyPistonBase> pistons = Module.GetBlocksByName<IMyPistonBase>(pistonName);

                    if (pistons.Count == 0)
                        return MessageFormatter.Format(BlockMessages.BlockNotFound, pistonName);

                    // determine the increment value
                    float increment = GetIncrementalValue(pistonDistanceString, command.Options);

                    pistons.ForEach(piston => {
                        // if the increment is zero, we assume the user
                        // wants to set an absolute distance
                        float newDistance = increment == 0 
                            ? float.Parse(pistonDistanceString) 
                            : piston.CurrentPosition + increment;

                        Module.SetDistance(piston, newDistance, speed);
                    });


                    return MessageFormatter.Format(BlockMessages.BlockUpdated, pistonName, $"distance={pistonDistanceString}m");
                }

                return CommandBus.Messages.InvalidCommandFormat;
            }
        }
    }
}
