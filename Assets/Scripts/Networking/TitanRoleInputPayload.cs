using System;
using Unity.Netcode;
using UnityEngine;

public struct TitanRoleInputPayload : INetworkSerializable, IEquatable<TitanRoleInputPayload>
{
    public bool TorsoDrillPressedThisFrame;
    public bool TorsoShieldPressedThisFrame;
    public bool TorsoClawPressedThisFrame;
    public bool TorsoShieldHeld;
    public uint TorsoDrillPressCounter;
    public uint TorsoShieldPressCounter;
    public uint TorsoClawPressCounter;
    public float TorsoYawInput;
    public Vector2 MouseDelta;
    public float TorsoCameraScrollInput;
    public float ArmElbowInput;
    public float LegScrollInput;
    public uint TransientInputSequence;

    public TitanRoleInputPayload(in TitanAggregatedInput input)
    {
        TorsoDrillPressedThisFrame = input.TorsoDrillPressedThisFrame;
        TorsoShieldPressedThisFrame = input.TorsoShieldPressedThisFrame;
        TorsoClawPressedThisFrame = input.TorsoClawPressedThisFrame;
        TorsoShieldHeld = input.TorsoShieldHeld;
        TorsoDrillPressCounter = input.TorsoDrillPressCounter;
        TorsoShieldPressCounter = input.TorsoShieldPressCounter;
        TorsoClawPressCounter = input.TorsoClawPressCounter;
        TorsoYawInput = input.TorsoYawInput;
        MouseDelta = input.MouseDelta;
        TorsoCameraScrollInput = input.TorsoCameraScrollInput;
        ArmElbowInput = input.ArmElbowInput;
        LegScrollInput = input.LegScrollInput;
        TransientInputSequence = input.TransientInputSequence;
    }

    public TitanAggregatedInput ToAggregatedInput()
    {
        return new TitanAggregatedInput
        {
            TorsoDrillPressedThisFrame = TorsoDrillPressedThisFrame,
            TorsoShieldPressedThisFrame = TorsoShieldPressedThisFrame,
            TorsoClawPressedThisFrame = TorsoClawPressedThisFrame,
            TorsoShieldHeld = TorsoShieldHeld,
            TorsoDrillPressCounter = TorsoDrillPressCounter,
            TorsoShieldPressCounter = TorsoShieldPressCounter,
            TorsoClawPressCounter = TorsoClawPressCounter,
            TorsoYawInput = TorsoYawInput,
            MouseDelta = MouseDelta,
            TorsoCameraScrollInput = TorsoCameraScrollInput,
            ArmElbowInput = ArmElbowInput,
            LegScrollInput = LegScrollInput,
            TransientInputSequence = TransientInputSequence,
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TorsoDrillPressedThisFrame);
        serializer.SerializeValue(ref TorsoShieldPressedThisFrame);
        serializer.SerializeValue(ref TorsoClawPressedThisFrame);
        serializer.SerializeValue(ref TorsoShieldHeld);
        serializer.SerializeValue(ref TorsoDrillPressCounter);
        serializer.SerializeValue(ref TorsoShieldPressCounter);
        serializer.SerializeValue(ref TorsoClawPressCounter);
        serializer.SerializeValue(ref TorsoYawInput);
        serializer.SerializeValue(ref MouseDelta);
        serializer.SerializeValue(ref TorsoCameraScrollInput);
        serializer.SerializeValue(ref ArmElbowInput);
        serializer.SerializeValue(ref LegScrollInput);
        serializer.SerializeValue(ref TransientInputSequence);
    }

    public bool Equals(TitanRoleInputPayload other)
    {
        return TorsoDrillPressedThisFrame == other.TorsoDrillPressedThisFrame
            && TorsoShieldPressedThisFrame == other.TorsoShieldPressedThisFrame
            && TorsoClawPressedThisFrame == other.TorsoClawPressedThisFrame
            && TorsoShieldHeld == other.TorsoShieldHeld
            && TorsoDrillPressCounter == other.TorsoDrillPressCounter
            && TorsoShieldPressCounter == other.TorsoShieldPressCounter
            && TorsoClawPressCounter == other.TorsoClawPressCounter
            && TorsoYawInput.Equals(other.TorsoYawInput)
            && MouseDelta.Equals(other.MouseDelta)
            && TorsoCameraScrollInput.Equals(other.TorsoCameraScrollInput)
            && ArmElbowInput.Equals(other.ArmElbowInput)
            && LegScrollInput.Equals(other.LegScrollInput)
            && TransientInputSequence == other.TransientInputSequence;
    }
}

