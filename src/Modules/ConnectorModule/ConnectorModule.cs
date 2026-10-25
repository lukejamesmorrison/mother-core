using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
//using Sandbox.ModAPI;
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
using VRage.Scripting;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The ConnectorModule monitors connector blocks on the grid. It emits events 
    /// and runs hooks when a connector locks, unlocks or becomes ready to lock, 
    /// which the BlockCatalogue uses to keep the construct up to date. It does 
    /// not offer commands: scripts that let players operate connectors (ie. 
    /// Mother OS) register those themselves.
    /// </summary>
    public class ConnectorModule : BaseCoreModule
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        BlockCatalogue BlockCatalogue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public ConnectorModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Boot the module. We reference modules, register our connectors for 
        /// state monitoring, and add the connector state to conditions so every 
        /// script can test one (<c>if "Dock" connected</c>).
        /// </summary>
        public override void Boot()
        {
            // Modules
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();

            Block<IMyShipConnector>()
                .Status("status", connector => connector.Status)
                .Monitor(connector => connector.Status, HandleConnectorStateChange);

            // connected and connectable are state words of the status above
            Mother.GetModule<ConditionModule>().RegisterPredicate("disconnected", "status == unconnected");
        }

        /// <summary>
        /// Handles the connector state change event. This is called when 
        /// the connector's state changes.
        /// </summary>
        /// <param name="connector"></param>
        /// <param name="newState"></param>
        protected void HandleConnectorStateChange(IMyShipConnector connector, object newState)
        {
            var status = newState as MyShipConnectorStatus?;

            var previousStatus = PreviousStates.ContainsKey(connector.EntityId) 
                ? PreviousStates[connector.EntityId] as MyShipConnectorStatus? 
                : null;

            // we are docked
            if (status == MyShipConnectorStatus.Connected)
            {
                Emit<ConnectorLockedEvent>(connector);
                BlockCatalogue.RunHook(connector, "onLock");
            }

            // We are undocked
            else if (
                (status == MyShipConnectorStatus.Connectable && previousStatus == MyShipConnectorStatus.Connected)
                || status == MyShipConnectorStatus.Unconnected
            )
            {
                Emit<ConnectorUnlockedEvent>(connector);
                BlockCatalogue.RunHook(connector, "onUnlock");
            }

            // We are ready to lock
            else if (status == MyShipConnectorStatus.Connectable)
            {
                Emit<ConnectorReadyToLockEvent>(connector);
                BlockCatalogue.RunHook(connector, "onReady");
            }
        }
    }
}
