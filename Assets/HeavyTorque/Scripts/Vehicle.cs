using System;
using System.Diagnostics;

using UdonSharp;

using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif


/// <summary>
/// A representation of a vehicle that contains various vehicle nodes.
/// Anywhere that a vehicle node is the last in the chain should have its own internal angular momentum as the rest of the system inherits from it.
/// </summary>
[RequireComponent(typeof(Rigidbody)), UdonBehaviourSyncMode(BehaviourSyncMode.None),]
public class Vehicle : UdonSharpBehaviour {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public static Texture2D VehicleIcon => Resources.Load<Texture2D>("HeavyTorque/UI/Vehicle");
#endif
    private bool _pinDebug;

    public int substeps = 8;

    private Rigidbody _rb;
    public  Rigidbody Rigidbody => _rb ? _rb : _rb = GetComponent<Rigidbody>();

    [Header("Debug")]
    public bool doImpulse;
    public Vector3 impulseForce;

    private VehicleNode[] _vehicleNodes;

    public float totalTime;
    public float engineTime;
    public float clutchTime;
    public float transmissionTime;
    public float wheelTime;
    public float steeringTime;
    public int   frames;

    public Vector3 size = new Vector3(2f, 1f, 4f);

    private float _timescale = 1;

    private void Start() {
        _vehicleNodes = GetComponentsInChildren<VehicleNode>();

        foreach (var node in _vehicleNodes)
            node.vehicle = this;

        Rigidbody.drag                   = 0;
        Rigidbody.angularDrag            = 0;
        Rigidbody.useGravity             = false;
        Rigidbody.automaticInertiaTensor = false;

        Rigidbody.inertiaTensor = new Vector3(
            Rigidbody.mass * (size.y * size.y + size.x * size.x) / 12f,
            Rigidbody.mass * (size.x * size.x + size.z * size.z) / 12f,
            Rigidbody.mass * (size.x * size.x + size.y * size.y) / 12f
        );
    }

    private void FixedUpdate() {
        if (doImpulse) {
            Rigidbody.AddForce(impulseForce, ForceMode.Impulse);
            doImpulse = false;
        }

        var velocity        = Rigidbody.velocity.magnitude / _timescale;
        var angularVelocity = Rigidbody.angularVelocity.magnitude / _timescale;
        var newTimescale    = Input.GetKey(KeyCode.LeftControl) ? 0.1f : 1f;
        _timescale                = Mathf.Lerp(_timescale, newTimescale, Time.fixedDeltaTime * 5f);
        Rigidbody.velocity        = Rigidbody.velocity.normalized * (velocity * _timescale);
        Rigidbody.angularVelocity = Rigidbody.angularVelocity.normalized * (angularVelocity * _timescale);

        Rigidbody.AddForce(Physics.gravity * _timescale, ForceMode.Acceleration);

        var dt = Time.fixedDeltaTime / substeps * _timescale;

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < substeps; i++)
            foreach (var node in _vehicleNodes) {
                node.Tick(dt);
            }

        stopwatch.Stop();
        totalTime += (float)stopwatch.Elapsed.TotalMilliseconds;
        frames++;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        Handles.matrix = transform.localToWorldMatrix;
        Handles.color  = Color.yellow;
        Handles.DrawLine(Rigidbody.centerOfMass, Rigidbody.centerOfMass + Vector3.up, 0.2f);
        Handles.DrawWireDisc(Rigidbody.centerOfMass, Vector3.up, 0.2f);

        Handles.color = Color.cyan;
        Handles.DrawWireCube(Vector3.zero, size);
    }
#endif
}