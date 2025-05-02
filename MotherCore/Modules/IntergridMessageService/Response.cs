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
//using static IngameScript.Program.Request;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// The Response class inherits from IntergridMessageObject and contains 
        /// data in response to a Request.
        /// </summary>
        public class Response : IntergridMessageObject
        {
            /// <summary>
            /// Response status codes that are included in every Response Header. These 
            /// function similar to an HTTP status code. 
            /// Many remain unused at this time.
            /// 
            /// <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status"/>
            /// </summary>
            public enum ResponseStatusCodes
            {
                // successful
                OK = 200,
                COMMAND_EXECUTED = 201,

                // client errors
                UNAUTHORIZED = 401,
                NOT_FOUND = 404,

                // system errors
                ERROR = 500,

                // docking
                DOCKING_APPROVED = 600,
                DOCKING_DENIED = 601,
                DOCKING_COMPLETE = 602,
                DOCKING_CANCELLED = 603,
                CONNECTOR_NOT_FOUND = 604,
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="body"></param>
            /// <param name="header"></param>
            public Response(Dictionary<string, object> body, Dictionary<string, object> header) : base(body, header) { }

            /// <summary>
            /// Serialize the Response object to a string.
            /// </summary>
            /// <returns></returns>
            public override string Serialize()
            {
                return "RESPONSE::" + base.Serialize();
            }

            /// <summary>
            /// De-serialize a string message to a Response object. We remove the message 
            /// type from the message string and then de-serialize the message parts.
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public static Response Deserialize(string message)
            {
                message = message.Replace("RESPONSE::", "");

                string headersPart = ExtractTagContent(message, "header");
                string bodyPart = ExtractTagContent(message, "body");

                return new Response(
                    Serializer.DeserializeDictionary(bodyPart),
                    Serializer.DeserializeDictionary(headersPart)
                );
            }

            /// <summary>
            /// Get the integer value of a ResponseStatusCodes enum. This allows us 
            /// to use the same response code across grids with minification.
            /// </summary>
            /// <param name="code"></param>
            /// <returns></returns>
            static public int GetResponseCodeValue(ResponseStatusCodes code)
            {
                return (int) code;
            }
        }
    }
}
