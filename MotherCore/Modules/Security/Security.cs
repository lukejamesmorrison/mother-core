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
        /// The Mother instance.
        /// </summary>
        //readonly Mother Mother;

        /// <summary>
        /// The configuration core module.
        /// </summary>
        Configuration Configuration;

        /// <summary>
        /// Should encryption be used?
        /// </summary>
        public bool USE_ENCRYPTION;

        /// <summary>
        /// The passcodes used for encryption.
        /// </summary>
        string CONFIG_PASSKEYS;

        /// <summary>
        /// The identifier for encrypted messages. These characters 
        /// occur at the beginning of all encrypted messages to 
        /// let recipients know that the message is encrypted.
        /// </summary>
        static string IDENTIFIER = "##";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public Security(Mother mother) : base(mother)
        {
            //Mother = mother;
        }

        /// <summary>
        /// Boot the module.  We determine whether to use encryption from the 
        /// configuration, and load the passkeys.
        /// </summary>
        public override void Boot()
        {
            Configuration = Mother.GetModule<Configuration>();

            USE_ENCRYPTION = GetUseEncryptionFromConfig();
            CONFIG_PASSKEYS = Configuration.Get("security.passcodes");
        }

        /// <summary>
        /// Get whether encryption should be used from Mother's configuration.
        /// </summary>
        /// <returns></returns>
        bool GetUseEncryptionFromConfig()
        {
            return Configuration.Get("security.encrypt_messages") == "true";
        }

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
        public static string Encrypt(string input, string passcode)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(passcode))
                throw new ArgumentException("Input and passcode cannot be null or empty.");

            var encryptedChars = new char[input.Length];

            for (int i = 0; i < input.Length; i++)
                encryptedChars[i] = (char)(input[i] ^ passcode[i % passcode.Length]);

            return IDENTIFIER + new string(encryptedChars);
        }

        /// <summary>
        /// Encrypt a string using the passcodes from the configuration.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public string Encrypt(string message)
        {
            return Encrypt(message, CONFIG_PASSKEYS);
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
            if (string.IsNullOrEmpty(encryptedInput) || string.IsNullOrEmpty(passcode))
                throw new ArgumentException("Encrypted input and passcode cannot be null or empty.");

            // remove identifier from start of string
            encryptedInput = encryptedInput.Substring(IDENTIFIER.Length);

            var decryptedChars = new char[encryptedInput.Length];

            for (int i = 0; i < encryptedInput.Length; i++)
                decryptedChars[i] = (char)(encryptedInput[i] ^ passcode[i % passcode.Length]);
                
            return new string(decryptedChars);
        }

        /// <summary>
        /// Decrypt a string using the passcodes from the configuration.
        /// </summary>
        /// <param name="encryptedInput"></param>
        /// <returns></returns>
        public string Decrypt(string encryptedInput)
        {
            return Decrypt(encryptedInput, CONFIG_PASSKEYS);
        }
    }
}
