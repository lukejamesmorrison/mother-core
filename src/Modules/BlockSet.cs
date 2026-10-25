using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// Registers commands, condition properties and state hooks for one block type 
    /// so the type is written once per group instead of once per registration.
    /// Create one from a module with <c>Block&lt;T&gt;(prefix)</c> and chain calls:
    /// 
    /// Block&lt;IMyPistonBase&gt;("piston")
    ///     .Command("stop", Stop, BlockMessages.BlockStopped)
    ///     .Number("position", block => block.CurrentPosition)
    ///     .Monitor(block => block.Status, OnStateChange);
    /// </summary>
    /// <typeparam name="T">The block type to target.</typeparam>
    public class BlockSet<T> where T : class, IMyTerminalBlock
    {
        readonly BaseModule Module;
        readonly string Prefix;
        readonly Dictionary<object, Action<T>> States = new Dictionary<object, Action<T>>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module">The module that owns the registrations.</param>
        /// <param name="prefix">Prepended to command names with a slash, ie. piston. Empty for none.</param>
        public BlockSet(BaseModule module, string prefix = "")
        {
            Module = module;
            Prefix = prefix;
        }

        /// <summary>
        /// Register a command that applies an action to all blocks matching the name 
        /// argument. Equivalent to <c>RegisterBlockCommand&lt;T&gt;(prefix/verb, ...)</c>.
        /// </summary>
        /// <param name="verb">The command name after the prefix, ie. open for door/open.</param>
        /// <param name="action">Applied to each matching block.</param>
        /// <param name="message">Format string for the success response; {0} is the block name.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Command(string verb, Action<T> action, string message)
        {
            Module.RegisterBlockCommand(string.IsNullOrEmpty(Prefix) ? verb : Prefix + "/" + verb, action, message);
            return this;
        }

        /// <summary>
        /// Make a numeric property of a block readable in conditions, ie. a battery'state 
        /// charge: <c>.Number("charge", block => block.CurrentStoredPower / block.MaxStoredPower)</c>.
        /// A group of blocks is compared by its average. Call this from Boot().
        /// </summary>
        /// <param name="key">Property name used in conditions.</param>
        /// <param name="read">Reads the property from a block.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Number(string key, Func<T, double> read)
        {
            Module.GetModule<ConditionModule>().RegisterProperty(key, read);
            return this;
        }

        /// <summary>
        /// Make a boolean property of a block readable in conditions, and optionally 
        /// name the predicates for each answer, ie. 
        /// <c>.Flag("detected", block => block.IsActive, "detected", "clear")</c>.
        /// A group of blocks is true when any block is. Call this from Boot().
        /// </summary>
        /// <param name="key">Property name used in conditions.</param>
        /// <param name="read">Reads the property from a block.</param>
        /// <param name="yes">Predicate name for true. Null for none.</param>
        /// <param name="no">Predicate name for false. Null for none.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Flag(string key, Func<T, bool> read, string yes = null, string no = null)
        {
            Module.GetModule<ConditionModule>().RegisterFlag(key, read, yes, no);
            return this;
        }

        /// <summary>
        /// Make an enum state of a block readable in conditions, ie. 
        /// <c>.Status("status", block => block.Status)</c>. The enum'state value names are the 
        /// words, and can be used as predicates (<c>"Door" open</c>). A group of 
        /// blocks matches when any block does. Call this from Boot().
        /// </summary>
        /// <typeparam name="E">The enum type.</typeparam>
        /// <param name="key">Property name used in conditions, ie. status.</param>
        /// <param name="read">Reads the state from a block.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Status<E>(string key, Func<T, E> read)
        {
            Module.GetModule<ConditionModule>().RegisterStatus(key, read);
            return this;
        }

        /// <summary>
        /// Monitor the state of every block of this type and call a handler when it changes. 
        /// Equivalent to <c>RegisterBlockTypeForStateMonitoring&lt;T&gt;</c>, except the 
        /// handler receives the block as T. Not to be confused with block hooks 
        /// (onOpen, onLock), which handlers typically trigger when the state changes.
        /// </summary>
        /// <param name="read">Reads the current state from a block.</param>
        /// <param name="handle">Called with the block and its new state when the state changes.</param>
        /// <param name="preserveState">Keep existing state history for known blocks, ie. after a construct refresh.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Monitor(Func<T, object> read, Action<T, object> handle, bool preserveState = false)
        {
            Module.RegisterBlockTypeForStateMonitoring(read, (block, state) => handle(block as T, state), preserveState);
            return this;
        }

        /// <summary>
        /// Monitor the state of every block of this type and, when it changes to a state 
        /// declared with <see cref="On{E}"/>, emit that event and run that hook. 
        /// Call this before the <c>.On</c> calls it dispatches to.
        /// </summary>
        /// <param name="read">Reads the current state from a block.</param>
        /// <param name="preserveState">Keep existing state history for known blocks, ie. after a construct refresh.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> Monitor(Func<T, object> read, bool preserveState = false)
        {
            return Monitor(read, Dispatch, preserveState);
        }

        /// <summary>
        /// Declare what happens when a monitored block enters a state: emit the event 
        /// with the block as its data, then run the block's hook. Blocks with the 
        /// state's hook configured in their Custom Data run it.
        /// 
        /// Block&lt;IMyAirVent&gt;("vent")
        ///     .Monitor(block => block.Status)
        ///     .On&lt;AirVentPressurizedEvent&gt;(VentStatus.Pressurized);
        /// </summary>
        /// <typeparam name="E">The event to emit.</typeparam>
        /// <param name="state">The state value, ie. VentStatus.Pressurized, or true.</param>
        /// <param name="hook">The hook to run. Defaults to "on" plus the state's name, ie. onPressurized.</param>
        /// <param name="also">Optional extra action run with the block after the hook.</param>
        /// <returns>This set, for chaining.</returns>
        public BlockSet<T> On<E>(object state, string hook = null, Action<T> also = null) where E : IEvent, new()
        {
            States[state] = block =>
            {
                Module.Emit<E>(block);
                Module.GetModule<BlockCatalogue>().RunHook(block, hook ?? "on" + state);
                also?.Invoke(block);
            };

            return this;
        }

        /// <summary>
        /// Run an action on a block based on its state.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="state"></param>
        void Dispatch(T block, object state)
        {
            Action<T> run;

            if (state != null && States.TryGetValue(state, out run))
                run(block);
        }
    }
}
