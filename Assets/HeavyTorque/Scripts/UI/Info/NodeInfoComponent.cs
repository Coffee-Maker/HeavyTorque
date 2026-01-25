using UdonSharp;

using UnityEngine;


public abstract class NodeInfoComponent : UdonSharpBehaviour {
    [HideInInspector] public VehicleNode node;

    public virtual void OnNodeSet() { }
}