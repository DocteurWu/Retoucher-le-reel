// Authored by Louai HAMLAT - All rights reserved

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using RemodelerLeReel.VR;

[AddComponentMenu("MOVIE/Part Socket")]
[RequireComponent(typeof(BoxCollider))]
public class PartSocket : XRSocketInteractor
{
    [SerializeField]
    private MachinePartType acceptedPart = MachinePartType.Tube;

    [Header("Retour Haptique d'Emboîtement")]
    [Tooltip("Intensité de l'impulsion quand la pièce s'emboîte dans le socket")]
    [Range(0f, 1f)] public float snapHapticIntensity = 0.85f;

    [Tooltip("Durée de l'impulsion quand la pièce s'emboîte (secondes)")]
    public float snapHapticDuration = 0.12f;

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        // Si l'objet possède un profil haptique spécifique, le jouer ou vibrer les mains à proximité
        if (args.interactableObject != null)
        {
            var haptic = args.interactableObject.transform.GetComponent<VRHapticInteractable>()
                      ?? args.interactableObject.transform.GetComponentInParent<VRHapticInteractable>();
            
            // Vibrer toutes les mains actives à proximité immédiate du socket (effet d'enclenchement mécanique)
            foreach (var hand in VRGrabHand.AllHands)
            {
                if (hand != null && Vector3.Distance(hand.transform.position, transform.position) < 0.6f)
                {
                    if (haptic != null)
                    {
                        haptic.TriggerCustomFeedback(hand, snapHapticIntensity, snapHapticDuration, priority: 5, HapticPattern.DoublePulse);
                    }
                    else
                    {
                        hand.TriggerHaptic(snapHapticIntensity, snapHapticDuration, priority: 5);
                    }
                }
            }
        }
    }

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

