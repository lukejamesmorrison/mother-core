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
    /// Command to halt execution by clearing all queued tasks and active coroutines.
    /// </summary>
    public class HaltCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Clock core module.
        /// </summary>
        readonly Clock Clock;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clock"></param>
        public HaltCommand(Clock clock)
        {
            Clock = clock;
        }

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "halt";

        /// <summary>
        /// Executes the command to halt all queued tasks and active coroutines.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            Clock.Halt();
            return "Halted.";
        }
    }
}
