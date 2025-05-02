namespace IngameScript
{
	partial class Program
	{
        /// <summary>
        /// Command to clear the terminal window.
        /// </summary>
        public class ClearCommand : BaseModuleCommand
        {
            /// <summary>
            /// The Terminal core module.
            /// </summary>
            readonly Terminal Module;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public override string Name => "clear";

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="module"></param>
            public ClearCommand(Terminal module)
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
                Module.ClearConsole();
                return "";
            }
        }
	}
}
