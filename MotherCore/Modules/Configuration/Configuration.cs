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
            { "general.debug", "false" },
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
        public Configuration(Mother mother) : base(mother)
        {
            MyIniParseResult result;

            if (!Ini.TryParse(mother.ProgrammableBlock.CustomData, out result))
                throw new Exception($"{result}");

            mother.ProgrammableBlock.CustomData = new VersionManager(Ini).Run().ToString();
        }

        /// <summary>
        /// Boot the module. We set the default configuration for the programmable block 
        /// and register commands that are defined with the block's custom data.
        /// </summary>
        public override void Boot()
        {
            SetDefaultConfiguration();

            RegisterCommands();
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

            if (tokenParts.Count == 2)
            {
                string section = tokenParts[0];
                string key = tokenParts[1];

                return $"{Ini.Get(section, key)}";
            }
            else
            {
                return $"";
            }
        }

        /// <summary>
        /// Set a value from the configuration. The token is a string that is 
        /// period-separated for a section-key relationship.
        /// 
        /// ie. "general.debug" = true => [general] debug = true
        /// </summary>
        //public void Set(string token, string value)
        //{
        //    //Mother.ProgrammableBlock.CustomData = configuration;
        //}

        /// <summary>
        /// Register commands from the programmable block's custom data as configuration 
        /// commands. Commands are defined in the [Commands] section.
        /// </summary>
        void RegisterCommands()
        {
            List<MyIniKey> keys = new List<MyIniKey>();
            Ini.GetKeys("Commands", keys);

            foreach (var key in keys)
            {
                string commandName = key.Name;
                string commandValue = $"{Ini.Get("Commands", commandName)}"
                    //.Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();

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
