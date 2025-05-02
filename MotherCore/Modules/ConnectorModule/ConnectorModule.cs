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
    /// The ConnectorModule is responsible for managing connector blocks on the grid.
    /// </summary>
    public class ConnectorModule : BaseCoreModule
    {
        //Mother Mother;

        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        BlockCatalogue BlockCatalogue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public ConnectorModule(Mother mother) : base(mother) {
            //Mother = mother;
        }

        /// <summary>
        /// Boot the module. We reference modules, register commands, subscribe 
        /// to events and register our connectors for state monitoring.
        /// </summary>
        public override void Boot()
        {
            // Modules
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();
            //CommandBus = Mother.GetModule<CommandBus>();

            // Commands
            RegisterCommand(new LockConnectorCommand(this));
            RegisterCommand(new UnlockConnectorCommand(this));
            RegisterCommand(new ToggleConnectorCommand(this));

            // State Monitoring
            RegisterBlockTypeForStateMonitoring<IMyShipConnector>(
                connector => connector.Status,
                (block, state) => HandleConnectorStateChange(block as IMyShipConnector, state)
            );

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

        /// <summary>
        /// Locks the connector. This is used to connect the connector to another connector.
        /// </summary>
        /// <param name="connector"></param>
        public void LockConnector(IMyShipConnector connector)
        {
            connector.Connect();
        }

        /// <summary>
        /// Unlocks the connector. This is used to disconnect the connector from another connector.
        /// </summary>
        /// <param name="connector"></param>
        public void UnlockConnector(IMyShipConnector connector)
        {
            connector.Disconnect();
        }

        /// <summary>
        /// Toggles the connector. This is used to connect or disconnect the connector from another connector.
        /// </summary>
        /// <param name="connector"></param>
        public void ToggleConnector(IMyShipConnector connector)
        {
            if(connector.Status == MyShipConnectorStatus.Connected)
                UnlockConnector(connector);
            else
                LockConnector(connector);
        }
    }
}
