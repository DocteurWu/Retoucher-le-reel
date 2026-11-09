// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRBanqueSons.cs — RÉGLAGES DU SOUND DESIGN (optionnel)
//
//  RÔLE
//    Permet de remplacer les sons procéduraux de <see cref="VRSons"/> par de vrais
//    fichiers audio et de régler le volume général. Chaque champ laissé VIDE garde
//    le son synthétisé par défaut : on peut donc remplacer les sons un par un.
//
//  À POSER SUR : n'importe quel objet de la scène (un seul). Sans ce composant,
//    tous les sons procéduraux sont joués au volume par défaut.
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>Banque de sons de la scène : remplacements et volume général.</summary>
    [AddComponentMenu("Remodeler le Reel/VR/Banque de sons")]
    public class VRBanqueSons : MonoBehaviour
    {
        /// <summary>Banque active (la dernière activée). Null si aucune dans la scène.</summary>
        public static VRBanqueSons Instance { get; private set; }

        [Header("Général")]
        [Tooltip("Coupe tous les sons d'interaction (pas la voix de la narration).")]
        public bool actif = true;

        [Range(0f, 1f)]
        [Tooltip("Volume général des sons d'interaction.")]
        public float volumeGeneral = 1f;

        [Header("Remplacements (vide = son procédural)")]
        public AudioClip saisie;
        public AudioClip lacher;
        public AudioClip teleportation;
        public AudioClip rangement;
        public AudioClip recuperation;
        public AudioClip duplication;
        public AudioClip suppression;
        public AudioClip impact;
        public AudioClip narrationApparition;
        public AudioClip narrationLettre;
        public AudioClip reussite;
        public AudioClip brulure;
        public AudioClip reconstruction;

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Clip de remplacement du son demandé (null = garder le procédural).</summary>
        public AudioClip ClipPour(VRSon son)
        {
            switch (son)
            {
                case VRSon.Saisie: return saisie;
                case VRSon.Lacher: return lacher;
                case VRSon.Teleportation: return teleportation;
                case VRSon.Rangement: return rangement;
                case VRSon.Recuperation: return recuperation;
                case VRSon.Duplication: return duplication;
                case VRSon.Suppression: return suppression;
                case VRSon.Impact: return impact;
                case VRSon.NarrationApparition: return narrationApparition;
                case VRSon.NarrationLettre: return narrationLettre;
                case VRSon.Reussite: return reussite;
                case VRSon.Brulure: return brulure;
                case VRSon.Reconstruction: return reconstruction;
                default: return null;
            }
        }
    }
}
