using System;
using Unity.Netcode;
using UnityEngine;

public struct TitanRoleInputPayload : INetworkSerializable, IEquatable<TitanRoleInputPayload>
{
    public float MouseX;
    public float MouseY;
    public float MouseDeltaX;
    public float MouseDeltaY;
    public bool RightMouseHeld;
    public bool RightMousePressedThisFrame;
    public bool RightMouseAttachBuffered;

    public float TorsoForward;
    public float TorsoStrafe;
    public float TorsoTurn;
    public float TorsoWaist;
    public bool TorsoDrillPressedThisFrame;
    public bool TorsoShieldPressedThisFrame;
    public bool TorsoClawPressedThisFrame;
    public uint TorsoDrillPressCounter;
    public uint TorsoShieldPressCounter;
    public uint TorsoClawPressCounter;

    public float LeftArmElbow;
    public float RightArmElbow;
    public float LeftLegKnee;
    public float RightLegKnee;
    public float LeftLegAnkle;
    public float RightLegAnkle;

    public TitanRoleInputPayload(in TitanAggregatedInput input)
    {
        MouseX = input.MousePosition.x;
        MouseY = input.MousePosition.y;
        MouseDeltaX = input.MouseDelta.x;
        MouseDeltaY = input.MouseDelta.y;
        RightMouseHeld = input.RightMouseHeld;
        RightMousePressedThisFrame = input.RightMousePressedThisFrame;
        RightMouseAttachBuffered = input.RightMouseAttachBuffered;
        TorsoForward = input.TorsoForward;
        TorsoStrafe = input.TorsoStrafe;
        TorsoTurn = input.TorsoTurn;
        TorsoWaist = input.TorsoWaist;
        TorsoDrillPressedThisFrame = input.TorsoDrillPressedThisFrame;
        TorsoShieldPressedThisFrame = input.TorsoShieldPressedThisFrame;
        TorsoClawPressedThisFrame = input.TorsoClawPressedThisFrame;
        TorsoDrillPressCounter = input.TorsoDrillPressCounter;
        TorsoShieldPressCounter = input.TorsoShieldPressCounter;
        TorsoClawPressCounter = input.TorsoClawPressCounter;
        LeftArmElbow = input.LeftArmElbow;
        RightArmElbow = input.RightArmElbow;
        LeftLegKnee = input.LeftLegKnee;
        RightLegKnee = input.RightLegKnee;
        LeftLegAnkle = input.LeftLegAnkle;
        RightLegAnkle = input.RightLegAnkle;
    }

    public TitanAggregatedInput ToAggregatedInput()
    {
        return new TitanAggregatedInput
        {
            MousePosition = new Vector2(MouseX, MouseY),
            MouseDelta = new Vector2(MouseDeltaX, MouseDeltaY),
            RightMouseHeld = RightMouseHeld,
            RightMousePressedThisFrame = RightMousePressedThisFrame,
            RightMouseAttachBuffered = RightMouseAttachBuffered,
            TorsoForward = TorsoForward,
            TorsoStrafe = TorsoStrafe,
            TorsoTurn = TorsoTurn,
            TorsoWaist = TorsoWaist,
            TorsoDrillPressedThisFrame = TorsoDrillPressedThisFrame,
            TorsoShieldPressedThisFrame = TorsoShieldPressedThisFrame,
            TorsoClawPressedThisFrame = TorsoClawPressedThisFrame,
            TorsoDrillPressCounter = TorsoDrillPressCounter,
            TorsoShieldPressCounter = TorsoShieldPressCounter,
            TorsoClawPressCounter = TorsoClawPressCounter,
            LeftArmElbow = LeftArmElbow,
            RightArmElbow = RightArmElbow,
            LeftLegKnee = LeftLegKnee,
            RightLegKnee = RightLegKnee,
            LeftLegAnkle = LeftLegAnkle,
            RightLegAnkle = RightLegAnkle,
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MouseX);
        serializer.SerializeValue(ref MouseY);
        serializer.SerializeValue(ref MouseDeltaX);
        serializer.SerializeValue(ref MouseDeltaY);
        serializer.SerializeValue(ref RightMouseHeld);
        serializer.SerializeValue(ref RightMousePressedThisFrame);
        serializer.SerializeValue(ref RightMouseAttachBuffered);
        serializer.SerializeValue(ref TorsoForward);
        serializer.SerializeValue(ref TorsoStrafe);
        serializer.SerializeValue(ref TorsoTurn);
        serializer.SerializeValue(ref TorsoWaist);
        serializer.SerializeValue(ref TorsoDrillPressedThisFrame);
        serializer.SerializeValue(ref TorsoShieldPressedThisFrame);
        serializer.SerializeValue(ref TorsoClawPressedThisFrame);
        serializer.SerializeValue(ref TorsoDrillPressCounter);
        serializer.SerializeValue(ref TorsoShieldPressCounter);
        serializer.SerializeValue(ref TorsoClawPressCounter);
        serializer.SerializeValue(ref LeftArmElbow);
        serializer.SerializeValue(ref RightArmElbow);
        serializer.SerializeValue(ref LeftLegKnee);
        serializer.SerializeValue(ref RightLegKnee);
        serializer.SerializeValue(ref LeftLegAnkle);
        serializer.SerializeValue(ref RightLegAnkle);
    }

