using UdonSharp;

using UnityEngine;

using VRC.Udon.Common;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GearStick : VehicleInput {
    public Transmission transmission;
    public Pedal        clutchPedal;
    public Pedal        throttlePedal;

    public float    activationThreshold   = 0.5f;
    public float    deactivationThreshold = 0.3f;
    public float    clutchEngagementTime  = 0.2f;

    public KeyCode shiftUpKey            = KeyCode.E;
    public KeyCode shiftDownKey          = KeyCode.Q;
    public KeyCode secondaryShiftUpKey   = KeyCode.Joystick1Button2;
    public KeyCode secondaryShiftDownKey = KeyCode.Joystick1Button1;
    public string  vrAxis                = "Oculus_CrossPlatform_PrimaryThumbstickVertical";

    private bool  _shiftReady;
    private float _clutchEngagement;

    private void Update() {
        if (!InControl) return;
        
        var vrInput      = Input.GetAxisRaw(vrAxis);
        var activating   = Abs(vrInput) > activationThreshold;
        var deactivating = Abs(vrInput) < deactivationThreshold;
        var vrShiftUp = _shiftReady && activating && vrInput > 0;
        var vrShiftDown = _shiftReady && activating && vrInput < 0;
        _shiftReady = (_shiftReady || deactivating) && !activating;

        if (Input.GetKeyDown(shiftUpKey) || Input.GetKeyDown(secondaryShiftUpKey) || vrShiftUp) {
            if (clutchPedal) clutchPedal.automatedBlend   = 1;
            _clutchEngagement            = 0;
            if (throttlePedal) throttlePedal.automatedBlend = 1;
            if (throttlePedal) throttlePedal.automatedInput = 0;
            transmission.SetGear(transmission.currentGear + 1);
        }

        if (Input.GetKeyDown(shiftDownKey) || Input.GetKeyDown(secondaryShiftDownKey) || vrShiftDown) {
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
}