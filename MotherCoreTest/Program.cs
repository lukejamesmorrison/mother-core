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
    public partial class Program : MyGridProgram
    {
        public Mother mother;

        public Program()
        {
            // Create the Mother instance
            mother = new Mother(this);

            // Register Extension Modules
            mother.RegisterModules(new List<IExtensionModule>() {
                new PistonModule(mother),
            });

            // Boot Mother with all registered modules.
            mother.Boot();
        }

        public void Save()
        {
            Storage = mother.Save();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            mother.Run(argument, updateSource);
        }
    }
}
