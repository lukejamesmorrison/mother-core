using Sandbox.Game.EntityComponents;
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
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The ActivityMonitor monitors the state of blocks in the grid as they are changing. 
    /// It can be used to observe a change until a terminal state is reached. For 
    /// ongoing state monitoring, use the Block Catalogue instead.
    /// </summary>
    public class ActivityMonitor : BaseCoreModule
    {
        /// <summary>
        /// Dictionary of blocks being monitored.
        /// </summary>
        public Dictionary<IMyTerminalBlock, MonitorEntry> ActiveBlocks { get; }

        /// <summary>
        /// Struct to hold monitoring entries as (expression, callback).
        /// </summary>
        public struct MonitorEntry
        {
            /// <summary>
            /// Expression to evaluate the block's terminal state.
            /// </summary>
            public Func<IMyTerminalBlock, bool> TerminalExpression;

            /// <summary>
            /// Callback to execute when the terminal state is reached.
            /// </summary>
            public Action<IMyTerminalBlock> OnTerminalStateReached;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="condition"></param>
            /// <param name="callback"></param>
            public MonitorEntry(Func<IMyTerminalBlock, bool> condition, Action<IMyTerminalBlock> callback)
            {
                TerminalExpression = condition;
                OnTerminalStateReached = callback;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActivityMonitor(Mother mother) : base(mother)
        {
            ActiveBlocks = new Dictionary<IMyTerminalBlock, MonitorEntry>();
        }

        /// <summary>
        /// Update the monitor, checking each block's terminal expression 
        /// and executing the callback if true.
        /// </summary>
        public override void Run()
        {
            var blocksToRemove = new List<IMyTerminalBlock>();

            foreach (var entry in ActiveBlocks)
            {
                IMyTerminalBlock block = entry.Key;
                MonitorEntry monitorEntry = entry.Value;

                // Check if the terminal condition is met
                if (monitorEntry.TerminalExpression(block))
                {
                    // Execute the callback if the terminal condition is met
                    monitorEntry.OnTerminalStateReached?.Invoke(block);
                    blocksToRemove.Add(block);
                }
            }

            // Remove blocks that reached their terminal state
            blocksToRemove.ForEach(block => UnregisterBlock(block));

            //foreach (var block in blocksToRemove)
            //    UnregisterBlock(block);
        }

        /// <summary>
        /// Register a block with an logical expression and a callback action.
        /// ie. when true, run action
        /// </summary>
        /// <param name="block"></param>
        /// <param name="terminalCondition"></param>
        /// <param name="onTerminalReached"></param>
        public void RegisterBlock(
            IMyTerminalBlock block, 
            Func<IMyTerminalBlock, bool> terminalCondition, 
            Action<IMyTerminalBlock> onTerminalReached
        )
        {
            if (!ActiveBlocks.ContainsKey(block))
                ActiveBlocks[block] = new MonitorEntry(terminalCondition, onTerminalReached);
        }

        /// <summary>
        /// Unregister a block from monitoring.
        /// </summary>
        /// <param name="block"></param>
        public void UnregisterBlock(IMyTerminalBlock block)
        {
            if (ActiveBlocks.ContainsKey(block))
                ActiveBlocks.Remove(block);
        }
    }
}