using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ClutchInfoDisplay : NodeInfoComponent {
    public Clutch Clutch => (Clutch)node;

    public Color           positiveVelocityColor = Color.green;
    public Color           negativeVelocityColor = Color.red;
    public Image           inVelocityBar,  outVelocityBar;
    public TextMeshProUGUI inVelocityText, outVelocityText;
    public RectTransform   engagementBar;
    public RectTransform   torqueArrow;
    public TextMeshProUGUI torqueText;

    public override void OnNodeSet() { }

    private void FixedUpdate() {
        var inVelocity    = Clutch.GetUpstreamAngularVelocity();
        var outVelocity   = Clutch.GetDownstreamAngularVelocity();
        var totalVelocity = Abs(inVelocity) + Abs(outVelocity);

        if (inVelocityBar) {
            var inScale = totalVelocity > 0f ? inVelocity / totalVelocity : 0f;
            inVelocityBar.rectTransform.localScale = new Vector3(inScale, 1f, 1f);
            inVelocityBar.color                    = inVelocity >= 0f ? positiveVelocityColor : negativeVelocityColor;
        }

        if (outVelocityBar) {
            var outScale = totalVelocity > 0f ? outVelocity / totalVelocity : 0f;
            outVelocityBar.rectTransform.localScale = new Vector3(outScale, 1f, 1f);
            outVelocityBar.color                    = outVelocity >= 0f ? positiveVelocityColor : negativeVelocityColor;
        }

        if (inVelocityText) inVelocityText.text   = $"{inVelocity * 60 / (2 * PI):N0}rpm";
        if (outVelocityText) outVelocityText.text = $"{outVelocity * 60 / (2 * PI):N0}rpm";

        if (engagementBar) engagementBar.localScale = new Vector3(1f, Clutch.Engagement, 1f);

        if (torqueArrow) {
            if (Approximately(0, Clutch.MaxTorque)) {
                torqueArrow.gameObject.SetActive(false);
            }
            else {
                var torque = Clutch.LastTorqueApplied / Clutch.MaxTorque;
                torqueArrow.sizeDelta     = new Vector2(Abs(torque) * 100f, torqueArrow.sizeDelta.y);
                torqueArrow.localRotation = Quaternion.AngleAxis(torque < 0f ? -90 : 90f, Vector3.forward);
            }
        }

        if (torqueText) torqueText.text = $"{Clutch.LastTorqueApplied:N0}Nm";
    }
}