using System;

using UdonSharp;

using UnityEngine;

using VRC.Udon.Common;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SteeringWheel : VehicleInput {
    public                        Steering steering;
    public                        KeyCode  leftKey   = KeyCode.A;
    public                        KeyCode  rightKey  = KeyCode.D;
    [Tooltip("Degrees/s")] public float    turnSpeed = 100;

    private float _input;

    private void Update() {
        if (InVR || !InControl) return;
        
        var targetAngle = ((Input.GetKey(leftKey) ? -1f : 0) + (Input.GetKey(rightKey) ? 1f : 0)) * steering.maxSteeringAngle;
        _input = Mathf.MoveTowards(_input, targetAngle, turnSpeed * Time.deltaTime);
    }

    public override float ReadFloat() => _input;

    public override int ReadInt() => (int)_input;

    public override void InputGrab(bool value, UdonInputEventArgs args) {
        
    }
}