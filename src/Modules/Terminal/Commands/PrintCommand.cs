namespace IngameScript
{
    /// <summary>
    /// Command to print a string to the terminal window.
    /// </summary>
    public class PrintCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Terminal core module.
        /// </summary>
        readonly Terminal Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "print";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public PrintCommand(Terminal module)
        {
            Module = module;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            return command.Arguments[0];
        }
    }
}
