
using System;

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleRespawner : UdonSharpBehaviour
{
    public Vehicle vehicle;
    
    private Vector3 _respawnPosition;
    private Quaternion _respawnRotation;

    private void Start() {
        _respawnPosition = vehicle.transform.position;
        _respawnRotation = vehicle.transform.rotation;
    }

    public override void Interact() {
        Networking.SetOwner(Networking.LocalPlayer, vehicle.gameObject);
        vehicle.transform.position  = _respawnPosition; 
        vehicle.transform.rotation  = _respawnRotation;
        
        vehicle.GetComponent<Rigidbody>().velocity = Vector3.zero;
        vehicle.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    }
}
