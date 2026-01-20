using UdonSharp;

using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Differential : VehicleNodeWithTorque {
    public VehicleNodeWithTorque input;
    public VehicleNodeWithTorque output1;
    public VehicleNodeWithTorque output2;

    public float ratio = 3.2f;

    public bool lockDifferential;

    private float _debugBias;

    public override void ApplyDownstreamTorque(float torqueForce, TorqueMode forceMode) {
        torqueForce *= ratio * 0.5f;
        output1.ApplyDownstreamTorque(torqueForce, forceMode);
        output2.ApplyDownstreamTorque(torqueForce, forceMode);
    }

    public override float GetInertia(InertiaFrom from, InertiaDirection direction) {
        var inertia = direction == InertiaDirection.Upstream
            ? input != null ? input.GetInertia(InertiaFrom.Output, InertiaDirection.Upstream) : 0f
            : (output1 != null ? output1.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) : 0f)
            + (output2 != null ? output2.GetInertia(InertiaFrom.Input, InertiaDirection.Downstream) : 0f);

        var goingThroughRatio = (from == InertiaFrom.Input && direction == InertiaDirection.Downstream)
            || (from == InertiaFrom.Output && direction == InertiaDirection.Upstream);

        if (goingThroughRatio) {
            var ratioSquared = ratio * ratio;

            if (direction == InertiaDirection.Upstream) inertia *= ratioSquared;
            else inertia                                        /= ratioSquared;
        }

        return inertia;
    }

    public override float GetDownstreamAngularVelocity() {
        var a1 = output1 != null ? output1.GetUpstreamAngularVelocity() : 0f;
        var a2 = output2 != null ? output2.GetUpstreamAngularVelocity() : 0f;
        return (a1 + a2) * 0.5f;
    }

    public override float GetUpstreamAngularVelocity() => GetDownstreamAngularVelocity() * ratio;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        if (output1 == null || output2 == null) return;

        Handles.color = Color.grey;
        Handles.DrawLine(output1.transform.position, output2.transform.position, 5);
        var midPoint = (output1.transform.position + output2.transform.position) / 2;
        var delta    = (output1.transform.position - output2.transform.position) / 2;
        Handles.color = Color.red;
        Handles.DrawLine(midPoint, midPoint + delta * _debugBias, 5);
    }
#endif
}