using Sandbox.Engine.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// A map display that can render grids, waypoints, and flight plans.
    /// </summary>
    class MapDisplay : Display
    {
        /// <summary>
        /// The default map scale for the display in meters.
        /// </summary>
        public float MapScale = 100f;

        /// <summary>
        /// Should the display render in 3D?
        /// </summary>
        public bool Is3dMode = false;

        /// <summary>
        /// The center coordinate to be used when rendering a map on the display.
        /// </summary>
        public Vector3D? MapCenter;

        /// <summary>
        /// A whitelist of grid and channel names to display on the map.
        /// </summary>
        readonly HashSet<string> Whitelist = new HashSet<string>();

        /// <summary>
        /// A blacklist of grid and channel names to exclude from the map display.
        /// </summary>
        readonly HashSet<string> Blacklist = new HashSet<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MapDisplay"/> class.
        /// </summary>
        /// <param name="surface">The IMyTextSurface related to the display.</param>
        /// <param name="block">The IMyTerminalBlock parent of the display and surface.</param>
        /// <param name="config">The configuration for the display's parent IMyTerminalBlock block.</param>
        public MapDisplay(IMyTextSurface surface, IMyTerminalBlock block, MyIni config) : base(surface, block, config) 
        {
            Surface.ContentType = ContentType.SCRIPT;
            Surface.Script = "";

            LoadConfiguration();
        }

        /// <summary>
        /// Set the center of the map display if set within the block's configuration.
        /// </summary>
        /// <param name="point"></param>
        public void SetCenterCoordinate(Vector3D? point)
        {
            MapCenter = point;
        }

        /// <summary>
        /// Set the configuration for the display. This uses the configuration 
        /// defined in the block's custom data.
        /// </summary>
        public void LoadConfiguration()
        {
            if (Configuration.ContainsSection("general"))
            {
                MapScale = Configuration.Get("general", "mapScale").ToSingle();
              
                Is3dMode = $"{Configuration.Get("general", "mode")}" == "3D";

                SetFilters(Configuration.Get("general", "filter").ToString());

                string center = Configuration.Get("general", "center").ToString();

                // Try for GPS waypoint string
                if (center.Contains(":"))
                    SetCenterCoordinate(Geometry.GetVectorFromGPSString(center));
            }
        }

        /// <summary>
        /// Set the filters for the map display. This allows the user to 
        /// whitelist or blacklist entities by name or channel.
        /// </summary>
        /// <param name="filterString"></param>
        void SetFilters(string filterString)
        {
            // 1. separate string by spaces
            var terms = filterString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // 2. if term starts with +, add to whitelist
            foreach (var term in terms)
            {
                if (term.StartsWith("+"))
                    Whitelist.Add(term.Substring(1).Trim());

                // 3. if term starts with -, add to blacklist
                else if (term.StartsWith("-"))
                    Blacklist.Add(term.Substring(1).Trim());
            }
        }

        /// <summary>
        /// Draw a waypoint on the display. Waypoints are displayed as yellow diamonds.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="waypointPosition"></param>
        /// <param name="currentPosition"></param>
        public void DrawWaypoint(AlmanacRecord record, Vector2 waypointPosition, Vector3D currentPosition)
        {
            // print indicator
            float scaleFactor = GetScalingFactor();
            DrawSquareSprite(waypointPosition, 10 * scaleFactor, Color.Yellow, 45 * scaleFactor);

            // Print waypoint name
            Vector2 textPosition = waypointPosition + new Vector2(10, 10) * scaleFactor;
            DrawText(record.Id, textPosition, Color.White, "White");

            // Print waypoint details
            var distance = Vector3D.Distance(currentPosition, record.Position);
            Vector2 distanceTextPosition = waypointPosition + new Vector2(10, 34) * scaleFactor;
            DrawText($"{distance:F0}m", distanceTextPosition, Color.White, "White");
        }

        /// <summary>
        /// Draw a grid on the display. Grids are displayed as circles and will have 
        /// different colors based upon their IFF type. Our local grid is 
        /// represented by a green triangle.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="position"></param>
        /// <param name="currentPosition"></param>
        /// <param name="gridName"></param>
        public void DrawGrid(AlmanacRecord record, Vector2 position, Vector3D currentPosition, string gridName)
        {
            string name = record.Nicknames.Count > 0 ? record.Nicknames[0] : record.Id;

            // We always want to show the current grid
            bool isCurrentGrid = name == gridName;

            // the whitelist can include grids and channels
            bool isWhitelisted = 
                Whitelist.Count == 0 
                || record.Nicknames.Any(n => Whitelist.Contains(n))
                || record.Channels.Any(n => Whitelist.Contains(n));

            // the blacklist can include grids and channels
            bool isBlacklisted =
                record.Nicknames.Any(n => Blacklist.Contains(n))
                || record.Channels.Any(n => Blacklist.Contains(n));

            // Skip drawing if:
            // - not whitelisted (when whitelist is present), OR
            // - blacklisted
            // AND it's not the current grid (which must always be visible)
            if ((!isWhitelisted || isBlacklisted) && !isCurrentGrid)
                return;

            string graphicType = name == gridName ? "SquareSimple" : "Circle";
            float size = 12;

            if (name == gridName)
                DrawTriangleSprite(position, (size + 6) * GetScalingFactor(), Color.Green);

            else
            {
                Color color = Color.White;

                if (record.IsFriendly())
                    color = Color.Green;

                else if (record.IsHostile())
                    color = Color.Red;

                else if (record.IsNeutral())
                    color = Color.RoyalBlue;

                DrawCircleSprite(position, size * GetScalingFactor(), color);
            }

            // Print grid name
            DrawText(name, position + new Vector2(10, 10) * GetScalingFactor(), Color.White, "White");

            // Print grid details
            var distance = Vector3D.Distance(currentPosition, record.Position);

            if (name != gridName)
                DrawText($"{distance:F0}m", position + new Vector2(10, 34) * GetScalingFactor(), Color.White, "White");
        }

        /// <summary>
        /// Draw the flight plan on the display. This draws the waypoints and flight 
        /// paths connecting them.
        /// </summary>
        /// <param name="flightPlan"></param>
        /// <param name="boundingBox"></param>
        /// <param name="grid"></param> 
        /// <param name="text"></param>
        /// <param name="center"></param>
        public void DrawFlightPlan(IFlightPlan flightPlan, BoundingBoxD boundingBox, IMyCubeGrid grid, string text, Vector3D? center = null)
        {
            Vector3D mapCenter = center ?? MapCenter ?? grid.GetPosition();
            MatrixD gridOrientation = MatrixD.CreateFromDir(grid.WorldMatrix.Forward, grid.WorldMatrix.Up);

            var waypoints = flightPlan.GetWaypoints();

            if (waypoints == null || waypoints.Count < 2) return;

            for (int i = 1; i < waypoints.Count; i++)
            {
                var currentNormalized = NormalizePositionForDisplay(mapCenter, waypoints[i].GetVector(), gridOrientation);
                var previousNormalized = NormalizePositionForDisplay(mapCenter, waypoints[i - 1].GetVector(), gridOrientation);

                DrawLineSprite(previousNormalized, currentNormalized, Color.White, 2);
            }

            DrawText(text, TopLeft, Color.White, "White");
        }

        /// <summary>
        /// Normalize a 3D position for the display. This is used to convert world 
        /// coordinates into 2D coordinates on a map display.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="position"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public Vector2 NormalizePositionForDisplay(Vector3D center, Vector3D position, MatrixD orientation)
        {
            // Determine the world-space height per pixel
            float worldHeight = MapScale;
            float pixelsPerMeter = Viewport.Height / worldHeight;

            // Maintain aspect ratio for width
            float worldWidth = (Viewport.Width / Viewport.Height) * worldHeight;

            // Compute relative position to the grid ViewportCenter
            Vector3D relativePos = position - center;

            // Apply 2D or 3D transformation
            Vector3D transformedPos = Is3dMode
                ? Vector3D.Transform(relativePos, MatrixD.Transpose(orientation))
                : new Vector3D(relativePos.X, 0, relativePos.Z);

            // Convert transformed position to normalized coordinates (Z is "up")
            double normalizedX = transformedPos.X / worldWidth;
            double normalizedY = transformedPos.Z / worldHeight;

            // Convert to screen coordinates (Y is flipped)
            float screenX = (float)(Viewport.X + (0.5 + normalizedX) * Viewport.Width);
            float screenY = (float)(Viewport.Y + (0.5 - normalizedY) * Viewport.Height);

            return new Vector2(screenX, screenY);
        }
    }
}
