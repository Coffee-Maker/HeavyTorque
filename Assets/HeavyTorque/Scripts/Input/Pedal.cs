using System;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon.Common;

using static UnityEngine.Mathf;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Pedal : VehicleInput {
    private float     _input;
    public  HandType  hand;
    public  PedalType pedalType;
    public KeyCode desktopKey;

    public float automatedBlend; // 0 = manual, 1 = automated
    public float automatedInput;

    protected override void OnRevokeControl() { _input = 0f; }

    public override float ReadFloat() => Lerp(_input, automatedInput, automatedBlend);

    public override int ReadInt() => ReadFloat() > 0.5f ? 1 : 0;

    private void Update() {
        if(!InControl || InVR) return;
        _input = Input.GetKey(desktopKey) ? 1f : 0f;
    }

    public override void InputUse(bool value, UdonInputEventArgs args) {
        if (!InControl || !InVR || pedalType != PedalType.Trigger || args.handType != hand) return;
        _input = args.floatValue;
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args) {
        if (!InControl || !InVR || args.handType != hand) return;

        switch (pedalType) {
            case PedalType.JoystickYPositive:
                _input = Mathf.Clamp01(value);
                break;
            case PedalType.JoystickYNegative:
                _input = Mathf.Clamp01(-value);
                break;
        }
    }
    
    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) {
        if (!InVR || args.handType != hand || !InControl) return;

        switch (pedalType) {
            case PedalType.JoystickXPositive:
                _input = Mathf.Clamp01(value);
                break;
            case PedalType.JoystickXNegative:
                _input = Mathf.Clamp01(-value);
                break;
        }
    }
}

public enum PedalType {
    Trigger,
    JoystickXPositive,
    JoystickYPositive,
    JoystickXNegative,
    JoystickYNegative,
}