#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UdonSharp;

using UnityEngine;


public class CameraFollower : MonoBehaviour {
    public Vehicle target;
    public Vector3   offset;
    public float     followSpeed;
    public float     distance;
    public float     rotationSpeed;
    public float     zoomSpeed;

    private void LateUpdate() {
        distance += Input.GetAxis("Mouse ScrollWheel") * -zoomSpeed;
        var pivot = target.transform.position + offset;

        var rotating = Input.GetMouseButton(1);
        Cursor.visible   = !rotating;
        Cursor.lockState = rotating ? CursorLockMode.Locked : CursorLockMode.None;

        if (rotating) {
            var totalRotation = Quaternion.identity;
            var mouseX        = Input.GetAxis("Mouse X") * rotationSpeed;
            totalRotation *= Quaternion.AngleAxis(mouseX, Vector3.up);
            var mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;
            totalRotation *= Quaternion.AngleAxis(-mouseY, transform.right);
            var offsetToTarget = transform.position - pivot;
            offsetToTarget     = totalRotation * offsetToTarget;
            transform.position = pivot + offsetToTarget;
            transform.rotation = totalRotation * transform.rotation;
        }

        var zoomVector = -transform.forward * distance;
        transform.position = pivot + zoomVector - target.Rigidbody.velocity * followSpeed;
    }
}
#endif