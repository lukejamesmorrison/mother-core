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
    partial class Program
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
            /// </summary>
            public List<string> Arguments = new List<string>();

            /// <summary>
            /// The options of the command. Options are key-value pairs that start with '--'.
            /// 
            /// ie. --speed=100, --delay=0.5, --force
            /// </summary>
            public Dictionary<string, string> Options = new Dictionary<string, string>();

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
            /// Parses the command string into its parts (arguments, options). Options 
            /// begin with '--' and other terms are considered Arguments. An Option 
            /// without a value is considered true.
            /// </summary>
            public void ParseCommand()
            {
                foreach (string term in SplitInputIntoTerms(CommandString))
                {
                    if (term.StartsWith("--"))
                    {
                        string[] parts = term.Split('=');

                        // Set key & remove leading '--'.
                        string key = parts[0].Substring(2);

                        // Set value. 
                        if (parts.Length == 2)
                            Options.Add(key, parts[1]);
                        else
                            Options.Add(key, "true");
                    }
                    else
                    {
                        Arguments.Add(term);
                    }
                }

                // Remove command name from arguments
                Name = Arguments[0];
                Arguments.RemoveAt(0);
            }

            /// <summary>
            /// Splits an input string into terms while preserving quoted sections.
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            List<string> SplitInputIntoTerms(string input)
            {
                List<string> terms = new List<string>();
                bool insideQuotes = false;
                string currentTerm = "";

                for (int i = 0; i < input.Length; i++)
                {
                    char currentChar = input[i];

                    if (currentChar == '"' && !insideQuotes)
                    {
                        // StartAutopilot of a quoted term
                        insideQuotes = true;
                    }
                    else if (currentChar == '"' && insideQuotes)
                    {
                        // End of a quoted term
                        insideQuotes = false;
                        terms.Add(currentTerm);
                        currentTerm = "";
                    }
                    else if (currentChar == ' ' && !insideQuotes)
                    {
                        // Split by space if not inside quotes
                        if (!string.IsNullOrWhiteSpace(currentTerm))
                        {
                            terms.Add(currentTerm);
                            currentTerm = "";
                        }
                    }
                    else
                    {
                        // Continue building the current term
                        currentTerm += currentChar;
                    }
                }

                // Add the last term if there is any
                if (!string.IsNullOrWhiteSpace(currentTerm))
                {
                    terms.Add(currentTerm);
                }

                return terms;
            }
        }
    }
}
