using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Policy;
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
using VRageRender;
using RectangleF = VRageMath.RectangleF;

namespace IngameScript
{
    /// <summary>
    /// The Almanac manages GPS waypoints and player grids 
    /// to enable navigation and communication.
    /// </summary>
    public class Almanac : BaseCoreModule
    {
        /// <summary>
        /// The list of records in the Almanac.
        /// </summary>
        public List<AlmanacRecord> Records = new List<AlmanacRecord>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public Almanac(Mother mother) : base(mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Boot the module. We load records from storage and update this grid's 
        /// record. We update our current position every second.
        /// </summary>
        public override void Boot()
        {
            LoadFromLocalStorage();

            Mother.GetModule<Clock>()
                .Schedule(UpdateCurrentPosition, 1);
        }

        /// <summary>
        /// Update this grid's current position in the Almanac.
        /// </summary>
        public void UpdateCurrentPosition()
        {
            AlmanacRecord record = GetRecord($"{Mother.Id}");
            Vector3D Position = Mother.CubeGrid.GetPosition();

            if (record == null)
            {
                // CreateShape a new record if it does not exist
                record = new AlmanacRecord(
                    $"{Mother.Id}",
                    "grid",
                    Position,
                    Mother.CubeGrid.Speed
                )
                {
                    SafeRadius = Mother.SafeZone.Radius
                };
            }

            else
            {
                // Update the existing record
                record.Position = Position;
                record.Speed = Mother.CubeGrid.Speed;
                record.SafeRadius = Mother.SafeZone.Radius;
            }

            record.UpdatedAt = DateTime.Now;
            record.AddNickname(Mother.Name);

            AddRecord(record);
        }

        /// <summary>
        /// Load records from local storage. This is called on boot and uses the 
        /// Programmable Blocks storage property to save data across recompiles.
        /// </summary>
        void LoadFromLocalStorage()
        {
            string serializedAlmanac = Mother.GetModule<LocalStorage>().Get("almanac") ?? "";

            if (serializedAlmanac != "")
            {
                Dictionary<string, object> recordDict = Serializer.DeserializeDictionary(serializedAlmanac);

                foreach (var record in recordDict)
                {
                    Dictionary<string, object> recordData = (Dictionary<string, object>) record.Value;
                    AddRecord(AlmanacRecord.CreateFromDict(recordData));
                }
            }
        }

        /// <summary>
        /// GetSaveData the Almanac to local storage. This uses the Programmable Block's 
        /// Storage property to save the almanac records across recompiles.
        /// </summary>
        void Save()
        {
            var recordDict = Records.ToDictionary(record => record.Id, record => (object) record);

            Mother.GetModule<LocalStorage>().Set("almanac", Serializer.SerializeDictionary(recordDict));
        }

        /// <summary>
        /// Clear the Almanac records. This is used to 'reset' the Almanac.
        /// </summary>
        public void Clear()
        {
            Records.Clear();
            Save();
        }

        /// <summary>
        /// Get a record from the Almanac by its identifier. You may us either a string 
        /// of the EntityId, or a nickname.  By default, grid names are used as 
        /// the first nickname. The grid name can be found in the Info tab.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public AlmanacRecord GetRecord(string identifier)
        {
            // identifier matches Id, or nickname if the record is a GridTerminalSystem
            return Records.Find(record =>
                record.Id == identifier
                || (record.Nicknames.Contains(identifier))
            );
        }

        /// <summary>
        /// Get a list of records by type.
        /// Types: grid, waypoint
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public List<AlmanacRecord> GetRecordsByType(string type)
        {
            return Records.FindAll(record => record.EntityType == type);
        }

        /// <summary>
        /// Add a record to the Almanac. If the record already exists, we 
        /// override with newer data.
        /// </summary>
        /// <param name="record"></param>
        public void AddRecord(AlmanacRecord record)
        {
            // check if the record already exists. if exists, check if new record UpdatedAt is more recent. If true, overwrite the record, else do nothing
            AlmanacRecord existingRecord = Records.Find(r => r.Id == record.Id);

            if (existingRecord == null)
                Records.Add(record);

            else
            {
                if (record.UpdatedAt > existingRecord.UpdatedAt)
                {
                    // remove old instance
                    Records.Remove(existingRecord);
                    // add new instance
                    Records.Add(record);
                }
            }

            Save();
        }
    }
}
