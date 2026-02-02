using System;

using UdonSharp;

using UnityEngine;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AntiRollBar : VehicleNode {
    public Transform connection1;
    public Transform connection2;
    public float     stiffness = 5000f;

    public override void Tick(float deltaTime) {
        var localPos1  = transform.InverseTransformPoint(connection1.position);
        var localPos2  = transform.InverseTransformPoint(connection2.position);
        var difference = localPos1.y - localPos2.y;
        var force      = difference * stiffness * deltaTime;
        vehicle.Rigidbody.AddForceAtPosition(transform.up * force,  connection1.position, ForceMode.Impulse);
        vehicle.Rigidbody.AddForceAtPosition(transform.up * -force, connection2.position, ForceMode.Impulse);
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnDrawGizmosSelected() {
        if (!connection1 || !connection2) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        var localPos1 = transform.InverseTransformPoint(connection1.position);
        var localPos2 = transform.InverseTransformPoint(connection2.position);
        Gizmos.color = Color.green;
        var barEnd1 = new Vector3(localPos1.x, 0, 0);
        var barEnd2 = new Vector3(localPos2.x, 0, 0);
        Gizmos.DrawLine(barEnd1, barEnd2);
        Gizmos.DrawLine(barEnd1, localPos1);
        Gizmos.DrawLine(barEnd2, localPos2);
        var delta = localPos1.y - localPos2.y;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(localPos1, localPos1 + Vector3.up * delta);
        Gizmos.DrawLine(localPos2, localPos2 - Vector3.up * delta);
    }
#endif
}