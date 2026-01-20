using UdonSharp;

using UnityEngine;

using VRC.SDKBase;


[RequireComponent(typeof(VRCStation)), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleStation : UdonSharpBehaviour {
    public bool           InControl => Networking.LocalPlayer.playerId == owner;
    public int            owner = -1;
    public VehicleInput[] inputs;

    public override void Interact() { Networking.LocalPlayer.UseAttachedStation(); }

    public override void OnStationEntered(VRCPlayerApi player) {
        owner = player.playerId;
        foreach (var input in inputs) input.TakeControl(this);
    }

    public override void OnStationExited(VRCPlayerApi player) {
        owner = -1;
        foreach (var input in inputs) input.RevokeControl();
    }
}