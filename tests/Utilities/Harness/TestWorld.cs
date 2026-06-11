using IngameScript;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// A shared test environment for multi-script tests.
    /// Owns the common IGC transport so that multiple <see cref="Script{TProgram}"/>
    /// instances can interact inside one test.
    /// </summary>
    /// <remarks>
    /// Basic multi-script test:
    /// <code>
    /// var world = new TestWorld();
    ///
    /// var shipA = world.CreateScript&lt;Program&gt;("ShipA").OnNetwork().Boot();
    /// var shipB = world.CreateScript&lt;Program&gt;("ShipB").OnNetwork().Boot();
    ///
    /// shipA.Bus.RunTerminalCommand("@ShipB help");
    /// world.DispatchIgc();
    /// world.RunIGC();
    /// </code>
    /// For tick-driven behavior:
    /// <code>
    /// world.Run(UpdateType.Terminal, "rename Frigate");
    /// world.RunMany(5, UpdateType.Update10);
    /// </code>
    /// </remarks>
    public class TestWorld
    {
        /// <summary>
        /// Records a block together with the grid that owns it in the world model.
        /// These registrations are replayed into the active topology-backed terminal
        /// system when the relevant grid is materialized.
        /// </summary>
        sealed class WorldBlockRegistration
        {
            public IMyTerminalBlock Block;
            public IMyCubeGrid Grid;
        }

        readonly FakeIgcNetwork _network = new FakeIgcNetwork();
        readonly List<IScript> _scripts = new List<IScript>();
        readonly List<WorldBlockRegistration> _worldBlocks = new List<WorldBlockRegistration>();
        readonly List<MergePair> _mergePairs = new List<MergePair>();
        FakeGridTerminalSystem _gridTerminalSystem;

        /// <summary>
        /// The world's shared fake IGC network. Scripts can opt into this network
        /// via <see cref="Script{TProgram}.OnNetwork(FakeIgcNetwork)"/>.
        /// </summary>
        public FakeIgcNetwork Network => _network;

        /// <summary>
        /// Messages sent through this world's shared IGC network.
        /// Useful for assertions without reaching into transport internals.
        /// </summary>
        public IReadOnlyList<FakeIgcNetwork.SentMessage> SentMessages => _network.SentMessages;

        /// <summary>
        /// Dropped message telemetry captured by this world's shared IGC network.
        /// </summary>
        public IReadOnlyList<FakeIgcNetwork.DroppedMessage> DroppedMessages => _network.DroppedMessages;

        /// <summary>
        /// Asserts that this world contains a script with the supplied runtime name.
        /// </summary>
        public void ShouldHaveScript(string name)
        {
            Assert.That(
                _scripts.Any(script => string.Equals(script.Mother?.Name, name, StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"Expected world to contain script '{name}', but no matching script was found.");
        }

        /// <summary>
        /// Asserts the number of scripts registered in this world.
        /// </summary>
        public void ShouldHaveScriptCount(int count)
        {
            Assert.That(_scripts.Count, Is.EqualTo(count),
                $"Expected world to contain {count} script(s), but found {_scripts.Count}.");
        }

        /// <summary>
        /// Asserts that the captured IGC traffic contains a unicast message from
        /// <paramref name="sourceName"/> to <paramref name="targetName"/> on <paramref name="tag"/>.
        /// </summary>
        public void ShouldHaveDeliveredIgcMessage(string sourceName, string targetName, string tag)
        {
            var source = _scripts.FirstOrDefault(script =>
                string.Equals(script.Mother?.Name, sourceName, StringComparison.OrdinalIgnoreCase));
            var target = _scripts.FirstOrDefault(script =>
                string.Equals(script.Mother?.Name, targetName, StringComparison.OrdinalIgnoreCase));

            Assert.That(source, Is.Not.Null,
                $"Expected world to contain source script '{sourceName}', but none was found.");
            Assert.That(target, Is.Not.Null,
                $"Expected world to contain target script '{targetName}', but none was found.");

            var sourceId = source.IGC.Me;
            var targetId = target.IGC.Me;
            var anyTag = string.IsNullOrWhiteSpace(tag) || tag == "*";

            Assert.That(
                SentMessages.Any(message =>
                    !message.IsBroadcast
                    && message.SourceId == sourceId
                    && message.TargetId == targetId
                    && (anyTag || message.Tag == tag)),
                Is.True,
                $"Expected world IGC traffic to include '{sourceName}' -> '{targetName}' on tag '{tag}', but no matching unicast message was captured.");

            Assert.That(
                DroppedMessages.Any(message =>
                    !message.IsBroadcast
                    && message.SourceId == sourceId
                    && message.TargetId == targetId
                    && (anyTag || message.Tag == tag)),
                Is.False,
                $"Expected world IGC message '{sourceName}' -> '{targetName}' on tag '{tag}' to avoid transport drops, but a matching drop was recorded.");
        }

        /// <summary>
        /// Asserts that captured traffic includes a broadcast from <paramref name="sourceName"/>
        /// on the provided <paramref name="tag"/>.
        /// </summary>
        public void ShouldHaveBroadcast(string tag, string sourceName)
        {
            var source = _scripts.FirstOrDefault(script =>
                string.Equals(script.Mother?.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            Assert.That(source, Is.Not.Null,
                $"Expected world to contain source script '{sourceName}', but none was found.");

            Assert.That(
                SentMessages.Any(message =>
                    message.IsBroadcast
                    && message.SourceId == source.IGC.Me
                    && message.Tag == tag),
                Is.True,
                $"Expected world IGC traffic to include broadcast from '{sourceName}' on tag '{tag}', but no matching broadcast was captured.");
        }

        /// <summary>
        /// Asserts that no queued transport deliveries or buffered endpoint messages remain.
        /// </summary>
        public void ShouldHaveNoPendingMessages()
        {
            Assert.That(_network.HasPendingMessages, Is.False,
                $"Expected world to have no pending IGC messages, but pending deliveries={_network.PendingDeliveryCount}, pending endpoints={_network.PendingEndpointCount}.");
        }

        /// <summary>
        /// Creates a new <see cref="Script{TProgram}"/> in this world.
        /// Network participation is opt-in via <see cref="Script{TProgram}.OnNetwork()"/>
        /// or <see cref="Script{TProgram}.OnNetwork(FakeIgcNetwork)"/>.
        /// Call <see cref="Script{TProgram}.Boot"/> to complete construction.
        /// </summary>
        public Script<TProgram> CreateScript<TProgram>(string scriptName = null)
            where TProgram : MyGridProgram, new()
        {
            var script = new Script<TProgram>(scriptName).WithDefaultNetwork(_network);
            _scripts.Add(script);
            return script;
        }

        /// <summary>
        /// Creates a new grid in this test world.
        /// Use this when world topology should exist before a script is booted.
        /// </summary>
        /// <param name="gridName">Optional display name for the new grid.</param>
        /// <param name="entityId">Optional entity ID for the new grid.</param>
        /// <returns>A grid handle that owns subsequent block registrations.</returns>
        public TestGrid CreateGrid(string gridName = null, long? entityId = null)
        {
            return new TestGrid(this, GridFactory.Create(gridName ?? "Test Grid", entityId));
        }

        /// <summary>
        /// Registers an existing block on the supplied world grid.
        /// If the world already has an active topology-backed terminal system, the block is
        /// added immediately; otherwise it is replayed when the first script binds to the world.
        /// </summary>
        /// <typeparam name="TBlock">The terminal block type being registered.</typeparam>
        /// <param name="block">The block instance to register.</param>
        /// <param name="grid">The grid that owns the block.</param>
        /// <returns>The same <paramref name="block"/> instance for fluent setup.</returns>
        internal TBlock RegisterBlock<TBlock>(TBlock block, IMyCubeGrid grid)
            where TBlock : class, IMyTerminalBlock
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (!TerminalBlockFactory.TryAssignCubeGrid(block, grid))
            {
                throw new InvalidOperationException(
                    "The supplied block does not expose an assignable CubeGrid. " +
                    "Create blocks with TerminalBlockFactory.Create<TBlock>() or provide a block with CubeGrid already configured.");
            }

            _worldBlocks.Add(new WorldBlockRegistration { Block = block, Grid = grid });
            _gridTerminalSystem?.AddBlock(block, grid);

            return block;
        }

        internal TBlock CreateBlock<TBlock>(
            IMyCubeGrid grid,
            string customName = null,
            string customData = "",
            long? entityId = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            var block = TerminalBlockFactory.Create(
                customName: customName,
                customData: customData,
                entityId: entityId,
                grid: grid,
                configure: configure);

            return RegisterBlock(block, grid);
        }

        internal IMyTerminalBlock FindBlock(IMyCubeGrid grid, string blockName)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (string.IsNullOrWhiteSpace(blockName))
                throw new ArgumentException("Block name is required.", nameof(blockName));

            return _worldBlocks
                .Where(registration => registration.Grid?.EntityId == grid.EntityId)
                .Select(registration => registration.Block)
                .FirstOrDefault(block => string.Equals(block.CustomName, blockName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(block.DisplayNameText, blockName, StringComparison.OrdinalIgnoreCase));
        }

        internal bool ContainsBlock(IMyCubeGrid grid, string blockName)
        {
            return FindBlock(grid, blockName) != null;
        }

        /// <summary>
        /// Registers a paired merge-block link in the world.
        /// The actual merge blocks are materialized when a script binds to the world topology.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the base-side merge block.</param>
        /// <param name="otherGrid">The grid that owns the opposite-side merge block.</param>
        /// <param name="baseMergeBlockName">Optional custom name for the base-side merge block.</param>
        /// <param name="otherMergeBlockName">Optional custom name for the opposite-side merge block.</param>
        /// <param name="initialState">The initial merge state to expose once the pair is materialized.</param>
        /// <returns>A descriptor for the deferred merge-block pair.</returns>
        public MergePair AddMergeBlockPair(
            TestGrid baseGrid,
            TestGrid otherGrid,
            string baseMergeBlockName = null,
            string otherMergeBlockName = null,
            MergeState initialState = MergeState.None)
        {
            if (baseGrid == null)
                throw new ArgumentNullException(nameof(baseGrid));

            if (otherGrid == null)
                throw new ArgumentNullException(nameof(otherGrid));

            var pair = new MergePair(
                baseGrid.Grid,
                otherGrid.Grid,
                baseMergeBlockName,
                otherMergeBlockName,
                initialState);

            _mergePairs.Add(pair);

            if (_gridTerminalSystem != null)
                MaterializeMergePair(pair);

            return pair;
        }

        /// <summary>
        /// Creates a new <see cref="Script{TProgram}"/> bound to an existing world grid.
        /// The script uses the world's topology-backed terminal system so it can observe
        /// pre-existing world blocks and later world-level merge operations.
        /// </summary>
        /// <param name="primaryGrid">The world grid that should own the programmable block.</param>
        /// <param name="scriptName">Optional display name for the script.</param>
        /// <returns>A script harness bound to this world's topology.</returns>
        public Script<TProgram> CreateScript<TProgram>(TestGrid primaryGrid, string scriptName = null)
            where TProgram : MyGridProgram, new()
        {
            if (primaryGrid == null)
                throw new ArgumentNullException(nameof(primaryGrid));

            EnsureWorldTopology(primaryGrid.Grid);

            var script = new Script<TProgram>(_gridTerminalSystem, scriptName).WithDefaultNetwork(_network);
            _scripts.Add(script);
            return script;
        }

        /// <summary>
        /// Merges two merge blocks at the world level, registering their pair lazily
        /// the first time they are merged.
        /// </summary>
        /// <param name="firstBlock">One side of the merge-block pair.</param>
        /// <param name="secondBlock">The opposite side of the merge-block pair.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld Merge(IMyShipMergeBlock firstBlock, IMyShipMergeBlock secondBlock)
        {
            if (_gridTerminalSystem == null)
                throw new InvalidOperationException(
                    "World topology has not been bound to a script yet. Create a script on an existing world grid first.");

            MaterializeGridBlocks(firstBlock?.CubeGrid);
            MaterializeGridBlocks(secondBlock?.CubeGrid);

            _gridTerminalSystem.MergeBlocks(firstBlock, secondBlock);

            return this;
        }

        /// <summary>
        /// Unmerges a paired merge-block link at the world level by disabling one side.
        /// </summary>
        /// <param name="mergeBlock">One side of the merge-block pair to disable.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld Unmerge(IMyShipMergeBlock mergeBlock)
        {
            if (_gridTerminalSystem == null)
                throw new InvalidOperationException(
                    "World topology has not been bound to a script yet. Create a script on an existing world grid first.");

            _gridTerminalSystem.UnmergeBlocks(mergeBlock);

            return this;
        }

        /// <summary>
        /// Routes all pending IGC messages to their recipients and triggers
        /// <c>HandleIncomingIGCMessages</c> on every script with pending input.
        /// Call this between sending a command and running the receiving script.
        /// </summary>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld DispatchIgc()
        {
            _network.Deliver();

            return this;
        }

        /// <summary>
        /// Runs one <c>Mother.Run</c> cycle on every script registered in this world.
        /// Mirrors a single real game update tick across all scripts.
        /// </summary>
        /// <param name="updateType">The game update type to pass to each script.</param>
        /// <param name="argument">The terminal argument to pass to each script.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld Run(UpdateType updateType = UpdateType.Update10, string argument = "")
        {
            foreach (var script in _scripts)
                script.Mother.Run(argument, updateType);

            return this;
        }

        /// <summary>
        /// Convenience helper. Equivalent to <see cref="Run"/> with
        /// <see cref="UpdateType.IGC"/>. Use after <see cref="DispatchIgc"/> to
        /// let each script process its incoming message queue.
        /// </summary>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld RunIGC() => Run(UpdateType.IGC);

        /// <summary>
        /// Advances the world by <paramref name="count"/> synchronized cycles.
        /// Each cycle first dispatches pending IGC traffic and then advances every
        /// script clock once.
        /// </summary>
        /// <param name="count">Number of world cycles to execute.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld Tick(int count = 1)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be zero or greater.");

            for (int i = 0; i < count; i++)
            {
                DispatchIgc();

                _scripts.ForEach(s => s.Tick());
                //foreach (var script in _scripts)
                //    script.Tick();
            }

            return this;
        }

        /// <summary>
        /// Intention-revealing world progression helper for message-driven tests.
        /// We tick twice, to ensure communications can take a round trip.
        /// Equivalent to <see cref="Tick(int)"/>.
        /// </summary>
        /// <param name="count">Number of world cycles to execute.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld TickMessages(int count = 2) => Tick(count);

        /// <summary>
        /// Advances the world by <paramref name="count"/> consecutive cycles
        /// with the same <paramref name="updateType"/>.
        /// </summary>
        /// <param name="count">The number of cycles to execute.</param>
        /// <param name="updateType">The game update type to pass to each cycle.</param>
        /// <param name="argument">The terminal argument to pass to each cycle.</param>
        /// <returns>The current world instance for fluent chaining.</returns>
        public TestWorld RunMany(int count, UpdateType updateType, string argument = "")
        {
            for (int i = 0; i < count; i++)
                Run(updateType, argument);

            return this;
        }

        /// <summary>
        /// Creates the topology-backed terminal system for this world the first time
        /// a script binds to a specific grid.
        /// </summary>
        /// <param name="primaryGrid">The grid that should become the terminal system root.</param>
        void EnsureWorldTopology(IMyCubeGrid primaryGrid)
        {
            if (_gridTerminalSystem != null)
            {
                if (_gridTerminalSystem.PrimaryGrid.EntityId != primaryGrid.EntityId)
                {
                    throw new InvalidOperationException(
                        "TestWorld currently supports one topology-backed primary script grid.");
                }

                return;
            }

            _gridTerminalSystem = new FakeGridTerminalSystem(primaryGrid);

            foreach (var pair in _mergePairs)
                MaterializeMergePair(pair);

            MaterializeGridBlocks(primaryGrid);
        }

        /// <summary>
        /// Replays all registered blocks for a specific grid into the active terminal
        /// system, skipping blocks that have already been materialized.
        /// </summary>
        /// <param name="grid">The grid whose blocks should be materialized.</param>
        void MaterializeGridBlocks(IMyCubeGrid grid)
        {
            if (_gridTerminalSystem == null || grid == null)
                return;

            foreach (var blockRegistration in _worldBlocks)
            {
                if (blockRegistration.Grid?.EntityId != grid.EntityId)
                    continue;

                if (_gridTerminalSystem.GetBlockWithId(blockRegistration.Block.EntityId) != null)
                    continue;

                _gridTerminalSystem.AddBlock(blockRegistration.Block, blockRegistration.Grid);
            }
        }

        /// <summary>
        /// Creates the concrete merge-block instances for a deferred merge pair and
        /// registers them with the active topology-backed terminal system.
        /// </summary>
        /// <param name="pair">The deferred merge pair to materialize.</param>
        void MaterializeMergePair(MergePair pair)
        {
            if (pair.BaseBlock != null)
                return;

            pair.BaseBlock = _gridTerminalSystem.ConnectGridsViaMergeBlock(
                pair.BaseGrid,
                pair.OtherGrid,
                pair.BaseMergeBlockName,
                pair.OtherMergeBlockName,
                pair.InitialState
            );

            pair.OtherBlock = _gridTerminalSystem.GetPairedMergeBlock(pair.BaseBlock);
        }
    }
}
