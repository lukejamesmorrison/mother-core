using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// The MechanicalBlockModule is responsible for monitoring the attached/detached state
    /// of IMyMechanicalConnectionBlock blocks (rotors, hinges, and pistons). When the state 
    /// changes, it emits events and triggers a construct refresh in BlockCatalogue.
    /// </summary>
    public class MechanicalBlockModule : BaseCoreModule
    {
        /// <summary>
        /// The BlockCatalogue core module.
        /// </summary>
        BlockCatalogue BlockCatalogue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public MechanicalBlockModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Boot the module. We reference modules and register mechanical blocks for state monitoring.
        /// </summary>
        public override void Boot()
        {
            // Modules
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();

            Subscribe<ConstructRefreshedEvent>();

            // State Monitoring - Monitor IsAttached state for all mechanical blocks
            RegisterMechanicalBlocksForStateMonitoring();
        }

        /// <summary>
        /// Re-register mechanical blocks discovered after a construct refresh so
        /// newly attached grids participate in ongoing state monitoring.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (e is ConstructRefreshedEvent)
                RegisterMechanicalBlocksForStateMonitoring(true);
        }

        /// <summary>
        /// Registers all currently discovered mechanical blocks for attachment-state monitoring.
        /// </summary>
        /// <param name="preserveState">
        /// When <c>true</c>, existing state history for already known blocks is kept so
        /// construct refreshes can add newly discovered mechanical blocks without losing
        /// in-flight transition history.
        /// </param>
        void RegisterMechanicalBlocksForStateMonitoring(bool preserveState = false)
        {
            Block<IMyMechanicalConnectionBlock>()
                .Monitor(mechanicalBlock => mechanicalBlock.IsAttached, preserveState)

                // A grid was added to the construct - crawl from the newly attached grid
                .On<MechanicalBlockAttachedEvent>(true, "onAttach", block => BlockCatalogue.OnMechanicalBlockAttached(block.TopGrid), previous => !true.Equals(previous))

                // A grid was removed from the construct - prune disconnected grids
                .On<MechanicalBlockDetachedEvent>(false, "onDetach", block => BlockCatalogue.OnMechanicalBlockDetached(), previous => true.Equals(previous));
        }
    }
}
