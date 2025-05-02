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

// Program

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// The IExtensionModule interface is used to define all Extension modules registered 
        /// with Mother. Extension modules are booted and run after Core modules.
        /// </summary>
        public interface IExtensionModule: IModule { }
    }
}
