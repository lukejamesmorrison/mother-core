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
    partial class Program
    {
        /// <summary>
        /// This class manages routines for action at a specific waypoint 
        /// within a flight plan.
        /// </summary>
        public class WaypointRoutineQueue
        {
            /// <summary>
            /// Dictionary of waypoints and their associated routine strings.
            /// </summary>
            public Dictionary<IWaypoint, string> WaypointRoutines = new Dictionary<IWaypoint, string>();

            /// <summary>
            /// Constructor.
            /// </summary>
            public WaypointRoutineQueue() { }

            /// <summary>
            /// Get the waypoint from the name.
            /// </summary>
            /// <param name="waypointName"></param>
            /// <returns></returns>
            IWaypoint GetWaypointFromName(string waypointName)
            {
                return WaypointRoutines.Keys.FirstOrDefault(w => w.GetName() == waypointName);
            }

            /// <summary>
            /// Get the routine string for a specific waypoint.
            /// </summary>
            /// <param name="waypointName"></param>
            /// <returns></returns>
            public string GetRoutineForWaypoint(string waypointName)
            {
                IWaypoint waypoint = GetWaypointFromName(waypointName);

                string routineString = waypoint != null
                    ? WaypointRoutines[waypoint]
                    : "";

                return routineString;
            }

            /// <summary>
            /// Check if the queue contains a routine for a specific waypoint.
            /// </summary>
            /// <param name="waypointName"></param>
            /// <returns></returns>
            public bool ContainsWaypointRoutine(string waypointName)
            {
                return GetWaypointFromName(waypointName) != null;
            }

            /// <summary>
            /// Add a routine for a specific waypoint.
            /// </summary>
            /// <param name="waypoint"></param>
            /// <param name="routine"></param>
            public void AddRoutineForWaypoint(IWaypoint waypoint, string routine)
            {
                WaypointRoutines[waypoint] = routine;
            }

            /// <summary>
            /// Remove the routine for a waypoint.
            /// </summary>
            /// <param name="waypointName"></param>
            public void RemoveRoutineForWaypoint(string waypointName)
            {
                IWaypoint waypoint = GetWaypointFromName(waypointName);

                if (waypoint != null)
                    WaypointRoutines.Remove(waypoint);
            }

            /// <summary>
            /// Is the queue empty?
            /// </summary>
            /// <returns></returns>
            public bool IsEmpty()
            {
                return WaypointRoutines.Count == 0;
            }
        }
    }
}
