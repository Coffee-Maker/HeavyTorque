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

    private void Start() {
        _vehicleNodes = GetComponentsInChildren<VehicleNode>();

        foreach (var node in _vehicleNodes)
            node.vehicle = this;

        Rigidbody.drag        = 0;
        Rigidbody.angularDrag = 0;
    }

    private void FixedUpdate() {
        if (doImpulse) {
            Rigidbody.AddForce(impulseForce, ForceMode.Impulse);
            doImpulse = false;
        }

        var dt = Time.fixedDeltaTime / substeps;

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < substeps; i++)
            foreach (var node in _vehicleNodes)
                node.Tick(dt);

        stopwatch.Stop();
        totalTime += (float)stopwatch.Elapsed.TotalMilliseconds;
        frames++;
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmos() {
        DrawHandleContent(transform.position, VehicleIcon, Color.red, new GUIContent($"V {Rigidbody.velocity.magnitude * 3.6f:0} km/h"), ref _pinDebug);
    }

    /// <summary>
    /// Draws a handle with an icon at the given position. When hovered or pinned, it shows additional content.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="icon"></param>
    /// <param name="color"></param>
    /// <param name="hoverContent"></param>
    /// <param name="pinDebug"></param>
    /// <returns>True if the element is being hovered</returns>
    public static bool DrawHandleContent(Vector3 position, Texture2D icon, Color color, GUIContent hoverContent, ref bool pinDebug) {
        var hoverStyle = new GUIStyle {
            alignment     = TextAnchor.MiddleLeft,
            fontSize      = 10,
            fontStyle     = pinDebug ? FontStyle.Bold : FontStyle.Normal,
            imagePosition = ImagePosition.ImageLeft,
            normal        = { textColor = Color.white, },
        };

        var size      = pinDebug ? hoverStyle.CalcSize(hoverContent) : Vector2.one * 30;
        var screenPos = HandleUtility.WorldToGUIPoint(position);
        var rect      = new Rect(screenPos.x - size.x / 2, screenPos.y - size.y / 2, size.x, size.y);

        bool hovering;

        if (SceneView.currentDrawingSceneView) {
            var screenRect = SceneView.currentDrawingSceneView.position;
            var mousePos   = GUIUtility.GUIToScreenPoint(Event.current.mousePosition) - new Vector2(screenRect.x, screenRect.y);
            hovering = rect.Contains(mousePos);
        }
        else {
            var mousePos = Event.current.mousePosition;
            mousePos -= new Vector2(0, 40); // Adjust for game view title bar
            hovering =  rect.Contains(mousePos);
        }

        Handles.BeginGUI();
        Handles.color = Color.white;
        GUI.color     = Color.white;

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButton(0) && hovering) pinDebug = false;
        else if (Input.GetMouseButton(0) && hovering) pinDebug                               = true;

        if (!hovering && !pinDebug) {
            GUI.Label(rect, icon);
        }
        else {
            if (!pinDebug) rect.x += size.x / 2 + 5;
            rect.y    -= size.y / 2;
            GUI.color =  color;
            GUI.Label(rect, hoverContent, hoverStyle);
        }

        Handles.EndGUI();
        return hovering;
    }
#endif
}