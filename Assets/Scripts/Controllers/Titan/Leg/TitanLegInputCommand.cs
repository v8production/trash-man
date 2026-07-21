using System;
using UnityEngine;

[Serializable]
public struct TitanLegInputCommand
{
    public Vector2 HorizontalDelta;
    public float LiftInput;

    public static TitanLegInputCommand From(in TitanAggregatedInput input)
    {
        return new TitanLegInputCommand
        {
            HorizontalDelta = input.MouseDelta,
            LiftInput = Mathf.Clamp01(Mathf.Abs(input.LegScrollInput)),
        };
    }
}
