using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake conveyor sorter for module command-path tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyConveyorSorter.html"/>
    /// </remarks>
    internal sealed class FakeConveyorSorter : FakeFunctionalBlock, IMyConveyorSorter
    {
        readonly List<MyInventoryItemFilter> _filters = new List<MyInventoryItemFilter>();

        public FakeConveyorSorter(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool DrainAll { get; set; }

        public MyConveyorSorterMode Mode { get; set; }

        public void GetFilterList(List<MyInventoryItemFilter> items)
        {
            if (items == null)
                return;

            items.AddRange(_filters);
        }

        public void AddItem(MyInventoryItemFilter item)
        {
            _filters.Add(item);
        }

        public void RemoveItem(MyInventoryItemFilter item)
        {
            _filters.Remove(item);
        }

        public bool IsAllowed(MyDefinitionId contentId)
        {
            return true;
        }

        public void SetFilter(MyConveyorSorterMode mode, List<MyInventoryItemFilter> items)
        {
            Mode = mode;
            _filters.Clear();

            if (items != null)
                _filters.AddRange(items);
        }
    }
}