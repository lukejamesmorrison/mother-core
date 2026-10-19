using IngameScript;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Plain fake module for tests. It intentionally mirrors <see cref="BaseModule"/>
    /// defaults so tests can subscribe a lightweight module without introducing
    /// extra observer behavior.
    /// </summary>
    public class FakeModule : BaseModule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public FakeModule(Mother mother) : base(mother) { }
    }
}