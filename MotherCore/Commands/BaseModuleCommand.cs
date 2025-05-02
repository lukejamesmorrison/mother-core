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
    /// Base class for all module commands.
    /// </summary>
    public abstract class BaseModuleCommand : IModuleCommand
    {
        /// <summary>
        /// Struct to hold the parsed incremental value, and flags for increment and decrement.
        /// </summary>
        protected struct IncrementalValue
        {
            public float Value;
            public bool IsIncrement;
            public bool IsDecrement;
        }
        /// <summary>
        /// The name of the command. This should be unique for each command.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Execute the a Terminal Command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public abstract string Execute(TerminalCommand command);

        /// <summary>
        /// Get the command name.
        /// </summary>
        /// <returns></returns>
        public string GetCommandName()
        {
            return Name;
        }

        /// <summary>
        /// Parses a string representing an incremental value.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected IncrementalValue ParseIncrementalValue(string speed, Dictionary<string, string> options)
        {
            // Assume value is always in the second position
            string valueString = speed;

            float parsedValue;
            if (!float.TryParse(valueString, out parsedValue))
                throw new ArgumentException("Invalid numerical value provided.");

            // Determine increment or decrement flags
            bool isIncrement = options.ContainsKey("add");
            bool isDecrement = options.ContainsKey("sub");

            return new IncrementalValue
            {
                Value = parsedValue,
                IsIncrement = isIncrement,
                IsDecrement = isDecrement
            };
        }

        protected float ApplyIncrementalChange(float currentValue, IncrementalValue parsedValue)
        {
            if (parsedValue.IsIncrement)
                return currentValue + parsedValue.Value;

            else if (parsedValue.IsDecrement)
                return currentValue - parsedValue.Value;

            return parsedValue.Value; // Default case (set value directly)
        }
    }
}
