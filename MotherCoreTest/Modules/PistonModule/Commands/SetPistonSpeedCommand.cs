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
        /// Command to set the speed of a piston.
        /// </summary>
        public class SetPistonSpeedCommand : BaseModuleCommand
        {
            /// <summary>
            /// The PistonModule extension module.
            /// </summary>
            readonly PistonModule Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "piston/speed";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public SetPistonSpeedCommand(PistonModule module)
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
                if (command.Arguments.Count < 2)
                    return CommandBus.Messages.NoArgumentsProvided;

                string pistonName = command.Arguments[0];
                string speedString = command.Arguments[1];

                List<IMyPistonBase> pistons = Module.GetBlocksByName<IMyPistonBase>(pistonName);

                if (pistons.Count == 0)
                    return MessageFormatter.Format(BlockMessages.BlockNotFound, pistonName);

                // determine the increment value
                float increment = GetIncrementalValue(speedString, command.Options);

                pistons.ForEach(piston =>
                {
                    // if the increment is zero, we assume the user wants
                    // to set an absolute distance.
                    float newSpeed = increment == 0
                            ? float.Parse(speedString)
                            : piston.Velocity + increment;

                    Module.SetPistonSpeed(piston, newSpeed);
                });

                return MessageFormatter.Format(BlockMessages.BlockUpdated, pistonName, $"speed={increment}");
            }
        }
    }
}
