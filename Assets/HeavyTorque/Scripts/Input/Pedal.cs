using System;

using UdonSharp;

using UnityEngine;
using UnityEngine.Serialization;

using VRC.SDKBase;
using VRC.Udon.Common;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Pedal : VehicleInput {
    private float    _input;
    public  HandType hand;
    public  KeyCode  desktopKey;
    public  bool     supportsGamepad;
    public  string   vrAxis;
    public  string   gamepadAxis;
    public  float    desktopModeDuration = 1;

    public float automatedBlend; // 0 = manual, 1 = automated
    public float automatedInput;

    private float _keyInput;

    protected override void OnRevokeControl() { _input = 0f; }

    public override float ReadFloat() => Lerp(_input, automatedInput, automatedBlend);

    public override int ReadInt() => ReadFloat() > 0.5f ? 1 : 0;

    private void Update() {
        if (!InControl) return;
        _keyInput = MoveTowards(_keyInput, Input.GetKey(desktopKey) ? 1f : 0f, Time.deltaTime / desktopModeDuration);
        var vrInput   = string.IsNullOrEmpty(vrAxis) ? 0 :Input.GetAxisRaw(vrAxis);
        var gamepadInput = supportsGamepad ? Input.GetAxisRaw(gamepadAxis) : 0;
        _input = Max(vrInput, gamepadInput, _keyInput);
    }
}