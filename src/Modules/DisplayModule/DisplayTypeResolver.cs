using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    /// <summary>
    /// Helper class for resolving display types and source filtering.
    /// This provides utility methods for determining what type a display is
    /// and whether the current Mother instance is allowed to write to it.
    /// 
    /// Modules should define their own display type constants and use this
    /// class to check if a display matches their type.
    /// 
    /// Display type is configured via the block's CustomData:
    /// [general]
    /// type=myDisplayType
    /// source=MotherOS   ; optional - matches Mother's SystemName or ShortId
    /// surfaceIndex=0    ; optional - for multi-surface blocks like cockpits
    /// </summary>
    public static class DisplayTypeResolver
    {
        /// <summary>
        /// The configuration section name for general settings.
        /// </summary>
        const string SECTION_GENERAL = "general";

        /// <summary>
        /// The configuration key for display type.
        /// </summary>
        const string KEY_TYPE = "type";

        /// <summary>
        /// The configuration key for display source filtering.
        /// </summary>
        const string KEY_SOURCE = "source";

        /// <summary>
        /// The configuration key for surface index on multi-surface blocks.
        /// </summary>
        const string KEY_SURFACE_INDEX = "surfaceIndex";

        /// <summary>
        /// Get the display type from the block's configuration.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <returns>The display type in lowercase, or empty string if not configured.</returns>
        public static string GetDisplayType(MyIni blockConfig)
        {
            return blockConfig.Get(SECTION_GENERAL, KEY_TYPE).ToString().Trim().ToLower();
        }

        /// <summary>
        /// Check if the display type matches the expected type.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <param name="expectedType">The expected display type (case-insensitive).</param>
        /// <returns>True if the display is of the expected type.</returns>
        public static bool IsDisplayType(MyIni blockConfig, string expectedType)
        {
            string actualType = GetDisplayType(blockConfig);
            return string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the source filter from the block's configuration.
        /// Strips surrounding quotes if present.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <returns>The source filter value, or empty string if not set.</returns>
        public static string GetSource(MyIni blockConfig)
        {
            string source = blockConfig.Get(SECTION_GENERAL, KEY_SOURCE).ToString().Trim();
            return Unquote(source);
        }

        /// <summary>
        /// Strips surrounding double quotes from a string if present.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The string without surrounding quotes.</returns>
        static string Unquote(string input)
        {
            if (input.Length >= 2 && input[0] == '"' && input[input.Length - 1] == '"')
                return input.Substring(1, input.Length - 2);

            return input;
        }

        /// <summary>
        /// Check if the current Mother instance can write to the display.
        /// Returns true if:
        /// - No source is specified (any instance can write)
        /// - The source matches Mother's system name
        /// - The source matches Mother's short ID
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <param name="mother">The Mother instance to check against.</param>
        /// <returns>True if this Mother instance can write to the display.</returns>
        public static bool CanWriteToDisplay(MyIni blockConfig, Mother mother)
        {
            string source = GetSource(blockConfig);

            // If no source specified, any instance can write
            if (string.IsNullOrEmpty(source))
                return true;

            // Check if source matches Mother's system name or short ID
            return string.Equals(source, mother.SystemName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, mother.ShortId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a display is valid for the given type and Mother instance.
        /// This combines type checking and source filtering.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <param name="expectedType">The expected display type.</param>
        /// <param name="mother">The Mother instance to check source filtering against.</param>
        /// <returns>True if the display is of the correct type and the Mother instance can write to it.</returns>
        public static bool IsValidDisplayForType(MyIni blockConfig, string expectedType, Mother mother)
        {
            return IsDisplayType(blockConfig, expectedType) 
                && CanWriteToDisplay(blockConfig, mother);
        }

        /// <summary>
        /// Get the surface index from the block's configuration.
        /// Used for multi-surface blocks like cockpits and programmable blocks.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <returns>The surface index, or -1 if not configured.</returns>
        public static int GetSurfaceIndex(MyIni blockConfig)
        {
            string surfaceIndexStr = blockConfig.Get(SECTION_GENERAL, KEY_SURFACE_INDEX).ToString().Trim();

            int surfaceIndex;

            if (int.TryParse(surfaceIndexStr, out surfaceIndex))
                return surfaceIndex;

            return 0;
        }
    }
}

