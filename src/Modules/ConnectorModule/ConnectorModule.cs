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
                .Monitor(connector => connector.Status)
                .On<ConnectorLockedEvent>(MyShipConnectorStatus.Connected, "onLock")
                .On<ConnectorUnlockedEvent>(MyShipConnectorStatus.Unconnected, "onUnlock")
                .On<ConnectorUnlockedEvent>(MyShipConnectorStatus.Connectable, "onUnlock", null, WasConnected)
                .On<ConnectorReadyToLockEvent>(MyShipConnectorStatus.Connectable, "onReady");

            // connected and connectable are state words of the status above
            Mother.GetModule<ConditionModule>().RegisterPredicate("disconnected", "status == unconnected");
        }

        /// <summary>
        /// Whether the previous status was Connected, which makes a move to 
        /// Connectable an unlock rather than ready to lock.
        /// </summary>
        static bool WasConnected(object previous) => Equals(previous, MyShipConnectorStatus.Connected);
    }
}
