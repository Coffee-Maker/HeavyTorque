using UdonSharp;

using VRC.SDKBase;


public abstract class VehicleInput : UdonSharpBehaviour {
    private VRCPlayerApi _localPlayer;
    protected VRCPlayerApi LocalPlayer {
        get {
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            return _localPlayer;
        }
    }

    protected bool InVR => LocalPlayer.IsUserInVR();

    private VehicleStation _station;
    public  bool           InControl => _station != null && _station.InControl;

    public void TakeControl(VehicleStation station) {
        _station = station;
        OnTakeControl();
    }

    public void RevokeControl() {
        _station = null;
        OnRevokeControl();
    }

    protected virtual void OnTakeControl() { }

    protected virtual void OnRevokeControl() { }

    public abstract float ReadFloat();

    public abstract int ReadInt();
}