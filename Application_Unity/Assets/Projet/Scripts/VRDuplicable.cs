// =====================================================================================
//  VRDuplicable.cs — MARQUEUR : droits de duplication / suppression
//
//  RÔLE
//    Composant de configuration SANS AUCUNE LOGIQUE : il stocke les
//    autorisations et il est lu par VRDuplicateDelete au moment de l'action.
//      • duplicationEnabled = false : l'objet ne peut pas être dupliqué.
//      • deletionEnabled   = false : l'objet ne peut pas être supprimé.
//      • duplicationPrefab  : modèle à instancier à la place de l'objet.
//
//  C'est la façon simple d'EXCLURE un objet (sol, murs, décor) de la
//  suppression, ou de forcer une variante à la duplication.
//
//  RÉFÉRENCES : voir README.md (section 4)
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// MARQUEUR DE CONFIGURATION — à poser sur un objet pour régler ses droits de
    /// duplication / suppression en VR.
    ///
    /// Rôle dans le système :
    ///  - Ce composant ne fait RIEN tout seul. Il stocke uniquement les autorisations
    ///    et il est lu par <see cref="VRDuplicateDelete"/> au moment de l'action.
    ///  - Il n'est pas obligatoire : sans lui, tout objet tenu peut être dupliqué et
    ///    supprimé. Le poser est donc la façon simple d'EXCLURE un objet (sol, murs,
    ///    décor) de la suppression, ou de forcer un modèle de remplacement.
    ///
    /// Placement dans la scène (même logique que VRScalable) :
    ///   XR Origin
    ///    └── Caméra (VRHeadTracker)
    ///    └── Controller (VRGrabHand + VRDuplicateDelete)  ← le geste
    ///   Objet (Rigidbody + MeshRenderer)
    ///    └── VRDuplicable  ← ce composant (optionnel)
    ///
    /// Exemple : le sol porte un VRDuplicable avec deletionEnabled = false afin
    /// qu'un geste maladroit ne le fasse jamais disparaître, tout en gardant la
    /// duplication/suppression active sur le reste de la scène.
    /// </summary>
    [AddComponentMenu("Remodeler le Reel/VR/VRDuplicable (Objet duplicable)")]
    public class VRDuplicable : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // AUTORISATIONS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Autorise la duplication de cet objet. false = le bouton « dupliquer » est
        /// sans effet tant que cet objet est tenu.
        /// </summary>
        [Tooltip("Autorise la manette à dupliquer cet objet")]
        public bool duplicationEnabled = true;

        /// <summary>
        /// Autorise la suppression de cet objet. false = le bouton « supprimer » est
        /// sans effet : l'objet reste dans la scène (utile pour le sol, les murs…).
        /// </summary>
        [Tooltip("Autorise la suppression de cet objet")]
        public bool deletionEnabled = true;

        // ---------------------------------------------------------------------
        // MODÈLE DE REMPLACEMENT (optionnel)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Objet réellement instancié lors d'une duplication. Laisser vide pour dupliquer
        /// l'objet lui-même (cas courant). Renseigner un prefab (ou un autre objet de la
        /// scène) permet de dupliquer une variante : version « neuve », pièce différente,
        /// modèle allégé…
        ///
        /// Le clone reçoit la position/rotation de l'objet tenu, décalées du
        /// duplicateOffset de <see cref="VRDuplicateDelete"/>.
        /// </summary>
        [Tooltip("Prefab instancié à la place de l'objet (vide = dupliquer l'objet lui-même)")]
        public GameObject duplicationPrefab;
    }
}
