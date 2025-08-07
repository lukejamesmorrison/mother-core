namespace IngameScript
{
    /// <summary>
    /// The IEvent interface is a marker interface for system events that can be 
    /// emitted and subscribed to by modules. We keep events "stupid" to 
    /// simplify their transmission across module.  Data can be send 
    /// with an event as event data via the EventBus's Emit method.
    /// </summary>
    public interface IEvent { }
}
