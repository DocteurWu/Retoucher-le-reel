// =====================================================================================
//  VRCloneOrigin.cs — LIEN SOURCE → CLONES (marqueur)
//
//  RÔLE
//    Un Instantiate conserve le nom de l'objet source, et deux objets peuvent
//    porter le même nom dans une scène. Comme la limite de duplication compte
//    les « exemplaires d'une même source », il faut un lien explicite.
//
//    VRCloneOrigin.Attach(clone, source) est appelé par VRDuplicateDelete à
//    chaque duplication ; CountInstances(source) sert à plafonner le nombre
//    d'exemplaires (protection GPU : un scan Gaussian Splat × 30 = plantage).
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

        /// <summary>Instancié automatiquement sur tout clone (jamais posé à la main).</summary>
        public static VRCloneOrigin Attach(GameObject clone, GameObject sourceObject)
        {
            if (clone == null) return null;

            VRCloneOrigin marker = clone.GetComponent<VRCloneOrigin>();
            if (marker == null) marker = clone.AddComponent<VRCloneOrigin>();
            marker.source = sourceObject;
            return marker;
        }

        /// <summary>
        /// Nombre d'exemplaires vivant de la source donnée : l'original
        /// (s'il existe encore) plus tous ses clones marqués.
        /// </summary>
        public static int CountInstances(GameObject sourceObject)
        {
            if (sourceObject == null) return 0;

            int count = sourceObject.activeInHierarchy ? 1 : 0;

            VRCloneOrigin[] markers = FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                if (markers[i].source == sourceObject && markers[i].gameObject.activeInHierarchy) count++;
            }

            return count;
        }
    }
}