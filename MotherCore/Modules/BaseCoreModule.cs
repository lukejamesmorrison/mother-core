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
    /// This class acts as a base for all Core Modules.  It exposes several capabilities 
    /// that are useful across multiple Core Modules, simplifying access.
    /// </summary>
    public abstract class BaseCoreModule : BaseModule, ICoreModule
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        //public Mother Mother;

        /// <summary>
        /// The list of commands for this module.
        /// </summary>
        //public List<IModuleCommand> Commands = new List<IModuleCommand>();

        public BaseCoreModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Run the module every program cycle.  If you wish to run processes at 
        /// a different frequency, schedule them within the Boot() method.
        /// This method should be overridden by an implementation.
        /// </summary>
        //public virtual void Run() { }

        /// <summary>
        /// Boot the module. Core Modules are booted before Extension Modules.
        /// This method should be overridden by an implementation.
        /// </summary>
        //public virtual void Boot() { }

        /// <summary>
        /// Handle events that are sent to the module.  You should subscribe 
        /// to events in the Boot() method so that they are handled here. 
        /// This method should be overridden by an implementation.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        //public virtual void HandleEvent(IEvent e, object eventData) { }

        /// <summary>
        /// Get the name of module.
        /// </summary>
        /// <returns></returns>
        //public virtual string GetModuleName()
        //{
        //    return $"{GetType()}";
        //}

        /// <summary>
        /// Register a command with this module.
        /// </summary>
        /// <param name="command"></param>
        //public virtual void RegisterCommand(IModuleCommand command)
        //{
        //    Commands.Add(command);
        //}

        //protected void RegisterBlockTypeForStateMonitoring<T>(
        //    Func<T, object> stateSelector,
        //    Action<IMyTerminalBlock, object> stateHandler
        //) where T : class, IMyTerminalBlock
        //{
        //    BlockCatalogue BlockCatalogue = Mother.GetModule<BlockCatalogue>();

        //    foreach (var block in BlockCatalogue.GetBlocks<T>())
        //    {
        //        // Store the state selector and handler for this block type
        //        _stateSelectors[block] = (b) => stateSelector(b as T);
        //        _stateHandlers[block] = stateHandler;

        //        // Register the block for state monitoring
        //        BlockCatalogue.RegisterBlockForStateMonitoring(block, this);
        //        PreviousStates[block.EntityId] = stateSelector(block);
        //    }
        //}



        /// <summary>
        /// Get the commands for this module.
        /// </summary>
        /// <returns></returns>
        //public List<IModuleCommand> GetCommands()
        //{
        //    return Commands;
        //}
    }
}
