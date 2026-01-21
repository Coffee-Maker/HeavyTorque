using UdonSharp;

using UnityEngine;

using VRC.Udon.Common;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GearStick : VehicleInput {
    public Transmission transmission;
    public Pedal        clutchPedal;
    public Pedal        throttlePedal;

    public HandType handType;
    public float    activationThreshold   = 0.5f;
    public float    deactivationThreshold = 0.3f;
    public float    clutchEngagementTime  = 0.2f;

    public KeyCode shiftUpKey   = KeyCode.E;
    public KeyCode shiftDownKey = KeyCode.Q;

    private bool  _shiftReady;
    private float _clutchEngagement;

    private void Update() {
        if (!InControl || InVR) return;

        if (Input.GetKeyDown(shiftUpKey)) {
            if (clutchPedal) clutchPedal.automatedBlend   = 1;
            _clutchEngagement            = 0;
            if (throttlePedal) throttlePedal.automatedBlend = 1;
            if (throttlePedal) throttlePedal.automatedInput = 0;
            transmission.SetGear(transmission.currentGear + 1);
        }

        if (Input.GetKeyDown(shiftDownKey)) {
            if (clutchPedal) clutchPedal.automatedBlend   = 1;
            _clutchEngagement            = 0;
            if (throttlePedal) throttlePedal.automatedBlend = 1;
            if (throttlePedal) throttlePedal.automatedInput = 0;
            transmission.SetGear(transmission.currentGear - 1);
        }

        _clutchEngagement = Min(1, _clutchEngagement + Time.deltaTime / clutchEngagementTime);
        if (clutchPedal) clutchPedal.automatedInput = 1 - _clutchEngagement;

        if (_clutchEngagement >= 1) {
            if (clutchPedal) clutchPedal.automatedBlend     = 0;
            if (throttlePedal) throttlePedal.automatedBlend = 0;
        }
    }

    public override float ReadFloat() => transmission.currentGear;

    public override int ReadInt() => transmission.currentGear;

    public override void InputMoveVertical(float value, UdonInputEventArgs args) {
        if (!InControl || !InVR || args.handType != handType) return;
        var activating   = Abs(value) > activationThreshold;
        var deactivating = Abs(value) < deactivationThreshold;
        if (_shiftReady && activating) transmission.SetGear(transmission.currentGear + (int)Sign(args.floatValue));
        _shiftReady = (_shiftReady || deactivating) && !activating;
    }
}