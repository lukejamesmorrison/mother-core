using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Dynamic;
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
    /// This class acts as a base for all Extension Modules. It exposes several capabilities 
    /// that are useful for Extension Modules to simplifying access. Developers will find 
    /// that most functionality of Mother can be accessed from within this class.
    /// </summary>
    public abstract class BaseExtensionModule : BaseModule, IExtensionModule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BaseExtensionModule(Mother mother) : base(mother) { }
    }
}
