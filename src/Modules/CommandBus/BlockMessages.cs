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
    /// Messages for blocks. We centralize here to reduce the logic required 
    /// to print messages with modules and commands.
    /// </summary>
    public static class BlockMessages
    {
        public const string 
            BlockNotFound = "Block not found: {0}",
            InvalidArgument = "Invalid argument for block: {0}",
            InvalidOption = "Invalid command option: {0}",
            BlockUpdated = "Block updated: {0} -> {1}",
            BlockResetting = "Block resetting: {0}",
            BlockMoving = "Block moving: {0}",
            BlockStarted = "Block started: {0}",
            BlockStopped = "Block stopped: {0}",
            BlockLocked = "Block locked: {0}",
            BlockUnlocked = "Block unlocked: {0}",
            BlockOpen = "Block open: {0}",
            BlockCharging = "Block charging: {0}",
            BlockDischarging = "Block discharging: {0}",
            BlockAuto = "Block auto: {0}",
            BlockOn = "Block on: {0}",
            BlockOff = "Block off: {0}",
            BlockClosed = "Block closed: {0}",
            BlockToggled = "Block toggled: {0}",
            BlockStockpiling = "Block stockpiling: {0}",
            BlockSharing = "Block sharing: {0}",
            BlockActionRun = "Block action: {0} -> {1}",
            BlockRun = "Block: {0} -> {1}";
    }
}
