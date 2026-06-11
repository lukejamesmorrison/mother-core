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
    public sealed class FakeTerminalBlockExtras
    {
        public string CustomInfo { get; set; } = string.Empty;

        public string CustomNameWithFaction { get; set; }

        public string DetailedInfo { get; set; } = string.Empty;

        public SerializableDefinitionId BlockDefinition { get; set; }

        public float DisassembleRatio { get; set; }

        public bool IsBeingHacked { get; set; }

        public float Mass { get; set; }

        public Vector3I Max { get; set; }

        public Vector3I Min { get; set; }

        public int NumberInGrid { get; set; }

        public MyBlockOrientation Orientation { get; set; }

        public long OwnerId { get; set; }

        public Vector3I Position { get; set; }

        public IMyEntityComponentContainer Components { get; set; }

        public string EntityName { get; set; }

        public BoundingBoxD WorldAABB { get; set; }

        public BoundingBoxD WorldAABBHr { get; set; }

        public MatrixD WorldMatrix { get; set; } = MatrixD.Identity;

        public BoundingSphereD WorldVolume { get; set; }

        public BoundingSphereD WorldVolumeHr { get; set; }
    }

    /// <summary>
    /// Shared concrete base for fake terminal blocks used by the MotherCore harness.
    /// Only exposes the mutable state MotherCore currently uses directly; the rest of the
    /// Space Engineers interface is implemented explicitly with inert defaults so tests can
    /// opt into extra setup only when they truly need it.
    /// </summary>
    public abstract class FakeTerminalBlock : IMyTerminalBlock
    {
        readonly List<IMyInventory> _inventories = new List<IMyInventory>();

        protected FakeTerminalBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
        {
            CustomName = customName ?? GetType().Name;
            CustomData = customData ?? string.Empty;
            EntityId = entityId ?? CreateEntityId();
            CubeGrid = cubeGrid;
            Extra.EntityName = CustomName;
        }

        public FakeTerminalBlockExtras Extra { get; } = new FakeTerminalBlockExtras();

        public Func<IMyTerminalBlock, bool> SameConstructEvaluator { get; set; }

        public bool Enabled { get; set; } = true;

        public string CustomData { get; set; }

        public string CustomName { get; set; }

        public IMyCubeGrid CubeGrid { get; set; }

        public bool IsFunctional { get; set; } = true;

        public bool IsWorking { get; set; } = true;

        public bool Closed { get; set; }

        public long EntityId { get; set; }

        public Vector3D WorldPosition { get; set; }

        public void RequestEnable(bool enable)
        {
            Enabled = enable;
        }

        public bool IsSameConstructAs(IMyTerminalBlock other)
        {
            if (SameConstructEvaluator != null)
                return SameConstructEvaluator(other);

            return other != null
                && CubeGrid != null
                && other.CubeGrid != null
                && CubeGrid.EntityId == other.CubeGrid.EntityId;
        }

        public void SetCustomName(string text)
        {
            CustomName = text;
            Extra.EntityName = text;
            OnCustomNameChanged();
        }

        public void SetCustomName(StringBuilder text)
        {
            SetCustomName(text == null ? null : text.ToString());
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
            return WorldPosition;
        }

        string IMyTerminalBlock.CustomInfo => Extra.CustomInfo;

        string IMyTerminalBlock.CustomNameWithFaction => Extra.CustomNameWithFaction;

        string IMyTerminalBlock.DetailedInfo => Extra.DetailedInfo;

        bool IMyTerminalBlock.ShowInInventory { get; set; } = true;

        bool IMyTerminalBlock.ShowInTerminal { get; set; } = true;

        bool IMyTerminalBlock.ShowInToolbarConfig { get; set; } = true;

        bool IMyTerminalBlock.ShowOnHUD { get; set; }

        void IMyTerminalBlock.GetActions(List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect)
        {
        }

        ITerminalAction IMyTerminalBlock.GetActionWithName(string name)
        {
            return null;
        }

        void IMyTerminalBlock.GetProperties(List<ITerminalProperty> resultList, Func<ITerminalProperty, bool> collect)
        {
        }

        ITerminalProperty IMyTerminalBlock.GetProperty(string id)
        {
            return null;
        }

        bool IMyTerminalBlock.HasLocalPlayerAccess()
        {
            return true;
        }

        bool IMyTerminalBlock.HasNobodyPlayerAccessToBlock()
        {
            return true;
        }

        bool IMyTerminalBlock.HasPlayerAccess(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return true;
        }

        bool IMyTerminalBlock.HasPlayerAccessWithNobodyCheck(long playerId, bool defaultNoUser)
        {
            return true;
        }

        void IMyTerminalBlock.SearchActionsOfName(string name, List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect)
        {
        }

        string IMyCubeBlock.GetOwnerFactionTag()
        {
            return string.Empty;
        }

        MyRelationsBetweenPlayerAndBlock IMyCubeBlock.GetPlayerRelationToOwner()
        {
            return MyRelationsBetweenPlayerAndBlock.NoOwnership;
        }

        MyRelationsBetweenPlayerAndBlock IMyCubeBlock.GetUserRelationToOwner(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return defaultNoUserRelation;
        }

        void IMyCubeBlock.UpdateIsWorking()
        {
        }

        void IMyCubeBlock.UpdateVisual()
        {
        }

        SerializableDefinitionId IMyCubeBlock.BlockDefinition => Extra.BlockDefinition;

        string IMyCubeBlock.DefinitionDisplayNameText => CustomName;

        float IMyCubeBlock.DisassembleRatio => Extra.DisassembleRatio;

        string IMyCubeBlock.DisplayNameText => CustomName;

        bool IMyCubeBlock.IsBeingHacked => Extra.IsBeingHacked;

        float IMyCubeBlock.Mass => Extra.Mass;

        Vector3I IMyCubeBlock.Max => Extra.Max;

        Vector3I IMyCubeBlock.Min => Extra.Min;

        int IMyCubeBlock.NumberInGrid => Extra.NumberInGrid;

        MyBlockOrientation IMyCubeBlock.Orientation => Extra.Orientation;

        long IMyCubeBlock.OwnerId => Extra.OwnerId;

        Vector3I IMyCubeBlock.Position => Extra.Position;

        IMyEntityComponentContainer IMyEntity.Components => Extra.Components;

        string IMyEntity.DisplayName => CustomName;

        bool IMyEntity.HasInventory => _inventories.Count > 0;

        int IMyEntity.InventoryCount => _inventories.Count;

        string IMyEntity.Name
        {
            get { return Extra.EntityName; }
        }

        BoundingBoxD IMyEntity.WorldAABB => Extra.WorldAABB;

        BoundingBoxD IMyEntity.WorldAABBHr => Extra.WorldAABBHr;

        MatrixD IMyEntity.WorldMatrix => Extra.WorldMatrix;

        BoundingSphereD IMyEntity.WorldVolume => Extra.WorldVolume;

        BoundingSphereD IMyEntity.WorldVolumeHr => Extra.WorldVolumeHr;

        protected void AddInventoryInternal(IMyInventory inventory)
        {
            _inventories.Add(inventory);
        }

        protected virtual void OnCustomNameChanged()
        {
        }

        protected static long CreateEntityId()
        {
            var random = new Random();
            return ((long)random.Next(100000, 1000000) * 10000000000L) + random.Next(0, 1000000000);
        }
    }
}