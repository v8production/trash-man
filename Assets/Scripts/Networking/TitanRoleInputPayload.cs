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
    public bool RightMouseDetachBuffered;

    public float BodyForward;
    public float BodyStrafe;
    public float BodyTurn;
    public float BodyWaist;

    public float LeftArmElbow;
    public float RightArmElbow;
    public float LeftLegKnee;
    public float RightLegKnee;

    public TitanRoleInputPayload(in TitanAggregatedInput input)
    {
        MouseX = input.MousePosition.x;
        MouseY = input.MousePosition.y;
        MouseDeltaX = input.MouseDelta.x;
        MouseDeltaY = input.MouseDelta.y;
        RightMouseHeld = input.RightMouseHeld;
        RightMousePressedThisFrame = input.RightMousePressedThisFrame;
        RightMouseDetachBuffered = input.RightMouseDetachBuffered;
        BodyForward = input.BodyForward;
        BodyStrafe = input.BodyStrafe;
        BodyTurn = input.BodyTurn;
        BodyWaist = input.BodyWaist;
        LeftArmElbow = input.LeftArmElbow;
        RightArmElbow = input.RightArmElbow;
        LeftLegKnee = input.LeftLegKnee;
        RightLegKnee = input.RightLegKnee;
    }

    public TitanAggregatedInput ToAggregatedInput()
    {
        return new TitanAggregatedInput
        {
            MousePosition = new Vector2(MouseX, MouseY),
            MouseDelta = new Vector2(MouseDeltaX, MouseDeltaY),
            RightMouseHeld = RightMouseHeld,
            RightMousePressedThisFrame = RightMousePressedThisFrame,
            RightMouseDetachBuffered = RightMouseDetachBuffered,
            BodyForward = BodyForward,
            BodyStrafe = BodyStrafe,
            BodyTurn = BodyTurn,
            BodyWaist = BodyWaist,
            LeftArmElbow = LeftArmElbow,
            RightArmElbow = RightArmElbow,
            LeftLegKnee = LeftLegKnee,
            RightLegKnee = RightLegKnee,
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
        serializer.SerializeValue(ref RightMouseDetachBuffered);
        serializer.SerializeValue(ref BodyForward);
        serializer.SerializeValue(ref BodyStrafe);
        serializer.SerializeValue(ref BodyTurn);
        serializer.SerializeValue(ref BodyWaist);
        serializer.SerializeValue(ref LeftArmElbow);
        serializer.SerializeValue(ref RightArmElbow);
        serializer.SerializeValue(ref LeftLegKnee);
        serializer.SerializeValue(ref RightLegKnee);
    }

    public bool Equals(TitanRoleInputPayload other)
    {
        return MouseX.Equals(other.MouseX)
            && MouseY.Equals(other.MouseY)
            && MouseDeltaX.Equals(other.MouseDeltaX)
            && MouseDeltaY.Equals(other.MouseDeltaY)
            && RightMouseHeld == other.RightMouseHeld
            && RightMousePressedThisFrame == other.RightMousePressedThisFrame
            && RightMouseDetachBuffered == other.RightMouseDetachBuffered
            && BodyForward.Equals(other.BodyForward)
            && BodyStrafe.Equals(other.BodyStrafe)
            && BodyTurn.Equals(other.BodyTurn)
            && BodyWaist.Equals(other.BodyWaist)
            && LeftArmElbow.Equals(other.LeftArmElbow)
            && RightArmElbow.Equals(other.RightArmElbow)
            && LeftLegKnee.Equals(other.LeftLegKnee)
            && RightLegKnee.Equals(other.RightLegKnee);
    }
}
