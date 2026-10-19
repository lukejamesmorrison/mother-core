using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates paired merge blocks with shared mutable lock state.
    /// </summary>
    internal static class MergeConnectionFactory
    {
        /// <summary>
        /// Shared mutable state for a paired merge-block connection.
        /// </summary>
        sealed class MergeConnectionState
        {
            /// <summary>
            /// Whether the base-side merge block is enabled.
            /// </summary>
            public bool BaseEnabled;

            /// <summary>
            /// Whether the opposite-side merge block is enabled.
            /// </summary>
            public bool OtherEnabled = true;

            /// <summary>
            /// Whether the pair is physically aligned closely enough to connect.
            /// </summary>
            public bool IsConnected = true;

            /// <summary>
            /// Whether the pair is currently reporting a merged state.
            /// </summary>
            public bool IsMerged;
        }

        /// <summary>
        /// Creates a paired merge-block link spanning two grids.
        /// The returned block is registered on <paramref name="baseGrid"/>, and the
        /// opposite-side block is returned through <paramref name="otherBlock"/>.
        /// Both blocks share mutable enabled and merge state so harness code can drive
        /// merge and unmerge transitions by toggling either side.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the primary merge block.</param>
        /// <param name="otherGrid">The grid that owns the paired merge block.</param>
        /// <param name="otherBlock">Returns the paired merge block on <paramref name="otherGrid"/>.</param>
        /// <param name="onMerged">Invoked once when the pair transitions from unmerged to merged.</param>
        /// <param name="onUnmerged">Invoked once when the pair transitions from merged to unmerged.</param>
        /// <param name="baseCustomName">Optional custom name for the base-side merge block.</param>
        /// <param name="otherCustomName">Optional custom name for the opposite-side merge block.</param>
        /// <param name="baseEnabled">Optional initial enabled state for the base-side merge block.</param>
        /// <param name="otherEnabled">Optional initial enabled state for the opposite-side merge block.</param>
        /// <returns>The merge block registered on <paramref name="baseGrid"/>.</returns>
        public static IMyShipMergeBlock Create(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            out IMyShipMergeBlock otherBlock,
            Action onMerged,
            Action onUnmerged,
            string baseCustomName = null,
            string otherCustomName = null,
            bool baseEnabled = false,
            bool otherEnabled = true)
        {
            if (baseGrid == null)
                throw new ArgumentNullException(nameof(baseGrid));

            if (otherGrid == null)
                throw new ArgumentNullException(nameof(otherGrid));

            if (onMerged == null)
                throw new ArgumentNullException(nameof(onMerged));

            if (onUnmerged == null)
                throw new ArgumentNullException(nameof(onUnmerged));

            var state = new MergeConnectionState
            {
                BaseEnabled = baseEnabled,
                OtherEnabled = otherEnabled,
            };

            var baseBlock = new FakeShipMergeBlock(
                customName: baseCustomName ?? DefaultName("Harness Merge", baseGrid, otherGrid),
                cubeGrid: baseGrid);

            otherBlock = new FakeShipMergeBlock(
                customName: otherCustomName ?? DefaultName("Harness Merge", otherGrid, baseGrid),
                cubeGrid: otherGrid);

            ConfigureMergeBlock(
                baseBlock,
                () => state.BaseEnabled,
                value => UpdateState(value, () => state.OtherEnabled, enabled => state.BaseEnabled = enabled, state, onMerged, onUnmerged),
                () => ComputeState(state.BaseEnabled, state.OtherEnabled, state.IsConnected));

            ConfigureMergeBlock(
                otherBlock,
                () => state.OtherEnabled,
                value => UpdateState(value, () => state.BaseEnabled, enabled => state.OtherEnabled = enabled, state, onMerged, onUnmerged),
                () => ComputeState(state.OtherEnabled, state.BaseEnabled, state.IsConnected));

            return baseBlock;
        }

        /// <summary>
        /// Configures an existing pair of fake merge blocks to share mutable enabled
        /// and merge state so the harness can merge them on demand.
        /// </summary>
        /// <param name="baseBlock">The base-side merge block.</param>
        /// <param name="otherBlock">The opposite-side merge block.</param>
        /// <param name="onMerged">Invoked once when the pair transitions from unmerged to merged.</param>
        /// <param name="onUnmerged">Invoked once when the pair transitions from merged to unmerged.</param>
        /// <param name="baseEnabled">Optional initial enabled state for the base-side merge block.</param>
        /// <param name="otherEnabled">Optional initial enabled state for the opposite-side merge block.</param>
        public static void Configure(
            IMyShipMergeBlock baseBlock,
            IMyShipMergeBlock otherBlock,
            Action onMerged,
            Action onUnmerged,
            bool baseEnabled = false,
            bool otherEnabled = true)
        {
            if (baseBlock == null)
                throw new ArgumentNullException(nameof(baseBlock));

            if (otherBlock == null)
                throw new ArgumentNullException(nameof(otherBlock));

            if (onMerged == null)
                throw new ArgumentNullException(nameof(onMerged));

            if (onUnmerged == null)
                throw new ArgumentNullException(nameof(onUnmerged));

            var state = new MergeConnectionState
            {
                BaseEnabled = baseEnabled,
                OtherEnabled = otherEnabled,
            };

            ConfigureMergeBlock(
                baseBlock,
                () => state.BaseEnabled,
                value => UpdateState(value, () => state.OtherEnabled, enabled => state.BaseEnabled = enabled, state, onMerged, onUnmerged),
                () => ComputeState(state.BaseEnabled, state.OtherEnabled, state.IsConnected));

            ConfigureMergeBlock(
                otherBlock,
                () => state.OtherEnabled,
                value => UpdateState(value, () => state.BaseEnabled, enabled => state.OtherEnabled = enabled, state, onMerged, onUnmerged),
                () => ComputeState(state.OtherEnabled, state.BaseEnabled, state.IsConnected));
        }

        /// <summary>
        /// Maps an initial merge-block state to the enabled-state combination needed
        /// to expose that state before any harness-driven merge transition occurs.
        /// </summary>
        /// <param name="initialState">The requested initial merge-block state.</param>
        /// <param name="baseEnabled">Returns the initial enabled state for the base side.</param>
        /// <param name="otherEnabled">Returns the initial enabled state for the opposite side.</param>
        public static void ResolveInitialEnabledStates(
            MergeState initialState,
            out bool baseEnabled,
            out bool otherEnabled)
        {
            switch (initialState)
            {
                case MergeState.Locked:
                    baseEnabled = true;
                    otherEnabled = true;
                    break;

                case MergeState.Working:
                    baseEnabled = true;
                    otherEnabled = false;
                    break;

                default:
                    baseEnabled = false;
                    otherEnabled = true;
                    break;
            }
        }

        /// <summary>
        /// Configures the programmable-block-facing members for a fake merge block.
        /// </summary>
        /// <param name="block">The fake merge block instance to configure.</param>
        /// <param name="enabledAccessor">Returns the current enabled state for this side of the pair.</param>
        /// <param name="enabledSetter">Updates the enabled state for this side of the pair.</param>
        /// <param name="stateAccessor">Returns the current merge status to expose through <see cref="IMyShipMergeBlock.State"/>.</param>
        static void ConfigureMergeBlock(
            IMyShipMergeBlock block,
            Func<bool> enabledAccessor,
            Action<bool> enabledSetter,
            Func<MergeState> stateAccessor)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            var concreteBlock = block as FakeShipMergeBlock;
            if (concreteBlock == null)
                throw new NotSupportedException("MergeConnectionFactory requires FakeShipMergeBlock instances.");

            concreteBlock.Configure(enabledAccessor, enabledSetter, stateAccessor);
        }

        /// <summary>
        /// Applies an enabled-state change to one side of the pair and emits the
        /// appropriate merged or unmerged callback if the overall pair state changed.
        /// </summary>
        /// <param name="newEnabledValue">The new enabled state for the side being updated.</param>
        /// <param name="otherEnabledAccessor">Returns the current enabled state of the opposite side.</param>
        /// <param name="storeEnabled">Stores the new enabled state for the side being updated.</param>
        /// <param name="state">The shared merge-pair state object.</param>
        /// <param name="onMerged">Invoked when the pair transitions into the merged state.</param>
        /// <param name="onUnmerged">Invoked when the pair transitions out of the merged state.</param>
        static void UpdateState(
            bool newEnabledValue,
            Func<bool> otherEnabledAccessor,
            Action<bool> storeEnabled,
            MergeConnectionState state,
            Action onMerged,
            Action onUnmerged)
        {
            bool wasMerged = state.IsMerged;

            storeEnabled(newEnabledValue);

            bool isMerged = newEnabledValue && otherEnabledAccessor() && state.IsConnected;
            state.IsMerged = isMerged;

            if (!wasMerged && isMerged)
                onMerged();

            if (wasMerged && !isMerged)
                onUnmerged();
        }

        /// <summary>
        /// Computes the merge-block state exposed to programmable-block code for one side
        /// of the pair based on that block's own enabled state, the opposite side's enabled
        /// state, and whether the pair is physically connected.
        /// </summary>
        /// <param name="ownEnabled">Whether the current side is enabled.</param>
        /// <param name="otherEnabled">Whether the opposite side is enabled.</param>
        /// <param name="isConnected">Whether the two merge faces are physically connected.</param>
        /// <returns>The merge status to expose through <see cref="IMyShipMergeBlock.State"/>.</returns>
        static MergeState ComputeState(bool ownEnabled, bool otherEnabled, bool isConnected)
        {
            if (!ownEnabled)
                return MergeState.None;

            return otherEnabled && isConnected
                ? MergeState.Locked
                : MergeState.Working;
        }

        /// <summary>
        /// Builds a readable default name for a fake merge block.
        /// </summary>
        /// <param name="prefix">The name prefix to apply.</param>
        /// <param name="ownGrid">The grid that owns the merge block being named.</param>
        /// <param name="otherGrid">The opposite grid in the merge pair.</param>
        /// <returns>A readable default merge-block name for the harness.</returns>
        static string DefaultName(string prefix, IMyCubeGrid ownGrid, IMyCubeGrid otherGrid)
        {
            return $"{prefix} {ownGrid.CustomName}->{otherGrid.CustomName}";
        }
    }
}