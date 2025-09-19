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
    /// This command is used to ping all grids that are connected to the 
    /// antenna network and running and instance of Mother Core.
    /// </summary>
    public class PingCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        readonly Mother Mother;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "ping";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public PingCommand(Mother mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Execute the command.  We ping all grids using the IntergridMessageService.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            Mother.GetModule<IntergridMessageService>().Ping();

            return "Pinging all grids";
        }
    }
}
