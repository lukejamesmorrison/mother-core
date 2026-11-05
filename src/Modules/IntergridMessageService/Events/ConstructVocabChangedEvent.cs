namespace IngameScript
{
    /// <summary>
    /// An event that is fired when the condition names another script on the
    /// construct advertises change, so conditions that wait on them can be rebuilt.
    /// </summary>
    public class ConstructVocabChangedEvent : IEvent { }
}
