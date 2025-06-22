using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
//using System.Runtime.Remoting.Messaging;
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
    /// The IntergridMessageObject class is a generalized class for representing messages 
    /// send via the intergrid communication system.  Due to data format limitations, 
    /// we serialize the message to a string for transmission.
    /// </summary>
    public abstract class IntergridMessageObject
    {
        /// <summary>
        /// The header of the message object.
        /// </summary>
        public Dictionary<string, object> Header = new Dictionary<string, object>();

        /// <summary>
        /// The body of the message object.
        /// </summary>
        public Dictionary<string, object> Body = new Dictionary<string, object>();

        /// <summary>
        /// The unique identifier of the message object.
        /// </summary>
        public string Id { get; } = GenerateUniqueId();

        /// <summary>
        /// Constructor. When we are creating via de-serializing an incoming message, the 
        /// Id of the message object will be present in the header. If it is not 
        /// present, we are creating a new message and will generate a new Id.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="header"></param>
        public IntergridMessageObject(Dictionary<string, object> body, Dictionary<string, object> header)
        {
            Body = body;
            Header = header;

            if (!Header.ContainsKey("Id"))
                Header["Id"] = GenerateUniqueId();
        }

        /// <summary>
        /// Get the object value of a field in the Body of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object B(string key)
        {
            object value;
            return Body.TryGetValue(key, out value) ? value ?? "" : "";
        }

        /// <summary>
        /// Get the string value of a field in the Body of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string BString(string key)
        {
            return $"{B(key)}";
        }

        /// <summary>
        /// Get the float value of a field in the Body of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public float BFloat(string key)
        {
            return float.Parse(BString(key));
        }

        /// <summary>
        /// Get the double value of a field in the Body of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public double BDouble(string key)
        {
            return double.Parse(BString(key));
        }

        /// <summary>
        /// Get the value of a field in the Header of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object H(string key)
        {
            object value;
            return Header.TryGetValue(key, out value) ? value ?? "" : "";
        }

        /// <summary>
        /// Get the string value of a field in the Header of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string HString(string key)
        {
            return $"{H(key)}";
        }

        /// <summary>
        /// Get the float value of a field in the Header of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public float HFloat(string key)
        {
            return float.Parse(HString(key));
        }

        /// <summary>
        /// Get the double value of a field in the Header of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public double HDouble(string key)
        {
            double value;
            double.TryParse(HString(key), out value);

            return value;
        }

        /// <summary>
        /// Get the long value of a field in the Header of the message object.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long HLong(string key)
        {
            long value;
            long.TryParse(HString(key), out value);

            return value;
        }

        /// <summary>
        /// Generate a unique identifier for the message object. We use a random 
        /// number for simplicity.
        /// </summary>
        /// <returns></returns>
        static string GenerateUniqueId()
        {
            long timestamp = DateTime.UtcNow.Ticks;
            int counter = new Random().Next(0, 1000);

            return $"{timestamp}_{counter}";
        }

        /// <summary>
        /// Serialize the message object to a string for transmission to other grids.
        /// </summary>
        /// <returns></returns>
        public virtual string Serialize()
        {
            string headerTag = "header";
            string bodyTag = "body";
            string headersSerialized = Serializer.SerializeDictionary(Header);
            string bodySerialized = Serializer.SerializeDictionary(Body);

            return $"<{headerTag}>{headersSerialized}</{headerTag}>" +
                    $"<{bodyTag}>{bodySerialized}</{bodyTag}>";
        }

        /// <summary>
        /// Extract the content of a specific message tag from the message string. 
        /// A typical message has "header" and "body" tags.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string ExtractTagContent(string message, string tagName)
        {
            string startTag = $"<{tagName}>";
            string endTag = $"</{tagName}>";

            int startIndex = message.IndexOf(startTag) + startTag.Length;
            int endIndex = message.IndexOf(endTag);

            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                return "";

            return message.Substring(startIndex, endIndex - startIndex).Trim();
        }
    }
}
