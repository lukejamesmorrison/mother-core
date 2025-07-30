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
    /// A named waypoint representing a waypoint saved by Mother for future 
    /// reference. Named waypoints are used in flight plans in 
    /// place of full GPS waypoint definitions.
    /// </summary>
    public class NamedWaypoint : IWaypoint
    {
        /// <summary>
        /// The vector (position) of the waypoint.
        /// </summary>
        readonly Vector3D Vector;

        /// <summary>
        /// The name of the waypoint.
        /// </summary>
        readonly string Name;

        /// <summary>
        /// Get the vector (position) of the waypoint.
        /// </summary>
        /// <returns></returns>
        public Vector3D GetVector()
        {
            return Vector;
        }

        /// <summary>
        /// Get the name of the waypoint.
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return Name;
        }
    }
}
