// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  Epreuve.cs — BASE COMMUNE DE TOUTES LES ÉPREUVES (ÉTAPES DU NIVEAU)
//
//  RÔLE
//    Une épreuve est une ÉTAPE du niveau : elle a un nom (affiché dans la liste des
//    étapes de ProgressionEtapes) et devient « réussie » une fois pour toutes.
//    Toutes les épreuves lèvent le même événement statique UneEpreuveReussie :
//      • la narration s'y branche (déclencheur « Epreuve Reussie ») ;
//      • ProgressionEtapes s'en sert pour déverrouiller la porte de sortie.
//
//  ÉPREUVES DISPONIBLES
//    EpreuveRecipient (remplir le bac), EpreuveTableau (agrandir + accrocher),
//    EpreuvePlante (agrandir + mettre dans le pot), EpreuveSuppression (faire
//    disparaître un objet), EpreuveCle (trouver un objet caché).
//
//  NOUVELLE ÉPREUVE : hériter de Epreuve et appeler SignalerReussite() quand c'est gagné.
// =====================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Étape du niveau : un nom et un état « réussie ».</summary>
    public abstract class Epreuve : MonoBehaviour
    {
        /// <summary>Levé une fois par épreuve, au moment où elle est réussie.</summary>
        public static event Action<Epreuve> UneEpreuveReussie;

        [Tooltip("Nom de l'étape, affiché dans la liste des étapes (ProgressionEtapes).")]
        public string nomEtape = "Épreuve";

        [TextArea(2, 5)]
        [Tooltip("Ce que la narration dit pour expliquer l'étape (annoncée par ProgressionEtapes quand c'est son tour).")]
        public string consigne = "";

        [TextArea(1, 3)]
        [Tooltip("Ce que la narration dit quand l'étape est réussie (vide = rien).")]
        public string felicitation = "";

        /// <summary>Vrai une fois l'épreuve réussie (définitif).</summary>
        public bool EstReussie { get; private set; }

        /// <summary>
        /// Secondes laissées au joueur avant que la main guide montre les objets de l'étape.
        /// À redéfinir pour une énigme : la main n'y donne qu'un indice.
        /// </summary>
        public virtual float DelaiAvantGuide => 0f;

        /// <summary>
        /// Valide l'étape depuis le PC (démo, voir CommandesDemo) en laissant la scène
        /// cohérente : tableau accroché, plante plantée, statue supprimée.
        /// </summary>
        public void ForcerReussite()
        {
            if (EstReussie) return;
            Debug.Log("[Demo] Étape validée depuis le PC : « " + nomEtape + " ».", this);
            Forcer();
            SignalerReussite(); // au cas où Forcer n'a pas pu aboutir (objet supprimé par le joueur…)
        }

        /// <summary>À redéfinir : met la scène dans l'état « réussi ». Par défaut : rien à faire.</summary>
        protected virtual void Forcer() { }

        /// <summary>Rend un objet disponible : retiré de la main et de la bulle du menu poignet.</summary>
        protected static void Liberer(Rigidbody corps)
        {
            if (corps == null) return;
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null && main.GrabbedBody == corps) main.ReleaseObject(applyThrowVelocity: false);
            }
            VRHandMenu menu = FindFirstObjectByType<VRHandMenu>();
            if (menu != null) menu.SortirDeLaBulle(corps);
        }

        /// <summary>Met un objet redimensionnable à la taille voulue (en multiple de sa taille de départ).</summary>
        protected static void MettreALaTaille(VRScalable scalable, float ratioVoulu)
        {
            float actuel = Ratio(scalable);
            if (scalable == null || actuel <= 0f) return;
            scalable.transform.localScale *= ratioVoulu / actuel;
            Physics.SyncTransforms(); // autoSyncTransforms = 0
        }

        /// <summary>
        /// Objets à mettre en surbrillance quand cette étape est celle en cours (ceux qui
        /// sont dans une main sont retirés : inutile de montrer ce qu'on tient déjà).
        /// </summary>
        public void RemplirObjetsAMontrer(List<Collider> liste)
        {
            liste.Clear();
            AjouterObjetsAMontrer(liste);
            liste.RemoveAll(c => c == null || (c.attachedRigidbody != null && EstTenu(c.attachedRigidbody)));
        }

        /// <summary>À redéfinir : ajoute les colliders des objets concernés par l'étape.</summary>
        protected abstract void AjouterObjetsAMontrer(List<Collider> liste);

        /// <summary>Premier collider solide et actif de l'objet (ou de ses enfants), null s'il n'y en a pas.</summary>
        protected static Collider ColliderDe(Component objet)
        {
            if (objet == null) return null;
            foreach (Collider c in objet.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger) return c;
            }
            return null;
        }

        /// <summary>Marque l'épreuve réussie et prévient la narration / la progression. Sans effet la 2e fois.</summary>
        protected void SignalerReussite()
        {
            if (EstReussie) return;
            EstReussie = true;
            Debug.Log("[Epreuve] Étape réussie : « " + nomEtape + " ».", this);
            UneEpreuveReussie?.Invoke(this);
        }

        // ------------------------------------------------------------------
        // Outils partagés : panneau de texte flottant tourné vers le joueur
        // ------------------------------------------------------------------
        /// <summary>Panneau TextMeshPro (taille auto dans un cadre largeur × hauteur, en m).</summary>
        protected TextMeshPro CreerPanneau(string nom, Vector2 cadre)
        {
            GameObject go = new GameObject(nom);
            go.transform.SetParent(transform, false);
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.01f;
            t.fontSizeMax = 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = cadre;
            return t;
        }

        /// <summary>Place le panneau et le tourne vers la caméra (rotation horizontale seulement).</summary>
        protected static void PlacerPanneau(TextMeshPro panneau, Vector3 position)
        {
            if (panneau == null) return;
            panneau.transform.position = position;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 regard = position - cam.transform.position;
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) panneau.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        /// <summary>L'objet est-il dans une main ?</summary>
        protected static bool EstTenu(Rigidbody corps)
        {
            if (corps == null) return false;
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null && main.GrabbedBody == corps) return true;
            }
            return false;
        }

        /// <summary>Taille actuelle d'un objet redimensionnable, en multiple de sa taille de départ.</summary>
        protected static float Ratio(VRScalable scalable)
        {
            if (scalable == null) return 1f;
            float initiale = scalable.InitialWorldSize;
            if (initiale <= 0f) return 1f;
            Vector3 e = scalable.transform.lossyScale;
            return Mathf.Max(Mathf.Abs(e.x), Mathf.Max(Mathf.Abs(e.y), Mathf.Abs(e.z))) / initiale;
        }
    }
}
