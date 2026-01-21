using UdonSharp;

using UnityEngine;

using VRC.SDKBase;


[RequireComponent(typeof(VRCStation)), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleStation : UdonSharpBehaviour {
    public Vehicle        vehicle;
    public bool           InControl => Networking.LocalPlayer.playerId == owner;
    public int            owner = -1;
    public VehicleInput[] inputs;

    public GameObject[] enableOnEnter;

    public override void Interact() { Networking.LocalPlayer.UseAttachedStation(); }

    public override void OnStationEntered(VRCPlayerApi player) {
        if (vehicle) Networking.SetOwner(player, vehicle.gameObject);
        owner = player.playerId;
        foreach (var input in inputs) input.TakeControl(this);
        foreach (var obj in enableOnEnter) obj.SetActive(true);
    }

    public override void OnStationExited(VRCPlayerApi player) {
        owner = -1;
        foreach (var input in inputs) input.RevokeControl();
        foreach (var obj in enableOnEnter) obj.SetActive(false);
    }
}