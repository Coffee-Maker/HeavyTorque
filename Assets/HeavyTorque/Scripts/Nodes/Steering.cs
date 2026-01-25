using System.Diagnostics;

using UdonSharp;

using static UnityEngine.Mathf;


/// <summary>
/// Represents a steering node that controls the steering angle of connected wheels.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Steering : VehicleNodeWithTorque {
    public Wheel[] wheels;

    public float manualTorque;
    public float maxSteeringAngle = 30f;
    public float angle;
    public float AngularVelocity { get; private set; } // radians/s
    public float AngularMomentum => AngularVelocity * GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);

    public VehicleInput steeringInput;

    private float _appliedTorque;

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        var steeringAngleInput = steeringInput ? steeringInput.ReadFloat() : 0f;
        steeringAngleInput = Clamp(steeringAngleInput, -maxSteeringAngle, maxSteeringAngle);
        var targetDelta = steeringAngleInput - angle;

        // Apply external torque
        var downstreamInertia   = GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);
        AngularVelocity += _appliedTorque * deltaTime / downstreamInertia;
        _appliedTorque  =  0;
        
        // Apply manual torque so we reach the target angle with 0 velocity
        var timeToStop   = Abs(AngularMomentum) / manualTorque;
        var timeToTarget = Approximately(AngularVelocity, 0) ? float.MaxValue : targetDelta / AngularVelocity;
        var clampedManualChange = Min(manualTorque * deltaTime / downstreamInertia, Abs(targetDelta / deltaTime - AngularVelocity));
        AngularVelocity += clampedManualChange * (timeToStop < timeToTarget ? Sign(AngularVelocity) : -Sign(AngularVelocity));

        angle += AngularVelocity * deltaTime;

        if (Abs(angle) > maxSteeringAngle) {
            angle           =  Sign(angle) * maxSteeringAngle;
            AngularVelocity *= 0.5f;
        }

        foreach (var wheel in wheels) wheel.angle = angle;

        stopwatch.Stop();
        vehicle.steeringTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        switch (forceMode) {
            case TorqueMode.Force:
                _appliedTorque += torqueForce;
                break;
            case TorqueMode.Impulse:
                AngularVelocity += torqueForce / GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);
                break;
        }
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        if (direction == InertiaDirection.Downstream) {
            var inertia                           = 0f;
            foreach (var wheel in wheels) inertia += wheel.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);
            return inertia;
        }

        return 0f;
    }

    public override float GetDownstreamAngularVelocity() => AngularVelocity;

    public override float GetUpstreamAngularVelocity() => AngularVelocity;
}