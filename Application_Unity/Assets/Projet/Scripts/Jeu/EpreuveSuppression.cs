// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuveSuppression.cs — ÉPREUVE « FAIRE DISPARAÎTRE UN OBJET »
//
//  RÔLE
//    Réussie quand l'objet cible ET toutes ses copies ont été supprimés (menu
//    poignet : ranger l'objet dans la bulle puis index droit sur majeur gauche,
//    ou bouton Y à la manette). Un objet simplement RANGÉ dans la bulle existe
//    toujours : ça ne compte pas.
//    Les copies sont suivies par famille (VRCloneOrigin.famille) : dupliquer la
//    statue puis supprimer l'original ne suffit pas.
//
//  À POSER SUR : un objet vide « Epreuve_Statue », cible = la statue.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Épreuve : supprimer un objet (et toutes ses copies).</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Supprimer un objet")]
    public class EpreuveSuppression : Epreuve
    {
        [Tooltip("Objet à faire disparaître.")]
        public GameObject cible;

        public UnityEvent aLaReussite = new UnityEvent();

        private int _famille;
        private Vector3 _dernierePosition;
        private float _prochainControle;

        private void Start()
        {
            if (cible == null)
            {
                Debug.LogWarning("[Epreuve] « " + name + " » : aucune cible à supprimer.", this);
                enabled = false;
                return;
            }
            _famille = VRCloneOrigin.FamilleDe(cible);
        }

        /// <summary>Validé depuis le PC : l'objet et toutes ses copies disparaissent.</summary>
        protected override void Forcer()
        {
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie != null && copie.famille == _famille) Supprimer(copie.gameObject);
            }
            if (cible != null)
            {
                _dernierePosition = cible.transform.position;
                Supprimer(cible);
            }
            VRSons.Jouer(VRSon.Reussite, _dernierePosition, 0.8f);
            aLaReussite?.Invoke();
        }

        private static void Supprimer(GameObject objet)
        {
            Liberer(objet.GetComponent<Rigidbody>());
            Destroy(objet);
        }

        /// <summary>L'objet et ses copies encore présents (une fois rangés dans la bulle, leurs colliders sont coupés).</summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste)
        {
            Collider c = cible != null ? ColliderDe(cible.transform) : null;
            if (c != null) liste.Add(c);
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie == null || copie.famille != _famille) continue;
                c = ColliderDe(copie);
                if (c != null) liste.Add(c);
            }
        }

        private void Update()
        {
            if (EstReussie || Time.time < _prochainControle) return;
            _prochainControle = Time.time + 0.25f;

            if (cible != null)
            {
                _dernierePosition = cible.transform.position;
                return;
            }

            // L'original a disparu : reste-t-il des copies ?
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie != null && copie.famille == _famille) return;
            }

            VRSons.Jouer(VRSon.Reussite, _dernierePosition, 0.8f);
            aLaReussite?.Invoke();
            SignalerReussite();
        }
    }
}
