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
using VRageMath;
//using static IngameScript.Program;

namespace IngameScript
{

    /// <summary>
    /// The Request class is responsible for containing outgoing communication
    /// messages. It inherits from IntergridMessageObject and is related to 
    /// a Response returned from other grids.
    /// </summary>
    public class Request : IntergridMessageObject
    {
        /// <summary>
        /// The target grid's Id.
        /// </summary>
        public string TargetId;

        /// <summary>
        /// The target grid's name / nickname.
        /// </summary>
        public string TargetName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="header"></param>
        public Request(Dictionary<string, object> body, Dictionary<string, object> header) : base(body, header) { }

        /// <summary>
        /// Fluent method to set the target for the request.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public Request To(AlmanacRecord record)
        {
            Header["TargetId"] = $"{record.Id}";
            Header["TargetName"] = record.Nicknames[0];

            return this;
        }

        /// <summary>
        /// Serialize the Request object.
        /// </summary>
        /// <returns></returns>
        public override string Serialize()
        {
            return "REQUEST::" + base.Serialize();
        }

        /// <summary>
        /// De-serialize a Request object from a message string. We remove the message 
        /// type from the message string and then de-serialize the message parts.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Request Deserialize(string message)
        {
            message = message.Replace("REQUEST::", "");

            string headersPart = ExtractTagContent(message, "header");
            string bodyPart = ExtractTagContent(message, "body");

            return new Request(
                Serializer.DeserializeDictionary(bodyPart),
                Serializer.DeserializeDictionary(headersPart)
            );
        }
    }
}
