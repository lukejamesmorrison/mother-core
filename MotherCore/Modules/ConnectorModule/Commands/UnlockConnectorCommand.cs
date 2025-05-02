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
    /// Command to unlock a connector block.
    /// </summary>
    public class UnlockConnectorCommand : BaseModuleCommand
    {
        /// <summary>
        /// The ConnectorModule extension module.
        /// </summary>
        readonly ConnectorModule Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "connector/unlock";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public UnlockConnectorCommand(ConnectorModule module)
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
            {
                return CommandBus.Messages.NoArgumentsProvided;
            }
            else
            {
                string connectorName = command.Arguments[0];

                List<IMyShipConnector> connectors = Module.GetBlocksByName<IMyShipConnector>(connectorName);

                if (connectors.Count == 0)
                    return MessageFormatter.Format(BlockMessages.BlockNotFound, connectorName);

                connectors.ForEach(connector => Module.UnlockConnector(connector));

                return MessageFormatter.Format(BlockMessages.BlockUnlocked, connectorName);
            }
        }
    }
}
