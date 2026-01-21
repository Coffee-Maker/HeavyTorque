using System.Diagnostics;

using UdonSharp;

using UnityEngine;

using static UnityEngine.Mathf;


/// <summary>
/// Represents a clutch node that can engage or disengage torque transfer between an input and output vehicle node.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Clutch : VehicleNodeWithTorque {
    public VehicleNodeWithTorque input;
    public VehicleNodeWithTorque output;

    [Range(0, 1)] public float        friction   = 0.5f;
    public               float        clampForce = 600f; // Nm
    public               VehicleInput engagementInput;   // 0.0 = disengaged, 1.0 = fully engaged

    public float AngularVelocity         { get; private set; } // radians/s
    public float RelativeAngularVelocity => AngularVelocity - output.GetUpstreamAngularVelocity();

    private float AngularMomentum => AngularVelocity * GetTotalInertia(); // kg·m²/s

    private float _appliedTorque;
    private float Engagement => 1 - engagementInput.ReadFloat();

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        var totalInertia = GetTotalInertia();
        AngularVelocity += _appliedTorque * deltaTime / totalInertia;
        _appliedTorque  =  0;

        var relativeVelocity = RelativeAngularVelocity;

        var clutchTorque = Clamp(friction * clampForce * Engagement * deltaTime, 0, Abs(relativeVelocity * totalInertia))
            * Sign(relativeVelocity)
            * 0.5f;

        AngularVelocity -= clutchTorque / totalInertia;
        output.ApplyDownstreamTorque(clutchTorque / deltaTime, TorqueMode.Force);

        stopwatch.Stop();
        vehicle.clutchTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    private float GetTotalInertia() {
        var selfInertia   = GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);
        var outputInertia = Abs(GetInertia(InertiaFrom.Input, InertiaDirection.Downstream));
        return selfInertia + outputInertia;
    }

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        switch (forceMode) {
            case TorqueMode.Force:
                _appliedTorque += torqueForce;
                break;
            case TorqueMode.Impulse:
                AngularVelocity += torqueForce / GetTotalInertia();
                break;
        }
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        var engagement = engagementInput ? engagementInput.ReadFloat() : 0f;

        if (direction == InertiaDirection.Upstream) {
            var inputInertia = input ? input.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream) : 0;
            return from == InertiaFrom.Input ? inputInertia : inputInertia * engagement;
        }

        var outputInertia = output ? output.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) : 0;
        return from == InertiaFrom.Input ? outputInertia * engagement * engagement : outputInertia;
    }

    public override float GetDownstreamAngularVelocity() => AngularVelocity;

    public override float GetUpstreamAngularVelocity() => AngularVelocity;
}