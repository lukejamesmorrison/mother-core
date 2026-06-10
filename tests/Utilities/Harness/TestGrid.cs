using System;
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
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
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
    }
}
