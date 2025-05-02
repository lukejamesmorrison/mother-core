using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Permissions;
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
        /// This command is used to lock a connector block on the grid.
        /// </summary>
        public class LockConnectorCommand : BaseModuleCommand
        {
            /// <summary>
            /// The ConnectorModule extension module.
            /// </summary>
            readonly ConnectorModule Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "connector/lock";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public LockConnectorCommand(ConnectorModule module)
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
                {
                    return CommandBus.Messages.InvalidCommandFormat;
                }
                else
                {
                    string connectorName = command.Arguments[0];

                    List<IMyShipConnector> connectors = Module.GetBlocksByName<IMyShipConnector>(connectorName);

                    if (connectors.Count == 0)
                        return MessageFormatter.Format(BlockMessages.BlockNotFound, connectorName);

                    connectors.ForEach(connector => Module.LockConnector(connector));

                    return MessageFormatter.Format(BlockMessages.BlockLocked, connectorName);
                }
            }
        }
    }
}
