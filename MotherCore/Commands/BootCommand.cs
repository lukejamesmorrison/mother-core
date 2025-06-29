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
    /// <summary>
    /// Command to purge module state data.
    /// </summary>
    public class BootCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        readonly Mother Mother;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "boot";

        /// <summary>
        /// Is the purge forced?
        /// </summary>
        //bool BootForced = false;

        /// <summary>
        /// List of modules to purge.
        /// </summary>
        //readonly List<string> ModulesToBoot = new List<string>();

        /// <summary>
        /// List of modules that have been purged.
        /// </summary>
        //readonly List<string> BootdModules = new List<string>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BootCommand(Mother mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Boot a module.
        /// </summary>
        /// <param name="module"></param>
        //void BootModule(string module)
        //{
        //    switch (module)
        //    {
        //        case "almanac":
        //            Mother.GetModule<Almanac>().Clear();
        //            BootdModules.Add(module);
        //            break;

        //        case "storage":
        //            Mother.GetModule<LocalStorage>().Clear();
        //            BootdModules.Add(module);
        //            break;

        //        default:
        //            break;
        //    }
        //}

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            Mother.Boot();

            return "Rebooting...";


            //ModulesToBoot.Clear();
            //BootdModules.Clear();

            // get Options
            //foreach (var switchItem in command.Options)
            //{
            //    string key = switchItem.Key;
            //    string value = switchItem.Value;

            //    if (key == "force" && (value == "true" || value == "1"))
            //        BootForced = true;
            //}

            // early escape if not forced
            //if (!BootForced) return "Run command with --force to purge";

            //if (command.Arguments.Count == 0)
            //    return CommandBus.Messages.NoArgumentsProvided;

            //else if(command.Arguments.Count >= 1)
            //else
            //{
            //    string moduleString = command.Arguments[0];
            //    List<string> modules = moduleString.Split(',').ToList();

            //    // capture "all" case
            //    if (modules.Contains("*"))
            //    {
            //        ModulesToBoot.Add("almanac");
            //        ModulesToBoot.Add("storage");
            //    }

            //    // purge by module
            //    if (modules.Contains("storage")) ModulesToBoot.Add("storage");
            //    if (modules.Contains("almanac")) ModulesToBoot.Add("almanac");

            //    ModulesToBoot.ForEach(module => BootModule(module));

            //    return BootdModules.Count == 0
            //        ? "No modules purged"
            //        : $"Bootd {BootdModules.Count} modules: {string.Join(", ", BootdModules)}";
            //}
        }
    }
}
