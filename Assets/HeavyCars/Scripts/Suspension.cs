using System.Linq;

using UdonSharp;

using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif


[ExecuteInEditMode, UdonBehaviourSyncMode(BehaviourSyncMode.None),]
public class Suspension : VehicleNode {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private static Texture2D SuspensionIcon => Resources.Load<Texture2D>("HeavyCars/UI/Suspension");
#endif
    private bool _pinDebug;

    public Wheel wheel;

    public LayerMask groundLayer;
    public float     restLength = 0.5f;
    public float     stiffness  = 3000f;
    public float     damping    = 450f;

    public float currentLength;

    public float Force => stiffness * (restLength - currentLength);

    public float velocity;

    public RaycastHit HitInfo;
    public bool       contacting;

    private void Awake() { currentLength = restLength; }

    public override void Tick(float deltaTime) {
        velocity += Force / wheel.mass * deltaTime;
        velocity -= velocity * damping * deltaTime;

        // Apply suspension velocity
        currentLength += velocity * deltaTime;

        var hit = Physics.Raycast(transform.position, -transform.up, out HitInfo, currentLength + wheel.radius, groundLayer);
        contacting = hit && HitInfo.distance < currentLength + wheel.radius;

        if (contacting) {
            currentLength = HitInfo.distance - wheel.radius;

            var force = transform.up * (Force * deltaTime);
            wheel.vehicle.Rigidbody.AddForceAtPosition(force, transform.position, ForceMode.Impulse);
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
            new GUIContent($"{(currentLength / restLength - 1) * 100:0}%\nF {Force:0} N"),
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