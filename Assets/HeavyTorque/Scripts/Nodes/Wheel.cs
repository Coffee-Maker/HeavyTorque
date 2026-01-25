#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

using System.Diagnostics;
using System.Linq;

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
    public float radius = 0.5f;             // m
    public float mass              = 20f;   // kg
    public float turnInertia       = 1f;    // kg·m²
    public float axleFrictionForce = 1f;    // Nm/s
    public float brakeForce        = 1500f; // Nm

    [Range(4,    12)]   public float bStiffness        = 8f;
    [Range(2,    12)]   public float bStiffnessLateral = 4f;
    [Range(0.1f, 1.9f)] public float dPeak             = 1f;
    [Range(0.1f, 1.9f)] public float dPeakLateral      = 1f;
    [Range(-10,  1)]    public float eCurvature        = 0.97f;
    [Range(-10,  1)]    public float eCurvatureLateral = -4f;

    [Header("Aligning Torque")]
    [Range(0,   12)] public float bAligning = 2;
    [Range(2,   12)]   public float cAligning = 2.2f;
    [Range(.1f, 1.9f)] public float dAligning = .4f;
    [Range(-10, 1)]    public float eAligning = -7;
    //            const float bAligning      = 4f;
    // const float cAligning      = 3.1f;
    // const float dAligning = .4f;
    // const float eAligning = 0.9f;


    // Info
    public float        AngularMomentum => AngularVelocity * UpstreamInertia; // kg·m²/s
    public float        AngularVelocity { get; private set; }                 // radians/s
    public float        Rpm             => AngularVelocity * 60 / (2 * PI);
    public float        GroundSpeed     => AngularVelocity * radius; // m/s
    public float        angle;
    public float        rotation;
    public VehicleInput brakeInput;

    public Vector3 LongitudinalDirection => transform.forward;
    public Vector3 LateralDirection      => transform.right;

    // Computed properties
    public float      SpinInertia   => 0.5f * mass * radius * radius; // kg·m²
    public Quaternion WheelRotation => transform.rotation * Quaternion.AngleAxis(rotation * Rad2Deg, Vector3.right);

    private float UpstreamInertia => GetInertia(InertiaFrom.Input, InertiaDirection.Upstream);

    private float _appliedTorque;
    private float _appliedTorqueLastTick;
    private float _forceLastTick;
    private float _slipLastTick;

    // Debug data
    public float LongitudinalSlipRatio { get; private set; }
    public float LongitudinalForce     { get; private set; }
    public float LateralSlipAngle      { get; private set; }
    public float LateralForce          { get; private set; }

    public float FrictionLimit => Max(0, suspension.Force);

    private void OnValidate() {
        if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
    }

    public override void Tick(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();

        transform.localRotation = Quaternion.AngleAxis(angle, transform.up);

        // Wheel torque and friction
        AngularVelocity        += _appliedTorque / UpstreamInertia * deltaTime;
        _appliedTorqueLastTick =  _appliedTorque;
        _appliedTorque         =  0;

        if (suspension.contacting) {
            var axleVelocity          = vehicle.Rigidbody.GetPointVelocity(transform.position);
            var longitudinalAxleSpeed = Vector3.Dot(axleVelocity, LongitudinalDirection);

            // Longitudinal slip
            LongitudinalSlipRatio = Abs(AngularVelocity * radius / Max(Abs(longitudinalAxleSpeed), 1e-5f) - 1)
                * Sign(AngularVelocity * radius - longitudinalAxleSpeed);
            LongitudinalSlipRatio = Lerp(_slipLastTick, LongitudinalSlipRatio, 0.5f);
            _slipLastTick         = LongitudinalSlipRatio;
            // LongitudinalSlipRatio *= InverseLerp(0.1f, 1f, Abs(longitudinalAxleSpeed));

            // Lateral slip
            var lateralAxleSpeed = Vector3.Dot(axleVelocity, transform.right);
            LateralSlipAngle = Vector2.SignedAngle(Vector2.up, new Vector2(lateralAxleSpeed, longitudinalAxleSpeed).normalized) * Deg2Rad;

            // Calculate combined forces
            PacejkaCombined(LongitudinalSlipRatio, LateralSlipAngle);
            LongitudinalForce = (_forceLastTick + LongitudinalForce) * 0.5f;
            _forceLastTick    = LongitudinalForce;

            // Apply forces to the vehicle
            vehicle.Rigidbody.AddForceAtPosition(LongitudinalDirection * (LongitudinalForce * deltaTime), transform.position, ForceMode.Impulse);

            LateralForce *= Clamp01(Abs(lateralAxleSpeed) * 2);
            vehicle.Rigidbody.AddForceAtPosition(transform.right * (LateralForce * deltaTime), transform.position, ForceMode.Impulse);

            // Recalculate slip after applying forces so we can clamp the wheel torque properly
            var tireRoadDifference = AngularVelocity * radius - longitudinalAxleSpeed;
            var maxChange          = Abs(tireRoadDifference / radius);
            AngularVelocity -= Clamp(LongitudinalForce * radius * deltaTime / UpstreamInertia, -maxChange, maxChange); // Apply an opposing torque to the wheel

            // Aligning torque
            var bSlipSpeed     = bAligning * lateralAxleSpeed;
            var aligningTorque = dAligning * suspension.Force * Sin(cAligning * Atan(bSlipSpeed - eAligning * (bSlipSpeed - Atan(bSlipSpeed))));
            if (steering) steering.ApplyDownstreamTorque(-aligningTorque * Abs(lateralAxleSpeed), TorqueMode.Force);
        }

        // Axle friction
        // TODO: Without axle friction the wheel slowly accelerates
        // AngularMomentum -= Clamp(axleFrictionForce * deltaTime, 0, Abs(AngularMomentum)) * Sign(AngularMomentum);

        // Braking
        if (Abs(AngularVelocity) > 0 && brakeInput) {
            var breakTorque        = brakeInput.ReadFloat() * brakeForce;
            var appliedBrakeTorque = Sign(AngularVelocity) * Min(Abs(breakTorque), Abs(AngularMomentum / deltaTime));
            AngularVelocity -= appliedBrakeTorque * deltaTime / UpstreamInertia;
        }

        // Update rotation and visual
        rotation                  += AngularVelocity * deltaTime;
        visual.transform.rotation =  WheelRotation;

        stopwatch.Stop();
        vehicle.wheelTime += (float)stopwatch.Elapsed.TotalMilliseconds;
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

    /// <summary>
    /// Provides the longitudinal and lateral friction forces using the Pacejka "Magic Formula" tire model.
    /// </summary>
    /// <param name="verticalForce">kN/s</param>
    /// <param name="slipRatio"></param>
    /// <param name="slipAngle">Angle between the wheel's facing direction and actual movement of axel along ground plane.</param>
    /// <param name="longitudinalModel">A value from 0 to 1 where 0 is a simplified and stable model and 1 is Pacejkas magic formula</param>
    /// <param name="lateralModel">A value from 0 to 1 where 0 is a simplified and stable model and 1 is Pacejkas magic formula</param>
    /// <param name="longitudinalForce">A force in kN/s that should be applied </param>
    /// <param name="lateralForce">kN/s</param>
    private void PacejkaCombined(float slipRatio, float slipAngle) {
        var frictionLimit = FrictionLimit;

        if (frictionLimit <= 0f) {
            LongitudinalForce = 0f;
            LateralForce      = 0f;
            return;
        }

        var bSlipRatio = bStiffness * slipRatio;
        LongitudinalForce = dPeak * Sin(CShapeLongitudinal * Atan(bSlipRatio - eCurvature * (bSlipRatio - Atan(bSlipRatio))));

        var bSlipAngle = bStiffnessLateral * slipAngle;
        LateralForce = dPeakLateral * Sin(CShapeLateral * Atan(bSlipAngle - eCurvatureLateral * (bSlipAngle - Atan(bSlipAngle))));

        var length = Sqrt(LongitudinalForce * LongitudinalForce + LateralForce * LateralForce);

        if (length > 1) {
            LongitudinalForce /= length;
            LateralForce      /= length;
        }

        LongitudinalForce *= frictionLimit;
        LateralForce      *= frictionLimit;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        Handles.color = Color.red;
        Handles.DrawWireDisc(transform.position, transform.right, radius, 2f);
        Handles.DrawLine(transform.position, transform.position + WheelRotation * Vector3.forward * radius, 2f);

        // Forces
        if (suspension && suspension.contacting) {
            Handles.color = Color.green;
            Handles.DrawWireArc(transform.position, transform.up, transform.forward, LateralSlipAngle * Rad2Deg, radius, 2f);
            Handles.DrawLine(transform.position, transform.position + transform.right * LateralForce / suspension.Force, 5f);

            Handles.color = Color.blue;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * LongitudinalSlipRatio, 5f);
            Handles.color = Color.cyan;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * LongitudinalForce / suspension.Force, 2f);
        }
    }
#endif
}