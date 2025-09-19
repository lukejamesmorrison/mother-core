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
        public const string BlockNotFound = "Block not found: {0}";
        public const string InvalidArgument = "Invalid argument for block: {0}";
        public const string InvalidOption = "Invalid command option: {0}";
        public const string BlockUpdated = "Block updated: {0} -> {1}";
        public const string BlockResetting = "Block resetting: {0}";
        public const string BlockMoving = "Block moving: {0}";
        public const string BlockStarted = "Block started: {0}";
        public const string BlockStopped = "Block stopped: {0}";
        public const string BlockLocked = "Block locked: {0}";
        public const string BlockUnlocked = "Block unlocked: {0}";
        public const string BlockOpen = "Block open: {0}";
        public const string BlockCharging = "Block charging: {0}";
        public const string BlockDischarging = "Block discharging: {0}";
        public const string BlockAuto = "Block auto: {0}";
        public const string BlockOn = "Block on: {0}";
        public const string BlockOff = "Block off: {0}";
        public const string BlockClosed = "Block closed: {0}";
        public const string BlockToggled = "Block toggled: {0}";
        public const string BlockStockpiling = "Block stockpiling: {0}";
        public const string BlockSharing = "Block sharing: {0}";
        public const string BlockActionRun = "Block action: {0} -> {1}";
        public const string BlockRun = "Block: {0} -> {1}";
    }
}
