using System.Linq;

using UdonSharp;

using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

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
            var newLength = HitInfo.distance - wheel.radius;
            var velocity  = (newLength - currentLength) / deltaTime;
            currentLength = newLength;
            var dampingForce = Max(0, velocity * damping);
            var force        = stiffness * (restLength - currentLength);
            lastForce = transform.up * (force - dampingForce);
            wheel.vehicle.Rigidbody.AddForceAtPosition(lastForce, transform.position);
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
    private void OnDrawGizmos() {
        if (Selection.gameObjects.Contains(gameObject)) return;

        var hovering = Vehicle.DrawHandleContent(transform.position,
            SuspensionIcon,
            Color.red,
            new GUIContent($"{(currentLength / restLength - 1) * 100:0}%\nF {lastForce.magnitude:0} N"),
            ref _pinDebug
        );

        if (hovering || _pinDebug) DrawAdditionalGizmos();
    }

    private void OnDrawGizmosSelected() { DrawAdditionalGizmos(); }

    private void DrawAdditionalGizmos() {
        Handles.color = Color.grey;
        Handles.DrawLine(transform.position, transform.position - transform.up * restLength, 3);

        if (!wheel) return;
        Handles.color = Color.red;
        Handles.DrawLine(transform.position, wheel.transform.position, 3);
    }
#endif
}