using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{
	partial class Program
	{
        /// <summary>
        /// Interface for block state handlers.
        /// </summary>
        public interface IBlockStateHandler
        {
            /// <summary>
            /// Get the current state of a block.
            /// </summary>
            /// <param name="block"></param>
            /// <returns></returns>
            object GetBlockCurrentState(IMyTerminalBlock block);

            /// <summary>
            /// Compare the current state of a block with a previous state.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="previousState"></param>
            /// <returns></returns>
            bool HasBlockStateChanged(IMyTerminalBlock block, object previousState);

            /// <summary>
            /// Handle a state change event.
            /// </summary>
            /// <param name="block"></param>
            void OnBlockStateChanged(IMyTerminalBlock block);
        }
    }
}
