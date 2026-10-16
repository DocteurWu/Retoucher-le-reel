using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// MARQUEUR DE CONFIGURATION — à poser sur un objet pour qu'il devienne redimensionnable.
    ///
    /// Rôle dans le système :
    ///  - Ce composant ne fait RIEN tout seul. Il stocke uniquement les règles de taille
    ///    autorisées et il est lu par <see cref="VRScaleGrab"/> au moment du redimensionnement.
    ///  - Il n'est pas obligatoire : si l'objet n'a pas de VRScalable, VRScaleGrab applique
    ///    alors ses propres valeurs par défaut (defaultMinRatio / defaultMaxRatio).
    ///  - cooldown : poser ce composant est la façon simple d'exclure un objet du scale
    ///    (murs, sol, décor) tout en gardant la mécanique active sur tout le reste de la scène.
    ///
    /// Exemple : un cylindre posé à 0,5 m de haut avec minSizeRatio = 0,25 / maxSizeRatio = 4
    /// pourra aller de 0,125 m à 2 m — à condition de respecter aussi les bornes absolues
    /// (minWorldSize / maxWorldSize).
    ///
    /// Placement dans la scène :
    ///   XR Origin
    ///    └── Caméra (VRHeadTracker)
    ///    └── Controller (VRGrabHand + VRScaleGrab)
    ///   Objet (Rigidbody + MeshRenderer)
    ///    └── VRScalable  ← ce composant
    /// </summary>
    [AddComponentMenu("Remodeler le Reel/VR/VRScalable (Objet redimensionnable)")]
    public class VRScalable : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // INTERRUPTEUR PRINCIPAL
        // ---------------------------------------------------------------------

        /// <summary>
        /// Interrupteur global de l'objet.
        /// false = la main est bien capable de le saisir, mais VRScaleGrab ignore complètement
        /// cet objet (aucun scale possible).
        /// </summary>
        [Tooltip("Autorise la manette à redimensionner cet objet")]
        public bool scalingEnabled = true;

        // ---------------------------------------------------------------------
        // LIMITES RELATIVES  (prioritaire, exprimées en % de la taille de saisie)
        // ---------------------------------------------------------------------

        [Header("--- Limites relatives à la taille de départ ---")]

        /// <summary>
        /// Borne basse RELATIVE : fraction de la taille qu'avait l'objet au moment où on l'a
        /// attrapé. Valeur 0.25 = l'objet ne peut pas descendre sous un quart de sa taille initiale.
        /// Avantage de ce type de borne : un gros objet et un petit objet réagissent pareil.
        /// </summary>
        [Tooltip("Taille minimale en % de la taille au moment de la saisie")]
        [Range(0.01f, 1f)] public float minSizeRatio = 0.25f;

        /// <summary>
        /// Borne haute RELATIVE : fraction maximale de la taille de saisie.
        /// Valeur 4 = l'objet peut être agrandi jusqu'à 4 fois sa taille initiale.
        /// </summary>
        [Tooltip("Taille maximale en % de la taille au moment de la saisie")]
        [Range(1f, 20f)] public float maxSizeRatio = 4f;

        // ---------------------------------------------------------------------
        // LIMITES ABSOLUES  (sécurité, en mètres, sur la plus grande dimension)
        // ---------------------------------------------------------------------

        [Header("--- Limites absolues (plus grande dimension en mètres) ---")]

        /// <summary>
        /// Borne basse ABSOLUE en mètres (plus grande dimension de l'objet).
        /// Elle complète minSizeRatio : sans elle, un objet minuscule (1 cm) pourrait être
        /// réduit jusqu'à devenir invisible / traverser les murs.
        /// La taille finale retenue est la PLUS GRANDE des deux bornes (voir ClampWorldScale).
        /// </summary>
        public float minWorldSize = 0.02f;

        /// <summary>
        /// Borne haute ABSOLUE en mètres. Même principe : empêche d'agrandir un objet au-delà
        /// de ce que la scène peut encaisser (performance + physique).
        /// </summary>
        public float maxWorldSize = 20f;

        // ---------------------------------------------------------------------
        // PHYSIQUE
        // ---------------------------------------------------------------------

        [Header("--- Physique ---")]

        /// <summary>
        /// Si true, la masse du Rigidbody est recalculée à chaque changement d'échelle
        /// selon le volume (masse ∝ ratio³). Un objet 2 fois plus grand devient donc 8 fois
        /// plus lourd : il chute de façon plus réaliste et se laisse moins « repousser ».
        /// false = la masse reste constante quelle que soit la taille.
        /// </summary>
        [Tooltip("Adapter la masse du Rigidbody proportionnellement au volume")]
        public bool scaleRigidbodyMass = false;

        /// <summary>
        /// Rigidbody dont la masse doit suivre l'échelle.
        /// null = utiliser celui porté par ce GameObject (cas le plus courant).
        /// À renseigner uniquement si le Rigidbody est sur un parent (objet composé).
        /// </summary>
        [Tooltip("Rigidbody à piloter (par défaut : celui de cet objet)")]
        public Rigidbody body;
    }
}