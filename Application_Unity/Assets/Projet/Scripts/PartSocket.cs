using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[AddComponentMenu("MOVIE/Part Socket")]
[RequireComponent(typeof(BoxCollider))]
public class PartSocket : XRSocketInteractor
{
    [SerializeField]
    private MachinePartType acceptedPart = MachinePartType.Tube;

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        if (interactable == null || !base.CanSelect(interactable))
            return false;

        Transform candidate = interactable.transform;

        PartId part = candidate.GetComponent<PartId>();

        if (part == null)
            part = candidate.GetComponentInParent<PartId>();

        if (part == null)
            part = candidate.GetComponentInChildren<PartId>();

        return part != null && part.PartType == acceptedPart;
    }
}
