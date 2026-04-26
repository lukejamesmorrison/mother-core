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
        /// The view name used in <c>[surfaces]</c> entries to designate a log surface.
        /// </summary>
        public const string VIEW_NAME_LOG = "LogView";

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
        /// Load a single text panel surface. Text panels are single-surface blocks, so we look
        /// for a <c>[surfaces]</c> entry with index <c>0</c>.
        /// </summary>
        void LoadTextPanelSurface(IMyTextPanel panel)
        {
            MyIni config = BlockCatalogue.GetBlockConfiguration(panel);

            foreach (SurfaceEntry entry in DisplayTypeResolver.GetSurfaceEntries(config))
            {
                if (entry.Index != 0)
                    continue;

                TextSurfaces.Add(panel);

                if (string.Equals(entry.ViewName, VIEW_NAME_LOG, StringComparison.OrdinalIgnoreCase)
                    && DisplayTypeResolver.CanWriteToDisplay(entry.Parameter, Mother))
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    LogSurfaces.Add(panel);
                }

                AddSurfaceToRegisteredType(entry.ViewName, panel);
                break;
            }
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
        /// Load text surfaces from a text surface provider block. Each entry in the
        /// <c>[surfaces]</c> section maps a surface index to a view name, optionally
        /// followed by a source filter.
        /// </summary>
        void LoadTextSurfaceProviderSurfaces(IMyTerminalBlock block)
        {
            IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
            if (provider == null)
                return;

            MyIni config = BlockCatalogue.GetBlockConfiguration(block);

            foreach (SurfaceEntry entry in DisplayTypeResolver.GetSurfaceEntries(config))
            {
                if (entry.Index >= provider.SurfaceCount)
                    continue;

                IMyTextSurface surface = provider.GetSurface(entry.Index);
                TextSurfaces.Add(surface);

                if (string.Equals(entry.ViewName, VIEW_NAME_LOG, StringComparison.OrdinalIgnoreCase)
                    && DisplayTypeResolver.CanWriteToDisplay(entry.Parameter, Mother))
                {
                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    LogSurfaces.Add(surface);
                }

                AddSurfaceToRegisteredType(entry.ViewName, surface);
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
        /// <summary>
        /// Get all surfaces registered for a specific view name.
        /// Extension modules use this to retrieve surfaces claimed for them via
        /// a <c>[surfaces]</c> entry (e.g. <c>1=MapView</c>).
        /// </summary>
        /// <param name="viewName">The view name (case-insensitive).</param>
        /// <returns>List of text surfaces for the view, or empty list if none registered.</returns>
        public List<IMyTextSurface> GetSurfacesForDisplayType(string viewName)
        {
            string key = viewName.ToLower();

            if (RegisteredDisplaySurfaces.ContainsKey(key))
                return RegisteredDisplaySurfaces[key];

            return new List<IMyTextSurface>();
        }

        /// <summary>
        /// Add a surface to the view name's surface list, creating the list if needed.
        /// </summary>
        void AddSurfaceToRegisteredType(string viewName, IMyTextSurface surface)
        {
            string key = viewName.ToLower();

            if (!RegisteredDisplaySurfaces.ContainsKey(key))
                RegisteredDisplaySurfaces[key] = new List<IMyTextSurface>();

            RegisteredDisplaySurfaces[key].Add(surface);
        }
    }
}
