namespace IngameScript
{
    /// <summary>
    /// Interface for all module commands.
    /// </summary>
    public interface IModuleCommand
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        /// <returns></returns>
        string GetCommandName();

        /// <summary>
        /// Execute the command from a provided TerminalCommand.
        /// </summary>
        /// <param name="command"></param>
        /// <see cref="TerminalCommand"/>
        /// <returns></returns>
        string Execute(TerminalCommand command);
    }
}
