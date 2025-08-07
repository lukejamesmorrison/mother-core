namespace IngameScript
{
    /// <summary>
    /// This class acts as a base for all Core Modules.  It exposes several capabilities 
    /// that are useful across multiple Core Modules, simplifying access.
    /// </summary>
    public abstract class BaseCoreModule : BaseModule, ICoreModule
    {
        /// <summary>
        /// Constructor for the BaseCoreModule class.
        /// </summary>
        /// <param name="mother"></param>
        public BaseCoreModule(Mother mother) : base(mother) { }
    }
}
