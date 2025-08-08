namespace IngameScript
{
    /// <summary>
    /// Utility class for formatting messages.
    /// </summary>
    public static class MessageFormatter
    {
        /// <summary>
        /// Formats a message with the given arguments.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Format(string message, params object[] args)
        {
            return string.Format(message, args);
        }
    }
}
