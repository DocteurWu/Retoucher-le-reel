// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuveCle.cs — ÉPREUVE « TROUVER LA CLÉ »
//
//  RÔLE
//    Réussie dès que le joueur SAISIT l'objet caché (la clé, derrière le tableau).
//    Brancher une narration « Objet Saisi » sur la clé pour commenter la découverte.
//
//  À POSER SUR : un objet vide « Epreuve_Cle », objet = le Rigidbody de la clé.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Épreuve : trouver (saisir) un objet caché.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Trouver un objet")]
    public class EpreuveCle : Epreuve
    {
        [Tooltip("Objet à trouver : l'épreuve est réussie quand une main le saisit.")]
        public Rigidbody objet;

        [Tooltip("Ce qui cache l'objet (le tableau) : c'est lui qui est montré tant qu'il n'a pas été déplacé. Vide = l'objet est montré tout de suite.")]
        public Collider cachette;

        public UnityEvent aLaReussite = new UnityEvent();

        /// <summary>Au-delà de cet écart (m), la cachette a été déplacée : l'objet est à découvert.</summary>
        private const float DistanceCachee = 0.25f;

        /// <summary>C'est une énigme : la main guide laisse le joueur chercher avant de donner l'indice.</summary>
        public override float DelaiAvantGuide => 45f;

        /// <summary>Tant que la cachette est en place, on la montre (sans dévoiler la clé) ; ensuite, la clé.</summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste)
        {
            bool cachee = cachette != null && objet != null && cachette.enabled
                && Vector3.Distance(cachette.transform.position, objet.position) < DistanceCachee;
            Collider c = cachee ? cachette : ColliderDe(objet);
            if (c != null) liste.Add(c);
        }

        /// <summary>Validé depuis le PC : la clé reste où elle est.</summary>
        protected override void Forcer()
        {
            if (objet != null) VRSons.Jouer(VRSon.Reussite, objet.position, 0.8f, 1.2f);
            aLaReussite?.Invoke();
        }

        private void OnEnable() => VRGrabHand.ObjetSaisi += SurSaisie;
        private void OnDisable() => VRGrabHand.ObjetSaisi -= SurSaisie;

        private void SurSaisie(VRGrabHand main, Rigidbody corps)
        {
            if (EstReussie || objet == null || corps != objet) return;
            VRSons.Jouer(VRSon.Reussite, corps.position, 0.8f, 1.2f);
            main?.TriggerHaptic(0.7f, 0.15f, 4);
            aLaReussite?.Invoke();
            SignalerReussite();
        }
    }
}
