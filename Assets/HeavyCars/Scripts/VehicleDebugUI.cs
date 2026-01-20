using TMPro;

using UdonSharp;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleDebugUI : UdonSharpBehaviour {
    public Vehicle vehicle;
    public Engine engine;
    public Clutch clutch;
    public Transmission transmission;
    
    public TextMeshProUGUI debugText;

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
            $"Engagement: {clutch.engagement:F2}",
            $"Transmission Relative Velocity: {clutch.RelativeAngularVelocity:F1} rad/s",
            "",
            "<b>Transmission</b>",
            $"Gear: {transmission.CurrentGearName} ({transmission.GearRatio:F2})",
        };
        
        debugText.text = string.Join("\n", lines);
    }
}
