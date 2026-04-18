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
            // Set a pending boot flag rather than calling Boot() directly.
            // Calling Boot() from inside a coroutine shifts BootSequence to a
            // lower Coroutines index, causing it to execute in the same Clock.Run()
            // tick while the system is still in WORKING state (crash).
            Mother.PendingBoot = true;

            return "Rebooting...";
        }
    }
}
