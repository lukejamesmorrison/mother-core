using System;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// A GPSWaypoint represents an in-game GPS waypoint that can be accessed and 
    /// copied via the GPS tab in the game menu. GPS strings may be interpreted 
    /// directly to simplify the use of player-created waypoints.
    /// </summary>
    /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyRemoteControl"/>
    /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.MyWaypointInfo"/>
    public class GPSWaypoint : IWaypoint
    {
        /// <summary>
        /// The Id of the waypoint. This is a random integer generated at
        /// </summary>
        readonly long Id;

        /// <summary>
        /// The name of the waypoint. This is the name of the GPS waypoint 
        /// as it appears in the GPS String and on the player HUD.
        /// </summary>
        readonly string Name;

        /// <summary>
        /// The color of the waypoint. This is the color of the GPS waypoint 
        /// in hexadecimal format as it appears on the player HUD.
        /// </summary>
        readonly string Color;

        /// <summary>
        /// The vector of the waypoint. This is the position of the GPS waypoint.
        /// </summary>
        Vector3D Vector;

        /// <summary>
        /// Constructor.
        /// We great a GPS waypoint from a GPS string that a player can copy from the 
        /// GPS menu. It contains 6 parts.
        /// </summary>
        /// <see href="https://lukejamesmorrison.github.io/mother-docs/IngameScript/Modules/Extension/NavigationModule.html#navigation-module"/>
        /// <param name="gps_string"></param>
        public GPSWaypoint(string gps_string)
        {
            // generate a random integer for the Id
            Id = new Random().Next(0, 1000000);

            string[] parts = gps_string.Split(':');

            if (parts.Length < 6) return;

            Name = parts[1];
            Vector = new Vector3D(double.Parse(parts[2]), double.Parse(parts[3]), double.Parse(parts[4]));
            Color = parts[5];
        }

        /// <summary>
        /// Get the vector (position) of the waypoint.
        /// </summary>
        /// <returns></returns>
        public Vector3D GetVector() => Vector;

        /// <summary>
        /// Get the name of the waypoint.
        /// </summary>
        /// <returns></returns>
        public string GetName() => Name;
    }
}
