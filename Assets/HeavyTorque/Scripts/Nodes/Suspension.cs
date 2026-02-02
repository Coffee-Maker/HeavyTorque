#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

using UdonSharp;

using UnityEngine;

using static UnityEngine.Mathf;


[ExecuteInEditMode, UdonBehaviourSyncMode(BehaviourSyncMode.None),]
public class Suspension : VehicleNode {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private static Texture2D SuspensionIcon => Resources.Load<Texture2D>("HeavyTorque/UI/Suspension");
#endif
    private bool _pinDebug;

    public Wheel wheel;

    public LayerMask groundLayer;
    public float     restLength = 0.5f;
    public float     stiffness  = 3000f;
    public float     damping    = 450f;

    public float currentLength;

    public float MaxForce => stiffness * restLength;

    public RaycastHit HitInfo;
    public bool       contacting;

    [HideInInspector] public Vector3 lastForce;

    private void Awake() { currentLength = restLength; }

    public override void Tick(float deltaTime) {
        contacting = Physics.Raycast(transform.position, -transform.up, out HitInfo, restLength + wheel.radius, groundLayer);

        if (contacting) {
            var newLength = Max(0, HitInfo.distance - wheel.radius);
            var velocity  = (newLength - currentLength) / deltaTime;
            currentLength = newLength;
            var dampingForce = Max(0, velocity * damping);
            var force        = Max(0, stiffness * (restLength - currentLength) - dampingForce);
            lastForce = HitInfo.normal * (force * Vector3.Dot(HitInfo.normal, transform.up));
            vehicle.Rigidbody.AddForceAtPosition(lastForce * deltaTime, HitInfo.point, ForceMode.Impulse);
        }
        else {
            currentLength = restLength;
        }
    }

    private void LateUpdate() { UpdateVisual(); }

    public void UpdateVisual() {
        if (!wheel) return;
        wheel.transform.position = transform.position - transform.up * currentLength;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        Handles.color = Color.grey;
        Handles.DrawLine(transform.position, transform.position - transform.up * restLength, 3);

        if (!wheel) return;
        Handles.color = Color.red;
        Handles.DrawLine(transform.position, wheel.transform.position, 3);
    }
#endif
}