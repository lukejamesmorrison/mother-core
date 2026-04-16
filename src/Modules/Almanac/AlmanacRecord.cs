using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        public enum TransponderCode
        {
            /// <summary>
            /// Construct entities are those that are on the same construct as this program.
            /// </summary>
            Construct,
            /// <summary>
            /// Friendly entities are those communicating on non-public channels.
            /// </summary>
            Friendly,
            /// <summary>
            /// Neutral entities are those communicating on a public channel.
            /// </summary>
            Neutral,
            /// <summary>
            /// Hostile entities are those deemed hostile. This code is current unused.
            /// </summary>
            Hostile
        }

        /// <summary>
        /// The ID of the entity. This is a unique identifier for the entity. For 
        /// grids, we use CubeGrid.EntityId. For GPS Waypoints, we use the name.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The unicast target ID for IGC communication. For grids, this is the 
        /// IGC.Me value of the relay instance. Required for SendUnicastMessage.
        /// </summary>
        public long UnicastId { get; set; }

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
        /// The last known forward direction of the entity.
        /// </summary>
        public Vector3D Forward = Vector3D.Forward;

        /// <summary>
        /// The last known up direction of the entity.
        /// </summary>
        public Vector3D Up = Vector3D.Up;

        /// <summary>
        /// The last know speed of the entity.
        /// </summary>
        public float Speed;

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
        /// The display name of the entity. This is the current name 
        /// used for display and lookup purposes.
        /// </summary>
        public string DisplayName { get; set; }

        ///<summary>
        /// The transponder code for the entity. This is used to 
        /// categorize the entity for communications and combat.
        /// </summary>
        public TransponderCode IFFCode { get; set; }

        /// <summary>
        /// The communication channels the entity is subscribed to.
        /// </summary>
        public HashSet<string> Channels = new HashSet<string>();

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
            IFFCode = TransponderCode.Neutral;
        }

        /// <summary>
        /// Update the record's mutable fields.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="speed"></param>
        /// <param name="displayName"></param>
        /// <param name="safeRadius"></param>
        public void Update(Vector3D position, float speed, string displayName = null, double safeRadius = 0, Vector3D? forward = null, Vector3D? up = null)
        {
            Position = position;
            Speed = speed;
            UpdatedAt = DateTime.Now;
            DisplayName = displayName ?? DisplayName;
            SafeRadius = safeRadius > 0 ? safeRadius : SafeRadius;
            Forward = forward ?? Forward;
            Up = up ?? Up;
        }

        /// <summary>
        /// Is the entity friendly?
        /// </summary>
        /// <returns></returns>
        public bool IsFriendly() => IFFCode == TransponderCode.Friendly;

        /// <summary>
        /// Is the entity hostile?
        /// </summary>
        /// <returns></returns>
        public bool IsHostile() => IFFCode == TransponderCode.Hostile;

        /// <summary>
        /// Is the entity neutral?
        /// </summary>
        /// <returns></returns>
        public bool IsNeutral() => IFFCode == TransponderCode.Neutral;

        /// <summary>
        /// Is the entity on the construct?
        /// </summary>
        /// <returns></returns>
        public bool IsOnConstruct() => IFFCode == TransponderCode.Construct;

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
                { "UnicastId", $"{UnicastId}" },
                { "DisplayName", $"{DisplayName}" },
                { "UpdatedAt", $"{UpdatedAt.Ticks}" },
                { "pos", $"{Position}" },
                { "LastKnownSpeed", $"{Speed}" },
                { "EntityType", EntityTypes[EntityType] },
                { "SafeRadius", $"{SafeRadius}" },
                { "fx", $"{Forward.X}" },
                { "fy", $"{Forward.Y}" },
                { "fz", $"{Forward.Z}" },
                { "ux", $"{Up.X}" },
                { "uy", $"{Up.Y}" },
                { "uz", $"{Up.Z}" }
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

            AlmanacRecord almanacRecord = new AlmanacRecord(
                $"{dict["Id"]}",
                EntityTypes[entityType],
                new Vector3D(x, y, z),
                0
            );

            // Load UnicastId if present (for backward compatibility)
            object unicastIdObj;
            long unicastId;
            if (dict.TryGetValue("UnicastId", out unicastIdObj) &&
                unicastIdObj != null &&
                long.TryParse(unicastIdObj.ToString(), out unicastId))
            {
                almanacRecord.UnicastId = unicastId;
            }

            // Load DisplayName if present (for backward compatibility)
            object displayNameObj;
            if (dict.TryGetValue("DisplayName", out displayNameObj) &&
                displayNameObj != null &&
                !string.IsNullOrEmpty(displayNameObj.ToString()))
            {
                almanacRecord.DisplayName = displayNameObj.ToString();
            }

            double safeRadius;
            object safeRadiusObj;

            if (dict.TryGetValue("SafeRadius", out safeRadiusObj) &&
                safeRadiusObj != null &&
                double.TryParse(safeRadiusObj.ToString(), out safeRadius))
            {
                almanacRecord.SafeRadius = safeRadius;
            }

            // Load orientation if present (for backward compatibility)
            double fx, fy, fz, ux, uy, uz;
            object fxObj, fyObj, fzObj, uxObj, uyObj, uzObj;
            if (dict.TryGetValue("fx", out fxObj) && double.TryParse($"{fxObj}", out fx) &&
                dict.TryGetValue("fy", out fyObj) && double.TryParse($"{fyObj}", out fy) &&
                dict.TryGetValue("fz", out fzObj) && double.TryParse($"{fzObj}", out fz))
            {
                almanacRecord.Forward = new Vector3D(fx, fy, fz);
            }
            if (dict.TryGetValue("ux", out uxObj) && double.TryParse($"{uxObj}", out ux) &&
                dict.TryGetValue("uy", out uyObj) && double.TryParse($"{uyObj}", out uy) &&
                dict.TryGetValue("uz", out uzObj) && double.TryParse($"{uzObj}", out uz))
            {
                almanacRecord.Up = new Vector3D(ux, uy, uz);
            }

            return almanacRecord;
        }

        /// <summary>
        /// Get the long type representation of the ID. This is use to get 
        /// the EntityId for records of type "grid".
        /// </summary>
        /// <returns></returns>
        public long GetLongId() => long.Parse(Id);
    }
}
