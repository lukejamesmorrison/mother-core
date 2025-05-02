using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
//using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// An Almanac Record contains information about the particular entity. 
        /// Grids and GPS waypoints are the currently supported types. 
        /// Mother communicates with entities of type "grid".
        /// </summary>
        public class AlmanacRecord: ISerializable
        {
            /// <summary>
            /// Dictionary of all entity types. We must maintain a string representation as 
            /// this value is transmitted in request headers and should not be minified.
            /// </summary>
            public static readonly Dictionary<string, string> EntityTypes = new Dictionary<string, string>
            {
                { "grid", "grid" },
                { "waypoint", "waypoint" }
            };

            /// <summary>
            /// Not yet implemented.
            /// 
            /// The transponder codes for each entity. These are set 
            /// based up communication channel. 
            /// </summary>
            public enum TransponderStatus
            {
                Local,
                Friendly,
                Neutral,
                Hostile
            }

            /// <summary>
            /// The ID of the entity. This is a unique identifier for the entity. For 
            /// grids, we use CubeGrid.EntityId. For GPS Waypoints, we use the name.
            /// </summary>
            public string Id { get; }

            /// <summary>
            /// The time the record was last updated. This is used to determine 
            /// if the record should be updated with new data.
            /// </summary>
            public DateTime UpdatedAt;

            /// <summary>
            /// The last known position of the entity.
            /// </summary>
            public Vector3D Position;

            /// <summary>
            /// The last know speed of the entity.
            /// </summary>
            public float Speed;

            /// <summary>
            /// Is the entity local? This is true when an entity is of type "grid" 
            /// and is on the same construct as the current Programmable Block.
            /// </summary>
            public bool IsLocal = false;

            /// <summary>
            /// The safe radius of the entity. This is used t ensure we never 
            /// navigate within an unsafe distance of an entity.
            /// </summary>
            public double SafeRadius;

            /// <summary>
            /// The type of entity. Value in EntityTypes.
            /// </summary>
            public string EntityType { get; set; }

            /// <summary>
            /// The nicknames of the entity. These are used to identify the entity and easily 
            /// target it for communication.  A grid's name is automatically assigned as a 
            /// nickname, and GPS waypoint's name is automatically assigned as a nickname.
            /// </summary>
            public List<string> Nicknames = new List<string>();

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="entityId"></param>
            /// <param name="entityType"></param>
            /// <param name="position"></param>
            /// <param name="speed"></param>
            public AlmanacRecord(string entityId, string entityType, Vector3D position, float speed = 0)
            {
                Id = entityId;
                UpdatedAt = DateTime.Now;
                Position = position;
                Speed = speed;
                EntityType = entityType;
            }

            /// <summary>
            /// Add a nickname to the entity.
            /// </summary>
            /// <param name="nickname"></param>
            /// <returns></returns>
            public bool AddNickname(string nickname)
            {
                if (Nicknames.Contains(nickname))
                    return false;

                Nicknames.Add(nickname);
                return true;
            }

            /// <summary>
            /// Serialize the AlmanacRecord object to a string. This is used to 
            /// save the record, or transmit it via a message.
            /// </summary>
            /// <returns></returns>
            public string Serialize()
            {
                Dictionary<string, object> fieldDictionary = new Dictionary<string, object>
                {
                    { "Id", $"{Id}" },
                    { "UpdatedAt", $"{UpdatedAt.Ticks}" },
                    { "IsLocal", $"{IsLocal}" },
                    { "pos", $"{Position}" },
                    { "LastKnownSpeed", $"{Speed}" },
                    { "EntityType", EntityTypes[EntityType] },
                    { "Nicknames", string.Join(",", Nicknames) },
                    { "SafeRadius", $"{SafeRadius}" }
                };

                return Serializer.SerializeDictionary(fieldDictionary);
            }

            /// <summary>
            /// Create a new AlmanacRecord instance from a dictionary. This is 
            /// used when de-serializing a string representation of a record.
            /// </summary>
            /// <param name="dict"></param>
            /// <returns></returns>
            public static AlmanacRecord CreateFromDict(Dictionary<string, object> dict)
            {
                string[] vectorParts = $"{dict["pos"]}".Split(' ');

                // get value and exclude first 2 characters, and convert to double
                double x = double.Parse(vectorParts[0].Substring(2));
                double y = double.Parse(vectorParts[1].Substring(2));
                double z = double.Parse(vectorParts[2].Substring(2));
                string entityType = $"{dict["EntityType"]}";
                //bool isLocal = bool.Parse(dict["IsLocal"].ToString());

                AlmanacRecord almanacRecord = new AlmanacRecord(
                    $"{dict["Id"]}",
                    EntityTypes[entityType],
                    new Vector3D(x, y, z),
                    0
                );

                // add nicknames
                string[] nicknames = $"{dict["Nicknames"]}".Split(',');
                foreach (var nickname in nicknames)
                {
                    almanacRecord.AddNickname(nickname);
                }

                double safeRadius;
                object safeRadiusObj;

                if (dict.TryGetValue("SafeRadius", out safeRadiusObj) &&
                    safeRadiusObj != null &&
                    double.TryParse(safeRadiusObj.ToString(), out safeRadius))
                {
                    almanacRecord.SafeRadius = safeRadius;
                }

                //almanacRecord.IsLocal = isLocal;

                return almanacRecord;
            }

            /// <summary>
            /// Get the long type representation of the ID. This is use to get 
            /// the EntityId for records of type "grid".
            /// </summary>
            /// <returns></returns>
            public long GetLongId()
            {
                return long.Parse(Id);
            }
        }
    }
}
