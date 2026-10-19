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
            RegisterBlockTypeForStateMonitoring<IMyMechanicalConnectionBlock>(
                mechanicalBlock => mechanicalBlock.IsAttached,
                (block, state) => HandleMechanicalBlockStateChange(block as IMyMechanicalConnectionBlock, state),
                preserveState
            );
        }

        /// <summary>
        /// Handles the mechanical block state change event. This is called when
        /// the attached/detached state changes.
        /// </summary>
        /// <param name="mechanicalBlock"></param>
        /// <param name="newState"></param>
        protected void HandleMechanicalBlockStateChange(IMyMechanicalConnectionBlock mechanicalBlock, object newState)
        {
            var isAttached = newState as bool?;

            var previousState = PreviousStates.ContainsKey(mechanicalBlock.EntityId)
                ? PreviousStates[mechanicalBlock.EntityId] as bool?
                : null;

            // Attached - a grid was added to the construct
            if (isAttached == true && previousState != true)
            {
                Emit<MechanicalBlockAttachedEvent>(mechanicalBlock);
                BlockCatalogue.RunHook(mechanicalBlock, "onAttach");

                // Trigger optimized construct attach - crawl from the newly attached grid
                BlockCatalogue.OnMechanicalBlockAttached(mechanicalBlock.TopGrid);
            }

            // Detached - a grid was removed from the construct
            else if (isAttached == false && previousState == true)
            {
                Emit<MechanicalBlockDetachedEvent>(mechanicalBlock);
                BlockCatalogue.RunHook(mechanicalBlock, "onDetach");

                // Trigger optimized construct detach - prune disconnected grids
                BlockCatalogue.OnMechanicalBlockDetached();
            }
        }
    }
}
