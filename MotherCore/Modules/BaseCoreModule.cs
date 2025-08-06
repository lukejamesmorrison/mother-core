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
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// This class acts as a base for all Core Modules.  It exposes several capabilities 
    /// that are useful across multiple Core Modules, simplifying access.
    /// </summary>
    public abstract class BaseCoreModule : BaseModule, ICoreModule
    {
        /// <summary>
        /// Constructor for the BaseCoreModule class.
        /// </summary>
        /// <param name="mother"></param>
        public BaseCoreModule(Mother mother) : base(mother) { }
    }
}
