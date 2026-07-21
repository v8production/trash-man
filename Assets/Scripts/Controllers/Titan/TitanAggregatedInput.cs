[System.Serializable]
public struct TitanAggregatedInput
{
    public bool TorsoDrillPressedThisFrame;
    public bool TorsoShieldPressedThisFrame;
    public bool TorsoClawPressedThisFrame;
    public bool TorsoShieldHeld;
    public uint TorsoDrillPressCounter;
    public uint TorsoShieldPressCounter;
    public uint TorsoClawPressCounter;
    public float TorsoYawInput;
    public UnityEngine.Vector2 MouseDelta;
    public float TorsoCameraScrollInput;
    public float ArmElbowInput;
    public float LegScrollInput;
    public uint TransientInputSequence;
}
