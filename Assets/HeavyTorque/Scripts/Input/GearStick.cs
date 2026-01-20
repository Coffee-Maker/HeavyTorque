using UdonSharp;

using UnityEngine;

using VRC.Udon.Common;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GearStick : VehicleInput {
    public Transmission transmission;

    public HandType handType;
    public float    activationThreshold   = 0.5f;
    public float    deactivationThreshold = 0.3f;

    public KeyCode shiftUpKey   = KeyCode.E;
    public KeyCode shiftDownKey = KeyCode.Q;

    private bool _shiftReady;

    private void Update() {
        if (!InControl || InVR) return;
        if (Input.GetKeyDown(shiftUpKey)) transmission.SetGear(transmission.currentGear + 1);
        if (Input.GetKeyDown(shiftDownKey)) transmission.SetGear(transmission.currentGear - 1);
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