using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing.Printing;
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
    /// Configuration class for Mother. It leverages the built-in MyIni 
    /// class to manage structure and parsing.
    /// <see href="https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni"/>
    /// </summary>
    public class Configuration : BaseCoreModule
    {
        /// <summary>
        /// Default configuration values for Mother. We will insert these into the custom 
        /// data for the programmable block if it is completely empty on boot.
        /// </summary>
        readonly Dictionary<string, string> MotherConfigDefaults = new Dictionary<string, string>()
        {
            //{ "general.debug", "false" },
            //{ "channels.*", "" },
        };

        /// <summary>
        /// Default configuration values for blocks. We will insert these into blocks on 
        /// boot if custom data is complete empty. It would be nice to scope this by 
        /// block type in the future.
        /// </summary>
        public readonly Dictionary<string, string> BlockConfigDefaults = new Dictionary<string, string>()
        {
            // { "hooks.onOn", "" },
            // { "hooks.onOff", "" },
        };

        readonly string[] DefaultSections = new string[]
        {
            "general",
            "channels",
            "variables",
            "commands",
            "hooks",
            "rules",
        };

        /// <summary>
        /// MyIni object for managing configuration data.
        /// </summary>
        public readonly MyIni Ini = new MyIni();

        /// <summary>
        /// Raw MyIni object. This is used to access the raw configuration data.
        /// </summary>
        public MyIni Raw => Ini;

        /// <summary>
        /// The maximum depth to which variable values that reference other variables
        /// are expanded.
        /// </summary>
        public const int MaxVariableDepth = 5;

        /// <summary>
        /// Variable names grouped by first character, longest name first. Holds names only,
        /// so values are always read live from Mother.ConfigVariables.
        /// </summary>
        Dictionary<char, string[]> VariableIndex;

        /// <summary>
        /// The number of variables when VariableIndex was built. A differing count
        /// triggers a rebuild.
        /// </summary>
        int VariableIndexCount;

        /// <summary>
        /// Constructor. We load the configuration of the programmable block running Mother.
        /// </summary>
        /// <param name="mother"></param>
        /// <exception cref="Exception"></exception>
        public Configuration(Mother mother) : base(mother) { }

        /// <summary>
        /// Load the custom data from the programmable block into the MyIni object.
        /// </summary>
        /// <exception cref="Exception"></exception>
        void LoadCustomData()
        {
            MyIniParseResult result;

            if (!Ini.TryParse(Mother.ProgrammableBlock.CustomData, out result))
                throw new Exception($"{result}");
        }

        /// <summary>
        /// Boot the module. We set the default configuration for the programmable block 
        /// and register commands that are defined with the block's custom data.
        /// </summary>
        public override void Boot()
        {
            // Load and process configuration
            LoadConfiguration();

            // Run version manager to migrate old configurations (only on boot)
            Mother.ProgrammableBlock.CustomData = $"{new VersionManager(Ini).Run()}";

            // Subscribe to system config changed events
            Mother.GetModule<EventBus>()?.Subscribe<SystemConfigChangedEvent>(this);

            // Commands
            RegisterCommand("var/set", 2, c =>
            {
                string name = c.Arguments[0];
                string value = c.Arguments[1];
                bool save = c.Options.ContainsKey("save");

                SetVariable(name, value, save);

                return save
                    ? $"${name} = \"{value}\" (saved)"
                    : $"${name} = \"{value}\"";
            });
        }

        /// <summary>
        /// Reload the configuration from the programmable block's custom data.
        /// This is called when the system config changes during runtime, allowing
        /// modules to pick up new values without a full system reboot.
        /// </summary>
        public void Reload()
        {
            LoadConfiguration();
        }

        /// <summary>
        /// Load and process configuration from the programmable block's custom data.
        /// This shared method is used by both Boot() and Reload() to avoid duplication.
        /// </summary>
        void LoadConfiguration()
        {
            // Load custom data from programmable block
            LoadCustomData();

            // Ensure default configuration is set
            SetDefaultConfiguration();

            // Load variables from custom data
            LoadVariables();

            // Register commands from custom data
            RegisterCommands();

            // Update Mother debug mode
            Mother.DebugMode = Get("general.debug").ToLower() == "true";

            // Update Mother name from config if set, otherwise use grid name
            var configName = Get("general.name");
            //if (!string.IsNullOrEmpty(configName))
            //    Mother.Name = Unquote(configName);
            //else
            //    Mother.Name = Mother.ProgrammableBlock.CubeGrid.CustomName;

            Mother.Name = !string.IsNullOrEmpty(configName)
                ? Unquote(configName)
                : Mother.CubeGrid.CustomName;
        }

        /// <summary>
        /// Handle events. When the system config changes, we reload the configuration.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (e is SystemConfigChangedEvent)
                Reload();
        }

        /// <summary>
        /// Get a value from the configuration. The token is a string that is 
        /// period-separated for a section.name relationship.
        /// 
        /// ie. "general.debug" => [general] debug = true
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public string Get(string token)
        {
            // split token string by delimiter '.'
            List<string> tokenParts = new List<string>(token.Split('.'));

            if (tokenParts.Count != 2) return $"";

            else
            {
                string section = tokenParts[0];
                string key = tokenParts[1];

                return $"{Ini.Get(section, key)}";
            }
        }

        /// <summary>
        /// Strips surrounding double quotes from a string if present.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        string Unquote(string input)
        {
            if (input.Length >= 2 && input[0] == '"' && input[input.Length - 1] == '"')
                return input.Substring(1, input.Length - 2);

            return input;
        }

        /// <summary>
        /// Collapses runs of spaces and tabs into one space, outside quoted strings.
        /// Splitting on quotes makes even parts unquoted and odd parts quoted, so large
        /// quoted blocks (ie. screen art) are never scanned character by character,
        /// which would exhaust the instruction limit at boot.
        /// </summary>
        static string CollapseWhitespaceOutsideQuotes(string input)
        {
            string[] parts = input.Split('"');

            for (int i = 0; i < parts.Length; i += 2)
                parts[i] = System.Text.RegularExpressions.Regex.Replace(parts[i], "[ \t]+", " ");

            return string.Join("\"", parts);
        }

        /// <summary>
        /// Load variables from the programmable block's custom data. Variables are 
        /// defined in the [variables] section and can be referenced in commands 
        /// using the $VARIABLE_NAME syntax. Uppercase is recommended but not
        /// required.
        /// </summary>
        void LoadVariables()
        {
            Mother.ConfigVariables.Clear();
            var sectionName = "variables";

            List<MyIniKey> keys = new List<MyIniKey>();
            Ini.GetKeys(sectionName, keys);

            foreach (var key in keys)
            {
                string variableName = key.Name;
                string variableValue = $"{Ini.Get(sectionName, variableName)}";

                // Strip leading '$' from variable name if present
                if (variableName.StartsWith("$"))
                    variableName = variableName.Substring(1);

                // Strip surrounding double quotes from value
                variableValue = Unquote(variableValue);
                
                Mother.ConfigVariables[variableName] = variableValue;
            }

            VariableIndex = null;
        }

        /// <summary>
        /// Register commands from the programmable block's custom data as configuration 
        /// commands. Commands are defined in the [Commands] section. Command values are 
        /// stored as raw templates to support runtime parameter substitution using the
        /// {{param}} and {{param:default}} syntax.
        /// </summary>
        void RegisterCommands()
        {
            // Clear existing commands before reloading
            Mother.ConfigCommands.Clear();
            
            List<MyIniKey> keys = new List<MyIniKey>();
            Ini.GetKeys("Commands", keys);

            foreach (var key in keys)
            {
                string commandName = key.Name;
                string commandValue = $"{Ini.Get("Commands", commandName)}"
                    .Replace("\r", "")
                    .Replace("\n", " ")
                    .Trim();

                // Collapse multiple spaces into a single space, but only outside quoted strings.
                commandValue = CollapseWhitespaceOutsideQuotes(commandValue);

                // Strip surrounding double quotes from command value
                commandValue = Unquote(commandValue);

                // Do NOT substitute variables here — defer to runtime so that
                // {{param:$VAR}} defaults and $VAR references are resolved after
                // command parameters have been applied.

                Mother.ConfigCommands[commandName] = commandValue;
            }
        }

        /// <summary>
        /// Finds the longest variable name that matches the text at the given position.
        /// Matching is ordinal and case-sensitive. CompareOrdinal never matches a name
        /// that runs past the end of the text.
        /// </summary>
        /// <returns>The variable name, or null if none matches.</returns>
        string MatchVariableName(string text, int position)
        {
            string[] candidates;
            if (position >= text.Length || !VariableIndex.TryGetValue(text[position], out candidates))
                return null;

            foreach (var name in candidates)
                if (string.CompareOrdinal(text, position, name, 0, name.Length) == 0)
                    return name;

            return null;
        }

        /// <summary>
        /// Scans text and appends it to the builder with variables resolved. Values that
        /// reference other variables are scanned in turn, down to MaxVariableDepth.
        /// </summary>
        void AppendWithVariables(string text, StringBuilder output, int depth, string[] chain, ref string error)
        {
            int i = 0;

            while (i < text.Length)
            {
                int dollar = text.IndexOf('$', i);

                if (dollar < 0)
                {
                    output.Append(text, i, text.Length - i);
                    return;
                }

                output.Append(text, i, dollar - i);

                // $$ is an escaped literal $
                if (dollar + 1 < text.Length && text[dollar + 1] == '$')
                {
                    output.Append('$');
                    i = dollar + 2;
                    continue;
                }

                string name = MatchVariableName(text, dollar + 1);

                if (name == null)
                {
                    output.Append('$');
                    i = dollar + 1;
                    continue;
                }

                i = dollar + 1 + name.Length;
                string value = Mother.ConfigVariables[name];

                if (depth >= MaxVariableDepth && value.IndexOf('$') >= 0)
                {
                    output.Append('$').Append(name);
                    error = error ?? $"variable nesting deeper than {MaxVariableDepth}: {string.Join(" > ", chain)}";
                    continue;
                }

                chain[depth] = name;
                AppendWithVariables(value, output, depth + 1, chain, ref error);
            }
        }

        /// <summary>
        /// Substitute variables into a string, reporting whether nesting went too deep.
        /// Variables are referenced using $VARIABLE_NAME and replaced with their values
        /// from the [variables] section. At each $ the longest matching name wins, so a
        /// shorter name never partially matches a longer one, and text with no matching
        /// name is left unchanged. $$ yields a literal $. A value may reference other
        /// variables; these are resolved when used, to a depth of MaxVariableDepth. At the
        /// limit the $NAME text is left in place and error names the chain of variables.
        /// </summary>
        /// <param name="input">The text to resolve.</param>
        /// <param name="result">The resolved text. The same reference as input if it contains no $.</param>
        /// <param name="error">A description of the failure, or null.</param>
        /// <returns>True if no error occurred.</returns>
        public bool TrySubstituteVariables(string input, out string result, out string error)
        {
            result = input;
            error = null;

            if (string.IsNullOrEmpty(input) || input.IndexOf('$') < 0)
                return true;

            var variables = Mother.ConfigVariables;

            // Rebuilt when loaded variables are cleared or a name is added
            if (VariableIndex == null || VariableIndexCount != variables.Count)
            {
                VariableIndex = variables.Keys
                    .Where(k => k.Length > 0)
                    .GroupBy(k => k[0])
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.Length).ToArray());
                VariableIndexCount = variables.Count;
            }

            var output = new StringBuilder(input.Length + 16);
            var chain = new string[MaxVariableDepth + 1];

            AppendWithVariables(input, output, 0, chain, ref error);
            result = output.ToString();

            return error == null;
        }

        /// <summary>
        /// Sets a variable value in memory. If save is true, also persists it to
        /// the programmable block's custom data so the value survives a reboot.
        /// Because ConfigCommands stores raw templates and variables are substituted
        /// at runtime, updating ConfigVariables is sufficient to affect all
        /// subsequent command executions.
        /// </summary>
        /// <param name="name">Variable name (without the $ prefix).</param>
        /// <param name="value">New value for the variable.</param>
        /// <param name="save">When true, persists the change to CustomData.</param>
        public void SetVariable(string name, string value, bool save = false)
        {
            Mother.ConfigVariables[name] = value;

            if (save)
            {
                Ini.Set("variables", name, value);
                Mother.ProgrammableBlock.CustomData = $"{Ini}";
            }
        }

        /// <summary>
        /// Sets the default configuration for Mother into the programmable block's custom data. This 
        /// will be inserted into the programmable block's CustomData property if it does not 
        /// contain content during boot ensuring that CustomData is always compliant with 
        /// Mother's configuration requirements but does not overwrite player data.
        /// </summary>
        void SetDefaultConfiguration()
        {
            // set default sections
            foreach (string section in DefaultSections)
                if (!Ini.ContainsSection(section))
                    Ini.AddSection(section);

            // set default values
            foreach (KeyValuePair<string, string> defaultPair in MotherConfigDefaults)
            {
                string[] keyParts = defaultPair.Key.Split('.');
                string section = keyParts[0];
                string key = keyParts[1];
                string value = defaultPair.Value;

                // if key has value, don't change, otherwise create.
                if (Ini.Get(section, key).IsEmpty) 
                    Ini.Set(section, key, value);
            }

            Mother.ProgrammableBlock.CustomData = $"{Ini}";
        }
    }
}
