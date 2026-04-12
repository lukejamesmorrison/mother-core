namespace IngameScript
{
    /// <summary>
    /// This event is emitted when the programmable block's system configuration (Custom Data) changes.
    /// Modules that depend on system configuration should subscribe to this event and reload
    /// their configuration values instead of requiring a full system reboot.
    /// </summary>
    public class SystemConfigChangedEvent : IEvent { }
}
