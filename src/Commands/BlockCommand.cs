using Sandbox.ModAPI.Ingame;
using System;

namespace IngameScript
{
    /// <summary>
    /// Command that applies an action to every block of type T matching the
    /// name given as the first argument.
    /// </summary>
    /// <typeparam name="T">The block type to target.</typeparam>
    public class BlockCommand<T> : BaseModuleCommand where T : class, IMyTerminalBlock
    {
        readonly BaseModule Module;
        readonly Action<T> Action;
        readonly string Message;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module">The module used to look up blocks.</param>
        /// <param name="name">The command name, ie. door/open.</param>
        /// <param name="action">Applied to each matching block.</param>
        /// <param name="message">Format string for the success response; {0} is the block name.</param>
        public BlockCommand(BaseModule module, string name, Action<T> action, string message)
        {
            Module = module;
            Name = name;
            Action = action;
            Message = message;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            if (command.Arguments.Count == 0)
                return CommandBus.Messages.NoArgumentsProvided;

            string name = command.Arguments[0];
            var blocks = Module.GetBlocksByName<T>(name);

            if (blocks.Count == 0)
                return MessageFormatter.Format(BlockMessages.BlockNotFound, name);

            blocks.ForEach(Action);

            return MessageFormatter.Format(Message, name);
        }
    }
}
