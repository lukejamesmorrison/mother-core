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

namespace IngameScript
{
    public class PingCommand : BaseModuleCommand
    {
        readonly Mother Mother;

        public override string Name => "ping";

        public PingCommand(Mother mother)
        {
            Mother = mother;
        }

        public override string Execute(TerminalCommand command)
        {
            Mother.GetModule<IntergridMessageService>().Ping();
            return "Pinging all grids";
        }
    }
}
