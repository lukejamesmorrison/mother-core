namespace IngameScript
{
    /// <summary>
    /// Emitted by Mother once all modules have completed their <c>Boot()</c> calls
    /// and the system state has transitioned to <see cref="Mother.SystemStates.WORKING"/>.
    ///
    /// Modules that depend on other modules being fully initialised before they can
    /// perform their own setup (e.g. <see cref="ViewModule"/> waiting for all views
    /// to be registered) should subscribe to this event rather than relying on
    /// boot-order assumptions.
    /// </summary>
    public class SystemBootedEvent : IEvent { }
}
