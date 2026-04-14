using Sandbox.Engine.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
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
using VRageRender;
using Color = VRageMath.Color;
using Vector2 = VRageMath.Vector2;

namespace IngameScript
{
    /// <summary>
    /// The DisplayModule core module. Provides an API for extension modules
    /// to register custom display types and access text surfaces on the grid.
    /// </summary>
    public class DisplayModule : BaseCoreModule
    {
        /// <summary>
        /// The display type for log surfaces. This is the only built-in display type.
        /// </summary>
        public const string DISPLAY_TYPE_LOG = "log";

        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Log core module.
        /// </summary>
        Log Log;

        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        BlockCatalogue BlockCatalogue;

        /// <summary>
        /// All text surfaces on the grid. This includes LCD Panels, and cockpit displays.
        /// </summary>
        readonly HashSet<IMyTextSurface> TextSurfaces = new HashSet<IMyTextSurface>();

        /// <summary>
        /// The surfaces to be used to render the log.
        /// </summary>
        readonly List<IMyTextSurface> LogSurfaces = new List<IMyTextSurface>();

        /// <summary>
        /// Registered display types and their associated surfaces.
        /// Extension modules can register custom display types.
        /// </summary>
        readonly Dictionary<string, List<IMyTextSurface>> RegisteredDisplaySurfaces = new Dictionary<string, List<IMyTextSurface>>();

        /// <summary>
        /// The default text size for the displays. This is used when 
        /// printing text to screens.
        /// </summary>
        public static float DEFAULT_TEXT_SIZE = 1;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public DisplayModule(Mother mother): base(mother) { }


        /// <summary>
        /// Boot the module. We set up our text surfaces and schedule a periodic 
        /// refresh of their rendered content. We also register commands 
        /// and reference modules.
        /// </summary>
        public override void Boot()
        {
            // Modules
            Clock = Mother.GetModule<Clock>();
            Log = Mother.GetModule<Log>();
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();

            // Monitor block configuration changes so we can
            // reload surfaces when display types change.
            Subscribe<BlockConfigChangedEvent>();

            // Load all text surfaces on the grid.
            LoadTextSurfaces();
        }

