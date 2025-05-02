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
    /// Interface for waypoints used by Mother.
    /// </summary>
    public interface IWaypoint
    {
        /// <summary>
        /// Get the vector of the waypoint in 3D.
        /// </summary>
        /// <returns></returns>
        Vector3D GetVector();

        /// <summary>
        /// Get the name of the waypoint.
        /// </summary>
        /// <returns></returns>
        string GetName();
    }
}
