using System;

using UdonSharp;

using UnityEditor;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon.Common;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SteeringWheel : VehicleInput {
    public                        Steering steering;
    public                        KeyCode  leftKey  = KeyCode.A;
    public                        KeyCode  rightKey = KeyCode.D;
    public                        string   vrAxis   = "Oculus_CrossPlatform_PrimaryThumbstick";
    public                        bool     supportsGamepad;
    public                        string   gamepadAxis;
    [Tooltip("Degrees/s")] public float    turnSpeed       = 100;
    public                        float    turnSmoothSpeed = 5f;

    public float wheelRadius = 0.15f;
    public float grabDistanceThreshold;
    public float maxRotation = 280f;

    public Transform steeringWheelVisual;
    public Transform leftDebugOrb;
    public Transform rightDebugOrb;

    private float _input;

    private float _keyboardInput;
    private float _smoothKeyboardInput;
    private float _gamepadInput;
    private float _vrJoystickInput;
    private float _vrInput;

    private bool  _grabbingLeft,  _grabbingRight;
    private float _grabLeftAngle, _grabRightAngle;
    private float _leftHandAngle, _rightHandAngle;

    private void LateUpdate() {
        if (!InControl) return;

        _vrJoystickInput = Input.GetAxisRaw(vrAxis) * maxRotation;

        var targetAngle = ((Input.GetKey(leftKey) ? -1f : 0) + (Input.GetKey(rightKey) ? 1f : 0)) * maxRotation;
        _keyboardInput = MoveTowards(_keyboardInput, targetAngle, turnSpeed * Time.deltaTime);
        _smoothKeyboardInput = Lerp(_smoothKeyboardInput, _keyboardInput, Time.deltaTime * turnSmoothSpeed);

        if (supportsGamepad) _gamepadInput = Input.GetAxisRaw(gamepadAxis) * maxRotation;

        if (_grabbingLeft) {
            var grabAngle  = GetGrabAngle(Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position);
            var angleDelta = DeltaAngle(_grabLeftAngle, grabAngle);
            _leftHandAngle += angleDelta;
            _grabLeftAngle =  grabAngle;

            var wheelPosition = transform.InverseTransformPoint(Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position);
            wheelPosition.z                 = 0;
            wheelPosition                   = wheelPosition.normalized * wheelRadius;
            leftDebugOrb.transform.position = transform.TransformPoint(wheelPosition);
        }

        if (_grabbingRight) {
            var grabAngle  = GetGrabAngle(Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position);
            var angleDelta = DeltaAngle(_grabRightAngle, grabAngle);
            _rightHandAngle += angleDelta;
            _grabRightAngle =  grabAngle;

            var wheelPosition = transform.InverseTransformPoint(Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position);
            wheelPosition.z                  = 0;
            wheelPosition                    = wheelPosition.normalized * wheelRadius;
            rightDebugOrb.transform.position = transform.TransformPoint(wheelPosition);
        }

        _vrInput                          = GetVRAngle();
        _input                            = Clamp(_smoothKeyboardInput + _gamepadInput + _vrJoystickInput + _vrInput, -maxRotation, maxRotation);
        _input                            = _input / maxRotation * steering.maxSteeringAngle;
        
        steeringWheelVisual.localRotation = Quaternion.AngleAxis(steering.angle / steering.maxSteeringAngle * maxRotation, Vector3.forward);
    }

    private float GetVRAngle() {
        if (!_grabbingLeft && !_grabbingRight) return 0f;

        var angle  = 0f;
        var weight = 0;

        if (_grabbingLeft) {
            angle += _leftHandAngle;
            weight++;
        }

        if (_grabbingRight) {
            angle += _rightHandAngle;
            weight++;
        }

        angle /= weight;
        return Clamp(angle, -maxRotation, maxRotation);
    }

    public override float ReadFloat() => _input;

    public override int ReadInt() => (int)_input;

    private float GetGrabAngle(Vector3 globalHandPos) {
        var localHandPos = transform.InverseTransformPoint(globalHandPos);
        return Atan2(localHandPos.y, localHandPos.x) * Rad2Deg;
    }

    public override void InputGrab(bool value, UdonInputEventArgs args) {
        if (!value) {
            if (args.handType == HandType.LEFT) _grabbingLeft = false;
            else _grabbingRight                               = false;

            return;
        }

        if (!InControl || !InVR) return;

        var trackingData =
            Networking.LocalPlayer.GetTrackingData(args.handType == HandType.LEFT
                ? VRCPlayerApi.TrackingDataType.LeftHand
                : VRCPlayerApi.TrackingDataType.RightHand);

        var localHandPos     = transform.InverseTransformPoint(trackingData.position);
        var nearestGrabPoint = Vector3.ProjectOnPlane(localHandPos, transform.forward).normalized * wheelRadius;
        var distance         = Vector3.Distance(nearestGrabPoint, localHandPos);
        if (distance > grabDistanceThreshold) return;
        var grabAngle = GetGrabAngle(trackingData.position);

        if (args.handType == HandType.LEFT) {
            _leftHandAngle = GetVRAngle();
            _grabbingLeft  = true;
            _grabLeftAngle = grabAngle;
        }
        else {
            _rightHandAngle = GetVRAngle();
            _grabbingRight  = true;
            _grabRightAngle = grabAngle;
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(steeringWheelVisual.position, steeringWheelVisual.forward, wheelRadius);
    }
#endif
}