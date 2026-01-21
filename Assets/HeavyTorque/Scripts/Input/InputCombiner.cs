using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class InputCombiner : VehicleInput {
    public VehicleInput inputA;
    public VehicleInput inputB;

    public InputCombineMode mode;

    public override float ReadFloat() => Combine();

    public override int ReadInt() => Mathf.RoundToInt(Combine());

    private float Combine() {
        var a = inputA.ReadFloat();
        var b = inputB.ReadFloat();

        switch (mode) {
            case InputCombineMode.Add:
                return a + b;
            case InputCombineMode.Multiply:
                return a * b;
            case InputCombineMode.Average:
                return (a + b) * 0.5f;
            case InputCombineMode.Max:
                return Mathf.Max(a, b);
            case InputCombineMode.Min:
                return Mathf.Min(a, b);
            default:
                return 0f;
        }
    }
}

public enum InputCombineMode {
    Add,
    Multiply,
    Average,
    Max,
    Min,
}