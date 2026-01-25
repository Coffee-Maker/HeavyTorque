using UdonSharp;

using UnityEngine;


/// <summary>
/// Represents a generic vehicle node in the vehicle system that receives simulation ticks.
/// </summary>
public class VehicleNode : UdonSharpBehaviour {
    [HideInInspector] public Vehicle         vehicle;
    public                   NodeInfoDisplay infoDisplayPrefab;

    public virtual void Tick(float deltaTime) { }
}