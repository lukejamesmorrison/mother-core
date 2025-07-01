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
//using static IngameScript.Program;

namespace IngameScript
{

    /// <summary>
    /// This class is responsible for checking the configuration version of the 
    /// programmable block via the Custom Data field. It can update the 
    /// configuration if necessary to prevent crashes when players 
    /// migrate to a newer config version.
    /// 
    /// Note - Version checking should eventually be deprecated to minimize the size of this class.
    /// </summary>
    public class VersionManager
    {
        MyIni Config;

        /// <summary>
        /// Constructor for the VersionManager class.
        /// </summary>
        /// <param name="config"></param>
        public VersionManager(MyIni config)
        {
            Config = config;
        }

        /// <summary>
        /// Runs the version manager to check and update the configuration versions.
        /// </summary>
        public MyIni Run()
        {
            Version0_2_14();

            return Config;
        }

        /// <summary>
        /// This version does the following:
        /// 1. Converts the [Commands] section name to [commands] to be consistent with other section names.
        /// 2. Update communications settings to allow multiple encrypted communications channels with IFF capability.
        /// </summary>
        void Version0_2_14()
        {
            // convert commands section name
            if (Config.ContainsSection("Commands"))
            {
                string iniString = Config.ToString();
                iniString = iniString.Replace("[Commands]", "[commands]");

                Config.TryParse(iniString);
            }

            // update comms channels
            if (!Config.ContainsSection("channels"))
            {
                //string useEnc = Config.Get("security", "passcodes").ToString();
                // set channels section with the public channel open
                //Config.Set("channels", "*", "");

                // get current passcodes
                string passcodes = Config.Get("security", "passcodes").ToString();

                if(passcodes != "")
                    Config.Set("channels", "default", passcodes);
                else
                    Config.AddSection("channels");

                // delete security section as it is now redundant
                if (Config.ContainsSection("security"))
                    Config.DeleteSection("security");
            }
        }
    }
}
