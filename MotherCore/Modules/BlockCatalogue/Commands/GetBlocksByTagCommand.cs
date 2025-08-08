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
    public class GetBlocksByTagCommand : BaseModuleCommand
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        readonly BlockCatalogue Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "tag/get";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public GetBlocksByTagCommand(BlockCatalogue module)
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
            string tag = command.Arguments.FirstOrDefault();

            var blocks = Module
                .GetBlocksByTag(tag)
                .OrderBy(b => b.CustomName)
                .ToList();

            string output = $"Found {blocks.Count} blocks with tag: #{tag}\n";

            return  output + string.Join("\n", blocks.Select(b => $"- {b.CustomName}"));
        }
    }
}
