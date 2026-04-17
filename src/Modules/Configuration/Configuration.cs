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
        /// Constructor. We load the configuration of the programmable block running Mother.
        /// </summary>
        /// <param name="mother"></param>
        /// <exception cref="Exception"></exception>
        public Configuration(Mother mother) : base(mother) { }

        /// <summary>
        /// Load the custom data from the programmable block into the MyIni object.
        /// Sections with non-INI-compliant content (e.g. [menu:*]) are stripped
        /// before parsing so that MyIni does not choke on custom syntax.
        /// The raw CustomData is still available via Mother.ProgrammableBlock.CustomData.
        /// </summary>
        /// <exception cref="Exception"></exception>
        void LoadCustomData()
        {
            string sanitized = StripRawSections(Mother.ProgrammableBlock.CustomData);

            MyIniParseResult result;

            if (!Ini.TryParse(sanitized, out result))
                throw new Exception($"{result}");
        }

        /// <summary>
        /// Prefixes for INI sections that contain non-standard syntax and must
        /// be stripped before MyIni parsing. Other modules read these sections
        /// directly from the raw CustomData string.
        /// </summary>
        static readonly string[] RawSectionPrefixes = new string[] { "menu:" };

        /// <summary>
        /// Remove sections whose headers match any of the raw section prefixes.
        /// Everything from the matching [header] to the next [header] (or EOF)
        /// is excluded from the returned string.
        /// </summary>
        static string StripRawSections(string customData)
        {
            if (string.IsNullOrEmpty(customData)) return customData;

            var sb = new StringBuilder();
            string[] lines = customData.Split('\n');
            bool skipping = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("[") && trimmed.Contains("]"))
                {
                    string header = trimmed.Substring(1, trimmed.IndexOf(']') - 1).Trim();
                    skipping = false;

                    for (int p = 0; p < RawSectionPrefixes.Length; p++)
                    {
                        if (header.StartsWith(RawSectionPrefixes[p]))
                        {
                            skipping = true;
                            break;
                        }
                    }
                }

                if (!skipping)
                    sb.Append(lines[i]).Append('\n');
            }

            return sb.ToString();
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
                string variableValue = $"{Ini.Get(sectionName, variableName)}"
                    .Replace("\r", "")
                    .Trim();

                // Strip leading '$' from variable name if present
                if (variableName.StartsWith("$"))
                    variableName = variableName.Substring(1);

                // Strip surrounding double quotes from value
                variableValue = Unquote(variableValue);
                
                Mother.ConfigVariables[variableName] = variableValue;
            }
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

                // Collapse multiple spaces into a single space
                commandValue = System.Text.RegularExpressions.Regex.Replace(commandValue, @"\s+", " ");

                // Strip surrounding double quotes from command value
                commandValue = Unquote(commandValue);

                // Do NOT substitute variables here — defer to runtime so that
                // {{param:$VAR}} defaults and $VAR references are resolved after
                // command parameters have been applied.

                Mother.ConfigCommands[commandName] = commandValue;
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
