using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// A minimal Program class for testing purposes.
    /// </summary>
    public partial class Program : MyGridProgram
    {
        /// <summary>
        /// The Mother instance is the core service of the Mother script.
        /// </summary>
        private readonly Mother mother;

        /// <summary>
        /// Program constructor. Creates a minimal Mother instance for testing.
        /// </summary>
        public Program()
        {
            // Create the Mother instance
            mother = new Mother(this)
            {
                SystemName = "MotherCore Test",
            };

            // Register Extension Modules (none for core tests)
            mother.RegisterModules(new List<IExtensionModule>());
        }

        /// <summary>
        /// Saves the program state.
        /// </summary>
        public void Save() => Storage = mother.Save();

        /// <summary>
        /// The main loop of the program.
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="updateType"></param>
        public void Main(string argument, UpdateType updateType) => mother.Run(argument, updateType);
    }
}
