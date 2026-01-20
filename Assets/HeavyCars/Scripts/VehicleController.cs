using UdonSharp;

using UnityEngine;


/// <summary>
/// Controls a vehicle by mapping user inputs to various vehicle parts.
/// </summary>
public class VehicleController : UdonSharpBehaviour {
    public Engine       engine;
    public Clutch       clutch;
    public Transmission transmission;
    public Wheel[]      breakWheels;
    public Steering     steering;

    private void Update() {
        engine.throttleInput = Input.GetAxis("Vertical");                                               // Throttle:    W or Up Arrow 
        clutch.engagement    = Input.GetKey(KeyCode.LeftShift) ? 0f : 1f;                               // Clutch:      Left Shift
        if (Input.GetKeyDown(KeyCode.Q)) transmission.SetGear(transmission.currentGear + 1);            // Shift up:    Q
        if (Input.GetKeyDown(KeyCode.E)) transmission.SetGear(transmission.currentGear - 1);            // Shift down:  E
        var horizontal = 0f;
        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        steering.steeringAngleInput = steering.maxSteeringAngle * horizontal;                           // Steer:       A or Left Arrow / D or Right Arrow
        foreach (var wheel in breakWheels) wheel.brakeEngagement = Input.GetKey(KeyCode.Space) ? 1 : 0; // Brake:       Space
    }
}