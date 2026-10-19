namespace IngameScript
{
    /// <summary>
    /// Emitted by BlockCatalogue when a construct refresh completes (attach, detach, or full refresh).
    /// Modules that register blocks for state monitoring should handle this event to pick up
    /// any new blocks that were added to the construct during the refresh.
    /// </summary>
    public class ConstructRefreshedEvent : IEvent { }
}
