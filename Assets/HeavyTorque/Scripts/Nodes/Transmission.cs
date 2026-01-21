using System.Diagnostics;

using UdonSharp;

using UnityEngine;


/// <summary>
/// Represents a transmission node that can apply gear ratios between a source and destination vehicle node.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Transmission : VehicleNodeWithTorque {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private static Texture2D TransmissionIcon => Resources.Load<Texture2D>("HeavyTorque/UI/Transmission");
#endif
    private bool _pinDebug;

    public const int NeutralGearIndex = 1;
    public const int ReverseGearIndex = 0;

    public VehicleNodeWithTorque source;
    public VehicleNodeWithTorque destination;
    [Tooltip("Gear ratios for each gear. First element is reverse, second is neutral (0), rest are forward gears.")]
    public float[] gearRatios = { -2.92f, 0f, 2.5f, 1.61f, 1.10f, 0.81f, 0.68f, };
    public int currentGear = 1; // Default to neutral
    public string CurrentGearName {
        get {
            switch (currentGear) {
                case ReverseGearIndex:
                    return "R";
                case NeutralGearIndex:
                    return "N";
                default: return (currentGear - 1).ToString();
            }
        }
    }

    public float GearRatio => gearRatios[currentGear];

    // Terminal angular momentum for neutral gear
    private float _appliedTorqueForce;
    public  float AngularMomentum => AngularVelocity * UpstreamInertia; // kg·m²/s
    public  float AngularVelocity { get; private set; }                 // radians/s

    private float UpstreamInertia => source.GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);

    private void OnValidate() {
        if (gearRatios == null || gearRatios.Length < 2) gearRatios = new float[] { -2.92f, 0f, 2.5f };
        gearRatios[1] = 0f; // Ensure neutral is always zero
    }

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        if (currentGear == NeutralGearIndex) AngularVelocity += _appliedTorqueForce * deltaTime / UpstreamInertia;
        _appliedTorqueForce = 0;

        stopwatch.Stop();
        vehicle.transmissionTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    public void SetGear(int gear) {
        if (gear == NeutralGearIndex)
            AngularVelocity = GetUpstreamAngularVelocity();

        currentGear = Mathf.Clamp(gear, 0, gearRatios.Length - 1);
    }

    public override void ApplyDownstreamTorque(float torque, TorqueMode forceMode) {
        // Handle neutral gear
        if (currentGear == NeutralGearIndex) {
            switch (forceMode) {
                case TorqueMode.Force:
                    _appliedTorqueForce += torque;
                    break;
                case TorqueMode.Impulse:
                    AngularVelocity += torque / UpstreamInertia;
                    break;
            }

            _appliedTorqueForce += torque;
            return;
        }

        // Handle normal gear operation
        if (destination == null) return;
        destination.ApplyDownstreamTorque(torque * GearRatio, forceMode);
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        if (currentGear == NeutralGearIndex) {
            if (from == InertiaFrom.Input && direction == InertiaDirection.Upstream) return source.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream);
            return 0;
        }

        var inertia = direction == InertiaDirection.Upstream
            ? source.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream)
            : destination.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream);

        // Check if going through the gear ratio
        if ((from == InertiaFrom.Input && direction == InertiaDirection.Downstream)
            || (from == InertiaFrom.Output && direction == InertiaDirection.Upstream)) {
            var ratioSquared = GearRatio * GearRatio;

            if (direction == InertiaDirection.Upstream) inertia *= ratioSquared;
            else inertia                                        /= ratioSquared;
        }

        return inertia;
    }

    public override float GetDownstreamAngularVelocity() => destination.GetUpstreamAngularVelocity();

    public override float GetUpstreamAngularVelocity() {
        if (currentGear == NeutralGearIndex) return AngularVelocity;
        return GetDownstreamAngularVelocity() * GearRatio;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmos() {
        Vehicle.DrawHandleContent(transform.position,
            TransmissionIcon,
            Color.red,
            new GUIContent($"{(currentGear == 0 ? "R" : currentGear == 1 ? "N" : (currentGear - 1).ToString())} ({GearRatio:0.00})"),
            ref _pinDebug
        );
    }
#endif
}