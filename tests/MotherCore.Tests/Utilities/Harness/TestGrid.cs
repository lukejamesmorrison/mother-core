using System;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// A world-owned grid handle used to attach blocks before any script has
    /// bound to the world's topology.
    /// </summary>
    public sealed class TestGrid
    {
        readonly TestWorld _world;

        internal TestGrid(TestWorld world, IMyCubeGrid grid)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            _world = world;
            Grid = grid;
        }

        /// <summary>
        /// Gets the underlying fake cube grid represented by this handle.
        /// </summary>
        public IMyCubeGrid Grid { get; }

        /// <summary>
        /// Gets the display name of the underlying grid.
        /// </summary>
        public string Name => Grid.CustomName;

        /// <summary>
        /// Adds a block to this grid in the world model.
        /// If the world already has an active topology-backed terminal system,
        /// the block is also materialized immediately.
        /// </summary>
        /// <typeparam name="TBlock">The terminal block type being registered.</typeparam>
        /// <param name="block">The block instance to attach to this grid.</param>
        /// <returns>The same <paramref name="block"/> instance for fluent setup.</returns>
        public TBlock AddBlock<TBlock>(TBlock block)
            where TBlock : class, IMyTerminalBlock
        {
            return _world.RegisterBlock(block, Grid);
        }

        /// <summary>
        /// Creates a lightweight terminal block fake, registers it on this grid,
        /// and returns it for additional test arrangement.
        /// </summary>
        /// <typeparam name="TBlock">The terminal block interface to create.</typeparam>
        /// <param name="customName">Optional custom name for the block.</param>
        /// <param name="customData">Optional custom data payload.</param>
        /// <param name="entityId">Optional explicit entity ID.</param>
        /// <param name="configure">Optional last-mile configuration hook.</param>
        public TBlock AddBlock<TBlock>(
            string customName = null,
            string customData = "",
            long? entityId = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            return _world.CreateBlock(Grid, customName, customData, entityId, configure);
        }

        /// <summary>
        /// Looks up a block on this grid by custom name or display name.
        /// </summary>
        public IMyTerminalBlock GetBlock(string blockName)
        {
            return _world.FindBlock(Grid, blockName);
        }

        /// <summary>
        /// Looks up a strongly typed block on this grid by custom name or display name.
        /// </summary>
        public TBlock GetBlock<TBlock>(string blockName)
            where TBlock : class, IMyTerminalBlock
        {
            return GetBlock(blockName) as TBlock;
        }

        /// <summary>
        /// Reports whether this grid currently contains a block with the supplied name.
        /// </summary>
        public bool ContainsBlock(string blockName)
        {
            return _world.ContainsBlock(Grid, blockName);
        }

        /// <summary>
        /// Assertion helper for world-oriented tests that care about grid-local block registration.
        /// </summary>
        public void ShouldContainBlock(string blockName)
        {
            Assert.That(ContainsBlock(blockName), Is.True,
                $"Expected grid '{Name}' to contain block '{blockName}', but it was not registered.");
        }

        /// <summary>
        /// Asserts that two named blocks on this grid are part of the same construct.
        /// </summary>
        public void ShouldBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            var first = GetBlock(firstBlockName);
            var second = GetBlock(secondBlockName);

            Assert.That(first, Is.Not.Null,
                $"Expected block '{firstBlockName}' to exist on grid '{Name}', but it was not found.");

            Assert.That(second, Is.Not.Null,
                $"Expected block '{secondBlockName}' to exist on grid '{Name}', but it was not found.");

            Assert.That(first.IsSameConstructAs(second), Is.True,
                $"Expected blocks '{firstBlockName}' and '{secondBlockName}' to be on the same construct, but they were not.");
        }

        /// <summary>
        /// Asserts that two named blocks on this grid are not part of the same construct.
        /// </summary>
        public void ShouldNotBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            var first = GetBlock(firstBlockName);
            var second = GetBlock(secondBlockName);

            Assert.That(first, Is.Not.Null,
                $"Expected block '{firstBlockName}' to exist on grid '{Name}', but it was not found.");

            Assert.That(second, Is.Not.Null,
                $"Expected block '{secondBlockName}' to exist on grid '{Name}', but it was not found.");

            Assert.That(first.IsSameConstructAs(second), Is.False,
                $"Expected blocks '{firstBlockName}' and '{secondBlockName}' to be on separate constructs, but they were on the same construct.");
        }
    }
}
