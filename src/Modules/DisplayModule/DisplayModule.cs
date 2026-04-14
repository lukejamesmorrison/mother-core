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
    /// The DisplayModule extension module.
    /// </summary>
    public class DisplayModule : BaseCoreModule
    {
        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Log core module.
        /// </summary>
        Log Log;

        /// <summary>
        /// The Almanac core module.
        /// </summary>
        Almanac Almanac;

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
        /// The surfaces to be used to render almanac information.
        /// </summary>
        readonly List<IMyTextSurface> AlmanacSurfaces = new List<IMyTextSurface>();

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
            Almanac = Mother.GetModule<Almanac>();

            // Commands
         

            // Load all text surfaces on the grid.
            LoadTextSurfaces();

            // Schedule Almanac surfaces to refresh on the tick cycle (Update10).
            // Log and Debug surfaces are rendered on demand via RenderConsoleSurfaces().
            Clock.Schedule(RenderAlmanacSurfaces);
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
            string almanacCount = $"{Almanac.Records.Count()}";
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
                LogSurfaces.Add(panel);

            else if (IsAlmanacSurface(panel, config))
                AlmanacSurfaces.Add(panel);
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

                    if (displayType == DisplayTypeResolver.DisplayTypes.Log)
                        LogSurfaces.Add(surface);

                    else if (displayType == DisplayTypeResolver.DisplayTypes.Almanac)
                        AlmanacSurfaces.Add(surface);
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
            AlmanacSurfaces.Clear();
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
                DisplayTypeResolver.DisplayTypes.Log, 
                Mother
            );
        }


        /// <summary>
        /// Check if the surface is an almanac surface. Uses config-based type detection
        /// with source filtering.
        /// </summary>
        /// <param name="panel">The text panel to check.</param>
        /// <param name="config">The block's configuration.</param>
        /// <returns>True if this is an almanac surface that this Mother instance can write to.</returns>
        bool IsAlmanacSurface(IMyTextPanel panel, MyIni config)
        {
            bool isAlmanac = DisplayTypeResolver.IsValidDisplayForType(
                config, 
                DisplayTypeResolver.DisplayTypes.Almanac, 
                Mother
            );

            if (isAlmanac)
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

            return isAlmanac;
        }

        /// <summary>
        /// Render all display surfaces on the grid.
        /// </summary>
        //void RenderDisplaySurfaces()
        //{
        //    RenderConsoleSurfaces();
        //    RenderAlmanacSurfaces();
        //}

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
        /// Render the almanac surfaces. This runs on the tick cycle (Update10).
        /// </summary>
        void RenderAlmanacSurfaces()
        {
            var records = Almanac.Records.OrderBy(r => r.Nicknames.FirstOrDefault()).ToList();

            string outputString = string.Join("\n", records.Select(r => {
                bool isFriendly = r.IsFriendly();
                bool isNeutral = r.IsNeutral();

                string name = r.Nicknames.FirstOrDefault();
                string IFFCode = isFriendly ? "F" : isNeutral ? "N" : "U";
                double distance = Vector3D.Distance(Mother.CubeGrid.GetPosition(), r.Position);

                string line = Display.CreateTextLine(
                    $"({IFFCode}) {name}",
                    $"{distance:F0}m",
                    30
                );

                return line;
            }));

            AlmanacSurfaces.ForEach(surface => {
                surface.WriteText($"{outputString}", false);
            });
        }
    }
}
