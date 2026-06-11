using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using MotherCore.Tests.Utilities.Factories;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight concrete programmable block for tests.
    /// Implements the members Mother currently depends on and supplies safe defaults
    /// for the remaining inherited in-game surface.
    /// </summary>
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyProgrammableBlock.html"/>
    internal class FakeProgrammableBlock : FakeTerminalBlock, IMyProgrammableBlock
    {
        readonly List<IMyTextSurface> _surfaces = new List<IMyTextSurface>();

        public FakeProgrammableBlock(
            string customData = "",
            string customName = "Mother Core PB",
            long? entityId = null,
            string gridName = "Test Grid",
            long? gridEntityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(
                customName,
                customData,
                entityId,
                cubeGrid ?? GridFactory.Create(gridName, gridEntityId ?? (entityId ?? CreateEntityId()) + 1))
        {
        }

        public bool IsRunning { get; set; }

        public string TerminalRunArgument { get; private set; }

        public int SurfaceCount
        {
            get { return _surfaces.Count; }
        }

        public bool UseGenericLcd { get; set; }

        public bool TryRun(string argument)
        {
            if (IsRunning)
                return false;

            TerminalRunArgument = argument;
            return true;
        }

        public IMyTextSurface GetSurface(int index)
        {
            if (index < 0 || index >= _surfaces.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _surfaces[index];
        }

        public FakeProgrammableBlock AddSurface(IMyTextSurface surface)
        {
            _surfaces.Add(surface);
            return this;
        }

        public FakeProgrammableBlock AddInventory(IMyInventory inventory)
        {
            AddInventoryInternal(inventory);
            return this;
        }

    }
}