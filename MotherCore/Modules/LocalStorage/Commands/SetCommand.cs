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
//using static IngameScript.Program;
//using Sandbox.Game.GameSystems.Chat;

namespace IngameScript
{
	partial class Program
	{
        /// <summary>
        /// Command to set a key-value pair in the local storage of Mother.
        /// </summary>
        public class SetCommand : BaseModuleCommand
        {
            /// <summary>
            /// The LocalStorage core module.
            /// </summary>
            readonly LocalStorage Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "set";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public SetCommand(LocalStorage module)
            {
                Module = module;
            }

            /// <summary>
            /// Execute the command.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            public override string Execute(TerminalCommand command)
            {
                if (command.Arguments.Count == 0)
                    return CommandBus.Messages.NoArgumentsProvided;

                else if (command.Arguments.Count >= 2)
                {
                    string key = command.Arguments[0];
                    string value = command.Arguments[1];

                    Module.Set(key, value);

                   return $"{key}={value}";
                }

                return CommandBus.Messages.InvalidCommandFormat;
            }
        }
	}
}
