namespace IngameScript
{
    /// <summary>
    /// This class acts as a base for all Extension Modules. It exposes several capabilities 
    /// that are useful for Extension Modules to simplifying access. Developers will find 
    /// that most functionality of Mother can be accessed from within this class.
    /// </summary>
    public abstract class BaseExtensionModule : BaseModule, IExtensionModule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BaseExtensionModule(Mother mother) : base(mother) { }
    }
}
