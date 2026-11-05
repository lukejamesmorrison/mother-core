using System;

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

        /// <summary>
        /// Make a named condition available in conditions, ie. 
        /// <c>RegisterPredicate("low", "charge &lt; 20%", "5%");</c>
        /// Modules that register the same name each add their own rows, 
        /// so the battery module adds the charge row and the tank module the 
        /// fill row of low. Players can replace it in the [predicates] section 
        /// of their Custom Data. Call this from Boot().
        /// </summary>
        /// <param name="name">Predicate name, ie. low.</param>
        /// <param name="rows">"property operator value" rows joined by |.</param>
        /// <param name="band">Default band, ie. 5%. Empty for the implicit band.</param>
        protected void RegisterPredicate(string name, string rows, string band = "") =>
            GetModule<ConditionModule>().RegisterPredicate(name, rows, band);
    }
}
