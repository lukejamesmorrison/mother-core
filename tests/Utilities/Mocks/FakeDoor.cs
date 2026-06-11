using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal sealed class FakeDoor : FakeTerminalBlock, IMyDoor
    {
        public FakeDoor(
            string customData = "",
            string customName = null,
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool Open
        {
            get => Status == DoorStatus.Open || Status == DoorStatus.Opening;
            set
            {
                if (value)
                {
                    Status = DoorStatus.Open;
                    OpenRatio = 1f;
                    return;
                }

                Status = DoorStatus.Closed;
                OpenRatio = 0f;
            }
        }

        public DoorStatus Status { get; set; } = DoorStatus.Closed;

        public float OpenRatio { get; set; }

        public void OpenDoor()
        {
            Open = true;
        }

        public void CloseDoor()
        {
            Open = false;
        }

        public void ToggleDoor()
        {
            Open = !Open;
        }
    }
}