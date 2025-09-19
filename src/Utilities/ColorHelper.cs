using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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
    /// ColorHelper is a utility class that provides methods for managing colors.
    /// </summary>
    class ColorHelper
    {
        /// <summary>
        /// A dictionary of color names and their RGB values. This is used to 
        /// provide players with a simple accessor for common colors.
        /// </summary>
        public static readonly Dictionary<string, string> COLORS = new Dictionary<string, string>()
        {
            { "red", "255,0,0" },
            { "green", "0,255,0" },
            { "blue", "0,0,255" },
            { "yellow", "255,255,0" },
            { "cyan", "0,255,255" },
            { "magenta", "255,0,255" },
            { "orange", "255,165,0" },
            { "white", "255,255,255" },
            { "black", "0,0,0" }
        };

        /// <summary>
        /// Get a color from a comma-separated RGB string.
        /// </summary>
        /// <param name="rgbString"></param>
        /// <returns></returns>
        public static Color GetColorFromRGBString(string rgbString)
        {
            string[] rgb = rgbString.Split(',');
                
            return new Color(int.Parse(rgb[0]), int.Parse(rgb[1]), int.Parse(rgb[2]));
        }

        /// <summary>
        /// Get a color from a color name.  This is done by looking up the color 
        /// name in the COLORS dictionary. In a future version we should 
        /// leverage the built in colors for more variety.
        /// </summary>
        /// <param name="colorString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Color GetColorFromColorName(string colorString)
        {
            string rgbString;

            if (COLORS.TryGetValue(colorString.ToLower(), out rgbString))
                return GetColorFromRGBString(rgbString);

            else
                return Color.White;
        }

        /// <summary>
        /// Get a color from a string.  This is done by checking if we are 
        /// targeting by name, or an RGB string.
        /// </summary>
        /// <param name="colorString"></param>
        /// <returns></returns>
        public static Color GetColor(string colorString)
        {
            if (colorString.Contains(','))
                return GetColorFromRGBString(colorString);

            else
                return GetColorFromColorName(colorString);
        }
    }
}
