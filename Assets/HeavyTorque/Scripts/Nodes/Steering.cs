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
    public float angularMomentum;
    public float AngularVelocity => angularMomentum / GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);

    public VehicleInput steeringInput;

    private float _appliedTorque;

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        var steeringAngleInput = steeringInput ? steeringInput.ReadFloat() : 0f;
        steeringAngleInput = Clamp(steeringAngleInput, -maxSteeringAngle, maxSteeringAngle);
        var targetDelta = steeringAngleInput - angle;

        // Apply manual torque so we reach the target angle with 0 velocity
        var timeToStop   = Abs(angularMomentum) / manualTorque;
        var timeToTarget = Approximately(angularMomentum, 0) ? float.MaxValue : targetDelta / AngularVelocity;

        var downstreamInertia   = GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);
        var clampedManualTorque = Min(manualTorque * deltaTime, Abs(targetDelta * downstreamInertia / deltaTime - angularMomentum));
        angularMomentum += clampedManualTorque * (timeToStop < timeToTarget ? Sign(angularMomentum) : -Sign(angularMomentum));

        // Apply external torque
        angularMomentum += _appliedTorque * deltaTime;
        _appliedTorque  =  0;

        angle += angularMomentum * deltaTime;

        if (Abs(angle) > maxSteeringAngle) {
            angle           =  Sign(angle) * maxSteeringAngle;
            angularMomentum *= 0.5f;
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
                angularMomentum += torqueForce;
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

    public override float GetDownstreamAngularVelocity() => angularMomentum;

    public override float GetUpstreamAngularVelocity() => angularMomentum;
}