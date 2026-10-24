using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    /// <summary>
    /// Represents a routine of terminal commands.
    /// 
    /// @[Target] [Routine[Commands...]]
    /// 
    /// Routines may also contain parallel groups enclosed in curly braces:
    /// { cmd1; cmd2; } { cmd3; cmd4; }
    /// Each group will be executed in parallel as a separate coroutine.
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
        /// The commands that compose the routine. Used for sequential execution
        /// when no parallel groups are defined.
        /// </summary>
        public List<TerminalCommand> Commands = new List<TerminalCommand>();

        /// <summary>
        /// The parallel groups of commands. Each inner list represents a group 
        /// of commands that should be executed sequentially within the group,
        /// while all groups run in parallel with each other.
        /// </summary>
        public List<List<TerminalCommand>> ParallelGroups = new List<List<TerminalCommand>>();

        /// <summary>
        /// Whether this routine contains parallel groups.
        /// </summary>
        public bool HasParallelGroups => ParallelGroups.Count > 0;

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
            RoutineString = routine.Trim();

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

                // remove target from routine string (firstTerm includes the @ symbol)
                RoutineString = command.Substring(firstTerm.Length);
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
        /// Parses the routine string into individual commands, detecting 
        /// parallel groups enclosed in curly braces.
        /// </summary>
        void ParseRoutineString()
        {
            SetTarget(RoutineString);

            // Check if the routine contains parallel groups
            if (ContainsParallelGroups(RoutineString))
            {
                ParseParallelGroups(RoutineString);
            }
            else
            {
                // Standard sequential parsing
                List<string> commandStrings = SplitRoutineCommands(RoutineString);

                foreach (var commandString in commandStrings)
                {
                    if (!string.IsNullOrWhiteSpace(commandString))
                        Commands.Add(new TerminalCommand(commandString.Trim()));
                }
            }
        }

        /// <summary>
        /// Determines if the routine string contains parallel groups 
        /// denoted by curly braces. Returns true only when all 
        /// non-whitespace content is inside {} blocks, meaning 
        /// there is no meaningful content outside the braces.
        /// </summary>
        /// <param name="routine"></param>
        /// <returns></returns>
        static bool ContainsParallelGroups(string routine)
        {
            if (!routine.Contains("{") || !routine.Contains("}"))
                return false;

            // Check if there is any non-whitespace content outside {} blocks
            int braceDepth = 0;

            foreach (char c in routine)
            {
                if (c == '{')
                    braceDepth++;
                else if (c == '}')
                    braceDepth--;
                else if (braceDepth == 0 && !char.IsWhiteSpace(c))
                    return false; // Found content outside braces
            }

            return true;
        }

        /// <summary>
        /// Parses the routine string into parallel groups using a single-pass
        /// tree BFS approach. Scans the string once, tracking brace depth to
        /// identify group boundaries and semicolons to split commands within
        /// each group. Commands are built directly without intermediate string
        /// extraction.
        /// </summary>
        /// <param name="routine"></param>
        void ParseParallelGroups(string routine)
        {
            List<TerminalCommand> currentGroupCommands = null;
            StringBuilder currentCommand = new StringBuilder();
            bool insideQuotes = false;
            int braceDepth = 0;

            for (int i = 0; i < routine.Length; i++)
            {
                char c = routine[i];

                if (c == '"')
                    insideQuotes = !insideQuotes;

                if (!insideQuotes)
                {
                    if (c == '{')
                    {
                        braceDepth++;

                        if (braceDepth == 1)
                        {
                            // Entering a new top-level group
                            currentGroupCommands = new List<TerminalCommand>();
                            currentCommand.Clear();
                            continue;
                        }
                    }
                    else if (c == '}')
                    {
                        braceDepth--;

                        if (braceDepth == 0)
                        {
                            // Closing a top-level group - flush last command
                            string cmd = currentCommand.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(cmd))
                                currentGroupCommands.Add(new TerminalCommand(cmd));

                            if (currentGroupCommands.Count > 0)
                                ParallelGroups.Add(currentGroupCommands);

                            currentGroupCommands = null;
                            currentCommand.Clear();
                            continue;
                        }
                    }
                    else if (c == ';' && braceDepth == 1)
                    {
                        // Command separator inside a top-level group
                        string cmd = currentCommand.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(cmd))
                            currentGroupCommands.Add(new TerminalCommand(cmd));

                        currentCommand.Clear();
                        continue;
                    }
                }

                // Accumulate characters inside groups
                if (braceDepth > 0)
                    currentCommand.Append(c);
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
            // Expand the lookup once for all commands
            Dictionary<string, string> expandedValues = PreExpandNamedRoutines(lookup);

            foreach (TerminalCommand command in Commands)
            {
                // Unpack each command separately
                string unpackedCommandString = UnpackCommand(command, expandedValues);
                UnpackedCommands.Add(new TerminalCommand(unpackedCommandString));
            }

            var sb = new StringBuilder();
            foreach (TerminalCommand command in UnpackedCommands)
                sb.Append(command.CommandString).Append(';');
            UnpackedRoutineString = sb.ToString();

            return this;
        }

        /// <summary>
        /// Unpacks a single command string into its base command set against a dictionary 
        /// of custom commands. Useful for sending remote commands.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="expandedValues">Fully expanded routines.</param>
        /// <returns></returns>
        static string UnpackCommand(TerminalCommand command, Dictionary<string, string> expandedValues)
        {
            string commandString = command.CommandString;

            // Perform optimized multi-pass replacement
            commandString = ReplaceNamedRoutinesSafely(commandString, expandedValues);

            // Remove double semicolons and trim unnecessary semicolons
            commandString = CleanupSemicolons(commandString);

            return commandString;
        }

        /// <summary>
        /// Splits a routine into individual Commands, but ignores semi-colons 
        /// inside quoted strings and inside curly brace blocks.
        /// </summary>
        /// <param name="routine"></param>
        /// <returns></returns>
        List<string> SplitRoutineCommands(string routine)
        {
            bool insideQuotes = false;
            int braceDepth = 0;
            int start = 0;
            int i = 0;
            var markers = new[] { ';', '"', '{', '}' };
            List<string> commands = new List<string>();

            // Jump between markers rather than visiting every character, so large
            // quoted blocks stay cheap.
            while (true)
            {
                int next = routine.IndexOfAny(markers, i);

                if (next < 0)
                    break;

                char c = routine[next];
                i = next + 1;

                if (c == '"')
                    insideQuotes = !insideQuotes;
                else if (insideQuotes)
                    continue;
                else if (c == '{')
                    braceDepth++;
                else if (c == '}')
                    braceDepth--;
                else if (braceDepth == 0)
                {
                    // Outside quotes and braces, a semicolon is a separator
                    commands.Add(routine.Substring(start, next - start).Trim());
                    start = i;
                }
            }

            // Add last command if not empty
            if (start < routine.Length)
                commands.Add(routine.Substring(start).Trim());

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
            string result = value;
            bool replaced;
            int passes = 0;

            do
            {
                replaced = false;

                foreach (var entry in expandedValues)
                {
                    string replacedText = ReplaceWholeWord(result, entry.Key, entry.Value);

                    if (replacedText != result)
                    {
                        result = replacedText;
                        replaced = true;
                    }
                }

                // Cap passes so a self-referencing routine cannot loop forever
            } while (replaced && ++passes < MaxExpansionPasses);

            return result;
        }

        // Keys are visited longest first, so one pass resolves nearly everything. Extra
        // passes only matter for equal-length names that reference each other.
        const int MaxExpansionPasses = 3;

        /// <summary>
        /// Replaces every whole-word occurrence of a name with its expansion.
        /// </summary>
        static string ReplaceWholeWord(string text, string word, string replacement)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"(?<![\w$]){System.Text.RegularExpressions.Regex.Escape(word)}\b",
                m => replacement);
        }

        /// <summary>
        /// Replaces named routines safely in a command string.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="expandedValues"></param>
        /// <returns></returns>
        static string ReplaceNamedRoutinesSafely(string command, Dictionary<string, string> expandedValues)
        {
            foreach (var entry in expandedValues)
                command = ReplaceWholeWord(command, entry.Key, entry.Value);

            return command;
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
