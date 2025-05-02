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
//using static IngameScript.Program;
//using Sandbox.Game.GameSystems.Chat;

namespace IngameScript
{
	partial class Program
	{
        /// <summary>
        /// Command to purge module state data.
        /// </summary>
        public class PurgeCommand : BaseModuleCommand
        {
            /// <summary>
            /// The Mother instance.
            /// </summary>
            readonly Mother Mother;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "purge";

            /// <summary>
            /// Is the purge forced?
            /// </summary>
            bool PurgeForced = false;

            /// <summary>
            /// List of modules to purge.
            /// </summary>
            readonly List<string> ModulesToPurge = new List<string>();

            /// <summary>
            /// List of modules that have been purged.
            /// </summary>
            readonly List<string> PurgedModules = new List<string>();

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="mother"></param>
            public PurgeCommand(Mother mother)
            {
                Mother = mother;
            }

            /// <summary>
            /// Purge a module.
            /// </summary>
            /// <param name="module"></param>
            void PurgeModule(string module)
            {
                switch (module)
                {
                    case "almanac":
                        Mother.GetModule<Almanac>().Clear();
                        PurgedModules.Add(module);
                        break;

                    case "storage":
                        Mother.GetModule<LocalStorage>().Clear();
                        PurgedModules.Add(module);
                        break;

                    default:
                        break;
                }
            }

            /// <summary>
            /// Execute the command.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            public override string Execute(TerminalCommand command)
            {
                ModulesToPurge.Clear();
                PurgedModules.Clear();

                // get Options
                foreach (var switchItem in command.Options)
                {
                    string key = switchItem.Key;
                    string value = switchItem.Value;

                    if (key == "force" && (value == "true" || value == "1"))
                        PurgeForced = true;
                }

                // early escape if not forced
                if (!PurgeForced) return "Run command with --force to purge";

                if (command.Arguments.Count == 0)
                    return CommandBus.Messages.NoArgumentsProvided;

                //else if(command.Arguments.Count >= 1)
                else
                {
                    string moduleString = command.Arguments[0];
                    List<string> modules = moduleString.Split(',').ToList();

                    // capture "all" case
                    if (modules.Contains("*"))
                    {
                        ModulesToPurge.Add("almanac");
                        ModulesToPurge.Add("storage");
                    }

                    // purge by module
                    if (modules.Contains("storage")) ModulesToPurge.Add("storage");
                    if (modules.Contains("almanac")) ModulesToPurge.Add("almanac");

                    ModulesToPurge.ForEach(module => PurgeModule(module));

                    return PurgedModules.Count == 0
                        ? "No modules purged"
                        : $"Purged {PurgedModules.Count} modules: {string.Join(", ", PurgedModules)}";
                }
            }
        }
	}
}
