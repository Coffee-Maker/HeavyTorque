using System;

using TMPro;

using UdonSharp;

using UnityEngine;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class NodeInfoDisplay : UdonSharpBehaviour {
    public VehicleNode     node;
    public GameObject      contents;
    public TextMeshProUGUI title;

    private void Start() {
        if(node) SetNode(node);
    }

    public void Close() { Destroy(gameObject); }

    public void ToggleExpand() { contents.SetActive(!contents.activeSelf); }

    public void SetNode(VehicleNode newNode) {
        node       = newNode;
        title.text = newNode.name;

        foreach (var comp in GetComponentsInChildren<NodeInfoComponent>()) {
            comp.node = newNode;
            comp.OnNodeSet();
        }
    }
}