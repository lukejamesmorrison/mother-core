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
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The IModule interface is used to define all modules run by Mother. It ensures a high 
    /// level of interoperability and dependency control control between modules. 
    /// </summary>
    public interface IModule
    {
   
        /// <summary>
        /// Boot the module. This is called before the module is run for the first 
        /// time and is the ideal method to define dependencies on other modules.
        /// </summary>
        void Boot();

        /// <summary>
        /// Boot the module as a coroutine.
        /// </summary>
        /// <returns></returns>
        IEnumerator<double> BootCoroutine();

        /// <summary>
        /// Run the module every program cycle. If you don't need to run processes 
        /// during each cycle, consider scheduling an action with the Clock.
        /// </summary>
        void Run();

        /// <summary>
        /// Handle an event that is sent to the module, if the module is subscribed to it.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        void HandleEvent(IEvent e, object eventData);

        /// <summary>
        /// Get the name of the module.
        /// </summary>
        /// <returns></returns>
        string GetModuleName();


        /// <summary>
        /// Get the list of commands for this module.  This is used to to 
        /// register to commands with Mother during boot.
        /// </summary>
        /// <returns></returns>
        List<IModuleCommand> GetCommands();
    }
}
