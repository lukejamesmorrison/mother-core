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
    /// Represents a command run in the Programmable Block terminal.
    ///
    /// [Command] [Arguments..] [--Options..]
    /// </summary>
    public class TerminalCommand
    {
        /// <summary>
        /// The original command string.
        /// </summary>
        public string CommandString;

        /// <summary>
        /// The command name.
        /// 
        /// ie. rotor/rotate, light/blink
        /// </summary>
        public string Name;

        /// <summary>
        /// The arguments of the command. Arguments are the parts of the command that are not options.
        /// 
        /// ie. -45, "Hello World", red, #airlock-tag
        /// </summary>p
        public List<string> Arguments = new List<string>();

        /// <summary>
        /// The options of the command. Options are key-value pairs that start with '--'.
        /// 
        /// ie. --speed=100, --delay=0.5, --force
        /// </summary>
        public Dictionary<string, string> Options = new Dictionary<string, string>();

        /// <summary>
        /// The increment value for the command.
        /// </summary>
        public bool IsIncrement = false;

        /// <summary>
        /// The decrement value for the command.
        /// </summary>
        public bool IsDecrement = false;

        /// <summary>
        /// Indicates if the command should be forced to run locally,
        /// overriding any important commands on the construct.
        /// Set when the command is prefixed with "!!".
        /// </summary>
        public bool IsForceLocal = false;

        /// <summary>
        /// Creates a new terminal command from a command string.
        /// </summary>
        /// <param name="commandString"></param>
        public TerminalCommand(string commandString)
        {
            CommandString = commandString.Replace("\r", "").Trim();

            ParseCommand();
        }

        /// <summary>
        /// Gets an option value by key.
        /// 
        /// ie. --speed=100
        /// GetOption("speed") => "100"
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetOption(string key)
        {
            if (Options.ContainsKey(key))
                return Options[key];

            return "";
        }

        /// <summary>
        /// Parses a string representing an incremental value. The result is the
        /// value when --add is set, its negation when --sub is set, and 0 otherwise.
        /// </summary>
        /// <param name="valueString"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public float GetIncrement(string valueString) => GetIncrement(valueString, Options);

        /// <summary>
        /// Parses a string representing an incremental value from a set of options.
        /// </summary>
        /// <param name="valueString"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static float GetIncrement(string valueString, Dictionary<string, string> options)
        {
            float parsedValue;

            if (!float.TryParse(valueString, out parsedValue))
                throw new ArgumentException("Invalid numerical value provided.");

            if (options.ContainsKey("add"))
                return parsedValue;

            if (options.ContainsKey("sub"))
                return -parsedValue;

            // we assume no increment or decrement by default
            return 0;
        }

        /// <summary>
        /// Calculates the distributed value for cumulative operations across multiple blocks.
        /// When cumulative mode is enabled, the total value is divided evenly among all blocks.
        /// </summary>
        /// <param name="totalValue">The total value to distribute.</param>
        /// <param name="blockCount">The number of blocks to distribute across.</param>
        /// <param name="isCumulative">Whether cumulative mode is enabled.</param>
        /// <returns>The value per block (distributed if cumulative, otherwise the original value).</returns>
        public static float GetDistributedValue(float totalValue, int blockCount, bool isCumulative)
        {
            if (isCumulative && blockCount > 1)
                return totalValue / blockCount;

            return totalValue;
        }

        /// <summary>
        /// Determines if shared mode (--share) is enabled. When enabled, values
        /// are distributed evenly across all blocks in the group.
        /// </summary>
        /// <returns>True if shared mode is enabled.</returns>
        public bool IsShared() => Options.ContainsKey("share");

        /// <summary>
        /// Converts a string to a boolean. Recognizes "true" (case insensitive) and "1" as true.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GetBoolFromString(string value)
        {
            return value?.Trim().ToLower() == "true" || value?.Trim() == "1";
        }

        /// <summary>
        /// Parses the command string into its parts (arguments, options). Options 
        /// begin with '--' and other terms are considered Arguments. An Option 
        /// without a value is considered true.
        /// </summary>
        void ParseCommand()
        {
            foreach (string term in SplitInputIntoTerms(CommandString))
            {
                // If the term starts with '--', it's an option.
                if (term.StartsWith("--"))
                {
                    string[] parts = term.Split('=');

                    // Set key & remove leading '--'.
                    string key = parts[0].Substring(2);

                    if (Options.ContainsKey(key))
                        throw new ArgumentException("duplicate option --" + key);

                    // Set value if it exists, otherwise set to "true". 
                    Options.Add(key, parts.Length == 2 ? parts[1] : "true");
                }
                else
                {
                    Arguments.Add(term);
                }
            }

            // Remove command name from arguments
            Name = Arguments[0];

            // Check for !! prefix (force local) - must check before ! prefix
            if (Name.StartsWith("!!"))
            {
                IsForceLocal = true;
                Name = Name.Substring(2);
            }

            Arguments.RemoveAt(0);
        }

        /// <summary>
        /// Splits an input string into terms while preserving quoted sections.
        /// Quoted sections are treated as a single term with the surrounding
        /// quotes stripped.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static List<string> SplitInputIntoTerms(string input)
        {
            var terms = new List<string>();
            var breaks = new[] { ' ', '"' };
            int i = 0;

            while (i < input.Length)
            {
                // Skip whitespace
                if (input[i] == ' ')
                {
                    i++;
                    continue;
                }

                // A term runs to the next space outside quotes. Quote characters
                // toggle grouping and are stripped. An unclosed quote runs to the
                // end of the input. We jump between spaces and quotes rather than
                // visiting every character, so large quoted blocks stay cheap.
                var sb = new StringBuilder();
                bool inQuotes = false;

                while (i < input.Length)
                {
                    int next = input.IndexOfAny(breaks, i);

                    if (next < 0)
                    {
                        sb.Append(input, i, input.Length - i);
                        i = input.Length;
                        break;
                    }

                    sb.Append(input, i, next - i);

                    if (input[next] == '"')
                    {
                        inQuotes = !inQuotes;
                        i = next + 1;
                    }
                    else if (inQuotes)
                    {
                        sb.Append(' ');
                        i = next + 1;
                    }
                    else
                    {
                        i = next;
                        break;
                    }
                }

                terms.Add(sb.ToString());
            }

            return terms;
        }
    }
}