    public bool Equals(TitanRoleInputPayload other)
    {
        return MouseX.Equals(other.MouseX)
            && MouseY.Equals(other.MouseY)
            && MouseDeltaX.Equals(other.MouseDeltaX)
            && MouseDeltaY.Equals(other.MouseDeltaY)
            && RightMouseHeld == other.RightMouseHeld
            && RightMousePressedThisFrame == other.RightMousePressedThisFrame
            && RightMouseAttachBuffered == other.RightMouseAttachBuffered
            && TorsoForward.Equals(other.TorsoForward)
            && TorsoStrafe.Equals(other.TorsoStrafe)
            && TorsoTurn.Equals(other.TorsoTurn)
            && TorsoWaist.Equals(other.TorsoWaist)
            && TorsoDrillPressedThisFrame == other.TorsoDrillPressedThisFrame
            && TorsoShieldPressedThisFrame == other.TorsoShieldPressedThisFrame
            && TorsoClawPressedThisFrame == other.TorsoClawPressedThisFrame
            && TorsoDrillPressCounter == other.TorsoDrillPressCounter
            && TorsoShieldPressCounter == other.TorsoShieldPressCounter
            && TorsoClawPressCounter == other.TorsoClawPressCounter
            && LeftArmElbow.Equals(other.LeftArmElbow)
            && RightArmElbow.Equals(other.RightArmElbow)
            && LeftLegKnee.Equals(other.LeftLegKnee)
            && RightLegKnee.Equals(other.RightLegKnee)
            && LeftLegAnkle.Equals(other.LeftLegAnkle)
            && RightLegAnkle.Equals(other.RightLegAnkle);
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
    public Quaternion LeftFootRotation;

    public bool HasRightHip;
    public Quaternion RightHipRotation;
    public bool HasRightKnee;
    public Quaternion RightKneeRotation;
    public bool HasRightFoot;
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
        LeftFootRotation = snapshot.LeftFootRotation;

        HasRightHip = snapshot.HasRightHip;
        RightHipRotation = snapshot.RightHipRotation;
        HasRightKnee = snapshot.HasRightKnee;
        RightKneeRotation = snapshot.RightKneeRotation;
        HasRightFoot = snapshot.HasRightFoot;
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
            LeftFootRotation = LeftFootRotation,

            HasRightHip = HasRightHip,
            RightHipRotation = RightHipRotation,
            HasRightKnee = HasRightKnee,
            RightKneeRotation = RightKneeRotation,
            HasRightFoot = HasRightFoot,
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
        serializer.SerializeValue(ref LeftFootRotation);

        serializer.SerializeValue(ref HasRightHip);
        serializer.SerializeValue(ref RightHipRotation);
        serializer.SerializeValue(ref HasRightKnee);
        serializer.SerializeValue(ref RightKneeRotation);
        serializer.SerializeValue(ref HasRightFoot);
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
            && LeftFootRotation.Equals(other.LeftFootRotation)
            && HasRightHip == other.HasRightHip
            && RightHipRotation.Equals(other.RightHipRotation)
            && HasRightKnee == other.HasRightKnee
            && RightKneeRotation.Equals(other.RightKneeRotation)
            && HasRightFoot == other.HasRightFoot
            && RightFootRotation.Equals(other.RightFootRotation)
            && HasSpine == other.HasSpine
            && SpineRotation.Equals(other.SpineRotation);
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
