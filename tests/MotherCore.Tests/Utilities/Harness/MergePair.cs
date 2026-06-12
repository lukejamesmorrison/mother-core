using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Describes a pre-registered merge-block relationship between two world grids.
    /// The concrete merge-block instances are created lazily when the world binds
    /// to a topology-backed terminal system.
    /// </summary>
    public sealed class MergePair
    {
        internal MergePair(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseMergeBlockName,
            string otherMergeBlockName,
            MergeState initialState)
        {
            BaseGrid = baseGrid;
            OtherGrid = otherGrid;
            BaseMergeBlockName = baseMergeBlockName;
            OtherMergeBlockName = otherMergeBlockName;
            InitialState = initialState;
        }

        /// <summary>
        /// Gets the grid that owns the base-side merge block.
        /// </summary>
        public IMyCubeGrid BaseGrid { get; }

        /// <summary>
        /// Gets the grid that owns the opposite-side merge block.
        /// </summary>
        public IMyCubeGrid OtherGrid { get; }

        /// <summary>
        /// Gets the optional custom name to assign to the base-side merge block.
        /// </summary>
        public string BaseMergeBlockName { get; }

        /// <summary>
        /// Gets the optional custom name to assign to the opposite-side merge block.
        /// </summary>
        public string OtherMergeBlockName { get; }

        /// <summary>
        /// Gets the initial merge state to expose when the pair is materialized.
        /// </summary>
        public MergeState InitialState { get; }

        /// <summary>
        /// Gets the base-side merge block after the pair has been materialized.
        /// </summary>
        public IMyShipMergeBlock BaseBlock { get; internal set; }

        /// <summary>
        /// Gets the opposite-side merge block after the pair has been materialized.
        /// </summary>
        public IMyShipMergeBlock OtherBlock { get; internal set; }
    }
}
