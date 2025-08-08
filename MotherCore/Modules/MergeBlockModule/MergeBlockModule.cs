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
    /// This module handles interactions with merge blocks on the grid.
    /// </summary>
    public class MergeBlockModule : BaseCoreModule
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        //BlockCatalogue BlockCatalogue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public MergeBlockModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Boots the module. We reference modules, register commands, subscribe to 
        /// events, and register blocks for ongoing state monitoring.
        /// </summary>
        public override void Boot()
        {
            // Modules
            //BlockCatalogue = Mother.GetModule<BlockCatalogue>();

            // Commands
            //RegisterCommand(new LockMergeBlockCommand(this));
            //RegisterCommand(new UnlockMergeBlockCommand(this));
            //RegisterCommand(new ToggleMergeBlockCommand(this));

            // Events
            //Subscribe<MergeBlockLockedEvent>();
            //Subscribe<MergeBlockUnlockedEvent>();         
            //Subscribe<MergeBlockReadyToLockEvent>();

            // State Monitoring
            //RegisterBlockTypeForStateMonitoring<IMyShipMergeBlock>(
            //    mergeBlock => mergeBlock.State,  // State Selector
            //    (block, state) => HandleMergeBlockStateChange(block as IMyShipMergeBlock, state)
            //);
        }

        /// <summary>
        /// Handle state changes for merge blocks. This is called when the state of a merge block changes.
        /// </summary>
        /// <param name="mergeBlock"></param>
        /// <param name="newState"></param>
        protected void HandleMergeBlockStateChange(IMyShipMergeBlock mergeBlock, object newState)
        {
            var status = newState as MergeState?;

            if (status.HasValue)
            {
                switch (status)
                {
                    // when turning off - unmerged grids
                    case MergeState.None:
                        //Mother.Print($"{mergeBlock.CustomName} none.");
                        Emit<MergeBlockOffEvent>(mergeBlock);
                        break;

                    // when finding each other
                    case MergeState.Constrained:
                        //Mother.Print($"{mergeBlock.CustomName} constrained.");
                        break;

                    // when locking and merging grids
                    case MergeState.Locked:
                        //Mother.Print($"{mergeBlock.CustomName} locked.");
                        Emit<MergeBlockLockedEvent>(mergeBlock);
                        break;
                }
            }
        }

        /// <summary>
        /// Lock a merge block.
        /// </summary>
        /// <param name="mergeBlock"></param>
        public void LockMergeBlock(IMyShipMergeBlock mergeBlock)
        {
            mergeBlock.Enabled = true;
        }

        /// <summary>
        /// Unlock a merge block.
        /// </summary>
        /// <param name="mergeBlock"></param>
        public void UnlockMergeBlock(IMyShipMergeBlock mergeBlock)
        {
            mergeBlock.Enabled = false;
        }

        /// <summary>
        /// Toggle the enabled state of a merge block.
        /// </summary>
        /// <param name="mergeBlock"></param>
        public void ToggleMergeBlock(IMyShipMergeBlock mergeBlock)
        {
            if (mergeBlock.Enabled)
                UnlockMergeBlock(mergeBlock);

            else
                LockMergeBlock(mergeBlock);
        }

        /// <summary>
        /// Handle events emitted by a module if subscribed.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            //IMyShipMergeBlock mergeBlock = (IMyShipMergeBlock)eventData;

            //if (e is MergeBlockLockedEvent)
            //    BlockCatalogue.RunHook(mergeBlock, "onLock");

            //else if (e is MergeBlockUnlockedEvent)
            //    BlockCatalogue.RunHook(mergeBlock, "onUnlock");

            //else if (e is MergeBlockReadyToLockEvent)
            //    BlockCatalogue.RunHook(mergeBlock, "onReady");
        }
    }
}
