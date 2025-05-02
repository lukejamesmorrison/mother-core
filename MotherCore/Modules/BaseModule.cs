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
    partial class Program
    {
        /// <summary>
        /// This class acts as a base for all Core Modules.  It exposes several capabilities 
        /// that are useful across multiple Core Modules, simplifying access.
        /// </summary>
        public abstract class BaseModule : IModule, IBlockStateHandler
        {
            /// <summary>
            /// The Mother instance.
            /// </summary>
            public Mother Mother;

            /// <summary>
            /// The list of selectors used to target a block's state. We use selectors 
            /// as each block type has a different implementation of the state.
            /// </summary>
            private readonly Dictionary<IMyTerminalBlock, Func<IMyTerminalBlock, object>> _stateSelectors = new Dictionary<IMyTerminalBlock, Func<IMyTerminalBlock, object>>();

            /// <summary>
            /// The list of handlers used to handle a block's state. We use handlers as each 
            /// block type has a different implementation of the state and therefore 
            /// requires a different process for validating the state has changed.
            /// </summary>
            private readonly Dictionary<IMyTerminalBlock, Action<IMyTerminalBlock, object>> _stateHandlers = new Dictionary<IMyTerminalBlock, Action<IMyTerminalBlock, object>>();

            /// <summary>
            /// The list of previous states for each block. This is used to 
            /// determine if a block's state has changed.
            /// </summary>
            public readonly Dictionary<long, object> PreviousStates = new Dictionary<long, object>();

            /// <summary>
            /// The list of commands for this module.
            /// </summary>
            public MemorySafeList<IModuleCommand> Commands = new MemorySafeList<IModuleCommand>();

            public BaseModule(Mother mother)
            {
                Mother = mother;
            }

            /// <summary>
            /// Run the module every program cycle.  If you wish to run processes at 
            /// a different frequency, schedule them within the Boot() method.
            /// This method should be overridden by an implementation.
            /// </summary>
            public virtual void Run() { }

            /// <summary>
            /// Boot the module. Core Modules are booted before Extension Modules.
            /// This method should be overridden by an implementation.
            /// </summary>
            public virtual void Boot() { }

            /// <summary>
            /// Handle events that are sent to the module.  You should subscribe 
            /// to events in the Boot() method so that they are handled here. 
            /// This method should be overridden by an implementation.
            /// </summary>
            /// <param name="e"></param>
            /// <param name="eventData"></param>
            public virtual void HandleEvent(IEvent e, object eventData) { }

            /// <summary>
            /// Get the name of module.
            /// </summary>
            /// <returns></returns>
            public virtual string GetModuleName()
            {
                return $"{GetType()}";
            }

            /// <summary>
            /// Register a command with this module.
            /// </summary>
            /// <param name="command"></param>
            public virtual void RegisterCommand(IModuleCommand command)
            {
                Commands.Add(command);
            }

            /// <summary>
            /// Register a block type for ongoing state monitoring.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="stateSelector"></param>
            /// <param name="stateHandler"></param>
            protected void RegisterBlockTypeForStateMonitoring<T>(
                Func<T, object> stateSelector,
                Action<IMyTerminalBlock, object> stateHandler
            ) where T : class, IMyTerminalBlock
            {
                BlockCatalogue BlockCatalogue = Mother.GetModule<BlockCatalogue>();

                foreach (var block in BlockCatalogue.GetBlocks<T>())
                {
                    // Store the state selector and handler for this block type
                    _stateSelectors[block] = (b) => stateSelector(b as T);
                    _stateHandlers[block] = stateHandler;

                    // Register the block for state monitoring
                    BlockCatalogue.RegisterBlockForStateMonitoring(block, this);
                    PreviousStates[block.EntityId] = stateSelector(block);
                }
            }

            /// <summary>
            /// Get the current state of a block.
            /// </summary>
            /// <param name="block"></param>
            /// <returns></returns>
            public object GetBlockCurrentState(IMyTerminalBlock block)
            {
                return _stateSelectors.ContainsKey(block) ? _stateSelectors[block](block) : null;
            }

            /// <summary>
            /// Compare the current state of a block with a previous state.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="previousState"></param>
            /// <returns></returns>
            public bool HasBlockStateChanged(IMyTerminalBlock block, object previousState)
            {
                if (!_stateSelectors.ContainsKey(block)) return false;

                object currentState = _stateSelectors[block](block);

                return previousState == null || !Equals(previousState, currentState);
            }

            /// <summary>
            /// Handle a state change event and dispatch to handlers.
            /// </summary>
            /// <param name="block"></param>
            public void OnBlockStateChanged(IMyTerminalBlock block)
            {
                if (!_stateSelectors.ContainsKey(block)) return;

                long blockId = block.EntityId;
                object currentState = _stateSelectors[block](block);

                if (!PreviousStates.ContainsKey(blockId) || !Equals(PreviousStates[blockId], currentState))
                {
                    // Call the appropriate state handler for this block type
                    if (_stateHandlers.ContainsKey(block))
                        _stateHandlers[block](block, currentState);

                    // update the previous state after state change handled.
                    PreviousStates[blockId] = currentState;
                }
            }

            /// <summary>
            /// Get a block by its name. Accessor for BlockCatalogue.GetBlockByName.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="name"></param>
            /// <returns></returns>
            public List<T> GetBlocksByName<T>(string name) where T : class, IMyTerminalBlock
            {
                return Mother.GetModule<BlockCatalogue>().GetBlocksByName<T>(name);
            }

            /// <summary>
            /// Get a module registered with Mother.
            /// Accessor for Mother.GetModule.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public T GetModule<T>() where T : class, IModule
            {
                return Mother.GetModule<T>();
            }

            /// <summary>
            /// Subscribe to an event. Accessor for EventBus.Subscribe.
            /// </summary>
            /// <typeparam name="TEvent"></typeparam>
            public void Subscribe<TEvent>() where TEvent : IEvent
            {
                Mother.GetModule<EventBus>().Subscribe<TEvent>(this);
            }

            /// <summary>
            /// Emit an event. Accessor for EventBus.Emit.
            /// </summary>
            /// <param name="e"></param>
            /// <param name="eventData"></param>
            public void Emit(IEvent e, object eventData)
            {
                Mother.GetModule<EventBus>().Emit(e, eventData);
            }

            /// <summary>
            /// Emit an event of a specific type with optional event data.
            /// </summary>
            /// <typeparam name="TEvent"></typeparam>
            /// <param name="eventData"></param>
            public void Emit<TEvent>(object eventData = null) where TEvent : IEvent, new()
            {
                Emit(new TEvent(), eventData);
            }

            /// <summary>
            /// Get the commands for this module.
            /// </summary>
            /// <returns></returns>
            public MemorySafeList<IModuleCommand> GetCommands()
            {
                return Commands;
            }

            /// <summary>
            /// Add a route that other grids the may send requests to.
            /// Accessor for to the IntergridMessageService.Router.AddRoute().
            /// </summary>
            /// <param name="path"></param>
            /// <param name="route"></param>
            public void AddRoute(string path, Func<Request, Response> route)
            {
                GetModule<IntergridMessageService>().Router.AddRoute(path, route);
            }
        }
    }
}
