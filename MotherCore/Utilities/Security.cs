namespace IngameScript
{
    /// <summary>
    /// The Security utility provides encryption and decryption capabilities.
    /// </summary>
    public class Security
    {
        /// <summary>
        /// The identifier for encrypted messages. These characters 
        /// occur at the beginning of all encrypted messages to 
        /// let recipients know that the message is encrypted.
        /// </summary>
        static readonly string IDENTIFIER = "##";

        /// <summary>
        /// Check if a message is encrypted.  This is done by checking if the message 
        /// starts with the encryption identifier.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool IsEncrypted(string message) => message.StartsWith(IDENTIFIER);

        /// <summary>
        /// Encrypt a string using a provided passcode.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="passcode"></param>
        /// <returns></returns>
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
