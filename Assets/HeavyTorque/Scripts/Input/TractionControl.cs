using UdonSharp;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TractionControl : VehicleInput {
    public VehicleInput brakeInput;
    public Wheel        wheel;
    public float        slipThreshold = 0.2f;
    public float        slipResponse  = 0.1f;

    public override float ReadFloat() => Max(brakeInput ? brakeInput.ReadFloat() : 0f, DoTractionControl());

    public override int ReadInt() => (int)DoTractionControl();

    private float DoTractionControl() => Clamp01((wheel.LongitudinalSlipRatio - slipThreshold) * slipResponse);
}