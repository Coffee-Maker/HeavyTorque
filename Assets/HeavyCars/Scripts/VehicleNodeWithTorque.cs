using UnityEngine;


/// <summary>
/// Represents anything that can receive torque input, such as differentials, gearboxes, and wheels.
/// </summary>
public abstract class VehicleNodeWithTorque : VehicleNode {
    public abstract void ApplyDownstreamTorque(float torque, TorqueMode forceMode); // in Newton-meters (Nm)

    public abstract float GetInertia(InertiaFrom from, InertiaDirection direction);

    public abstract float GetDownstreamAngularVelocity();
    
    public abstract float GetUpstreamAngularVelocity();
}

public enum InertiaFrom {
    Input,
    Output,
}

public enum InertiaDirection {
    Upstream,
    Downstream,
}

public enum TorqueMode {
    Force,
    Impulse,
}