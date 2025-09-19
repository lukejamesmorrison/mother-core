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
using VRage.Scripting;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The Log module provides a simple logging mechanism for Mother.
    /// </summary>
    public class Log : BaseCoreModule
    {
        /// <summary>
        /// The maximum number of records to keep in the log.
        /// </summary>
        const int MAX_RECORDS = 30;

        /// <summary>
        /// The list of log records.
        /// </summary>
        public List<string> Records { get; } = new List<string>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public Log(Mother mother) : base (mother) { }

        /// <summary>
        /// Log a record with the "Info" prefix.
        /// </summary>
        /// <param name="record"></param>
        public void Info(string record)
        {
            AddRecord(record, "Info");
        }

        /// <summary>
        /// Log a record with the "Error" prefix.
        /// </summary>
        /// <param name="record"></param>
        public void Error(string record)
        {
            AddRecord(record, "Error");
        }

        /// <summary>
        /// Log a record with an optional prefix. We limit the total number of records 
        /// to minimize the performance impact.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="prefix"></param>
        void AddRecord(string record, string prefix = "")
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string cleanPrefix = prefix != "" ? "." + prefix : "";

            if (Records.Count > MAX_RECORDS)
                Records.RemoveAt(Records.Count - 1);

            // add to beginning of list
            Records.Insert(0, $"{time}{cleanPrefix} {record}");
        }
    }
}
