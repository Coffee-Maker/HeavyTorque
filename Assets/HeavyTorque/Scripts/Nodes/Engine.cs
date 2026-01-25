using System.Diagnostics;

using UdonSharp;

using UnityEngine;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Engine : VehicleNodeWithTorque {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public static Texture2D EngineIcon => Resources.Load<Texture2D>("HeavyTorque/UI/Engine");
#endif
    private bool _pinDebug;

    public VehicleNodeWithTorque output;
    public AnimationCurve        fullThrottle;         // X: RPM Y: Newton-meters (Nm)
    public AnimationCurve        noThrottle;           // X: RPM Y: Newton-meters (Nm)
    public float                 inertia       = 0.3f; // kg·m²
    public float                 frictionForce = 10f;  // Nm/s

    public float idleRpm  = 800f;
    public float limitRpm = 7000f;

    public VehicleInput throttleInput;

    public float Rpm    => output ? output.GetUpstreamAngularVelocity() * 60 / (2 * PI) : 0;
    public float Torque => SimpleThrottleModel(throttleInput.ReadFloat());

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        var idleThrottle                = 0f;
        if (Rpm < idleRpm) idleThrottle = 1 - Rpm / idleRpm + 0.5f;
        var throttle                    = Rpm > limitRpm ? 0 : Lerp(idleThrottle, 1, throttleInput.ReadFloat());
        var torque                      = SimpleThrottleModel(throttle);
        var systemAngularMomentum       = GetDownstreamAngularVelocity() * GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);
        var frictionTorque              = Clamp(frictionForce * deltaTime, 0, Abs(systemAngularMomentum)) * -Sign(systemAngularMomentum) / deltaTime;
        ApplyDownstreamTorque(torque + frictionTorque, TorqueMode.Force);

        stopwatch.Stop();
        vehicle.engineTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    public float SimpleThrottleModel(float throttle) => Lerp(noThrottle.Evaluate(Rpm), fullThrottle.Evaluate(Rpm), throttle);

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        if (output == null) return;
        output.ApplyDownstreamTorque(torqueForce, forceMode);
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        if (direction == InertiaDirection.Upstream) return inertia;
        return output ? output.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) + inertia : inertia;
    }

    public override float GetDownstreamAngularVelocity() => output ? output.GetDownstreamAngularVelocity() : 0;

    public override float GetUpstreamAngularVelocity() => GetDownstreamAngularVelocity();

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmos() {
        Vehicle.DrawHandleContent(transform.position,
            EngineIcon,
            Color.red,
            new GUIContent($"RPM: {Rpm:0}\nTorque: {fullThrottle.Evaluate(Rpm):0} Nm"),
            ref _pinDebug
        );
    }
#endif
}