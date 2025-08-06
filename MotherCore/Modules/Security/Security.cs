//using Microsoft.Build.Framework;
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

namespace IngameScript
{
    /// <summary>
    /// The Security module provides encryption and decryption for Mother 
    /// communications.
    /// </summary>
    public class Security : BaseCoreModule
    {
        /// <summary>
        /// The identifier for encrypted messages. These characters 
        /// occur at the beginning of all encrypted messages to 
        /// let recipients know that the message is encrypted.
        /// </summary>
        static readonly string IDENTIFIER = "##";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public Security(Mother mother) : base(mother) { }


        /// <summary>
        /// Check if a message is encrypted.  This is done by checking if the message 
        /// starts with the encryption identifier.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool IsEncrypted(string message)
        {
            return message.StartsWith(IDENTIFIER);
        }

        /// <summary>
        /// Encrypt a string using a provided passcode.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="passcode"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string Encrypt(string input, string passcode = "")
        {
            if (passcode == "")
                return input;
            else
            {
                var encryptedChars = new char[input.Length];

                for (int i = 0; i < input.Length; i++)
                    encryptedChars[i] = (char)(input[i] ^ passcode[i % passcode.Length]);

                return IDENTIFIER + new string(encryptedChars);
            }
        }

        /// <summary>
        /// Decrypt a string using a provided passcode.
        /// </summary>
        /// <param name="encryptedInput"></param>
        /// <param name="passcode"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string Decrypt(string encryptedInput, string passcode)
        {
            // remove identifier from start of string
            encryptedInput = encryptedInput.Substring(IDENTIFIER.Length);

            var decryptedChars = new char[encryptedInput.Length];

            for (int i = 0; i < encryptedInput.Length; i++)
                decryptedChars[i] = (char)(encryptedInput[i] ^ passcode[i % passcode.Length]);
                
            return new string(decryptedChars);
        }
    }
}
