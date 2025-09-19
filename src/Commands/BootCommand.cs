namespace IngameScript
{
    /// <summary>
    /// Command to purge module state data.
    /// </summary>
    public class BootCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        readonly Mother Mother;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "boot";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BootCommand(Mother mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            Mother.Boot();

            return "Rebooting...";
        }
    }
}