public struct TitanRigPosePayload : INetworkSerializable, IEquatable<TitanRigPosePayload>
{
    public bool IsValid;

    public Vector3 RootPosition;
    public Quaternion RootRotation;

    public bool HasLeftShoulder;
    public Quaternion LeftShoulderRotation;
    public bool HasLeftElbow;
    public Quaternion LeftElbowRotation;

    public bool HasRightShoulder;
    public Quaternion RightShoulderRotation;
    public bool HasRightElbow;
    public Quaternion RightElbowRotation;

    public bool HasLeftHip;
    public Quaternion LeftHipRotation;
    public bool HasLeftKnee;
    public Quaternion LeftKneeRotation;
    public bool HasLeftFoot;
    public Vector3 LeftFootPosition;
    public Quaternion LeftFootRotation;

    public bool HasRightHip;
    public Quaternion RightHipRotation;
    public bool HasRightKnee;
    public Quaternion RightKneeRotation;
    public bool HasRightFoot;
    public Vector3 RightFootPosition;
    public Quaternion RightFootRotation;

    public bool HasSpine;
    public Quaternion SpineRotation;

    public TitanRigPosePayload(in TitanRigPoseSnapshot snapshot)
    {
        IsValid = true;

        RootPosition = snapshot.RootPosition;
        RootRotation = snapshot.RootRotation;

        HasLeftShoulder = snapshot.HasLeftShoulder;
        LeftShoulderRotation = snapshot.LeftShoulderRotation;
        HasLeftElbow = snapshot.HasLeftElbow;
        LeftElbowRotation = snapshot.LeftElbowRotation;

        HasRightShoulder = snapshot.HasRightShoulder;
        RightShoulderRotation = snapshot.RightShoulderRotation;
        HasRightElbow = snapshot.HasRightElbow;
        RightElbowRotation = snapshot.RightElbowRotation;

        HasLeftHip = snapshot.HasLeftHip;
        LeftHipRotation = snapshot.LeftHipRotation;
        HasLeftKnee = snapshot.HasLeftKnee;
        LeftKneeRotation = snapshot.LeftKneeRotation;
        HasLeftFoot = snapshot.HasLeftFoot;
        LeftFootPosition = snapshot.LeftFootPosition;
        LeftFootRotation = snapshot.LeftFootRotation;

        HasRightHip = snapshot.HasRightHip;
        RightHipRotation = snapshot.RightHipRotation;
        HasRightKnee = snapshot.HasRightKnee;
        RightKneeRotation = snapshot.RightKneeRotation;
        HasRightFoot = snapshot.HasRightFoot;
        RightFootPosition = snapshot.RightFootPosition;
        RightFootRotation = snapshot.RightFootRotation;

        HasSpine = snapshot.HasSpine;
        SpineRotation = snapshot.SpineRotation;
    }

    public TitanRigPoseSnapshot ToSnapshot()
    {
        return new TitanRigPoseSnapshot
        {
            RootPosition = RootPosition,
            RootRotation = RootRotation,

            HasLeftShoulder = HasLeftShoulder,
            LeftShoulderRotation = LeftShoulderRotation,
            HasLeftElbow = HasLeftElbow,
            LeftElbowRotation = LeftElbowRotation,

            HasRightShoulder = HasRightShoulder,
            RightShoulderRotation = RightShoulderRotation,
            HasRightElbow = HasRightElbow,
            RightElbowRotation = RightElbowRotation,

            HasLeftHip = HasLeftHip,
            LeftHipRotation = LeftHipRotation,
            HasLeftKnee = HasLeftKnee,
            LeftKneeRotation = LeftKneeRotation,
            HasLeftFoot = HasLeftFoot,
            LeftFootPosition = LeftFootPosition,
            LeftFootRotation = LeftFootRotation,

            HasRightHip = HasRightHip,
            RightHipRotation = RightHipRotation,
            HasRightKnee = HasRightKnee,
            RightKneeRotation = RightKneeRotation,
            HasRightFoot = HasRightFoot,
            RightFootPosition = RightFootPosition,
            RightFootRotation = RightFootRotation,

            HasSpine = HasSpine,
            SpineRotation = SpineRotation,
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);

        serializer.SerializeValue(ref RootPosition);
        serializer.SerializeValue(ref RootRotation);

        serializer.SerializeValue(ref HasLeftShoulder);
        serializer.SerializeValue(ref LeftShoulderRotation);
        serializer.SerializeValue(ref HasLeftElbow);
        serializer.SerializeValue(ref LeftElbowRotation);

        serializer.SerializeValue(ref HasRightShoulder);
        serializer.SerializeValue(ref RightShoulderRotation);
        serializer.SerializeValue(ref HasRightElbow);
        serializer.SerializeValue(ref RightElbowRotation);

        serializer.SerializeValue(ref HasLeftHip);
        serializer.SerializeValue(ref LeftHipRotation);
        serializer.SerializeValue(ref HasLeftKnee);
        serializer.SerializeValue(ref LeftKneeRotation);
        serializer.SerializeValue(ref HasLeftFoot);
        serializer.SerializeValue(ref LeftFootPosition);
        serializer.SerializeValue(ref LeftFootRotation);

        serializer.SerializeValue(ref HasRightHip);
        serializer.SerializeValue(ref RightHipRotation);
        serializer.SerializeValue(ref HasRightKnee);
        serializer.SerializeValue(ref RightKneeRotation);
        serializer.SerializeValue(ref HasRightFoot);
        serializer.SerializeValue(ref RightFootPosition);
        serializer.SerializeValue(ref RightFootRotation);

        serializer.SerializeValue(ref HasSpine);
        serializer.SerializeValue(ref SpineRotation);
    }

