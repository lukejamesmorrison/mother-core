namespace IngameScript
{
    /// <summary>
    /// Utility class for formatting numbers.
    /// </summary>
    public static class NumberHelper
    {

        /// <summary>   
        /// Output dynamic number format from string.
        /// Supports: int, long, float, double, decimal.
        /// </summary>
        public static T Parse<T>(string value)
        {
            return (T)System.Convert.ChangeType(value, typeof(T));
        }
    }
}