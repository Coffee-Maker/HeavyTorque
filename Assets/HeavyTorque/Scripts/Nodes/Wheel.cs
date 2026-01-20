#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

using System.Linq;

using UdonSharp;

using UnityEngine;
using UnityEngine.Serialization;

using static UnityEngine.Mathf;


/// <summary>
/// Represents a driven or undriven wheel that can receive torque and steering.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Wheel : VehicleNodeWithTorque {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public static Texture2D WheelIcon => Resources.Load<Texture2D>("HeavyTorque/UI/Wheel");
#endif
    private bool _pinDebug;

    // Shape parameters of the Pacekja model
    private const float CShapeLongitudinal = 1.65f;
    private const float CShapeLateral      = 1.3f;

    [Header("Setup")]
    public Vehicle vehicle;
    public VehicleNodeWithTorque connection;
    public Steering              steering;
    public Suspension            suspension;

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
    [Range(-10,  1)]    public float eCurvature        = 0.97f;

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
    public float AngularMomentum { get; private set; }                                                          // kg·m²/s
    public float AngularVelocity => AngularMomentum / GetInertia(InertiaFrom.Input, InertiaDirection.Upstream); // radians/s
    public float GroundSpeed     => AngularVelocity * radius;                                                   // m/s
    public float angle;
    public float rotation;
    public VehicleInput brakeInput;

    public Vector3 LongitudinalDirection => transform.forward;
    public Vector3 LateralDirection      => transform.right;

    // Computed properties
    public float      SpinInertia   => 0.5f * mass * radius * radius; // kg·m²
    public Quaternion WheelRotation => transform.rotation * Quaternion.AngleAxis(rotation * Rad2Deg, Vector3.right);

    private float _appliedTorque;
    private float _appliedTorqueLastTick;

    // Debug data
    private float _longitudinalSlipRatio;
    private float _longitudinalForce;
    private float _lateralSlipAngle;
    private float _lateralForce;

    private void OnValidate() {
        if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
    }

    public override void Tick(float deltaTime) {
        transform.localRotation = Quaternion.AngleAxis(angle, transform.up);

        // Wheel torque and friction
        AngularMomentum        += _appliedTorque * deltaTime;
        _appliedTorqueLastTick =  _appliedTorque;
        _appliedTorque         =  0;

        if (suspension.contacting) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var axleVelocity = vehicle.Rigidbody.GetPointVelocity(transform.position);

            // var hitInfo      = suspension.HitInfo;
            // var cylindricalPoint = Vector3.ProjectOnPlane(hitInfo.point - transform.position, transform.right) + transform.position;
            // LongitudinalDirection = Vector3.Cross(contactOffset, transform.right).normalized;

            var longitudinalAxleSpeed = Vector3.Dot(axleVelocity, LongitudinalDirection);

            // Longitudinal slip
            var centripetalVelocity = AngularVelocity * radius;
            var tireRoadDifference  = centripetalVelocity - longitudinalAxleSpeed;
            _longitudinalSlipRatio = Approximately(0, longitudinalAxleSpeed) ? 1e-5f : tireRoadDifference / Abs(longitudinalAxleSpeed);

            // Lateral slip
            var lateralAxleSpeed = Vector3.Dot(axleVelocity, transform.right);
            _lateralSlipAngle = Vector2.SignedAngle(Vector2.up, new Vector2(lateralAxleSpeed, longitudinalAxleSpeed).normalized) * Deg2Rad;

            // Calculate combined forces
            PacejkaCombined(Max(0, suspension.Force), _longitudinalSlipRatio, _lateralSlipAngle, out _longitudinalForce, out _lateralForce);
            _longitudinalForce *= Clamp01(Abs(tireRoadDifference));
            _lateralForce      *= Clamp01(Abs(lateralAxleSpeed) * 2);

            // Stop sliding at slow speeds
            var simpleLateralFriction = Vector3.Dot(Vector3.ProjectOnPlane(suspension.Force * suspension.transform.up, Vector3.up), transform.right);
            simpleLateralFriction =  Clamp(Abs(simpleLateralFriction), 0, dPeak * suspension.Force) * -Sign(simpleLateralFriction);
            _lateralForce         += simpleLateralFriction * (1 - Clamp01(Abs(lateralAxleSpeed * 2)));

            // Apply forces to the vehicle
            vehicle.Rigidbody.AddForceAtPosition(LongitudinalDirection * (_longitudinalForce * deltaTime), transform.position, ForceMode.Impulse);
            AngularMomentum -= _longitudinalForce * radius * deltaTime; // Apply an opposing torque to the wheel

            // TODO: Varify that this force should be applied in this direction
            vehicle.Rigidbody.AddForceAtPosition(transform.right * (_lateralForce * deltaTime), transform.position, ForceMode.Impulse);

            // Aligning torque
            var bSlipSpeed     = bAligning * lateralAxleSpeed;
            var aligningTorque = dAligning * suspension.Force * Sin(cAligning * Atan(bSlipSpeed - eAligning * (bSlipSpeed - Atan(bSlipSpeed))));
            if (steering) steering.ApplyDownstreamTorque(-aligningTorque * Abs(lateralAxleSpeed), TorqueMode.Force);
            
            stopwatch.Stop();
            vehicle.wheelTime += (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        // Axle friction
        // TODO: Without axle friction the wheel slowly accelerates
        AngularMomentum -= Clamp(axleFrictionForce * deltaTime, 0, Abs(AngularMomentum)) * Sign(AngularMomentum);

        // Braking
        if (Abs(AngularVelocity) > 0 && brakeInput) {
            var breakTorque        = brakeInput.ReadFloat() * brakeForce;
            var appliedBrakeTorque = Sign(AngularVelocity) * Min(Abs(breakTorque), Abs(AngularMomentum / deltaTime));
            AngularMomentum -= appliedBrakeTorque * deltaTime;
        }

        // Update rotation and visual
        rotation                  += AngularVelocity * deltaTime;
        visual.transform.rotation =  WheelRotation;
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
    private void PacejkaCombined(float verticalForce, float slipRatio, float slipAngle, out float longitudinalForce, out float lateralForce) {
        if (verticalForce <= 0f) {
            longitudinalForce = 0f;
            lateralForce      = 0f;
            return;
        }

        var bSlipRatio = bStiffness * slipRatio;
        longitudinalForce = Sin(CShapeLongitudinal * Atan(bSlipRatio - eCurvature * (bSlipRatio - Atan(bSlipRatio))));

        var bSlipAngle = bStiffnessLateral * slipAngle;
        lateralForce = Sin(CShapeLateral * Atan(bSlipAngle - eCurvature * (bSlipAngle - Atan(bSlipAngle))));

        var length = Sqrt(longitudinalForce * longitudinalForce + lateralForce * lateralForce);

        if (length > 1) {
            longitudinalForce /= length;
            lateralForce      /= length;
        }

        var frictionLimit = verticalForce * dPeak;
        longitudinalForce *= frictionLimit;
        lateralForce      *= frictionLimit;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    // Debug visuals
    private void OnDrawGizmos() {
        if (Selection.gameObjects.Contains(gameObject)) return;

        var hovered = Vehicle.DrawHandleContent(transform.position,
            WheelIcon,
            Color.red,
            new GUIContent($"ω {AngularVelocity:F1} rad/s\n v {GroundSpeed:F1} m/s\n Torque {_appliedTorqueLastTick:F2}"),
            ref _pinDebug
        );

        // State
        if (hovered || _pinDebug) ShowAdditionalGizmos();
    }

    private void OnDrawGizmosSelected() {
        var forcePinDebug = true;

        Vehicle.DrawHandleContent(transform.position,
            WheelIcon,
            Color.red,
            new GUIContent($"ω {AngularVelocity:F1} rad/s\n v {GroundSpeed:F1} m/s\n Torque {_appliedTorqueLastTick:F2}"),
            ref forcePinDebug
        );

        ShowAdditionalGizmos();
    }

    private void ShowAdditionalGizmos() {
        Handles.color = Color.red;
        Handles.DrawWireDisc(transform.position, transform.right, radius, 2f);
        Handles.DrawLine(transform.position, transform.position + WheelRotation * Vector3.forward * radius, 2f);

        // Forces
        if (suspension && suspension.contacting) {
            Handles.color = Color.green;
            Handles.DrawWireArc(transform.position, transform.up, transform.forward, _lateralSlipAngle * Rad2Deg, radius, 2f);
            Handles.DrawLine(transform.position, transform.position + transform.right * _lateralForce / suspension.Force, 5f);

            Handles.color = Color.blue;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * _longitudinalSlipRatio, 5f);
            Handles.color = Color.cyan;
            Handles.DrawLine(transform.position, transform.position + LongitudinalDirection * _longitudinalForce / suspension.Force, 2f);
        }
    }
#endif
}