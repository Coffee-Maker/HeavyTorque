#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

using System;
using System.Diagnostics;

using UdonSharp;

using UnityEngine;

using static UnityEngine.Mathf;


/// <summary>
/// Represents a driven or undriven wheel that can receive torque and steering.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Wheel : VehicleNodeWithTorque {
    // Shape parameters of the Pacekja model
    private const float CShapeLongitudinal = 1.65f;
    private const float CShapeLateral      = 1.3f;

    [Header("Setup")]
    public VehicleNodeWithTorque connection;
    public Steering   steering;
    public Suspension suspension;

    public GameObject visual;

    [Header("Properties")]
    [Tooltip("Radius from axle to contact point in meters")]
    public float radius = 0.5f;                              // m
    public                float mass                = 20f;   // kg
    public                float turnInertia         = 1f;    // kg·m²
    public                float axleFrictionForce   = 1f;    // Nm/s
    public                float brakeForce          = 1500f; // Nm
    [Range(1, 10)] public float frictionCoefficient = 1f;

    [Range(4,    12)]   public float bStiffness = 8f;
    [Range(0.1f, 1.9f)] public float dPeak      = 1f;
    [Range(-10,  1)]    public float eCurvature = 0.97f;

    [Range(2,    12)]   public float bStiffnessLateral = 4f;
    [Range(0.1f, 1.9f)] public float dPeakLateral      = 1f;
    [Range(-10,  1)]    public float eCurvatureLateral = -4f;

    [Header("Aligning Torque")]
    [Range(0,   12)] public float bAligning = 2;
    [Range(2,   12)]   public float cAligning = 2.2f;
    [Range(.1f, 1.9f)] public float dAligning = .4f;
    [Range(-10, 1)]    public float eAligning = -7;

    // Info
    public float        AngularMomentum => AngularVelocity * UpstreamInertia; // kg·m²/s
    public float        AngularVelocity { get; private set; }                 // radians/s
    public float        Rpm             => AngularVelocity * 60 / (2 * PI);
    public float        GroundSpeed     => AngularVelocity * radius; // m/s
    public float        angle;
    public float        rotation;
    public VehicleInput brakeInput;

    public Vector3 LongitudinalDirection => Vector3.Cross(suspension.HitInfo.point - transform.position, transform.right).normalized;
    public Vector3 LateralDirection      => transform.right;

    // Computed properties
    public float SpinInertia => 0.5f * mass * radius * radius; // kg·m²

    private float UpstreamInertia => GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);

    private float _appliedTorque;

    // Debug data
    public float LongitudinalSlipRatio { get; private set; }
    public float LongitudinalForce     { get; private set; }
    public float LateralSlipAngle      { get; private set; }
    public float LateralForce          { get; private set; }

    public float FrictionLimit => Max(0, suspension.lastForce.magnitude * frictionCoefficient);

    private float _lastRotation;
    private float _rotationUpdateTime;

    private void OnValidate() {
        if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
    }

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        // Wheel torque
        AngularVelocity         += _appliedTorque / UpstreamInertia * deltaTime;
        _appliedTorque          =  0;
        transform.localRotation =  Quaternion.AngleAxis(angle, transform.up);

        // Handle friction forces
        if (suspension.contacting && !Approximately(0, FrictionLimit)) {
            var axleVelocity           = vehicle.Rigidbody.GetPointVelocity(transform.position);
            var longitudinalAxleSpeed  = Vector3.Dot(axleVelocity, LongitudinalDirection);
            var lateralAxleSpeed       = Vector3.Dot(axleVelocity, transform.right);
            var wheelSpeed             = AngularVelocity * radius;
            var relativeGroundVelocity = wheelSpeed - longitudinalAxleSpeed;

            var longitudinalModelBlend = InverseLerp(0.1f, 0.2f, Abs(relativeGroundVelocity));
            var lateralModelBlend      = InverseLerp(0.1f, 0.2f, Abs(lateralAxleSpeed));

            // Calculate slips
            LongitudinalSlipRatio = GetSlipRatio(longitudinalAxleSpeed, wheelSpeed) * longitudinalModelBlend;
            LateralSlipAngle      = Atan2(-lateralAxleSpeed, longitudinalAxleSpeed) * lateralModelBlend;

            // Calculate friction forces
            // Represents the force needed to change the wheel speed to match the ground speed in one tick
            var suspensionForceMag  = suspension.lastForce.magnitude;
            var longitudinalSimple  = relativeGroundVelocity * UpstreamInertia / (radius * radius) / deltaTime;
            var longitudinalPacejka = suspensionForceMag * Pacejka(LongitudinalSlipRatio, bStiffness, CShapeLongitudinal, dPeak, eCurvature);
            longitudinalPacejka *= frictionCoefficient;
            longitudinalPacejka =  Min(Abs(longitudinalPacejka), Abs(longitudinalSimple)) * Sign(longitudinalPacejka);
            var lateralGravityForce = -Vector3.Dot(Physics.gravity.normalized, transform.right) * suspensionForceMag;
            var lateralSimple       = suspensionForceMag * -lateralAxleSpeed + lateralGravityForce;
            var lateralPacejka      = suspensionForceMag * Pacejka(LateralSlipAngle, bStiffnessLateral, CShapeLateral, dPeakLateral, eCurvatureLateral);
            lateralPacejka *= frictionCoefficient;
            lateralPacejka += lateralGravityForce;
            // lateralPacejka =  Min(Abs(lateralPacejka), Abs(lateralSimple)) * Sign(lateralPacejka);

            CombineFrictionForces(
                FrictionLimit,
                Lerp(longitudinalSimple, longitudinalPacejka, longitudinalModelBlend),
                Lerp(lateralSimple,      lateralPacejka,      lateralModelBlend),
                out var combinedLongitudinal,
                out var combinedLateral
            );

            LongitudinalForce = combinedLongitudinal;
            LateralForce      = combinedLateral;

            // Apply forces to the vehicle
            vehicle.Rigidbody.AddForceAtPosition(LongitudinalDirection * (LongitudinalForce * deltaTime), transform.position, ForceMode.Impulse);
            AngularVelocity -= LongitudinalForce * radius * deltaTime / UpstreamInertia;
            vehicle.Rigidbody.AddForceAtPosition(transform.right * (LateralForce * deltaTime), transform.position, ForceMode.Impulse);

            // Aligning torque
            var bSlipSpeed     = bAligning * lateralAxleSpeed;
            var aligningTorque = dAligning * suspension.lastForce.magnitude * Sin(cAligning * Atan(bSlipSpeed - eAligning * (bSlipSpeed - Atan(bSlipSpeed))));
            if (steering) steering.ApplyDownstreamTorque(-aligningTorque * Abs(lateralAxleSpeed), TorqueMode.Force);
        }

        // Axle friction
        // TODO: Without axle friction the wheel slowly accelerates
        // AngularMomentum -= Clamp(axleFrictionForce * deltaTime, 0, Abs(AngularMomentum)) * Sign(AngularMomentum);

        // Braking
        if (Abs(AngularVelocity) > 0 && brakeInput) {
            var appliedBrakeChange = Min(Abs(brakeInput.ReadFloat() * brakeForce * deltaTime / UpstreamInertia), Abs(AngularVelocity)) * Sign(AngularVelocity);
            AngularVelocity -= appliedBrakeChange;
        }

        // Update rotation and visual
        _lastRotation       =  rotation;
        _rotationUpdateTime =  Time.realtimeSinceStartup;
        rotation            += AngularVelocity * deltaTime;

        stopwatch.Stop();
        vehicle.wheelTime += (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    private void LateUpdate() {
        if (visual)
            visual.transform.localRotation = Quaternion.AngleAxis(
                Lerp(_lastRotation, rotation, (Time.realtimeSinceStartup - _rotationUpdateTime) / Time.fixedDeltaTime) * Rad2Deg,
                Vector3.right
            );
    }

    /// <summary>
    /// Provides a numerically stable slip ratio value between the longitudinal axle speed and wheel speed.
    /// This is used for all friction force models.
    /// </summary>
    /// <param name="longitudinalAxleSpeed"></param>
    /// <param name="wheelSpeed"></param>
    /// <returns></returns>
    private static float GetSlipRatio(float longitudinalAxleSpeed, float wheelSpeed) {
        var denominator = Max(Abs(longitudinalAxleSpeed), Abs(wheelSpeed), 0.1f);
        return (wheelSpeed - longitudinalAxleSpeed) / denominator;
    }

    /// <summary>
    /// Pacejka's magic formula implementation.
    /// </summary>
    /// <param name="slip">Either slip ratio for longitudinal or slip angle for lateral forces.</param>
    /// <param name="b">Stiffness</param>
    /// <param name="c">Shape</param>
    /// <param name="d">Peak</param>
    /// <param name="e">Curvature</param>
    /// <returns>Calculated friction force.</returns>
    private static float Pacejka(float slip, float b, float c, float d, float e) {
        var bSlipRatio = b * slip;
        return d * Sin(c * Atan(bSlipRatio - e * (bSlipRatio - Atan(bSlipRatio))));
    }

    /// <summary>
    /// Combines longitudinal and lateral friction forces to not exceed the friction limit.
    /// </summary>
    /// <param name="frictionLimit">Maximum force that friction can provide. Calculated from normal force and friction coefficient.</param>
    /// <param name="longitudinalForce">Desired longitudinal friction force.</param>
    /// <param name="lateralForce">Desired lateral friction force.</param>
    /// <param name="newLongitudinal">The adjusted longitudinal friction force.</param>
    /// <param name="newLateral">The adjusted lateral friction force.</param>
    private static void CombineFrictionForces(
        float frictionLimit, float longitudinalForce, float lateralForce, out float newLongitudinal, out float newLateral
    ) {
        newLongitudinal = longitudinalForce;
        newLateral      = lateralForce;

        var length = Sqrt(newLongitudinal * newLongitudinal + newLateral * newLateral);

        if (length < frictionLimit) return;

        newLongitudinal = newLongitudinal / length * frictionLimit;
        newLateral      = newLateral / length * frictionLimit;
    }

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        switch (forceMode) {
            case TorqueMode.Force:
                _appliedTorque += torqueForce;
                break;
            case TorqueMode.Impulse:
                AngularVelocity += torqueForce / UpstreamInertia;
                break;
        }
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        if (direction == InertiaDirection.Downstream) return SpinInertia;
        return (connection ? connection.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream) : 0) + SpinInertia;
    }

    public override float GetDownstreamAngularVelocity() => AngularVelocity;

    public override float GetUpstreamAngularVelocity() => AngularVelocity;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        Handles.color = Color.red;
        Handles.DrawWireDisc(transform.position, transform.right, radius, 2f);
        var wheelRotation = transform.rotation * Quaternion.AngleAxis(rotation * Rad2Deg, Vector3.right);
        Handles.DrawLine(transform.position, transform.position + wheelRotation * Vector3.forward * radius, 2f);

        // Forces
        if (suspension && suspension.contacting) {
            Handles.color = Color.green;
            Handles.DrawWireArc(transform.position, transform.up, transform.forward, LateralSlipAngle * Rad2Deg, radius, 2f);
            Handles.DrawLine(transform.position, transform.position + transform.right * LateralForce / suspension.lastForce.magnitude, 5f);

            Handles.color = Color.blue;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * LongitudinalSlipRatio, 5f);
            Handles.color = Color.cyan;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * LongitudinalForce / suspension.lastForce.magnitude, 2f);
        }
    }
#endif
}