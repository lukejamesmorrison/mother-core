using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components.Interfaces;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal sealed class FakeCubeGrid : IMyCubeGrid
    {
        readonly List<IMyInventory> _inventories = new List<IMyInventory>();

        public FakeCubeGrid(string customName = null, long? entityId = null)
        {
            CustomName = customName ?? "Test Grid";
            EntityId = entityId ?? EntityIdFactory.Create();
            Name = CustomName;
        }

        public string CustomName { get; set; }

        public long EntityId { get; set; }

        public string Name { get; set; }

        public Vector3D WorldPosition { get; set; }

        public bool Closed { get; set; }

        public bool IsStatic { get; set; }

        public float GridSize { get; set; } = 2.5f;

        public MyCubeSize GridSizeEnum { get; set; } = MyCubeSize.Large;

        public Vector3I Max { get; set; }

        public Vector3I Min { get; set; }

        public Vector3 LinearVelocity { get; set; }

        public float Speed { get; set; }

        public BoundingBoxD WorldAABB { get; set; }

        public BoundingBoxD WorldAABBHr { get; set; }

        public MatrixD WorldMatrix { get; set; } = MatrixD.Identity;

        public BoundingSphereD WorldVolume { get; set; }

        public BoundingSphereD WorldVolumeHr { get; set; }

        public IMyEntityComponentContainer Components { get; set; }

        public void SetCustomName(string name)
        {
            CustomName = name;
            Name = name;
        }

        public void SetCustomName(StringBuilder text)
        {
            CustomName = text?.ToString();
            Name = CustomName;
        }

        public bool CubeExists(Vector3I pos)
        {
            return false;
        }

        public IMySlimBlock GetCubeBlock(Vector3I pos)
        {
            return null;
        }

        public Vector3D GridIntegerToWorld(Vector3I gridCoords)
        {
            return Vector3D.Zero;
        }

        public Vector3I WorldToGridInteger(Vector3D coords)
        {
            return Vector3I.Zero;
        }

        public bool IsSameConstructAs(IMyCubeGrid other)
        {
            return other != null && other.EntityId == EntityId;
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

        string IMyEntity.DisplayName => CustomName;

        bool IMyEntity.HasInventory => _inventories.Count > 0;

        int IMyEntity.InventoryCount => _inventories.Count;
    }
}