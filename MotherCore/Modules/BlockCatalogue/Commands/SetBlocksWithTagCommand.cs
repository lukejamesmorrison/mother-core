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
    /// <summary>
    /// Command to get all blocks with a specific tag.
    /// </summary>
    public class SetBlocksWithTagCommand : BaseModuleCommand
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        readonly BlockCatalogue Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "tag/set";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public SetBlocksWithTagCommand(BlockCatalogue module)
        {
            Module = module;
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            string selector = command.Arguments[0];
            string tag = command.Arguments[1];

            var blocks = Module.SetBlocksWithTag(selector, tag);

            string output = $"Tag \"{tag}\" set on {blocks.Count} blocks:\n";
            return output + string.Join("\n", blocks.Select(b => $"- {b.CustomName}"));
        }
    }
}
