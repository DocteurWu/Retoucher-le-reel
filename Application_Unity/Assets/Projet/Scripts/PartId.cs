using UnityEngine;

public enum MachinePartType
{
    Gear,
    Tube,
    Gauge,
    Handwheel,
    Lever
}

[DisallowMultipleComponent]
public sealed class PartId : MonoBehaviour
{
    [SerializeField]
    private MachinePartType partType = MachinePartType.Tube;

    public MachinePartType PartType => partType;
}
