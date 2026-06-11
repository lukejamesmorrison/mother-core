using Sandbox.ModAPI.Ingame;
using System;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal sealed class FakeGridProgramRuntimeInfo : IMyGridProgramRuntimeInfo
    {
        public long LifetimeTicks { get; set; }

        public TimeSpan TimeSinceLastRun { get; set; }

        public double LastRunTimeMs { get; set; }

        public int MaxInstructionCount { get; set; } = 50000;

        public int CurrentInstructionCount { get; set; }

        public int MaxCallChainDepth { get; set; } = 100;

        public int CurrentCallChainDepth { get; set; }

        public UpdateFrequency UpdateFrequency { get; set; }
    }
}