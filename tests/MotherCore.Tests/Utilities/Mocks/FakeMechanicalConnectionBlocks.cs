using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Base mechanical connection fake shared by rotor/hinge/piston test doubles.
    /// Tracks attachment state and top-part linkage exposed by
    /// <see cref="IMyMechanicalConnectionBlock"/>.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyMechanicalConnectionBlock.html"/>
    /// </remarks>
    internal abstract class FakeMechanicalConnectionBlock : FakeTerminalBlock, IMyMechanicalConnectionBlock
    {
        IMyAttachableTopBlock _top;
        IMyCubeGrid _topGrid;

        protected FakeMechanicalConnectionBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool IsAttached { get; private set; } = true;

        public bool IsLocked { get; set; }

        public bool SafetyLock { get; set; }

        public float SafetyLockSpeed { get; set; }

        public bool PendingAttachment => !IsAttached;

        public IMyAttachableTopBlock Top => IsAttached ? _top : null;

        public IMyCubeGrid TopGrid => IsAttached ? _topGrid : null;

        public void ConfigureTop(IMyAttachableTopBlock top, IMyCubeGrid topGrid, bool isAttached = true)
        {
            _top = top;
            _topGrid = topGrid;
            IsAttached = isAttached;
        }

        public virtual void Attach()
        {
            IsAttached = true;
        }

        public virtual void Detach()
        {
            IsAttached = false;
        }
    }

    /// <summary>
    /// Motor stator fake used by hinge/rotor modules to model angle, limits,
    /// target velocity, lock state, and rotor-head attachment.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyMotorStator.html"/>
    /// </remarks>
    internal sealed class FakeMotorStator : FakeMechanicalConnectionBlock, IMyMotorStator
    {
        public FakeMotorStator(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float Angle { get; set; }

        public float BrakingTorque { get; set; }

        public bool RotorLock
        {
            get { return IsLocked; }
            set { IsLocked = value; }
        }

        public float LowerLimitDeg { get; set; } = float.MinValue;

        public float LowerLimitRad { get; set; } = float.MinValue;

        public float TargetVelocityRPM { get; set; }

        public float TargetVelocityRad { get; set; }

        public float Torque { get; set; }

        public float UpperLimitDeg { get; set; } = float.MaxValue;

        public float UpperLimitRad { get; set; } = float.MaxValue;

        public float Velocity { get; set; }

        public float Displacement { get; set; }

        public IMyMotorRotor Rotor => Top as IMyMotorRotor;

        public void Attach(IMyMotorRotor rotor, bool updateGroup = true)
        {
            ConfigureTop(rotor, rotor?.CubeGrid, true);
        }

        public void Detach(IMyMotorRotor rotor)
        {
            if (Rotor != null && rotor != null && Rotor.EntityId == rotor.EntityId)
                Detach();
        }

        public void RotateToAngle(MyRotationDirection direction, float angle, float revolutionsPerMinute = float.MaxValue)
        {
            TargetVelocityRPM = revolutionsPerMinute;
            TargetVelocityRad = revolutionsPerMinute;
            Angle = angle;
        }
    }

    /// <summary>
    /// Piston fake used by piston module tests for movement direction, limits,
    /// velocity, and status-transition assertions.
    /// </summary>
    /// <remarks>
    /// Citations:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyPistonBase.html"/>
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.PistonStatus.html"/>
    /// </remarks>
    internal sealed class FakePistonBase : FakeMechanicalConnectionBlock, IMyPistonBase
    {
        public FakePistonBase(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float CurrentPosition { get; set; }

        public float HighestPosition { get; set; }

        public float LowestPosition { get; set; }

        public float MaxLimit { get; set; }

        public float MaxVelocity { get; set; }

        public float MinLimit { get; set; }

        public float NormalizedPosition => MaxLimit.Equals(MinLimit)
            ? 0f
            : (CurrentPosition - MinLimit) / (MaxLimit - MinLimit);

        public float Velocity { get; set; }

        public PistonStatus Status { get; set; } = PistonStatus.Extended;

        public void Attach(IMyPistonTop top)
        {
            ConfigureTop(top, top?.CubeGrid, true);
            Status = PistonStatus.Extended;
        }

        public void Detach(IMyPistonTop top)
        {
            if (Top != null && top != null && Top.EntityId == top.EntityId)
            {
                Detach();
                Status = PistonStatus.Retracted;
            }
        }

        public void Extend()
        {
            Velocity = Math.Abs(MaxVelocity > 0 ? MaxVelocity : 1f);
            Status = PistonStatus.Extending;
        }

        public void Retract()
        {
            Velocity = -Math.Abs(MaxVelocity > 0 ? MaxVelocity : 1f);
            Status = PistonStatus.Retracting;
        }

        public void Reverse()
        {
            Velocity = -Velocity;
        }

        public void MoveToPosition(float position, float speed)
        {
            CurrentPosition = position;
            Velocity = speed;
        }
    }
}