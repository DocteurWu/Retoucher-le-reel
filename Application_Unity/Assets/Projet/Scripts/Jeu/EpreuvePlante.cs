// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuvePlante.cs — ÉPREUVE « AGRANDIR LA PLANTE ET LA METTRE DANS LE POT »
//
//  RÔLE
//    Le joueur doit agrandir la plante (pincement à deux mains, VRScaleGrab) d'au
//    moins « ratioMin » (×2 par défaut) puis la poser SUR le pot. Quand c'est bon, la
//    plante se cale dans le pot (droite, centrée, figée) et devient enfant du pot :
//    déplacer le pot déplace la plante.
//
//  À POSER SUR : un objet vide « Epreuve_Plante ». Plante et pot : Rigidbody +
//    BoxCollider ; la plante a aussi un VRScalable (c'est son échelle qui est mesurée).
//  FEEDBACK : panneau au-dessus du pot « Plante ×1,0 / ×2 ».
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Épreuve : agrandir la plante puis la planter dans le pot.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Plante dans le pot")]
    public class EpreuvePlante : Epreuve
    {
        public Rigidbody plante;
        public Rigidbody pot;

        [Min(1.01f)]
        [Tooltip("Taille minimale à atteindre, en multiple de la taille de départ de la plante.")]
        public float ratioMin = 2f;

        [Min(0.02f)]
        [Tooltip("Écart horizontal max (m) entre le pied de la plante et le centre du pot (au moins la moitié de la largeur du pot).")]
        public float rayon = 0.15f;

        [Min(0.02f)]
        [Tooltip("Hauteur max (m) du pied de la plante au-dessus du haut du pot.")]
        public float hauteurMax = 0.2f;

        public UnityEvent aLaReussite = new UnityEvent();

        private BoxCollider _boitePlante, _boitePot;
        private VRScalable _scalable;
        private TextMeshPro _panneau;
        private float _prochainControle;

        private void Start()
        {
            _boitePlante = plante != null ? plante.GetComponent<BoxCollider>() : null;
            _boitePot = pot != null ? pot.GetComponent<BoxCollider>() : null;
            _scalable = plante != null ? plante.GetComponent<VRScalable>() : null;
            if (_boitePlante == null || _boitePot == null || _scalable == null)
            {
                Debug.LogWarning("[Epreuve] « " + name + " » : plante (BoxCollider + VRScalable) et pot (BoxCollider) requis.", this);
                enabled = false;
                return;
            }
            _panneau = CreerPanneau("Panneau_Epreuve_Plante", new Vector2(0.45f, 0.08f));
        }

        private void Update()
        {
            if (EstReussie || _boitePot == null) return;
            PlacerPanneau(_panneau, HautDuPot() + Vector3.up * 0.25f);
            if (Time.time < _prochainControle) return;
            _prochainControle = Time.time + 0.1f;

            float ratio = Ratio(_scalable);
            bool tailleOk = ratio >= ratioMin;
            Vector3 pied = PiedDeLaPlante();
            Vector3 haut = HautDuPot();
            Vector3 ecart = pied - haut;
            float horizontal = new Vector2(ecart.x, ecart.z).magnitude;
            float rayonUtile = Mathf.Max(rayon, DemiLargeurPot());
            bool dansLePot = horizontal <= rayonUtile && ecart.y >= -0.1f && ecart.y <= hauteurMax;

            if (tailleOk && dansLePot && !EstTenu(plante))
            {
                Planter();
                return;
            }

            if (_panneau != null)
            {
                string etat = tailleOk ? "<color=#5dff7a>taille OK</color> : pose-la sur le pot" : "agrandis-la";
                _panneau.text = "Plante  ×" + ratio.ToString("0.0") + " / ×" + ratioMin.ToString("0") + "  (" + etat + ")";
            }
        }

        /// <summary>Validé depuis le PC : la plante prend au moins la bonne taille et se plante dans le pot.</summary>
        protected override void Forcer()
        {
            if (plante == null || pot == null || _boitePlante == null || _boitePot == null) return;
            Liberer(plante);
            Liberer(pot);
            if (Ratio(_scalable) < ratioMin) MettreALaTaille(_scalable, ratioMin);
            Planter();
        }

        /// <summary>La plante et le pot.</summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste)
        {
            Collider c = ColliderDe(plante);
            if (c != null) liste.Add(c);
            c = ColliderDe(pot);
            if (c != null) liste.Add(c);
        }

        /// <summary>Centre du dessous de la boîte de la plante (repère monde).</summary>
        private Vector3 PiedDeLaPlante()
        {
            return _boitePlante.transform.TransformPoint(_boitePlante.center - new Vector3(0f, _boitePlante.size.y * 0.5f, 0f));
        }

        /// <summary>Centre du dessus de la boîte du pot (repère monde).</summary>
        private Vector3 HautDuPot()
        {
            return _boitePot.transform.TransformPoint(_boitePot.center + new Vector3(0f, _boitePot.size.y * 0.5f, 0f));
        }

        private float DemiLargeurPot()
        {
            Vector3 s = Vector3.Scale(_boitePot.size, _boitePot.transform.lossyScale);
            return 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }

        private void Planter()
        {
            if (!plante.isKinematic)
            {
                plante.linearVelocity = Vector3.zero;
                plante.angularVelocity = Vector3.zero;
            }
            plante.isKinematic = true;

            // Droite (seul le cap est gardé), pied posé au centre du haut du pot.
            Transform t = plante.transform;
            t.rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            t.position += HautDuPot() - PiedDeLaPlante();
            t.SetParent(pot.transform, true); // déplacer le pot déplace la plante
            Physics.SyncTransforms();         // autoSyncTransforms = 0

            VRSons.Jouer(VRSon.Reussite, t.position);
            if (_panneau != null) _panneau.text = "<color=#5dff7a><b>Bravo !</b></color>";
            aLaReussite?.Invoke();
            SignalerReussite();
        }
    }
}
