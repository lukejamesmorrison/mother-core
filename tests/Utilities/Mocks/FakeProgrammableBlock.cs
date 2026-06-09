using FakeItEasy;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components.Interfaces;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight concrete programmable block for tests.
    /// Implements the members Mother currently depends on and supplies safe defaults
    /// for the remaining inherited in-game surface.
    /// </summary>
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyProgrammableBlock.html"/>
    internal class FakeProgrammableBlock : IMyProgrammableBlock
    {
        readonly List<IMyInventory> _inventories = new List<IMyInventory>();
        readonly List<IMyTextSurface> _surfaces = new List<IMyTextSurface>();

        public FakeProgrammableBlock(
            string customData = "",
            string customName = "Mother Core PB",
            long? entityId = null,
            string gridName = "Test Grid",
            long? gridEntityId = null,
            IMyCubeGrid cubeGrid = null)
        {
            EntityId = entityId ?? CreateEntityId();
            CustomData = customData;
            CustomName = customName;
            Name = customName;
            CubeGrid = cubeGrid ?? CreateCubeGrid(gridName, gridEntityId ?? EntityId + 1);
        }

        public bool IsRunning { get; set; }

        public string TerminalRunArgument { get; private set; }

        public Func<IMyTerminalBlock, bool> SameConstructEvaluator { get; set; }

        public bool Enabled { get; set; } = true;

        public string CustomData { get; set; }

        public string CustomInfo { get; set; } = string.Empty;

        public string CustomName { get; set; }

        public string CustomNameWithFaction { get; set; }

        public string DetailedInfo { get; set; } = string.Empty;

        public bool ShowInInventory { get; set; } = true;

        public bool ShowInTerminal { get; set; } = true;

        public bool ShowInToolbarConfig { get; set; } = true;

        public bool ShowOnHUD { get; set; }

        public int SurfaceCount
        {
            get { return _surfaces.Count; }
        }

        public bool UseGenericLcd { get; set; }

        public SerializableDefinitionId BlockDefinition { get; set; }

        public IMyCubeGrid CubeGrid { get; set; }

        public string DefinitionDisplayNameText { get; set; } = "Programmable Block";

        public float DisassembleRatio { get; set; }

        public string DisplayNameText
        {
            get { return CustomName; }
        }

        public bool IsBeingHacked { get; set; }

        public bool IsFunctional { get; set; } = true;

        public bool IsWorking { get; set; } = true;

        public float Mass { get; set; }

        public Vector3I Max { get; set; }

        public Vector3I Min { get; set; }

        public int NumberInGrid { get; set; }

        public MyBlockOrientation Orientation { get; set; }

        public long OwnerId { get; set; }

        public Vector3I Position { get; set; }

        public bool Closed { get; set; }

        public IMyEntityComponentContainer Components { get; set; }

        public string DisplayName
        {
            get { return CustomName; }
        }

        public long EntityId { get; set; }

        public bool HasInventory
        {
            get { return _inventories.Count > 0; }
        }

        public int InventoryCount
        {
            get { return _inventories.Count; }
        }

        public string Name { get; set; }

        public BoundingBoxD WorldAABB { get; set; }

        public BoundingBoxD WorldAABBHr { get; set; }

        public MatrixD WorldMatrix { get; set; } = MatrixD.Identity;

        public BoundingSphereD WorldVolume { get; set; }

        public BoundingSphereD WorldVolumeHr { get; set; }

        public bool TryRun(string argument)
        {
            if (IsRunning)
                return false;

            TerminalRunArgument = argument;
            return true;
        }

        public void RequestEnable(bool enable)
        {
            Enabled = enable;
        }

        public void GetActions(List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect = null)
        {
        }

        public ITerminalAction GetActionWithName(string name)
        {
            return null;
        }

        public void GetProperties(List<ITerminalProperty> resultList, Func<ITerminalProperty, bool> collect = null)
        {
        }

        public ITerminalProperty GetProperty(string id)
        {
            return null;
        }

        public bool HasLocalPlayerAccess()
        {
            return true;
        }

        public bool HasNobodyPlayerAccessToBlock()
        {
            return true;
        }

        public bool HasPlayerAccess(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return true;
        }

        public bool HasPlayerAccessWithNobodyCheck(long playerId, bool defaultNoUser)
        {
            return true;
        }

        public bool IsSameConstructAs(IMyTerminalBlock other)
        {
            if (SameConstructEvaluator != null)
                return SameConstructEvaluator(other);

            return other != null && Equals(other.CubeGrid, CubeGrid);
        }

        public void SearchActionsOfName(string name, List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect = null)
        {
        }

        public void SetCustomName(string text)
        {
            CustomName = text;
        }

        public void SetCustomName(StringBuilder text)
        {
            CustomName = text == null ? null : text.ToString();
        }

        public IMyTextSurface GetSurface(int index)
        {
            if (index < 0 || index >= _surfaces.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _surfaces[index];
        }

        public string GetOwnerFactionTag()
        {
            return string.Empty;
        }

        public MyRelationsBetweenPlayerAndBlock GetPlayerRelationToOwner()
        {
            return MyRelationsBetweenPlayerAndBlock.NoOwnership;
        }

        public MyRelationsBetweenPlayerAndBlock GetUserRelationToOwner(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return defaultNoUserRelation;
        }

        public void UpdateIsWorking()
        {
        }

        public void UpdateVisual()
        {
        }

        public IMyInventory GetInventory()
        {
            return GetInventory(0);
        }

        public IMyInventory GetInventory(int index)
        {
            if (index < 0 || index >= _inventories.Count)
                return null;

            return _inventories[index];
        }

        public Vector3D GetPosition()
        {
            return WorldMatrix.Translation;
        }

        public FakeProgrammableBlock AddSurface(IMyTextSurface surface)
        {
            _surfaces.Add(surface);
            return this;
        }

        public FakeProgrammableBlock AddInventory(IMyInventory inventory)
        {
            _inventories.Add(inventory);
            return this;
        }

        static long CreateEntityId()
        {
            var random = new Random();
            return ((long)random.Next(100000, 1000000) * 10000000000L) + random.Next(0, 1000000000);
        }

        static IMyCubeGrid CreateCubeGrid(string customName, long entityId)
        {
            var grid = A.Fake<IMyCubeGrid>();
            A.CallTo(() => grid.CustomName).Returns(customName);
            A.CallTo(() => grid.EntityId).Returns(entityId);
            return grid;
        }
    }
}