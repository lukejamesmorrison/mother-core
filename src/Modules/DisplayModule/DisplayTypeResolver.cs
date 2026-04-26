using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    /// <summary>
    /// Represents a single parsed entry from the <c>[surfaces]</c> config section.
    ///
    /// Format: <c>surfaceIndex=ViewName [optional source/parameter]</c>
    ///
    /// Example:
    /// <code>
    /// [surfaces]
    /// 0=MainMenu
    /// 1=MapView
    /// 2=LogView "Mother OS"
    /// </code>
    /// </summary>
    public struct SurfaceEntry
    {
        /// <summary>The zero-based surface index on the block.</summary>
        public int Index;

        /// <summary>The view name that should be rendered on this surface (case-insensitive).</summary>
        public string ViewName;

        /// <summary>
        /// Optional source or parameter following the view name.
        /// For <c>LogView</c> this is the Mother instance name/ID that should own the surface.
        /// For other views it is forwarded as a view parameter.
        /// Surrounding quotes are stripped automatically.
        /// </summary>
        public string Parameter;
    }

    /// <summary>
    /// Helper class for resolving surface entries from a block's <c>[surfaces]</c>
    /// config section and performing source-based write filtering.
    ///
    /// Surfaces are configured in the block's CustomData like:
    /// <code>
    /// [surfaces]
    /// 0=MainMenu
    /// 1=MapView
    /// 2=LogView "Mother OS"
    /// </code>
    /// </summary>
    public static class DisplayTypeResolver
    {
        const string SECTION_SURFACES = "surfaces";

        /// <summary>
        /// Parse all entries from the <c>[surfaces]</c> section of a block's config.
        /// Keys whose names are not valid integers are silently skipped.
        /// </summary>
        /// <param name="blockConfig">The block's parsed configuration.</param>
        /// <returns>A list of <see cref="SurfaceEntry"/> values, one per valid key.</returns>
        public static List<SurfaceEntry> GetSurfaceEntries(MyIni blockConfig)
        {
            var entries = new List<SurfaceEntry>();
            var keys = new List<MyIniKey>();
            blockConfig.GetKeys(SECTION_SURFACES, keys);
            foreach (MyIniKey key in keys)
            {
                int index;
                if (!int.TryParse(key.Name, out index))
                    continue;

                string raw = blockConfig.Get(SECTION_SURFACES, key.Name).ToString().Trim();

                if (string.IsNullOrEmpty(raw))
                    continue;

                string viewName;
                string parameter = null;

                int spaceIdx = raw.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    viewName  = raw.Substring(0, spaceIdx).Trim();
                    parameter = Unquote(raw.Substring(spaceIdx + 1).Trim());
                }
                else
                {
                    viewName = raw;
                }

                entries.Add(new SurfaceEntry { Index = index, ViewName = viewName, Parameter = parameter });
            }

            return entries;
        }

        /// <summary>
        /// Check whether this Mother instance is allowed to write to a surface
        /// given an optional source string parsed from a <see cref="SurfaceEntry"/>.
        /// Returns <c>true</c> when:
        /// <list type="bullet">
        ///   <item>No source is specified (any instance may write).</item>
        ///   <item>The source matches <see cref="Mother.SystemName"/> (case-insensitive).</item>
        ///   <item>The source matches <see cref="Mother.ShortId"/> (case-insensitive).</item>
        /// </list>
        /// </summary>
        /// <param name="source">The source string from the surface entry, or null/empty for none.</param>
        /// <param name="mother">The Mother instance to check against.</param>
        public static bool CanWriteToDisplay(string source, Mother mother)
        {
            if (string.IsNullOrEmpty(source))
                return true;

            return string.Equals(source, mother.SystemName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, mother.ShortId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strips surrounding double quotes from a string if present.
        /// </summary>
        static string Unquote(string input)
        {
            if (input.Length >= 2 && input[0] == '"' && input[input.Length - 1] == '"')
                return input.Substring(1, input.Length - 2);

            return input;
        }
    }
}

