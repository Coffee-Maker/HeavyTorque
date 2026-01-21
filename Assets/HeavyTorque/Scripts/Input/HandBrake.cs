using System;

using UdonSharp;

using UnityEngine;

using VRC.Udon.Common;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HandBrake : VehicleInput {
    public KeyCode toggleKey = KeyCode.Space;
    public bool    _input;

    private void Update() {
        if (InVR || !InControl) return;
        if (Input.GetKeyDown(toggleKey)) _input = !_input;
    }

    public override float ReadFloat() => _input ? 1f : 0f;

    public override int ReadInt() => _input ? 1 : 0;

    public override void InputJump(bool value, UdonInputEventArgs args) {
        if (!InControl || !InVR) return;
        if (value) _input = !_input;
    }
}