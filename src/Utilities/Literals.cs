namespace IngameScript
{
    /// <summary>
    /// String literals that Core repeats. Each one is written once so the packaged
    /// script carries one copy and short references. Header keys and prefixes are part of
    /// the intergrid wire format. Do not change their values.
    /// </summary>
    public static class Literals
    {
        public const string OriginName = "OriginName",
            Id = "Id",
            OriginId = "OriginId",
            GridId = "GridId",
            TargetId = "TargetId",
            TargetName = "TargetName",
            SafeRadius = "SafeRadius",
            RespondingToId = "RespondingToId",
            Command = "Command",
            Commands = "Commands",
            Channels = "channels",
            Variables = "variables",
            Almanac = "almanac",
            Storage = "storage",
            Waypoint = "waypoint",
            Grid = "grid",
            Request = "REQUEST::",
            Response = "RESPONSE::",
            Self = "self",
            Wait = "wait",
            Rule = "------------------------------------------------------",
            Square = "SquareSimple",
            Circle = "Circle",
            Triangle = "Triangle",
            Mono = "Monospace",
            General = "general",
            CommandsSection = "commands",
            Hooks = "hooks",
            Rules = "rules";
    }
}
