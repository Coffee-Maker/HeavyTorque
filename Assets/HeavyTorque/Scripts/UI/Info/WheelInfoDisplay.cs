using TMPro;

using UdonSharp;

using UnityEngine;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class WheelInfoDisplay : NodeInfoComponent {
    public Wheel           Wheel => (Wheel)node;
    public TextMeshProUGUI radiusLabel, massLabel, rpmLabel, speedLabel;
    public RectTransform longitudinalSlipBar,
        lateralSlipBar,
        loadBar,
        wheelRotationRect,
        longitudinalFrictionArrow,
        lateralFrictionArrow,
        combinedFrictionArrow;

    public override void OnNodeSet() {
        if (radiusLabel) radiusLabel.text = $"{Wheel.radius:F}m";
        if (massLabel) massLabel.text     = $"{Wheel.mass}kg";
    }

    private void FixedUpdate() {
        if (rpmLabel) rpmLabel.text                             = $"{Wheel.Rpm:N0}rpm";
        if (speedLabel) speedLabel.text                         = $"{Wheel.AngularVelocity * Wheel.radius * 3.6f:N0}km/h";
        if (longitudinalSlipBar) longitudinalSlipBar.localScale = new Vector3(Wheel.LongitudinalSlipRatio,                        1, 1);
        if (lateralSlipBar) lateralSlipBar.localScale           = new Vector3(Abs(Wheel.LateralSlipAngle) / PI,                   1, 1);
        if (loadBar) loadBar.localScale                         = new Vector3(Wheel.suspension.lastForce.magnitude / Wheel.suspension.MaxForce, 1, 1);

        if (wheelRotationRect) wheelRotationRect.localRotation = Quaternion.AngleAxis(-Wheel.angle, Vector3.forward);

        if (longitudinalFrictionArrow) {
            if (Approximately(0, Wheel.FrictionLimit)) {
                longitudinalFrictionArrow.gameObject.SetActive(false);
            }
            else {
                longitudinalFrictionArrow.gameObject.SetActive(true);
                var length = Wheel.LongitudinalForce / Wheel.FrictionLimit * 100f;
                longitudinalFrictionArrow.sizeDelta     = new Vector2(longitudinalFrictionArrow.sizeDelta.x, Abs(length));
                longitudinalFrictionArrow.localRotation = Quaternion.AngleAxis(length < 0 ? 180f : 0, Vector3.forward);
            }
        }

        if (lateralFrictionArrow) {
            if (Approximately(0, Wheel.FrictionLimit)) {
                lateralFrictionArrow.gameObject.SetActive(false);
            }
            else {
                lateralFrictionArrow.gameObject.SetActive(true);
                var length = Wheel.LateralForce / Wheel.FrictionLimit * 100f;
                lateralFrictionArrow.sizeDelta     = new Vector2(lateralFrictionArrow.sizeDelta.x, Abs(length));
                lateralFrictionArrow.localRotation = Quaternion.AngleAxis(length < 0 ? -90f : 90f, Vector3.forward);
            }
        }

        if (combinedFrictionArrow) {
            if (Approximately(0, Wheel.FrictionLimit)) {
                combinedFrictionArrow.gameObject.SetActive(false);
            }
            else {
                combinedFrictionArrow.gameObject.SetActive(true);
                var combinedForce = Sqrt(Wheel.LongitudinalForce * Wheel.LongitudinalForce + Wheel.LateralForce * Wheel.LateralForce);
                combinedFrictionArrow.sizeDelta = new Vector2(combinedFrictionArrow.sizeDelta.x, combinedForce / Wheel.FrictionLimit * 100f);

                combinedFrictionArrow.localRotation = Quaternion.AngleAxis(
                    Atan2(Wheel.LateralForce, Wheel.LongitudinalForce) * Rad2Deg,
                    Vector3.forward
                );
            }
        }
    }
}