    public bool Equals(TitanRigPosePayload other)
    {
        return IsValid == other.IsValid
            && RootPosition.Equals(other.RootPosition)
            && RootRotation.Equals(other.RootRotation)
            && HasLeftShoulder == other.HasLeftShoulder
            && LeftShoulderRotation.Equals(other.LeftShoulderRotation)
            && HasLeftElbow == other.HasLeftElbow
            && LeftElbowRotation.Equals(other.LeftElbowRotation)
            && HasRightShoulder == other.HasRightShoulder
            && RightShoulderRotation.Equals(other.RightShoulderRotation)
            && HasRightElbow == other.HasRightElbow
            && RightElbowRotation.Equals(other.RightElbowRotation)
            && HasLeftHip == other.HasLeftHip
            && LeftHipRotation.Equals(other.LeftHipRotation)
            && HasLeftKnee == other.HasLeftKnee
            && LeftKneeRotation.Equals(other.LeftKneeRotation)
            && HasLeftFoot == other.HasLeftFoot
            && LeftFootPosition.Equals(other.LeftFootPosition)
            && LeftFootRotation.Equals(other.LeftFootRotation)
            && HasRightHip == other.HasRightHip
            && RightHipRotation.Equals(other.RightHipRotation)
            && HasRightKnee == other.HasRightKnee
            && RightKneeRotation.Equals(other.RightKneeRotation)
            && HasRightFoot == other.HasRightFoot
            && RightFootPosition.Equals(other.RightFootPosition)
            && RightFootRotation.Equals(other.RightFootRotation)
            && HasSpine == other.HasSpine
            && SpineRotation.Equals(other.SpineRotation);
    }
}

public struct TorsoCameraStatePayload : INetworkSerializable, IEquatable<TorsoCameraStatePayload>
{
    public bool IsValid;
    public float Yaw;
    public float Pitch;
    public float Distance;

    public TorsoCameraStatePayload(float yaw, float pitch, float distance)
    {
        IsValid = true;
        Yaw = yaw;
        Pitch = pitch;
        Distance = distance;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Pitch);
        serializer.SerializeValue(ref Distance);
    }

    public bool Equals(TorsoCameraStatePayload other)
    {
        return IsValid == other.IsValid
            && Yaw.Equals(other.Yaw)
            && Pitch.Equals(other.Pitch)
            && Distance.Equals(other.Distance);
    }
}

public struct TitanAbilityStatePayload : INetworkSerializable, IEquatable<TitanAbilityStatePayload>
{
    public bool Guard;
    public bool LeftDrillActive;
    public int RightClawLaunchCount;
    public TitanClawWirePhase RightClawPhase;
    public float RightClawWireLength;
    public Vector3 RightClawPosition;
    public Quaternion RightClawRotation;

    public TitanAbilityStatePayload(TitanController titanController)
    {
        Guard = titanController.Guard;
        LeftDrillActive = titanController.LeftDrillActive;
        RightClawLaunchCount = titanController.RightClawLaunchCount;
        TitanClawWireSnapshot clawSnapshot = titanController.RightClawWire != null
            ? titanController.RightClawWire.GetSnapshot()
            : default;
        RightClawPhase = clawSnapshot.Phase;
        RightClawWireLength = clawSnapshot.CurrentLength;
        RightClawPosition = clawSnapshot.ClawPosition;
        RightClawRotation = clawSnapshot.ClawRotation;
    }

