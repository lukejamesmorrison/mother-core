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

            // State Monitoring - Monitor IsAttached state for all mechanical blocks
            RegisterBlockTypeForStateMonitoring<IMyMechanicalConnectionBlock>(
                mechanicalBlock => mechanicalBlock.IsAttached,
                (block, state) => HandleMechanicalBlockStateChange(block as IMyMechanicalConnectionBlock, state)
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
