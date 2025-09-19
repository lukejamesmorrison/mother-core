namespace IngameScript
{
    /// <summary>
    /// Interface for objects that can be serialized to a string.
    /// </summary>
    public interface ISerializable
    {
        /// <summary>
        /// Serialize the object to a string.
        /// </summary>
        /// <returns></returns>
        string Serialize();
    }
}
