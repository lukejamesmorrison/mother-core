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
        //public Dictionary<string, object> Arguments2 = new Dictionary<string, object>();

        /// <summary>
        /// The options of the command. Options are key-value pairs that start with '--'.
        /// 
        /// ie. --speed=100, --delay=0.5, --force
        /// </summary>
        public Dictionary<string, string> Options = new Dictionary<string, string>();
        //public Dictionary<string, object> Options2 = new Dictionary<string, object>();

        /// <summary>
        /// The increment value for the command.
        /// </summary>
        public bool IsIncrement = false;

        /// <summary>
        /// The decrement value for the command.
        /// </summary>
        public bool IsDecrement = false;

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
        /// Gets an option value by key and converts it to the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        //public T GetOption2<T>(string name, T defaultValue = default(T))
        //{
        //    object value;
        //    if (Options2.TryGetValue(name, out value))
        //    {
        //        return (T)Convert.ChangeType(value, typeof(T));
        //    }
        //    return defaultValue;
        //}

        /// <summary>
        /// Gets an argument value by name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        //public T GetArgument<T>(string name)
        //{
        //    object value;
        //    if (Arguments2.TryGetValue(name, out value))
        //    {
        //        return (T)Convert.ChangeType(value, typeof(T));
        //    }
        //    throw new KeyNotFoundException("Argument '" + name + "' not found.");
        //}

        /// <summary>
        /// Gets an argument value by name as a string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        //public string GetArgument(string name)
        //{
        //    return GetArgument<string>(name);
        //}

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

                    // Set value if it exists, otherwise set to "true". 
                    if (parts.Length == 2)
                    {
                        Options.Add(key, parts[1]);
                        //Options2.Add(key, parts[1]);
                    }
                    else
                    {
                        Options.Add(key, "true");
                        //Options2.Add(key, true);
                    }
                    // Check for increment or decrement flags
                    //if (key == "sub")
                    //    IsDecrement = true;
                    //else if (key == "add")
                    //    IsIncrement = true;
                }
                else
                {
                    Arguments.Add(term);
                    //Arguments2.Add(term, term);
                }
            }

            // Remove command name from arguments
            Name = Arguments[0];
            //Name = Arguments2.First().Key;

            Arguments.RemoveAt(0);
            //Arguments2.Remove(Name);
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

                // If start of a quoted term
                if (currentChar == '"' && !insideQuotes)
                    insideQuotes = true;

                // Or end of a quoted term
                else if (currentChar == '"' && insideQuotes)
                {
                    insideQuotes = false;
                    terms.Add(currentTerm);
                    currentTerm = "";
                }

                // Or if not inside quotes, split by a space
                else if (currentChar == ' ' && !insideQuotes)
                {
                    if (!string.IsNullOrWhiteSpace(currentTerm))
                    {
                        terms.Add(currentTerm);
                        currentTerm = "";
                    }
                }

                // Otherwise continue building the current term
                else
                    currentTerm += currentChar;
            }

            // Add the last term if there is any
            if (!string.IsNullOrWhiteSpace(currentTerm))
                terms.Add(currentTerm);

            return terms;
        }
    }
}
