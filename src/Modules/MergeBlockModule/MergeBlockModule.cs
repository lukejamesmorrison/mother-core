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
    /// When merge blocks lock, two grids become one. When they unlock (turn off),
    /// the grids separate. This module monitors these state changes and triggers
    /// a construct refresh in BlockCatalogue to ensure all blocks remain targetable.
    /// </summary>
    public class MergeBlockModule : BaseCoreModule
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        BlockCatalogue BlockCatalogue;

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
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();

            // Commands
            //RegisterCommand(new LockMergeBlockCommand(this));
            //RegisterCommand(new UnlockMergeBlockCommand(this));
            //RegisterCommand(new ToggleMergeBlockCommand(this));

            // Events
            //Subscribe<MergeBlockLockedEvent>();
            //Subscribe<MergeBlockUnlockedEvent>();         
            //Subscribe<MergeBlockReadyToLockEvent>();

            // State Monitoring - Monitor State for all merge blocks
            RegisterBlockTypeForStateMonitoring<IMyShipMergeBlock>(
                mergeBlock => mergeBlock.State,
                (block, state) => HandleMergeBlockStateChange(block as IMyShipMergeBlock, state)
            );
        }

        /// <summary>
        /// Handle state changes for merge blocks. This is called when the state of a merge block changes.
        /// When blocks lock (merge), grids become one. When blocks turn off (unmerge), grids separate.
        /// Both scenarios require a full construct refresh since the grid topology changes.
        /// </summary>
        /// <param name="mergeBlock"></param>
        /// <param name="newState"></param>
        protected void HandleMergeBlockStateChange(IMyShipMergeBlock mergeBlock, object newState)
        {
            var status = newState as MergeState?;

            var previousState = PreviousStates.ContainsKey(mergeBlock.EntityId)
                ? PreviousStates[mergeBlock.EntityId] as MergeState?
                : null;

            if (status.HasValue)
            {
                switch (status)
                {
                    // When turning off - unmerged grids (grid separation)
                    case MergeState.None:
                        // Only trigger if we were previously locked (actual unmerge)
                        if (previousState == MergeState.Locked)
                        {
                            Emit<MergeBlockOffEvent>(mergeBlock);
                            BlockCatalogue.RunHook(mergeBlock, "onUnlock");

                            // Trigger full construct refresh - grids have separated
                            BlockCatalogue.RefreshConstruct();
                        }
                        break;

                    // When finding each other (approaching)
                    case MergeState.Constrained:
                        // No action needed - blocks are approaching but not yet merged
                        break;

                    // When locking and merging grids (grid merge)
                    case MergeState.Locked:
                        // Only trigger if we were not previously locked (actual merge)
                        if (previousState != MergeState.Locked)
                        {
                            Emit<MergeBlockLockedEvent>(mergeBlock);
                            BlockCatalogue.RunHook(mergeBlock, "onLock");

                            // Trigger full construct refresh - grids have merged into one
                            BlockCatalogue.RefreshConstruct();
                        }
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
