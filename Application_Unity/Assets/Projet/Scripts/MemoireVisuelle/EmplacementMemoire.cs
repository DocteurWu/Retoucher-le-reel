// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EmplacementMemoire.cs — UNE DESTINATION POSSIBLE POUR UN OBJET DÉPLACÉ
//
//  RÔLE
//    Un objet vide, posé À LA MAIN dans la scène (sur une étagère, une table, un canapé…), qui
//    dit au jeu : « un objet peut être déplacé ici ». Le scan n'a pas de collisions : le jeu ne
//    peut pas deviner où il y a une surface solide, c'est donc l'équipe qui choisit les endroits.
//    Ainsi un objet n'atterrit jamais dans un mur ni dans le vide.
//
//  OÙ LE POSER : la position de l'objet vide est l'endroit où le CENTRE de l'objet déplacé sera
//    placé. Le poser à la hauteur du centre de l'objet quand il repose sur la surface.
//
//  RÉGLAGES
//    tailleMax : taille maximale (m) d'un objet qui tient là (0 = sans limite). Un petit
//    emplacement ne reçoit pas un gros objet.
//
//  Dans la vue Scene, un gizmo cyan montre l'emplacement et sa taille maximale.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Destination possible d'un objet déplacé.</summary>
    [AddComponentMenu("Remodeler le Reel/Memoire visuelle/Emplacement memoire")]
    public class EmplacementMemoire : MonoBehaviour
    {
        /// <summary>Tous les emplacements actifs.</summary>
        public static readonly List<EmplacementMemoire> Tous = new List<EmplacementMemoire>();

        [Min(0f)]
        [Tooltip("Taille maximale (m) d'un objet qui tient ici. 0 = sans limite.")]
        public float tailleMax = 0f;

        private void OnEnable()
        {
            if (!Tous.Contains(this)) Tous.Add(this);
        }

        private void OnDisable()
        {
            Tous.Remove(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
            float cote = tailleMax > 0f ? tailleMax : 0.3f;
            Gizmos.DrawWireCube(transform.position, Vector3.one * cote);
            Gizmos.DrawSphere(transform.position, 0.03f);
        }
#endif
    }
}
