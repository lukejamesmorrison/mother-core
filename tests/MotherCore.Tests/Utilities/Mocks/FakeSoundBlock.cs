using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake sound block for sound-module tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/SpaceEngineers.Game.ModAPI.Ingame.IMySoundBlock.html"/>
    /// </remarks>
    internal sealed class FakeSoundBlock : FakeFunctionalBlock, IMySoundBlock
    {
        readonly List<string> _sounds = new List<string>();

        public FakeSoundBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public string SelectedSound { get; set; }

        public bool IsSoundSelected => !string.IsNullOrEmpty(SelectedSound);

        public bool IsPlaying { get; private set; }

        public int PlayCount { get; private set; }

        public int StopCount { get; private set; }

        public float Volume { get; set; }

        public float Range { get; set; }

        public float LoopPeriod { get; set; }

        public float LoopPeriodLength { get; set; }

        public bool PublicSound { get; set; }

        public void Play()
        {
            PlayCount++;
            IsPlaying = true;
        }

        public void Stop()
        {
            StopCount++;
            IsPlaying = false;
        }

        public void SelectSound(string cueId)
        {
            SelectedSound = cueId;
        }

        public void GetSounds(List<string> sounds)
        {
            if (sounds == null)
                return;

            sounds.AddRange(_sounds);
        }
    }
}