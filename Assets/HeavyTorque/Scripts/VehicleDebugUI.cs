using TMPro;

using UdonSharp;

using UnityEngine;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleDebugUI : UdonSharpBehaviour {
    public Vehicle      vehicle;
    public Engine       engine;
    public Clutch       clutch;
    public Transmission transmission;

    public TextMeshProUGUI debugText;

    public RectTransform timeGraph;
    public RectTransform engineTime;
    public RectTransform clutchTime;
    public RectTransform transmissionTime;
    public RectTransform wheelTime;
    public RectTransform steeringTime;

    private void FixedUpdate() {
        var lines = new string[] {
            "<b>Vehicle</b>",
            $"Speed: {vehicle.Rigidbody.velocity.magnitude * 3.6:F1} km/h",
            "",
            "<b>Engine</b>",
            $"RPM: {engine.Rpm:F0} rpm",
            $"Torque: {engine.Torque:F1} Nm",
            "",
            "<b>Clutch</b>",
            $"Engagement: {clutch.engagementInput.ReadFloat():F2}",
            $"Transmission Relative Velocity: {clutch.RelativeAngularVelocity:F1} rad/s",
            "",
            "<b>Transmission</b>",
            $"Gear: {transmission.CurrentGearName} ({transmission.GearRatio:F2})",
            "",
            "",
            "<b>Times</b>",
            $"Total: {vehicle.totalTime / vehicle.frames:F2} ms",
            $"Engine: {vehicle.engineTime / vehicle.frames:F2} ms",
            $"Clutch: {vehicle.clutchTime / vehicle.frames:F2} ms",
            $"Transmission: {vehicle.transmissionTime / vehicle.frames:F2} ms",
            $"Wheel: {vehicle.wheelTime / vehicle.frames:F2} ms",
            $"Steering: {vehicle.steeringTime / vehicle.frames:F2} ms",
        };

        debugText.text = string.Join("\n", lines);
        
        var totalTime = vehicle.totalTime;
        var engineRatio = vehicle.engineTime / totalTime;
        var clutchRatio = vehicle.clutchTime / totalTime;
        var transmissionRatio = vehicle.transmissionTime / totalTime;
        var wheelRatio = vehicle.wheelTime / totalTime;
        var steeringRatio = vehicle.steeringTime / totalTime;
        var graphWidth = timeGraph.rect.width;
        engineTime.sizeDelta = new Vector2(graphWidth * engineRatio, engineTime.sizeDelta.y);
        clutchTime.sizeDelta = new Vector2(graphWidth * clutchRatio, clutchTime.sizeDelta.y);
        transmissionTime.sizeDelta = new Vector2(graphWidth * transmissionRatio, transmissionTime.sizeDelta.y);
        wheelTime.sizeDelta = new Vector2(graphWidth * wheelRatio, wheelTime.sizeDelta.y);
        steeringTime.sizeDelta = new Vector2(graphWidth * steeringRatio, steeringTime.sizeDelta.y);
    }
}