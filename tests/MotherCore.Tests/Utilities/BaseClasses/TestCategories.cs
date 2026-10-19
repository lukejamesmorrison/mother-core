namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Shared NUnit category constants for layered and speed-oriented test tagging.
    /// </summary>
    public static class TestCategories
    {
        public const string LayerUnit = "Layer:Unit";
        public const string LayerModule = "Layer:Module";
        public const string LayerCommand = "Layer:Command";
        public const string LayerScript = "Layer:Script";
        public const string LayerWorld = "Layer:World";

        public const string SpeedFast = "Speed:Fast";
        public const string SpeedStandard = "Speed:Standard";
        public const string SpeedSlow = "Speed:Slow";
    }
}