    public void ApplyTo(TitanController titanController)
    {
        titanController.Guard = Guard;
        titanController.LeftDrillActive = LeftDrillActive;
        titanController.SetRightClawLaunchCount(RightClawLaunchCount);
        if (titanController.RightClawWire != null)
        {
            titanController.RightClawWire.ApplySnapshot(new TitanClawWireSnapshot
            {
                Phase = RightClawPhase,
                CurrentLength = RightClawWireLength,
                ClawPosition = RightClawPosition,
                ClawRotation = RightClawRotation,
            });
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Guard);
        serializer.SerializeValue(ref LeftDrillActive);
        serializer.SerializeValue(ref RightClawLaunchCount);
        serializer.SerializeValue(ref RightClawPhase);
        serializer.SerializeValue(ref RightClawWireLength);
        serializer.SerializeValue(ref RightClawPosition);
        serializer.SerializeValue(ref RightClawRotation);
    }

    public bool Equals(TitanAbilityStatePayload other)
    {
        return Guard == other.Guard
            && LeftDrillActive == other.LeftDrillActive
            && RightClawLaunchCount == other.RightClawLaunchCount
            && RightClawPhase == other.RightClawPhase
            && RightClawWireLength.Equals(other.RightClawWireLength)
            && RightClawPosition.Equals(other.RightClawPosition)
            && RightClawRotation.Equals(other.RightClawRotation);
    }
}

public struct StatPayload : INetworkSerializable, IEquatable<StatPayload>
{
    public int Hp;
    public int MaxHp;
    public int Attack;

    public StatPayload(Stat stat)
    {
        Hp = stat.Hp;
        MaxHp = stat.MaxHp;
        Attack = stat.Attack;
    }

    public void ApplyTo(Stat stat)
    {
        stat.MaxHp = MaxHp;
        stat.Hp = Hp;
        stat.Attack = Attack;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Hp);
        serializer.SerializeValue(ref MaxHp);
        serializer.SerializeValue(ref Attack);
    }

    public bool Equals(StatPayload other)
    {
        return Hp == other.Hp
            && MaxHp == other.MaxHp
            && Attack == other.Attack;
    }
}

public struct TitanStatPayload : INetworkSerializable, IEquatable<TitanStatPayload>
{
    public StatPayload BaseStat;
    public int Gauge;
    public int MaxGauge;

    public TitanStatPayload(TitanStat titanStat)
    {
        BaseStat = new StatPayload(titanStat);
        Gauge = titanStat.Gauge;
        MaxGauge = titanStat.MaxGauge;
    }

    public void ApplyTo(TitanStat titanStat)
    {
        BaseStat.ApplyTo(titanStat);
        titanStat.MaxGauge = MaxGauge;
        titanStat.Gauge = Gauge;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        BaseStat.NetworkSerialize(serializer);
        serializer.SerializeValue(ref Gauge);
        serializer.SerializeValue(ref MaxGauge);
    }

    public bool Equals(TitanStatPayload other)
    {
        return BaseStat.Equals(other.BaseStat)
            && Gauge == other.Gauge
            && MaxGauge == other.MaxGauge;
    }
}

public struct GrolarStatePayload : INetworkSerializable, IEquatable<GrolarStatePayload>
{
    public bool IsValid;
    public StatPayload Stat;
    public Vector3 Position;
    public Quaternion Rotation;
    public int AnimState;
    public bool AttackInProgress;

    public GrolarStatePayload(GrolarController grolarController)
    {
        IsValid = true;
        Stat = new StatPayload(grolarController.Stat);
        Position = grolarController.transform.position;
        Rotation = grolarController.transform.rotation;
        AnimState = (int)grolarController.AnimState;
        AttackInProgress = grolarController.AttackInProgress;
    }

    public void ApplyTo(GrolarController grolarController)
    {
        Stat.ApplyTo(grolarController.Stat);
        grolarController.ApplyNetworkState(Position, Rotation, (Define.GrolarAnimState)AnimState, AttackInProgress);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);
        Stat.NetworkSerialize(serializer);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref AnimState);
        serializer.SerializeValue(ref AttackInProgress);
    }

    public bool Equals(GrolarStatePayload other)
    {
        return IsValid == other.IsValid
            && Stat.Equals(other.Stat)
            && Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && AnimState == other.AnimState
            && AttackInProgress == other.AttackInProgress;
    }
}
