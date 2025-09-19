namespace IngameScript
{
    /// <summary>
    /// Command to get a value from the local storage module.
    /// </summary>
    public class GetCommand : BaseModuleCommand
    {
        /// <summary>
        /// The LocalStorage core module.
        /// </summary>
        readonly LocalStorage Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "get";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public GetCommand(LocalStorage module)
        {
            Module = module;
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            if (command.Arguments.Count == 0)
                return CommandBus.Messages.NoArgumentsProvided;

            string key = command.Arguments[0];
            string value = Module.Get(key);

            return value;
        }
    }
}
