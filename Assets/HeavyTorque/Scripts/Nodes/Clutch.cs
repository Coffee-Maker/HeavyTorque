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
    public               float        inertia;           // kg·m²

    public float AngularVelocity         { get; private set; } // radians/s
    public float RelativeAngularVelocity => AngularVelocity - output.GetUpstreamAngularVelocity();

    private float _appliedTorque;
    public  float Engagement => 1 - engagementInput.ReadFloat();

    public float LastTorqueApplied { get; private set; }
    public float MaxTorque         => friction * clampForce * Engagement;

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        var selfInertia = GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);
        AngularVelocity += _appliedTorque * deltaTime / selfInertia;
        _appliedTorque  =  0;

        var outputInertia = GetInertia(InertiaFrom.Output, InertiaDirection.Downstream);
        var totalInertia  = selfInertia + outputInertia;

        var relativeVelocity = RelativeAngularVelocity;

        LastTorqueApplied = Clamp(MaxTorque * deltaTime, 0, Abs(relativeVelocity * totalInertia * 0.5f))
            * Sign(relativeVelocity);

        AngularVelocity -= LastTorqueApplied / selfInertia;
        output.ApplyDownstreamTorque(LastTorqueApplied, TorqueMode.Impulse);

        stopwatch.Stop();
        vehicle.clutchTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        switch (forceMode) {
            case TorqueMode.Force:
                _appliedTorque += torqueForce;
                break;
            case TorqueMode.Impulse:
                AngularVelocity += torqueForce / GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);
                break;
        }
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        var engagement = engagementInput ? engagementInput.ReadFloat() : 0f;

        if (direction == InertiaDirection.Upstream)
            return from == InertiaFrom.Input
                ? input
                    ? input.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream) + inertia
                    : inertia
                : inertia;

        var outputInertia = output ? output.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) : 0;
        return from == InertiaFrom.Input ? outputInertia * engagement + inertia : outputInertia + inertia;
    }

    public override float GetDownstreamAngularVelocity() => output.GetUpstreamAngularVelocity();

    public override float GetUpstreamAngularVelocity() => AngularVelocity;
}