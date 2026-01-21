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

    public float AngularMomentum         { get; private set; }                   // kg·m²/s
    public float AngularVelocity         => AngularMomentum / GetTotalInertia(); // radians/s
    public float RelativeAngularVelocity => AngularVelocity - output.GetUpstreamAngularVelocity();

    private float _appliedTorque;
    private float _engagement;

    public override void Tick(float deltaTime) {
        var newEngagement = 1 - (engagementInput ? engagementInput.ReadFloat() : 0f);
        var engagementDelta = newEngagement - _engagement;
        _engagement = newEngagement;
        
        var stopwatch = Stopwatch.StartNew();
        
        AngularMomentum += _appliedTorque * deltaTime;
        _appliedTorque  =  0;

        var relativeVelocity = RelativeAngularVelocity;
        var totalInertia     = GetTotalInertia();

        var clutchTorque = Clamp(friction * clampForce * _engagement * deltaTime, 0, Abs(relativeVelocity * totalInertia))
            * Sign(relativeVelocity)
            * 0.5f;

        AngularMomentum -= clutchTorque;
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
                AngularMomentum += torqueForce;
                break;
        }
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        if (direction == InertiaDirection.Upstream) {
            var inputInertia = input ? input.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream) : 0;
            return from == InertiaFrom.Input ? inputInertia : inputInertia * _engagement;
        }

        var outputInertia = output ? output.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) : 0;
        return from == InertiaFrom.Output ? outputInertia : outputInertia * _engagement;
    }

    public override float GetDownstreamAngularVelocity() => AngularVelocity;

    public override float GetUpstreamAngularVelocity() => AngularVelocity;
}