using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

using static UnityEngine.Mathf;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Speedometer : UdonSharpBehaviour {
    public Vehicle      vehicle;
    public Engine       engine;
    public Clutch       clutch;
    public Transmission transmission;
    public Pedal        brakePedal;
    public HandBrake    handBrake;

    public TextMeshProUGUI speedText;
    public TextMeshProUGUI gearText;

    public RectTransform rpmNeedle;
    public float         needleStartAngle = -130f;
    public float         needlEndAngle    = 130f;

    public RectTransform   divisionsParent;
    public float           divisionWidth       = 2f;
    public float           devisionInnerRadius = 40f;
    public float           devisionOuterRadius = 50f;
    public TextMeshProUGUI divisionLabelTemplate;
    public float           divisionLabelDistance;
    public float           maxRpmForDivisions = 10000f;

    public float         barAngleStart;
    public float         barAngleEnd = 90;
    public RectTransform throttleFill, clutchFill, brakeFill, handBrakeFill;

    private void LateUpdate() {
        speedText.text = $"{vehicle.Rigidbody.velocity.magnitude * 3.6f:00}";
        gearText.text  = transmission.CurrentGearName;

        var needleAngle = needleStartAngle + engine.Rpm / maxRpmForDivisions * (needlEndAngle - needleStartAngle);
        rpmNeedle.localRotation = Quaternion.AngleAxis(needleAngle, Vector3.forward);

        var barRange = barAngleEnd - barAngleStart;
        throttleFill.transform.localRotation  = Quaternion.AngleAxis(barAngleStart + engine.throttleInput.ReadFloat() * barRange,   Vector3.forward);
        clutchFill.transform.localRotation    = Quaternion.AngleAxis(barAngleStart + clutch.engagementInput.ReadFloat() * barRange, Vector3.forward);
        brakeFill.transform.localRotation     = Quaternion.AngleAxis(barAngleStart + brakePedal.ReadFloat() * barRange,             Vector3.forward);
        handBrakeFill.transform.localRotation = Quaternion.AngleAxis(barAngleStart + handBrake.ReadFloat() * barRange,              Vector3.forward);
    }

#if !COMPILER_UDONSHARP

    [ContextMenu("Build Divisions")]
    void BuildDivisions() {
        if (!divisionsParent) return;

        for (var i = divisionsParent.childCount - 1; i >= 0; i--) DestroyImmediate(divisionsParent.GetChild(i).gameObject);
        var divisionCount = FloorToInt(maxRpmForDivisions / 1000);
        var angleStep     = (needlEndAngle - needleStartAngle) / divisionCount;
        var distance      = devisionInnerRadius + (devisionOuterRadius - devisionInnerRadius) / 2;

        for (var i = 0; i <= divisionCount; i++) {
            var angle    = needleStartAngle + i * angleStep;
            var division = new GameObject($"Division {i + 1}", typeof(RectTransform));
            division.AddComponent<Image>();
            division.transform.SetParent(divisionsParent, false);
            var rt = division.GetComponent<RectTransform>();
            rt.sizeDelta     = new Vector2(devisionOuterRadius - devisionInnerRadius, divisionWidth);
            rt.localRotation = Quaternion.AngleAxis(angle + 90, Vector3.forward);
            var direction = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;
            rt.anchoredPosition = direction * distance;

            var label = Instantiate(divisionLabelTemplate, divisionsParent);
            label.gameObject.name = $"Label {i + 1}";
            label.gameObject.SetActive(true);
            label.text                           = $"{i}";
            label.rectTransform.anchoredPosition = direction * (distance + divisionLabelDistance);
        }
    }
#endif
}