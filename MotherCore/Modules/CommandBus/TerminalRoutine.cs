using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// Represents a routine of terminal commands.
        /// 
        /// @[Target] [Routine[Commands...]]
        /// </summary>
        public class TerminalRoutine
        {
            /// <summary>
            /// The initial string provided to the routine.
            /// </summary>
            string RoutineString;

            /// <summary>
            /// The target name of the routine. Defaults to "self". Remote commands 
            /// will be sent to this target using the name of their grid.
            /// </summary>  
            public string Target = "self";

            /// <summary>
            /// The unpacked routine string with all custom commands 
            /// expanded to their base commands.
            /// </summary>
            public string UnpackedRoutineString = "";

            /// <summary>
            /// The commands that compose the routine.
            /// </summary>
            public List<TerminalCommand> Commands = new List<TerminalCommand>();

            /// <summary>
            /// The unpacked commands that compose the routine.
            /// </summary>
            List<TerminalCommand> UnpackedCommands = new List<TerminalCommand>();

            /// <summary>
            /// Constructor. We create from a routine string and immediately parse 
            /// for its underlying commands.
            /// </summary>
            /// <param name="routine"></param>
            public TerminalRoutine(string routine)
            {
                RoutineString = routine
                    //.Replace("\n", "")
                    .Trim();

                ParseRoutineString();
            }

            /// <summary>
            /// Sets the target of the routine based on the first term of the routine. 
            /// Players can target a grid remotely using the @ character, and may 
            /// use an @* to target all grids.
            /// </summary>
            /// <param name="command"></param>
            void SetTarget(string command)
            {
                // space separated terms
                string[] commandTerms = command.Split(' ');
                string firstTerm = commandTerms[0];

                // if we are targeting another grid
                if (firstTerm.StartsWith("@"))
                {
                    // get target without @ symbol
                    Target = firstTerm.Substring(1);

                    // remove target from routine string
                    RoutineString = command.Substring(("@" + firstTerm).Length);
                }
                
                // if we are targeting every grid
                if (firstTerm == "*")
                {
                    Target = firstTerm;

                    // remove target from routine string
                    RoutineString = command.Substring(1);
                }
            }

            /// <summary>
            /// Parses the routine string into individual commands.
            /// </summary>
            void ParseRoutineString()
            {
                SetTarget(RoutineString);

                List<string> commandStrings = SplitRoutineCommands(RoutineString);

                foreach (var commandString in commandStrings)
                {
                    if (!string.IsNullOrWhiteSpace(commandString))
                        Commands.Add(new TerminalCommand(commandString.Trim()));
                }
            }

            /// <summary>
            /// Fluent method that unpacks the routine into its base command set against 
            /// a dictionary of custom commands defined in the programmable block's 
            /// custom data. Useful for sending remote commands.
            /// </summary>
            /// <param name="lookup"></param>
            /// <returns></returns>
            public TerminalRoutine Unpack(Dictionary<string,string> lookup)
            {
                foreach (TerminalCommand command in Commands)
                {
                    // Unpack each command separately
                    string unpackedCommandString = UnpackCommand(command, lookup);
                    UnpackedCommands.Add(new TerminalCommand(unpackedCommandString));
                }

                foreach (TerminalCommand command in UnpackedCommands)
                {
                    UnpackedRoutineString += command.CommandString + ";";
                }

                return this;
            }

            /// <summary>
            /// Unpacks a single command string into its base command set against a dictionary 
            /// of custom commands. Useful for sending remote commands.
            /// </summary>
            /// <param name="command"></param>
            /// <param name="lookup"></param>
            /// <returns></returns>
            static string UnpackCommand(TerminalCommand command, Dictionary<string, string> lookup)
            {
                string commandString = command.CommandString;

                // Expand all defined routines efficiently
                Dictionary<string, string> expandedValues = PreExpandNamedRoutines(lookup);

                // Perform optimized multi-pass replacement
                commandString = ReplaceNamedRoutinesSafely(commandString, expandedValues);

                // Remove double semicolons and trim unnecessary semicolons
                commandString = CleanupSemicolons(commandString);

                return commandString;
            }

            /// <summary>
            /// Splits a routine into individual Commands, but ignores semi-colons 
            /// inside quoted strings.
            /// </summary>
            /// <param name="routine"></param>
            /// <returns></returns>
            List<string> SplitRoutineCommands(string routine)
            {
                bool insideQuotes = false;
                List<string> commands = new List<string>();
                StringBuilder currentCommand = new StringBuilder();

                foreach (char c in routine)
                {
                    if (c == '"')
                        insideQuotes = !insideQuotes;

                    // If outside quotes, treat as a separator
                    if (c == ';' && !insideQuotes)
                    {
                        commands.Add($"{currentCommand}".Trim());
                        currentCommand.Clear();
                    }
                    else
                    {
                        currentCommand.Append(c);
                    }
                }

                // Add last command if not empty
                if (currentCommand.Length > 0)
                    commands.Add($"{currentCommand}".Trim());

                return commands;
            }

            /// <summary>
            /// Pre-expands all named routines in a lookup dictionary. We use this to lookup 
            /// a custom command within a routine, against the commands registered with 
            /// Mother. This lookup includes all commands registered via modules.
            /// </summary>
            /// <param name="lookup"></param>
            /// <returns></returns>
            static Dictionary<string, string> PreExpandNamedRoutines(Dictionary<string, string> lookup)
            {
                Dictionary<string, string> expandedValues = new Dictionary<string, string>();

                // Sort keys longest first to prevent premature replacements
                var sortedKeys = lookup.Keys.OrderByDescending(k => k.Length).ToList();

                sortedKeys.ForEach(key =>
                {
                    // Expand each entry fully before storing it
                    expandedValues[key] = ExpandValueSafely(lookup[key], expandedValues);
                });

                return expandedValues;
            }

            /// <summary>
            /// Expands a value fully before storing it (ensuring deep replacements).
            /// </summary>
            /// <param name="value"></param>
            /// <param name="expandedValues"></param>
            /// <returns></returns>
            static string ExpandValueSafely(string value, Dictionary<string, string> expandedValues)
            {
                StringBuilder sb = new StringBuilder(value);
                bool replaced;

                do
                {
                    replaced = false;

                    foreach (var entry in expandedValues)
                    {
                        string key = entry.Key;
                        string expandedValue = entry.Value;

                        if (ContainsWholeWord(sb.ToString(), key))
                        {
                            sb.Replace(key, expandedValue);
                            replaced = true;
                        }
                    }

                } while (replaced); // Continue until no more replacements happen

                return $"{sb}";
            }

            /// <summary>
            /// Replaces named routines safely in a command string.
            /// </summary>
            /// <param name="command"></param>
            /// <param name="expandedValues"></param>
            /// <returns></returns>
            static string ReplaceNamedRoutinesSafely(string command, Dictionary<string, string> expandedValues)
            {
                StringBuilder sb = new StringBuilder(command);

                foreach (var entry in expandedValues)
                {
                    if (ContainsWholeWord($"{sb}", entry.Key))
                        sb.Replace(entry.Key, entry.Value);
                }

                return $"{sb}";
            }

            /// <summary>
            /// Checks if a string contains a whole word. We ensure that whole words are replace and not sub parts.
            /// ie. "20p" should not be replaced by "0p".
            /// </summary>
            /// <param name="text"></param>
            /// <param name="word"></param>
            /// <returns></returns>
            static bool ContainsWholeWord(string text, string word)
            {
                return System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{word}\b");
            }

            /// <summary>
            /// Cleans up consecutive semicolons (;;) and trims leading/trailing semicolons.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            static string CleanupSemicolons(string command)
            {
                while (command.Contains(";;"))
                    command = command.Replace(";;", ";");

                return command.Trim(';');
            }
        }
    }
}
