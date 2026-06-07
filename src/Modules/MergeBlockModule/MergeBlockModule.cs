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
        /// Merge blocks whose "onMerge" hook should run after the next construct refresh.
        /// We defer the hook because RunHook calls GetBlocksByName which filters against
        /// ConstructGridIds. At the moment the state change fires, RefreshConstruct has only
        /// been queued — ConstructGridIds still reflects the old topology. Running the hook
        /// after the refresh ensures block lookups resolve correctly.
        /// </summary>
        readonly List<IMyShipMergeBlock> _pendingMergeHooks = new List<IMyShipMergeBlock>();

        /// <summary>
        /// Merge blocks whose "onUnmerge" hook should run after the next construct refresh.
        /// </summary>
        readonly List<IMyShipMergeBlock> _pendingUnmergeHooks = new List<IMyShipMergeBlock>();

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

            // Clear any hooks that were pending from a previous boot cycle so they
            // do not fire spuriously after the next construct refresh.
            _pendingMergeHooks.Clear();
            _pendingUnmergeHooks.Clear();

            // After a construct refresh, pick up any merge blocks that were added from
            // the newly merged grid and were not present during this Boot().
            Subscribe<ConstructRefreshedEvent>();

            // Monitor State for all merge blocks currently on grid
            RegisterBlockTypeForStateMonitoring<IMyShipMergeBlock>(
                mergeBlock => mergeBlock.State,
                (block, state) => HandleMergeBlockStateChange(block as IMyShipMergeBlock, state)
            );
        }

        /// <summary>
        /// Handle state changes for merge blocks. This is called when the state of a merge block changes.
        /// When blocks lock (merge), grids become one. When blocks turn off (unmerge), grids separate.
        /// Hooks are deferred to run after RefreshConstruct completes so that ConstructGridIds reflects
        /// the new topology before any block lookups are attempted.
        /// </summary>
        /// <param name="mergeBlock"></param>
        /// <param name="newState"></param>
        protected void HandleMergeBlockStateChange(IMyShipMergeBlock mergeBlock, object newState)
        {
            var status = newState as MergeState?;

            if(Mother.DebugMode)
                Mother.Print($"Merge block status changed:\n{mergeBlock.CustomName} : {status}", false);

            var previousState = PreviousStates.ContainsKey(mergeBlock.EntityId)
                ? PreviousStates[mergeBlock.EntityId] as MergeState?
                : null;

            if (status.HasValue)
            {
                switch (status)
                {
                    // When turning off - unmerged grids (grid separation)
                    case MergeState.Working:
                    case MergeState.None:
                        if (previousState == MergeState.Locked)
                        {
                            Emit<MergeBlockOffEvent>(mergeBlock);

                            // Defer hook - ConstructGridIds is updated by RefreshConstruct which
                            // has only been queued at this point, not yet executed.
                            _pendingUnmergeHooks.Add(mergeBlock);

                            //Mother.Print($"Merge block unlocked: {status}");
                        }
                        break;

                    // When locking and merging grids (grid merge)
                    case MergeState.Locked:
                        if (previousState != MergeState.Locked)
                        {
                            Emit<MergeBlockLockedEvent>(mergeBlock);

                            // Defer hook for the same reason as above.
                            _pendingMergeHooks.Add(mergeBlock);

                            //Mother.Print($"Merge block locked: {status}");
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Handle events emitted by other modules.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (e is ConstructRefreshedEvent)
            {
                // Register any merge blocks that arrived from the newly merged/attached grid.
                // preserveState: true keeps existing PreviousStates so in-flight transitions
                // (e.g. Locked recorded before the refresh) are not discarded.
                RegisterBlockTypeForStateMonitoring<IMyShipMergeBlock>(
                    mergeBlock => mergeBlock.State,
                    (block, state) => HandleMergeBlockStateChange(block as IMyShipMergeBlock, state),
                    preserveState: true
                );

                // ConstructGridIds is now up to date — run any deferred hooks.
                foreach (var block in _pendingMergeHooks)
                    BlockCatalogue.RunHook(block, "onMerge");

                foreach (var block in _pendingUnmergeHooks)
                    BlockCatalogue.RunHook(block, "onUnmerge");

                _pendingMergeHooks.Clear();
                _pendingUnmergeHooks.Clear();
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
    }
}