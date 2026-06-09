using FakeItEasy;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly List<IMyTerminalBlock> _blocks = new List<IMyTerminalBlock>();
        readonly List<IMyBlockGroup> _groups = new List<IMyBlockGroup>();
        readonly HashSet<long> _connectedGridIds = new HashSet<long>();

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
        {
            PrimaryGrid = TerminalBlockFactory.CreateCubeGrid(primaryGridName, primaryGridEntityId);
            _connectedGridIds.Add(PrimaryGrid.EntityId);
        }

        /// <summary>
        /// Gets the root grid for this terminal system.
        /// All additional grids registered with this fake are treated as part of
        /// the same construct relative to this grid.
        /// </summary>
        public IMyCubeGrid PrimaryGrid { get; private set; }

        /// <summary>
        /// Creates an additional grid that is reachable from this terminal system.
        /// </summary>
        /// <param name="gridName">Optional display name for the new grid.</param>
        /// <param name="entityId">Optional entity ID for the new grid.</param>
        /// <returns>
        /// A synthetic <see cref="IMyCubeGrid"/> that is automatically linked back
        /// to <see cref="PrimaryGrid"/> as part of the same construct.
        /// </returns>
        public IMyCubeGrid CreateGrid(
            string gridName = null,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            var grid = TerminalBlockFactory.CreateCubeGrid(
                gridName ?? $"Grid-{_connectedGridIds.Count + 1}",
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

            _connectedGridIds.Add(baseGrid.EntityId);
            _connectedGridIds.Add(topGrid.EntityId);

            var connection = MechanicalConnectionFactory.Create(connectionKind, baseGrid, topGrid);
            AddBlock(connection, baseGrid);

            return connection;
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

            if (!TerminalBlockFactory.TryAssignCubeGrid(block, targetGrid))
            {
                throw new InvalidOperationException(
                    "The supplied block does not expose an assignable CubeGrid. " +
                    "Create blocks with TerminalBlockFactory.Create<TBlock>() or provide a block with CubeGrid already configured.");
            }

            EnsureConnectedToPrimary(targetGrid);

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
        void EnsureConnectedToPrimary(IMyCubeGrid grid)
        {
            if (grid == null || grid.EntityId == PrimaryGrid.EntityId || _connectedGridIds.Contains(grid.EntityId))
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
                && _connectedGridIds.Contains(block.CubeGrid.EntityId)
                && _connectedGridIds.Contains(other.CubeGrid.EntityId);

            var programmableBlock = block as FakeProgrammableBlock;

            if (programmableBlock != null)
            {
                programmableBlock.SameConstructEvaluator = evaluator;
                return;
            }

            TerminalBlockFactory.TryAssignSameConstructEvaluator(block, evaluator);
        }
    }
}