using IngameScript;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates lightweight module test doubles that stay aligned with BaseModule defaults.
    /// </summary>
    public static class ModuleFactory
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        /// <returns></returns>
        public static FakeModule Create(Mother mother)
        {
            return new FakeModule(mother);
        }
    }
}
