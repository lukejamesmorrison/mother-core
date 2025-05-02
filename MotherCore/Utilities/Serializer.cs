using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
//using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
//using System.Runtime.Serialization;
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
    /// NESTED LISTS NOT WORKING!!!
    /// 
    /// This class is for serializing and de-serializing dictionaries 
    /// and lists. This is commonly used in intergrid communications as we send message 
    /// payloads as a string.
    /// </summary>
    public class Serializer
    {
        /// <summary>
        /// Serializes a dictionary to a string. The dictionary can contain nested dictionaries and lists.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static string SerializeDictionary(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            foreach (var kvp in dict)
            {
                sb.Append("\"").Append(Escape(kvp.Key)).Append("\":");

                // Check if value implements ISerializable
                var serializable = kvp.Value as ISerializable;
                if (serializable != null)
                {
                    sb.Append(serializable.Serialize());
                }
                else if (kvp.Value is Dictionary<string, object>)
                {
                    sb.Append(SerializeDictionary((Dictionary<string, object>)kvp.Value));
                }
                else if (kvp.Value is List<object>)
                {
                    sb.Append(SerializeList((List<object>)kvp.Value));
                }
                else if (kvp.Value is string)
                {
                    sb.Append("\"").Append(Escape((string)kvp.Value)).Append("\"");
                }
                else
                {
                    throw new InvalidOperationException("Unsupported value type: " + kvp.Value?.GetType().Name);
                }

                sb.Append(",");
            }
            if (sb.Length > 1) sb.Length--; // Remove trailing comma
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a string into a dictionary. The string should be in the format of a JSON object.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public static Dictionary<string, object> DeserializeDictionary(string section)
        {
            var keyValuePairs = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(section) || section[0] != '{' || section[section.Length - 1] != '}')
                return keyValuePairs; // Return empty if the format is invalid

            section = section.Substring(1, section.Length - 2); // Remove curly braces

            int length = section.Length;
            int i = 0;

            while (i < length)
            {
                // Find the start of the key
                int keyStart = section.IndexOf('"', i);
                if (keyStart == -1) break;

                int keyEnd = section.IndexOf('"', keyStart + 1);
                if (keyEnd == -1) break;

                string key = Unescape(section.Substring(keyStart + 1, keyEnd - keyStart - 1));

                // Find the start of the value
                int valueStart = section.IndexOf(':', keyEnd) + 1;
                if (valueStart == 0) break; // No ':' found after the key

                object value;
                if (section[valueStart] == '{')
                {
                    // Value is a nested dictionary
                    int valueEnd = FindClosingBrace(section, valueStart, '{', '}');
                    if (valueEnd == -1) break;

                    string nestedDictString = section.Substring(valueStart, valueEnd - valueStart + 1);
                    value = DeserializeDictionary(nestedDictString);
                    i = valueEnd + 1;
                }
                else if (section[valueStart] == '[')
                {
                    // Value is a list
                    int valueEnd = FindClosingBrace(section, valueStart, '[', ']');
                    if (valueEnd == -1) break;

                    string listString = section.Substring(valueStart, valueEnd - valueStart + 1);
                    value = DeserializeList(listString);
                    i = valueEnd + 1;
                }
                else if (section[valueStart] == '"')
                {
                    // Value is a string
                    int valueEnd = section.IndexOf('"', valueStart + 1);
                    while (valueEnd != -1 && section[valueEnd - 1] == '\\') // Handle escaped quotes
                    {
                        valueEnd = section.IndexOf('"', valueEnd + 1);
                    }
                    if (valueEnd == -1) break;

                    value = Unescape(section.Substring(valueStart + 1, valueEnd - valueStart - 1));
                    i = valueEnd + 1;
                }
                else
                {
                    // Value is not a string, find the next comma or end brace
                    int valueEnd = section.IndexOf(',', valueStart);
                    if (valueEnd == -1) valueEnd = section.Length;

                    value = section.Substring(valueStart, valueEnd - valueStart).Trim();
                    i = valueEnd;
                }

                keyValuePairs.Add(key, value);

                // Skip to the next key-value pair
                i = section.IndexOf(',', i) + 1;
                if (i == 0) break;
            }

            return keyValuePairs;
        }

        /// <summary>
        /// Serializes a list to a string. The list can contain nested lists and dictionaries.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static string SerializeList(IEnumerable<object> list)
        {
            var sb = new StringBuilder();
            sb.Append("[");

            foreach (var item in list)
            {
                if (item is List<object>) // Explicitly handle nested lists
                {
                    sb.Append(SerializeList((List<object>)item));
                }
                else if (item is Dictionary<string, object>)
                {
                    // Serialize dictionaries
                    sb.Append(SerializeDictionary((Dictionary<string, object>)item));
                }
                else if (item is string)
                {
                    // Serialize strings with escaping
                    sb.Append("\"").Append(Escape((string)item)).Append("\"");
                }
                else if (item is ISerializable)
                {
                    // Serialize objects implementing ISerializable
                    sb.Append(((ISerializable)item).Serialize());
                }
                else
                {
                    // Fallback for other objects
                    sb.Append("\"").Append(Escape(item != null ? item.ToString() : string.Empty)).Append("\"");
                }
                sb.Append(",");
            }

            if (sb.Length > 1) sb.Length--; // Remove trailing comma
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a string into a list. The string should be in the format of a JSON array.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public static List<object> DeserializeList(string section)
        {
            var list = new List<object>();

            if (string.IsNullOrEmpty(section) || section[0] != '[' || section[section.Length - 1] != ']')
                return list; // Return empty if the format is invalid

            section = section.Substring(1, section.Length - 2); // Remove square brackets

            int length = section.Length;
            int i = 0;

            while (i < length)
            {
                char currentChar = section[i];

                if (currentChar == '"')
                {
                    // Deserialize a string
                    int valueEnd = section.IndexOf('"', i + 1);
                    while (valueEnd != -1 && section[valueEnd - 1] == '\\') // Handle escaped quotes
                    {
                        valueEnd = section.IndexOf('"', valueEnd + 1);
                    }
                    if (valueEnd == -1) break;

                    list.Add(Unescape(section.Substring(i + 1, valueEnd - i - 1)));
                    i = valueEnd + 1;
                }
                else if (currentChar == '{')
                {
                    // Deserialize a dictionary
                    int valueEnd = FindClosingBrace(section, i, '{', '}');
                    if (valueEnd == -1) break;

                    string dictString = section.Substring(i, valueEnd - i + 1);
                    list.Add(DeserializeDictionary(dictString));
                    i = valueEnd + 1;
                }
                else if (currentChar == '[')
                {
                    // Deserialize a nested list
                    int valueEnd = FindClosingBrace(section, i, '[', ']');
                    if (valueEnd == -1) break;

                    string listString = section.Substring(i, valueEnd - i + 1);
                    list.Add(DeserializeList(listString));
                    i = valueEnd + 1;
                }
                else
                {
                    // Skip unexpected characters or whitespace
                    i++;
                }

                // Skip to the next value
                if (i < length && section[i] == ',') i++;
            }

            return list;
        }

        /// <summary>
        /// Finds the closing brace for a given opening brace in a string.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="start"></param>
        /// <param name="open"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        static int FindClosingBrace(string section, int start, char open, char close)
        {
            int depth = 0;
            for (int i = start; i < section.Length; i++)
            {
                if (section[i] == open) depth++;
                else if (section[i] == close) depth--;

                if (depth == 0) return i;
            }
            return -1; // No matching closing brace found
        }

        /// <summary>
        /// Escapes special characters in a string for JSON serialization.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static string Escape(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Unescapes special characters in a string from JSON serialization.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static string Unescape(string str)
        {
            return str.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        //void PrintSerializationDebug()
        //{
        //    // print a serialized list

        //    List<string> list = new List<string> { "one", "two", "three" };

        //    Dictionary<string, object> dict = new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } };

        //    // list of lists
        //    List<List<string>> listList = new List<List<string>> {
        //        new List<string> { "one", "two", "three" },
        //        new List<string> { "one", "two", "three" }
        //    };

        //    // list of dicts
        //    List<Dictionary<string, object>> listDict = new List<Dictionary<string, object>> {
        //        new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } },
        //        new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } }
        //    };

        //    // dict of dicts
        //    Dictionary<string, object> dictDict = new Dictionary<string, object> {
        //        { "key1", new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } } },
        //        { "key2", new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } } }
        //    };

        //    List<Dictionary<string, object>> dictList = new List<Dictionary<string, object>> {
        //        new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } },
        //        new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } }
        //    };


        //    var mixedList = new List<object>
        //    {
        //        new List<object>
        //        {
        //            "item1",
        //            "item2",
        //            new List<object> { "nested1", "nested2" }
        //        },
        //        new Dictionary<string, object> { { "key", "value" } },
        //        "stringItem"
        //    };

        //    var mixedDict = new Dictionary<string, object>
        //    {
        //        { "list", new List<object> { "item1", "item2", new List<object> { "nested1", "nested2" } } },
        //        { "dict", new Dictionary<string, object> { { "key", "value" } } },
        //        { "string", "stringItem" }
        //    };


        //    string testPhrase = "This is a test phraseeeeeee";
        //    string passphrase = "secret-asdasdsa";

        //    string encrypted = Security.Encrypt(testPhrase, passphrase);

        //    string decrypted = Security.Decrypt(encrypted, passphrase);


        //    // get list of lights on grid
        //    List<IMyTerminalBlock> lights = new List<IMyTerminalBlock>();
        //    Mother.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, block => block.CubeGrid == Mother.GridTerminalSystem);

        //    string lightString = "";
        //    foreach (IMyLightingBlock light in lights)
        //    {
        //        lightString += $"{light.CustomName}\n";
        //    }




        //    return
        //        //$"{lightString}\n" +
        //        //$"E: {encrypted}\n" +
        //        //$"D: {decrypted}" +
        //        "";


        //    //return Serializer.SerializeList(list);
        //    //return Serializer.SerializeList(dictList.Cast<object>());
        //    //return Serializer.SerializeList(Almanac.Records);
        //    //return Serializer.SerializeDictionary(dict);
        //    //return Serializer.SerializeDictionary(dictDict);

        //    //return Serializer.SerializeList(listList);
        //    //return Serializer.SerializeList(listDict);

        //    return "Nested Lists not working in Serializer:\n" + Serializer.SerializeList(mixedList);
        //    //return Serializer.SerializeDictionary(mixedDict);

        //    /*
        //    [
        //        "item1",
        //        "item2",
        //        "nested1",
        //        "nested2"
        //    ],
        //    {
        //        "key":"value"
        //    },
        //    "stringItem"
        //    */
        //}
    }
}
