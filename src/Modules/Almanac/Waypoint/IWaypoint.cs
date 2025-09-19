using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Interface for waypoints used by Mother.
    /// </summary>
    public interface IWaypoint
    {
        /// <summary>
        /// Get the vector of the waypoint in 3D.
        /// </summary>
        /// <returns></returns>
        Vector3D GetVector();

        /// <summary>
        /// Get the name of the waypoint.
        /// </summary>
        /// <returns></returns>
        string GetName();
    }
}