        /// <summary>
        /// Handle events. We listen for block configuration changes
        /// to update our surface registrations when display types change.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (
                e is BlockConfigChangedEvent
                && (eventData is IMyTextPanel
                   || eventData is IMyTextSurfaceProvider
                )
            )
            {
                // Reload all surfaces to update type registrations
                LoadTextSurfaces();
            }
        }

        /// <summary>
        /// Get the header for the display. This is used for the log and debugging screens.
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        string GetHeader(string screenName = "")
        {
            return
                $" {Mother.SystemName} - {screenName}     ({Clock.GetLoader()})\n" +
                $" {Mother.Name} *{Mother.ShortId}                                  {GetIndicators()}\n" +
                "------------------------------------------------------"
                ;
        }

        /// <summary>
        /// Get the indicators for the display. This is used to show the status of 
        /// various systems like the Activity Monitor and Almanac. 
        /// 
        /// THIS SHOULD BE REFACTORED AND DELEGATED TO MODULES. THIS SHOULD NOT LIVE IN THIS FILE.
        /// </summary>
        /// <returns></returns>
        public string GetIndicators()
        {
            string activityMonitorIndicator = Mother.GetModule<ActivityMonitor>().ActiveBlocks.Count() > 0 ? "M" : "   ";
            string activeRequests = Mother.GetModule<IntergridMessageService>().activeRequests.Count() > 0 ? "C" : "    ";
            string almanacCount = $"{Mother.GetModule<Almanac>()?.Records.Count() ?? 0}";
            string autopilotIndication = Mother.AutopilotEngaged ? "A" : "   ";
            string waitQueueIndicator = Clock.QueuedTaskCount > 0 ? "W" : "   ";

            return String.Join(
                "  ",
                waitQueueIndicator,
                autopilotIndication,
                activeRequests,
                activityMonitorIndicator,
                almanacCount
            );
        }

        /// <summary>
        /// Get a text surfaces by block name and surface index
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public List<IMyTextSurface> GetDisplaysByName(string name)
        {
            // split block name by colon separator
            var blockNameParts = name.Split(':');
            var surfaces = new List<IMyTextSurface>();

            // if there are more than 1 parts, we use the second part as the surface index
            if (blockNameParts.Length > 1)
            {
                string blockName = blockNameParts[0].Trim();
                int surfaceIndex = int.Parse(blockNameParts[1].Trim());

                surfaces = BlockCatalogue
                    .GetBlocksByName<IMyTerminalBlock>(blockName)
                    .Where(block => block is IMyTextSurfaceProvider)
                    .Select(block => ((IMyTextSurfaceProvider) block).GetSurface(surfaceIndex))
                    .Where(surface => surface != null)
                    .ToList();
            } 
            else
            {
                surfaces = BlockCatalogue
                    .GetBlocksByName<IMyTerminalBlock>(name)
                    .Where(block => block is IMyTextSurface)
                    .Select(block => (IMyTextSurface) block)
                    .ToList();
            }

            return surfaces;
        }

        /// <summary>
        /// Load all text panels on the grid. This includes LCD Panels, but not cockpit displays.
        /// </summary>
        void LoadTextPanelSurfaces()
        {
            BlockCatalogue
                .GetBlocks<IMyTextPanel>()
                ?.ForEach(panel => LoadTextPanelSurface(panel));
        }

        /// <summary>
        /// Load a single text panel surface. Uses config-based type detection with source
        /// filtering, falling back to legacy name-based detection for backwards compatibility.
        /// </summary>
        /// <param name="panel"></param>
        void LoadTextPanelSurface(IMyTextPanel panel)
        {
            TextSurfaces.Add(panel);

            MyIni config = BlockCatalogue.GetBlockConfiguration(panel);

            if (IsLogSurface(panel, config))
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;
                LogSurfaces.Add(panel);
            }

            // Add to registered display surfaces based on type
            string displayType = DisplayTypeResolver.GetDisplayType(config);
            if (!string.IsNullOrEmpty(displayType) && DisplayTypeResolver.CanWriteToDisplay(config, Mother))
                AddSurfaceToRegisteredType(displayType, panel);
        }


        /// <summary>
        /// Load all text surfaces from text surface provider blocks on the grid.
        /// </summary>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider"/>
        void LoadTextSurfaceProviderSurfaces()
        {
            var textSurfaceProviders = new List<IMyTerminalBlock>();

            // cockpit blocks
            textSurfaceProviders.AddRange(BlockCatalogue.GetBlocks<IMyCockpit>());

            // programmable blocks
            textSurfaceProviders.AddRange(BlockCatalogue.GetBlocks<IMyProgrammableBlock>());

            // sound blocks (ie. Inset Entertainment Corner, Jukebox)
            textSurfaceProviders.AddRange(BlockCatalogue.GetBlocks<IMySoundBlock>());

            // Load surfaces from each block
            textSurfaceProviders.ForEach(
                block => LoadTextSurfaceProviderSurfaces(block)
             );
        }

        /// <summary>
        /// Load text surfaces from a text surface provider block. This is 
        /// necessary as some block with embedded screens contain text 
        /// surfaces rather than LCD panels. Uses config-based type detection
        /// with source filtering.
        /// </summary>
        void LoadTextSurfaceProviderSurfaces(IMyTerminalBlock block)
        {
            MyIni config = BlockCatalogue.GetBlockConfiguration(block);

            // Get display type and surface index from config
            int surfaceIndex = DisplayTypeResolver.GetSurfaceIndex(config);
            
            // If no surface index configured, skip this block
            if (surfaceIndex < 0)
                return;

            // Check source filtering
            if (!DisplayTypeResolver.CanWriteToDisplay(config, Mother))
                return;

            if (block is IMyTextSurfaceProvider)
            {
                IMyTextSurfaceProvider textSurfaceProvider = (IMyTextSurfaceProvider) block;
             
                if (surfaceIndex < textSurfaceProvider.SurfaceCount)
                {
                    IMyTextSurface surface = textSurfaceProvider.GetSurface(surfaceIndex);
                    TextSurfaces.Add(surface);

                    string displayType = DisplayTypeResolver.GetDisplayType(config);

                    if (displayType == DISPLAY_TYPE_LOG)
                    {
                        surface.ContentType = ContentType.TEXT_AND_IMAGE;
                        LogSurfaces.Add(surface);
                    }

                    // Add to registered display surfaces based on type
                    if (!string.IsNullOrEmpty(displayType))
                        AddSurfaceToRegisteredType(displayType, surface);
                }
            }
        }

        /// <summary>
        /// Load all text surfaces on the grid. This includes LCD Panels, and cockpit displays.
        /// </summary>
        public void LoadTextSurfaces()
        {
            ClearSurfacesAndDisplays();

            LoadTextPanelSurfaces();
            LoadTextSurfaceProviderSurfaces();
        }

        /// <summary>
        /// Clear all surfaces. This is used to reset the surfaces when the grid is reloaded.
        /// </summary>
        void ClearSurfacesAndDisplays()
        {
            TextSurfaces.Clear();
            LogSurfaces.Clear();

            // Clear all registered display type surfaces
            foreach (var list in RegisteredDisplaySurfaces.Values)
                list.Clear();
        }

        /// <summary>
        /// Check if the surface is a log surface. Uses config-based type detection
        /// with source filtering.
        /// </summary>
        /// <param name="panel">The text panel to check.</param>
        /// <param name="config">The block's configuration.</param>
        /// <returns>True if this is a log surface that this Mother instance can write to.</returns>
        bool IsLogSurface(IMyTerminalBlock panel, MyIni config)
        {
            return DisplayTypeResolver.IsValidDisplayForType(
                config, 
                DISPLAY_TYPE_LOG, 
                Mother
            );
        }


        /// <summary>
        /// Render the console surfaces (Log). This is called on every 
        /// input cycle to ensure immediate feedback after terminal commands.
        /// </summary>
        public void RenderConsoleSurfaces()
        {
            RenderLogSurfaces();
        }

        /// <summary>
        /// Get the log string. This is used when displaying the log on screens.
        /// </summary>
        /// <returns></returns>
        string GetLogString()
        {
            string logString = string.Join("\n", Log.Records);

            return GetHeader("LOG") + "\n" + logString;
        }

        /// <summary>
        /// Render the log surfaces. This is used to display the log on screens.
        /// </summary>
        void RenderLogSurfaces()
        {
            string logString = GetLogString();

            LogSurfaces.ForEach(surface => surface.WriteText($"{logString}", false));
        }

        /// <summary>
        /// Register a custom display type. Extension modules should call this to 
        /// register their display types so surfaces can be collected.
        /// </summary>
        /// <param name="displayType">The display type name (case-insensitive).</param>
        public void RegisterDisplayType(string displayType)
        {
            string key = displayType.ToLower();
            if (!RegisteredDisplaySurfaces.ContainsKey(key))
                RegisteredDisplaySurfaces[key] = new List<IMyTextSurface>();
        }

        /// <summary>
        /// Get all surfaces registered for a specific display type.
        /// Extension modules can use this to get their display surfaces.
        /// </summary>
        /// <param name="displayType">The display type name (case-insensitive).</param>
        /// <returns>List of text surfaces for the display type, or empty list if not registered.</returns>
        public List<IMyTextSurface> GetSurfacesForDisplayType(string displayType)
        {
            string key = displayType.ToLower();
            if (RegisteredDisplaySurfaces.ContainsKey(key))
                return RegisteredDisplaySurfaces[key];

            return new List<IMyTextSurface>();
        }

        /// <summary>
        /// Add a surface to a registered display type's surface list.
        /// </summary>
        /// <param name="displayType">The display type name.</param>
        /// <param name="surface">The surface to add.</param>
        void AddSurfaceToRegisteredType(string displayType, IMyTextSurface surface)
        {
            string key = displayType.ToLower();
            if (RegisteredDisplaySurfaces.ContainsKey(key))
                RegisteredDisplaySurfaces[key].Add(surface);
        }
    }
}
