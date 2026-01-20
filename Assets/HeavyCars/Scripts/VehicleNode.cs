using UdonSharp;


/// <summary>
/// Represents a generic vehicle node in the vehicle system that receives simulation ticks.
/// </summary>
public class VehicleNode : UdonSharpBehaviour {
    public virtual void Tick(float deltaTime) { }
}