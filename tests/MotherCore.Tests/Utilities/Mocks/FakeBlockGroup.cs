using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight in-memory block group for tests.
    /// </summary>
    /// <see cref="IMyBlockGroup"/>
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyBlockGroup.html"/>
    internal class FakeBlockGroup : IMyBlockGroup
    {
        /// <summary>
        /// The blocks that belong to this group.
        /// </summary>
        readonly List<IMyTerminalBlock> _blocks;

        /// <summary>
        /// The name of the block group.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="blocks"></param>
        public FakeBlockGroup(string name, IEnumerable<IMyTerminalBlock> blocks)
        {
            Name = name;
            _blocks = blocks == null ? new List<IMyTerminalBlock>() : blocks.ToList();
        }

        /// <summary>
        /// Gets the blocks that belong to this group. Optionally accepts a collect function to filter the blocks that are returned.
        /// </summary>
        /// <param name="blocks"></param>
        /// <param name="collect"></param>
        public void GetBlocks(List<IMyTerminalBlock> blocks, Func<IMyTerminalBlock, bool> collect = null)
        {
            foreach (var block in _blocks)
            {
                if (collect == null || collect(block))
                    blocks.Add(block);
            }
        }

        /// <summary>
        /// Gets the blocks of the specified type that belong to this group. Optionally accepts a collect 
        /// function to filter the blocks that are returned.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="blocks"></param>
        /// <param name="collect"></param>
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
        /// Gets the blocks of the specified type that belong to this group. Optionally accepts a 
        /// collect function to filter the blocks that are returned.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="blocks"></param>
        /// <param name="collect"></param>
        public void GetBlocksOfType<T>(List<T> blocks, Func<T, bool> collect = null)
            where T : class
        {
            foreach (var block in _blocks.OfType<T>())
            {
                if (collect == null || collect(block))
                    blocks.Add(block);
            }
        }
    }
}