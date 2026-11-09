// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRCloneOrigin.cs — LIEN SOURCE → CLONES (marqueur)
//
//  RÔLE
//    Un Instantiate conserve le nom de l'objet source, et deux objets peuvent
//    porter le même nom dans une scène. Comme la limite de duplication compte
//    les « exemplaires d'une même source », il faut un lien explicite.
//
//    VRCloneOrigin.Attach(clone, source) est appelé par VRDuplicateDelete à
//    chaque duplication ; CountInstances(objet) sert à plafonner le nombre
//    d'exemplaires (protection GPU : un scan Gaussian Splat × 30 = plantage).
//
//  FAMILLE
//    Chaque clone porte aussi l'identifiant de sa FAMILLE (= identifiant de
//    l'objet original). Contrairement au lien « source », il survit à la
//    suppression de l'original : sans lui, supprimer l'original faisait de
//    chaque copie une famille à part (plafond contournable, narration et
//    épreuve qui ne reconnaissaient plus les copies).
//
//  Composant minuscule, ajouté uniquement aux clones, sans aucune logique.
//
//  RÉFÉRENCES : voir README.md (section 4)
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Marque posée sur un clone pour remonter à son objet d'origine.
    ///
    /// Un <c>Instantiate</c> conserve le nom de l'objet source, mais deux
    /// objets différents peuvent porter le même nom dans une scène. Comme les
    /// limites de duplication comptent des « exemplaires d'une même source »,
    /// on a besoin d'un lien explicite source → clones, indépendant du nom.
    ///
    /// Composant minuscule, ajouté uniquement au clone, sans aucune logique.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class VRCloneOrigin : MonoBehaviour
    {
        /// <summary>Objet dupliqué. Destroyed quand l'original est supprimé.</summary>
        public GameObject source;

        /// <summary>
        /// Identifiant de la famille (InstanceID de l'original). Reste valable
        /// après la suppression de l'original, contrairement à <see cref="source"/>.
        /// </summary>
        [HideInInspector] public int famille;

        /// <summary>Instancié automatiquement sur tout clone (jamais posé à la main).</summary>
        public static VRCloneOrigin Attach(GameObject clone, GameObject sourceObject)
        {
            if (clone == null) return null;

            // Dupliquer une copie : la nouvelle copie rejoint la famille de l'original,
            // et pointe vers l'original s'il existe encore.
            VRCloneOrigin sourceMarker = sourceObject != null ? sourceObject.GetComponent<VRCloneOrigin>() : null;
            int familleSource = FamilleDe(sourceObject);
            GameObject original = sourceMarker != null && sourceMarker.source != null ? sourceMarker.source : sourceObject;

            VRCloneOrigin marker = clone.GetComponent<VRCloneOrigin>();
            if (marker == null) marker = clone.AddComponent<VRCloneOrigin>();
            marker.source = original;
            marker.famille = familleSource;
            return marker;
        }

        /// <summary>
        /// Famille d'un objet : celle de son marqueur pour un clone (cherché aussi
        /// dans les parents, un collider enfant suffit), son propre identifiant
        /// pour un original. 0 pour null.
        /// </summary>
        public static int FamilleDe(GameObject objet)
        {
            if (objet == null) return 0;
            VRCloneOrigin marker = objet.GetComponentInParent<VRCloneOrigin>();
            return marker != null ? marker.famille : objet.GetInstanceID();
        }

        /// <summary>Vrai si les deux objets sont le même ou l'un est une copie de l'autre (ou ont le même original).</summary>
        public static bool MemeFamille(GameObject a, GameObject b)
        {
            int fa = FamilleDe(a);
            return fa != 0 && fa == FamilleDe(b);
        }

        /// <summary>
        /// Nombre d'exemplaires vivants de la famille de <paramref name="membre"/> :
        /// l'original (s'il existe encore) plus tous les clones de la famille, même
        /// si l'original a été supprimé.
        /// </summary>
        public static int CountInstances(GameObject membre)
        {
            if (membre == null) return 0;

            int id = FamilleDe(membre);
            GameObject original = membre.GetComponent<VRCloneOrigin>() == null ? membre : null;
            int count = 0;

            VRCloneOrigin[] markers = FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                VRCloneOrigin m = markers[i];
                if (m == null || m.famille != id) continue;
                if (m.gameObject.activeInHierarchy) count++;
                if (original == null && m.source != null && m.source.GetComponent<VRCloneOrigin>() == null) original = m.source;
            }

            if (original != null && original.activeInHierarchy) count++;
            return count;
        }
    }
}
