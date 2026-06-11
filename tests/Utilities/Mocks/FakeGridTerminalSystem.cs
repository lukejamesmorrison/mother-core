using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// In-memory test implementation of <see cref="IMyGridTerminalSystem"/>.
    /// It models the subset of the programmable block terminal system that the
    /// MotherCore test harness needs: block enumeration, group enumeration,
    /// name and ID lookup, and basic construct reachability.
    /// </summary>
    /// <remarks>
    /// In the real game, a grid terminal system can reach blocks on the local
    /// grid and on mechanically connected subgrids. This fake mirrors that
    /// behavior by treating <see cref="PrimaryGrid"/> as the root grid and by
    /// creating synthetic <see cref="IMyMechanicalConnectionBlock"/> links for
    /// any additional grids created through <see cref="CreateGrid"/> or inferred
    /// when blocks are added on another grid.
    /// </remarks>
    internal class FakeGridTerminalSystem : IMyGridTerminalSystem
    {
        /// <summary>
        /// Tracks the mutable topology state for one merge-block pair.
        /// It remembers the original grids, the live block instances, and which
        /// blocks must be rebound when the pair merges or unmerges.
        /// </summary>
        sealed class MergeConnectionRegistration
        {
            public IMyCubeGrid BaseGrid;
            public IMyCubeGrid OtherGrid;
            public IMyShipMergeBlock BaseBlock;
            public IMyShipMergeBlock OtherBlock;
            public IMyCubeGrid SurvivingGrid;
            public IMyCubeGrid AbsorbedGrid;
            public HashSet<long> AbsorbedBlockIds = new HashSet<long>();
            public bool WasPrimaryGridAbsorbed;
            public bool IsMerged;
        }

        readonly List<IMyTerminalBlock> _blocks = new List<IMyTerminalBlock>();
        readonly List<IMyBlockGroup> _groups = new List<IMyBlockGroup>();
        readonly HashSet<long> _reachableGridIds = new HashSet<long>();
        readonly HashSet<long> _constructGridIds = new HashSet<long>();
        readonly List<MergeConnectionRegistration> _mergeConnections = new List<MergeConnectionRegistration>();

        /// <summary>
        /// Initializes a new in-memory grid terminal system rooted at a single
        /// primary grid.
        /// </summary>
        /// <param name="primaryGridName">
        /// Optional display name for the primary grid that owns the terminal system.
        /// </param>
        /// <param name="primaryGridEntityId">
        /// Optional entity ID for the primary grid. When omitted, a synthetic ID is generated.
        /// </param>
        public FakeGridTerminalSystem(string primaryGridName = "Test Grid", long? primaryGridEntityId = null)
            : this(GridFactory.Create(primaryGridName, primaryGridEntityId))
        {
        }

        /// <summary>
        /// Initializes a new in-memory grid terminal system rooted at the supplied
        /// primary grid.
        /// </summary>
        /// <param name="primaryGrid">The grid that should own the programmable block.</param>
        public FakeGridTerminalSystem(IMyCubeGrid primaryGrid)
        {
            PrimaryGrid = primaryGrid 
                ?? throw new ArgumentNullException(nameof(primaryGrid));

            _reachableGridIds.Add(PrimaryGrid.EntityId);
            _constructGridIds.Add(PrimaryGrid.EntityId);
        }

        /// <summary>
        /// Gets the root grid for this terminal system.
        /// All additional grids registered with this fake are treated as part of
        /// the same construct relative to this grid.
        /// </summary>
        public IMyCubeGrid PrimaryGrid { get; private set; }

        /// <summary>
        /// Reports whether this terminal system has topology knowledge for
        /// <paramref name="grid"/>.
        /// </summary>
        public bool KnowsGrid(IMyCubeGrid grid)
        {
            if (grid == null)
                return false;

            return _reachableGridIds.Contains(grid.EntityId);
        }

        /// <summary>
        /// Reports whether two grids currently belong to the same construct
        /// according to this terminal system's topology model.
        /// </summary>
        public bool IsSameConstruct(IMyCubeGrid firstGrid, IMyCubeGrid secondGrid)
        {
            if (firstGrid == null || secondGrid == null)
                return false;

            return _constructGridIds.Contains(firstGrid.EntityId)
                && _constructGridIds.Contains(secondGrid.EntityId);
        }

        /// <summary>
        /// Creates an additional grid that is reachable from this terminal system.
        /// </summary>
        /// <param name="gridName">Optional display name for the new grid.</param>
        /// <param name="entityId">Optional entity ID for the new grid.</param>
        /// <param name="connectionKind">The synthetic mechanical connection type used to attach the new grid.</param>
        /// <returns>
        /// A synthetic <see cref="IMyCubeGrid"/> that is automatically linked back
        /// to <see cref="PrimaryGrid"/> as part of the same construct.
        /// </returns>
        public IMyCubeGrid CreateGrid(
            string gridName = null,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            var grid = GridFactory.Create(
                gridName ?? $"Grid-{_reachableGridIds.Count + 1}",
                entityId);

            ConnectGrids(PrimaryGrid, grid, connectionKind);
            return grid;
        }

        /// <summary>
        /// Creates a mechanical connection block between two grids and registers
        /// the base block in this terminal system.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the mechanical base block.</param>
        /// <param name="topGrid">The grid attached through the mechanical top part.</param>
        /// <param name="connectionKind">The connection type to create.</param>
        /// <returns>The registered mechanical base block.</returns>
        public IMyMechanicalConnectionBlock ConnectGrids(
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            if (baseGrid == null)
                throw new ArgumentNullException(nameof(baseGrid));

            if (topGrid == null)
                throw new ArgumentNullException(nameof(topGrid));

            var existingConnection = _blocks
                .OfType<IMyMechanicalConnectionBlock>()
                .FirstOrDefault(block =>
                    block.CubeGrid != null
                    && block.TopGrid != null
                    && block.CubeGrid.EntityId == baseGrid.EntityId
                    && block.TopGrid.EntityId == topGrid.EntityId);

            if (existingConnection != null)
                return existingConnection;

            _reachableGridIds.Add(baseGrid.EntityId);
            _reachableGridIds.Add(topGrid.EntityId);
            _constructGridIds.Add(baseGrid.EntityId);
            _constructGridIds.Add(topGrid.EntityId);

            var connection = MechanicalConnectionFactory.Create(connectionKind, baseGrid, topGrid);
            AddBlock(connection, baseGrid);

            return connection;
        }

        /// <summary>
        /// Creates a paired connector link between two grids while keeping them as
        /// separate constructs for <see cref="IMyTerminalBlock.IsSameConstructAs(IMyTerminalBlock)"/>.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the primary connector.</param>
        /// <param name="otherGrid">The grid that owns the paired connector.</param>
        /// <param name="baseConnectorName">Optional custom name for the base-grid connector.</param>
        /// <param name="otherConnectorName">Optional custom name for the other-grid connector.</param>
        /// <param name="initialStatus">The initial shared connector status.</param>
        /// <returns>The connector registered on <paramref name="baseGrid"/>.</returns>
        public IMyShipConnector ConnectGridsViaConnector(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseConnectorName = null,
            string otherConnectorName = null,
            MyShipConnectorStatus initialStatus = MyShipConnectorStatus.Connected)
        {
            if (baseGrid == null)
                throw new ArgumentNullException(nameof(baseGrid));

            if (otherGrid == null)
                throw new ArgumentNullException(nameof(otherGrid));

            _reachableGridIds.Add(baseGrid.EntityId);
            _reachableGridIds.Add(otherGrid.EntityId);
            _constructGridIds.Add(baseGrid.EntityId);

            IMyShipConnector otherConnector;
            var baseConnector = ConnectorConnectionFactory.Create(
                baseGrid,
                otherGrid,
                out otherConnector,
                baseConnectorName,
                otherConnectorName,
                initialStatus);

            AddBlock(baseConnector, baseGrid);
            AddBlock(otherConnector, otherGrid);

            return baseConnector;
        }

        /// <summary>
        /// Creates a paired merge-block link between two grids.
        /// Before the blocks lock, the remote grid stays outside the construct.
        /// When they lock, blocks on the absorbed side are rebound onto the surviving grid.
        /// When they unlock, those blocks are restored to their original grid.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the base-side merge block.</param>
        /// <param name="otherGrid">The grid that owns the opposite-side merge block.</param>
        /// <param name="baseMergeBlockName">Optional custom name for the base-side merge block.</param>
        /// <param name="otherMergeBlockName">Optional custom name for the opposite-side merge block.</param>
        /// <param name="initialState">The initial merge state to expose for the pair.</param>
        /// <returns>The merge block registered on <paramref name="baseGrid"/>.</returns>
        public IMyShipMergeBlock ConnectGridsViaMergeBlock(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseMergeBlockName = null,
            string otherMergeBlockName = null,
            MergeState initialState = MergeState.Locked)
        {
            if (baseGrid == null)
                throw new ArgumentNullException(nameof(baseGrid));

            if (otherGrid == null)
                throw new ArgumentNullException(nameof(otherGrid));

            _reachableGridIds.Add(baseGrid.EntityId);
            _reachableGridIds.Add(otherGrid.EntityId);
            _constructGridIds.Add(baseGrid.EntityId);

            var registration = new MergeConnectionRegistration
            {
                BaseGrid = baseGrid,
                OtherGrid = otherGrid,
            };

            bool baseEnabled;
            bool otherEnabled;
            MergeConnectionFactory.ResolveInitialEnabledStates(initialState, out baseEnabled, out otherEnabled);

            IMyShipMergeBlock otherMergeBlock;
            var baseMergeBlock = MergeConnectionFactory.Create(
                baseGrid,
                otherGrid,
                out otherMergeBlock,
                onMerged: () => MergeGrids(registration),
                onUnmerged: () => UnmergeGrids(registration),
                baseCustomName: baseMergeBlockName,
                otherCustomName: otherMergeBlockName,
                baseEnabled: baseEnabled,
                otherEnabled: otherEnabled);

            registration.BaseBlock = baseMergeBlock;
            registration.OtherBlock = otherMergeBlock;

            _mergeConnections.Add(registration);

            AddBlock(baseMergeBlock, baseGrid);
            AddBlock(otherMergeBlock, otherGrid);

            if (initialState == MergeState.Locked)
                MergeGrids(registration);

            return baseMergeBlock;
        }

        /// <summary>
        /// Forces a paired merge-block link into the merged state by enabling both
        /// sides of the pair. The second enable transition triggers the topology rewrite.
        /// </summary>
        /// <param name="mergeBlock">Either side of the merge-block pair to merge.</param>
        public void MergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            var registration = FindMergeConnection(mergeBlock);

            registration.BaseBlock.Enabled = true;
            registration.OtherBlock.Enabled = true;
        }

        /// <summary>
        /// Forces a paired merge-block link into the merged state after verifying that
        /// both supplied blocks belong to the same merge pair.
        /// </summary>
        /// <param name="firstMergeBlock">One side of the merge-block pair.</param>
        /// <param name="secondMergeBlock">The opposite side of the merge-block pair.</param>
        /// <returns>None. The call mutates merge state in place.</returns>
        public void MergeBlocks(IMyShipMergeBlock firstMergeBlock, IMyShipMergeBlock secondMergeBlock)
        {
            var registration = FindOrCreateMergeConnection(firstMergeBlock, secondMergeBlock);

            MergeBlocks(registration.BaseBlock);
        }

        /// <summary>
        /// Forces a paired merge-block link out of the merged state by disabling the
        /// supplied side of the pair. The pair then reports an unlocked state.
        /// </summary>
        /// <param name="mergeBlock">The side of the merge-block pair to turn off.</param>
        public void UnmergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            FindMergeConnection(mergeBlock);
            mergeBlock.Enabled = false;
        }

        /// <summary>
        /// Registers a terminal block with this grid terminal system.
        /// </summary>
        /// <param name="block">The block to register.</param>
        /// <param name="grid">
        /// Optional target grid. When omitted, the block's current grid is used;
        /// if the block has no assigned grid, it is placed on <see cref="PrimaryGrid"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="block"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the supplied block does not expose a grid that can be
        /// assigned or updated by the harness.
        /// </exception>
        /// <remarks>
        /// If the target grid is not yet connected, this method synthesizes a
        /// mechanical link so that subsequent enumeration behaves like a single
        /// multi-grid construct.
        /// </remarks>
        public void AddBlock(IMyTerminalBlock block, IMyCubeGrid grid = null)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            var targetGrid = grid ?? block.CubeGrid ?? PrimaryGrid;
            var mergedRegistration = _mergeConnections.FirstOrDefault(connection =>
                connection.IsMerged
                && connection.AbsorbedGrid != null
                && targetGrid != null
                && connection.AbsorbedGrid.EntityId == targetGrid.EntityId);

            if (mergedRegistration != null)
            {
                mergedRegistration.AbsorbedBlockIds.Add(block.EntityId);
                targetGrid = mergedRegistration.SurvivingGrid;
            }

            if (!TerminalBlockFactory.TryAssignCubeGrid(block, targetGrid))
            {
                throw new InvalidOperationException(
                    "The supplied block does not expose an assignable CubeGrid. " +
                    "Create blocks with TerminalBlockFactory.Create<TBlock>() or provide a block with CubeGrid already configured.");
            }

            EnsureReachableFromPrimary(targetGrid);

            AssignSameConstructEvaluator(block);

            if (!_blocks.Contains(block))
                _blocks.Add(block);
        }

        /// <summary>
        /// Registers a named block group in this terminal system.
        /// </summary>
        /// <param name="name">The block group name.</param>
        /// <param name="blocks">The blocks that belong to the group.</param>
        public void AddBlockGroup(string name, params IMyTerminalBlock[] blocks)
        {
            _groups.Add(new FakeBlockGroup(name, blocks));
        }

        /// <summary>
        /// Fills the provided list with every block currently reachable by this
        /// fake terminal system.
        /// </summary>
        /// <param name="blocks">The destination list to append blocks to.</param>
        /// <remarks>
        /// Reachable blocks include the primary grid, any registered secondary
        /// grids, and any synthetic mechanical connection blocks created by the harness.
        /// </remarks>
        public void GetBlocks(List<IMyTerminalBlock> blocks)
        {
            blocks.AddRange(_blocks);
        }

        /// <summary>
        /// Fills the provided list with all registered block groups that satisfy
        /// the optional predicate.
        /// </summary>
        /// <param name="blockGroups">The destination list to append groups to.</param>
        /// <param name="collect">
        /// Optional filter used to decide whether a group should be included.
        /// </param>
        public void GetBlockGroups(List<IMyBlockGroup> blockGroups, Func<IMyBlockGroup, bool> collect = null)
        {
            foreach (var group in _groups)
            {
                if (collect == null || collect(group))
                    blockGroups.Add(group);
            }
        }

        /// <summary>
        /// Fills the provided list with all reachable blocks assignable to
        /// <typeparamref name="T"/> that satisfy the optional predicate.
        /// </summary>
        /// <typeparam name="T">The requested block interface or base type.</typeparam>
        /// <param name="blocks">The destination list to append blocks to.</param>
        /// <param name="collect">Optional filter applied after type selection.</param>
        public void GetBlocksOfType<T>(List<IMyTerminalBlock> blocks, Func<IMyTerminalBlock, bool> collect = null)
            where T : class
        {
            foreach (var block in _blocks.OfType<T>().OfType<IMyTerminalBlock>())
            {
                if (collect == null || collect(block))
                    blocks.Add(block);
            }
        }

        /// <summary>
        /// Fills the provided strongly typed list with all reachable blocks
        /// assignable to <typeparamref name="T"/> that satisfy the optional predicate.
        /// </summary>
        /// <typeparam name="T">The requested block interface or base type.</typeparam>
        /// <param name="blocks">The destination list to append blocks to.</param>
        /// <param name="collect">Optional filter applied after type selection.</param>
        public void GetBlocksOfType<T>(List<T> blocks, Func<T, bool> collect = null)
            where T : class
        {
            foreach (var block in _blocks.OfType<T>())
            {
                if (collect == null || collect(block))
                    blocks.Add(block);
            }
        }

        /// <summary>
        /// Appends all reachable blocks whose custom name contains the provided text.
        /// </summary>
        /// <param name="name">The case-insensitive name fragment to search for.</param>
        /// <param name="blocks">The destination list to append matches to.</param>
        /// <param name="collect">Optional filter applied to each matching block.</param>
        public void SearchBlocksOfName(string name, List<IMyTerminalBlock> blocks, Func<IMyTerminalBlock, bool> collect = null)
        {
            foreach (var block in _blocks.Where(block =>
                block.CustomName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (collect == null || collect(block))
                    blocks.Add(block);
            }
        }

        /// <summary>
        /// Returns the first reachable block whose custom name or display name
        /// matches the supplied name.
        /// </summary>
        /// <param name="name">The block name to match.</param>
        /// <returns>
        /// The first matching block, or <see langword="null"/> when no match exists.
        /// </returns>
        public IMyTerminalBlock GetBlockWithName(string name)
        {
            return _blocks.FirstOrDefault(block =>
                string.Equals(block.CustomName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(block.DisplayNameText, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the first registered block group whose name matches the supplied value.
        /// </summary>
        /// <param name="name">The block group name to match.</param>
        /// <returns>
        /// The first matching group, or <see langword="null"/> when no group exists with that name.
        /// </returns>
        public IMyBlockGroup GetBlockGroupWithName(string name)
        {
            return _groups.FirstOrDefault(group =>
                string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Attempts to locate a reachable block by entity ID.
        /// </summary>
        /// <param name="id">The entity ID to search for.</param>
        /// <returns>
        /// The matching block, or <see langword="null"/> when no registered block has that ID.
        /// </returns>
        public IMyTerminalBlock GetBlockWithId(long id)
        {
            return _blocks.FirstOrDefault(block => block.EntityId == id);
        }

        /// <summary>
        /// Reports whether the supplied block is still considered reachable by
        /// this fake terminal system.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <param name="scope">
        /// The access scope requested by the caller. This fake currently accepts
        /// any non-null block regardless of scope.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="block"/> is non-null;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool CanAccess(IMyTerminalBlock block, MyTerminalAccessScope scope)
        {
            return block != null;
        }

        /// <summary>
        /// Reports whether the supplied grid is still considered reachable by
        /// this fake terminal system.
        /// </summary>
        /// <param name="grid">The grid to test.</param>
        /// <param name="scope">
        /// The access scope requested by the caller. This fake currently accepts
        /// any non-null grid regardless of scope.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="grid"/> is non-null;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool CanAccess(IMyCubeGrid grid, MyTerminalAccessScope scope)
        {
            return grid != null;
        }

        /// <summary>
        /// Ensures a secondary grid is represented as reachable from the primary
        /// grid by inserting a synthetic mechanical connection block.
        /// </summary>
        /// <param name="grid">The grid that should become part of the construct.</param>
        void EnsureReachableFromPrimary(IMyCubeGrid grid)
        {
            if (grid == null || grid.EntityId == PrimaryGrid.EntityId || _reachableGridIds.Contains(grid.EntityId))
                return;

            ConnectGrids(PrimaryGrid, grid, MechanicalConnectionKind.Rotor);
        }

        /// <summary>
        /// Assigns construct-membership behavior to a block so
        /// <see cref="IMyTerminalBlock.IsSameConstructAs(IMyTerminalBlock)"/>
        /// reflects the set of connected grids tracked by this fake terminal system.
        /// </summary>
        /// <param name="block">The block whose construct evaluator should be updated.</param>
        void AssignSameConstructEvaluator(IMyTerminalBlock block)
        {
            Func<IMyTerminalBlock, bool> evaluator = other =>
                other != null
                && block.CubeGrid != null
                && other.CubeGrid != null
                && _constructGridIds.Contains(block.CubeGrid.EntityId)
                && _constructGridIds.Contains(other.CubeGrid.EntityId);

            FakeTerminalBlock fakeBlock = block as FakeTerminalBlock;

            if (fakeBlock != null)
            {
                fakeBlock.SameConstructEvaluator = evaluator;
                return;
            }

            TerminalBlockFactory.TryAssignSameConstructEvaluator(block, evaluator);
        }

        /// <summary>
        /// Finds the registered merge-pair state that owns the supplied merge block.
        /// </summary>
        /// <param name="mergeBlock">One side of a registered merge-block pair.</param>
        /// <returns>The matching merge registration.</returns>
        MergeConnectionRegistration FindMergeConnection(IMyShipMergeBlock mergeBlock)
        {
            if (mergeBlock == null)
                throw new ArgumentNullException(nameof(mergeBlock));

            var registration = _mergeConnections.FirstOrDefault(connection =>
                connection.BaseBlock?.EntityId == mergeBlock.EntityId
                || connection.OtherBlock?.EntityId == mergeBlock.EntityId);

            if (registration == null)
            {
                throw new InvalidOperationException(
                    "The supplied merge block is not registered with this fake grid terminal system.");
            }

            return registration;
        }

        /// <summary>
        /// Finds an existing merge registration for the supplied blocks, or creates one
        /// lazily when two standalone merge blocks are merged for the first time.
        /// </summary>
        /// <param name="firstMergeBlock">One side of the merge-block pair.</param>
        /// <param name="secondMergeBlock">The opposite side of the merge-block pair.</param>
        /// <returns>The existing or newly created merge registration.</returns>
        MergeConnectionRegistration FindOrCreateMergeConnection(
            IMyShipMergeBlock firstMergeBlock,
            IMyShipMergeBlock secondMergeBlock)
        {
            if (firstMergeBlock == null)
                throw new ArgumentNullException(nameof(firstMergeBlock));

            if (secondMergeBlock == null)
                throw new ArgumentNullException(nameof(secondMergeBlock));

            if (firstMergeBlock.EntityId == secondMergeBlock.EntityId)
            {
                throw new InvalidOperationException(
                    "A merge pair requires two distinct merge blocks.");
            }

            var existingRegistration = _mergeConnections.FirstOrDefault(connection =>
                connection.BaseBlock?.EntityId == firstMergeBlock.EntityId
                || connection.OtherBlock?.EntityId == firstMergeBlock.EntityId
                || connection.BaseBlock?.EntityId == secondMergeBlock.EntityId
                || connection.OtherBlock?.EntityId == secondMergeBlock.EntityId);

            if (existingRegistration != null)
            {
                if ((existingRegistration.BaseBlock?.EntityId != firstMergeBlock.EntityId
                        && existingRegistration.OtherBlock?.EntityId != firstMergeBlock.EntityId)
                    || (existingRegistration.BaseBlock?.EntityId != secondMergeBlock.EntityId
                        && existingRegistration.OtherBlock?.EntityId != secondMergeBlock.EntityId))
                {
                    throw new InvalidOperationException(
                        "The supplied merge blocks do not belong to the same merge pair.");
                }

                return existingRegistration;
            }

            if (firstMergeBlock.CubeGrid == null || secondMergeBlock.CubeGrid == null)
            {
                throw new InvalidOperationException(
                    "Both merge blocks must be assigned to grids before they can be merged.");
            }

            var registration = new MergeConnectionRegistration
            {
                BaseGrid = firstMergeBlock.CubeGrid,
                OtherGrid = secondMergeBlock.CubeGrid,
                BaseBlock = firstMergeBlock,
                OtherBlock = secondMergeBlock,
            };

            _reachableGridIds.Add(registration.BaseGrid.EntityId);
            _reachableGridIds.Add(registration.OtherGrid.EntityId);
            _constructGridIds.Add(registration.BaseGrid.EntityId);

            MergeConnectionFactory.Configure(
                registration.BaseBlock,
                registration.OtherBlock,
                onMerged: () => MergeGrids(registration),
                onUnmerged: () => UnmergeGrids(registration),
                baseEnabled: false,
                otherEnabled: true);

            _mergeConnections.Add(registration);
            RebuildTopologyState();

            return registration;
        }

        /// <summary>
        /// Gets the opposite side of a registered merge-block pair.
        /// </summary>
        /// <param name="mergeBlock">One side of the merge-block pair.</param>
        /// <returns>The paired merge block on the opposite grid.</returns>
        public IMyShipMergeBlock GetPairedMergeBlock(IMyShipMergeBlock mergeBlock)
        {
            var registration = FindMergeConnection(mergeBlock);

            return registration.BaseBlock?.EntityId == mergeBlock.EntityId
                ? registration.OtherBlock
                : registration.BaseBlock;
        }

            /// <summary>
            /// Rewrites all absorbed-grid blocks onto the surviving grid for a merge pair
            /// and rebuilds reachability and construct state afterwards.
            /// </summary>
            /// <param name="registration">The merge registration entering the merged state.</param>
        void MergeGrids(MergeConnectionRegistration registration)
        {
            if (registration == null || registration.IsMerged)
                return;

            var currentBaseGrid = registration.BaseBlock?.CubeGrid ?? registration.BaseGrid;
            var currentOtherGrid = registration.OtherBlock?.CubeGrid ?? registration.OtherGrid;

            registration.SurvivingGrid = currentBaseGrid;

            registration.AbsorbedGrid = registration.SurvivingGrid.EntityId == currentBaseGrid.EntityId
                ? currentOtherGrid
                : currentBaseGrid;

            registration.WasPrimaryGridAbsorbed = PrimaryGrid != null
                && PrimaryGrid.EntityId == registration.AbsorbedGrid.EntityId;

            registration.AbsorbedBlockIds = new HashSet<long>(_blocks
                .Where(block => block.CubeGrid != null && block.CubeGrid.EntityId == registration.AbsorbedGrid.EntityId)
                .Select(block => block.EntityId));

            foreach (var block in _blocks.Where(block => registration.AbsorbedBlockIds.Contains(block.EntityId)))
                TerminalBlockFactory.TryAssignCubeGrid(block, registration.SurvivingGrid);

            if (registration.WasPrimaryGridAbsorbed)
                PrimaryGrid = registration.SurvivingGrid;

            registration.IsMerged = true;
            RebuildTopologyState();
        }

        /// <summary>
        /// Restores absorbed-grid blocks back to their original grid for a merge pair
        /// and rebuilds reachability and construct state afterwards.
        /// </summary>
        /// <param name="registration">The merge registration leaving the merged state.</param>
        void UnmergeGrids(MergeConnectionRegistration registration)
        {
            if (registration == null || !registration.IsMerged)
                return;

            foreach (var block in _blocks.Where(block => registration.AbsorbedBlockIds.Contains(block.EntityId)))
                TerminalBlockFactory.TryAssignCubeGrid(block, registration.AbsorbedGrid);

            if (registration.WasPrimaryGridAbsorbed)
                PrimaryGrid = registration.AbsorbedGrid;

            registration.IsMerged = false;
            RebuildTopologyState();
        }

        /// <summary>
        /// Recomputes reachable grids, construct membership, primary-grid ownership,
        /// and per-block same-construct evaluators from the currently registered blocks.
        /// </summary>
        void RebuildTopologyState()
        {
            _reachableGridIds.Clear();

            foreach (var block in _blocks)
            {
                if (block.CubeGrid != null)
                    _reachableGridIds.Add(block.CubeGrid.EntityId);

                var mechanicalBlock = block as IMyMechanicalConnectionBlock;
                if (mechanicalBlock?.TopGrid != null)
                    _reachableGridIds.Add(mechanicalBlock.TopGrid.EntityId);
            }

            var programmableBlock = _blocks.OfType<IMyProgrammableBlock>().FirstOrDefault();
            if (programmableBlock?.CubeGrid != null)
                PrimaryGrid = programmableBlock.CubeGrid;

            _constructGridIds.Clear();
            if (PrimaryGrid != null)
            {
                var pendingGridIds = new Queue<long>();
                pendingGridIds.Enqueue(PrimaryGrid.EntityId);

                while (pendingGridIds.Count > 0)
                {
                    var gridId = pendingGridIds.Dequeue();
                    if (!_constructGridIds.Add(gridId))
                        continue;

                    foreach (var connection in _blocks
                        .OfType<IMyMechanicalConnectionBlock>()
                        .Where(block => block.IsAttached && block.CubeGrid != null && block.TopGrid != null))
                    {
                        if (connection.CubeGrid.EntityId == gridId)
                            pendingGridIds.Enqueue(connection.TopGrid.EntityId);

                        if (connection.TopGrid.EntityId == gridId)
                            pendingGridIds.Enqueue(connection.CubeGrid.EntityId);
                    }
                }
            }

            foreach (var block in _blocks)
                AssignSameConstructEvaluator(block);
        }
    }
}