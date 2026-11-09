// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  BoutonMemoire3D.cs — LE BOUTON « JE SUIS PRÊT » / « VALIDER » / « REJOUER »
//
//  RÔLE
//    Un bouton flottant, qu'on actionne en le TOUCHANT :
//      • mains nues : le bout de l'index (VRHandGestures) reste dans le bouton ;
//      • manettes   : la manette reste dans le bouton.
//    Il faut y rester un court instant (maintien) : un geste maladroit ne valide pas par hasard.
//    Un seul bouton sert à tout le jeu : son texte et sa couleur changent selon la phase.
//
//  APPARITION : MemoireVisuelle le fait apparaître devant le joueur quand il est utile
//    (Montrer) et le cache sinon.
//
//  PC / TEST : Presser() simule le toucher (touches de DemoMemoire).
//
//  CRÉÉ PAR : MemoireVisuelle, au besoin (rien à poser dans la scène).
// =====================================================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Bouton 3D actionné au toucher.</summary>
    [AddComponentMenu("")]
    public class BoutonMemoire3D : MonoBehaviour
    {
        /// <summary>Levé quand le joueur actionne le bouton.</summary>
        public event Action Presse;

        [Tooltip("Distance (m) du bout du doigt au centre du bouton pour le toucher.")]
        public float rayonToucher = 0.07f;

        [Tooltip("Temps (s) pendant lequel le doigt doit rester dans le bouton.")]
        public float maintien = 0.4f;

        /// <summary>Vrai tant que le bouton est affiché et actionnable.</summary>
        public bool Actif { get; private set; }

        private Transform _pastille;
        private Material _matPastille;
        private TextMeshPro _texte;
        private Color _couleur = new Color(0.2f, 0.6f, 1f, 1f);
        private float _entreeDansLeBouton = -1f;
        private bool _declenche;
        private float _prochainePossible;

        private void Awake()
        {
            GameObject pastille = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pastille.name = "Pastille";
            Destroy(pastille.GetComponent<Collider>()); // visuel seulement : ne doit rien saisir
            pastille.transform.SetParent(transform, false);
            pastille.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // le disque fait face au joueur
            pastille.transform.localScale = new Vector3(0.2f, 0.015f, 0.2f);
            _matPastille = VRUtils.CreateUnlitMaterial(_couleur);
            Renderer rendu = pastille.GetComponent<Renderer>();
            rendu.sharedMaterial = _matPastille;
            rendu.shadowCastingMode = ShadowCastingMode.Off;
            rendu.receiveShadows = false;
            _pastille = pastille.transform;

            GameObject go = new GameObject("Texte");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            _texte = go.AddComponent<TextMeshPro>();
            _texte.enableAutoSizing = true;
            _texte.fontSizeMin = 0.01f;
            _texte.fontSizeMax = 10f;
            _texte.alignment = TextAlignmentOptions.Center;
            _texte.textWrappingMode = TextWrappingModes.NoWrap;
            _texte.rectTransform.sizeDelta = new Vector2(0.18f, 0.07f);
            _texte.color = Color.white;

            Cacher();
        }

        private void OnDestroy()
        {
            if (_matPastille != null) Destroy(_matPastille);
        }

        /// <summary>Affiche le bouton à cette position, avec ce texte et cette couleur.</summary>
        public void Montrer(Vector3 position, string texte, Color couleur)
        {
            transform.position = position;
            Orienter();
            _texte.text = texte;
            _couleur = couleur;
            _matPastille.color = couleur;
            gameObject.SetActive(true);
            Actif = true;
            _entreeDansLeBouton = -1f;
            _declenche = false;
            _prochainePossible = Time.time + 1f; // pas de validation involontaire à l'apparition
        }

        /// <summary>Change seulement le texte (le bouton reste où il est).</summary>
        public void ChangerTexte(string texte)
        {
            if (_texte != null) _texte.text = texte;
        }

        /// <summary>Cache le bouton.</summary>
        public void Cacher()
        {
            Actif = false;
            gameObject.SetActive(false);
        }

        /// <summary>Actionne le bouton sans le toucher (PC, tests).</summary>
        public void Presser()
        {
            if (!Actif) return;
            Declencher();
        }

        private void Update()
        {
            if (!Actif) return;
            Orienter();

            bool touche = EstTouche();
            if (touche)
            {
                if (_entreeDansLeBouton < 0f) _entreeDansLeBouton = Time.time;
                // Teinte plus claire pendant qu'on appuie : le joueur voit que ça prend.
                float avancement = Mathf.Clamp01((Time.time - _entreeDansLeBouton) / Mathf.Max(0.01f, maintien));
                _matPastille.color = Color.Lerp(_couleur, Color.white, avancement * 0.6f);

                if (!_declenche && Time.time >= _prochainePossible && Time.time - _entreeDansLeBouton >= maintien)
                {
                    _declenche = true; // un seul déclenchement par toucher : il faut ressortir puis re-toucher
                    Declencher();
                }
            }
            else
            {
                _entreeDansLeBouton = -1f;
                _declenche = false;
                _matPastille.color = _couleur;
            }
        }

        private void Declencher()
        {
            _prochainePossible = Time.time + 1f;
            VRSons.Jouer(VRSon.Saisie, transform.position, 0.8f, 1.3f);
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null) main.TriggerHaptic(0.5f, 0.1f, 2);
            }
            Presse?.Invoke();
        }

        /// <summary>Un bout d'index (mains nues) ou une manette est-il dans le bouton ?</summary>
        private bool EstTouche()
        {
            Vector3 centre = transform.position;
            if (VRModalityManager.IsHandMode)
            {
                if (VRHandGestures.TryGetIndexTipWorld(XRNode.LeftHand, out Vector3 gauche)
                    && Vector3.Distance(gauche, centre) <= rayonToucher) return true;
                if (VRHandGestures.TryGetIndexTipWorld(XRNode.RightHand, out Vector3 droite)
                    && Vector3.Distance(droite, centre) <= rayonToucher) return true;
                return false;
            }

            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null && Vector3.Distance(main.transform.position, centre) <= rayonToucher + 0.04f) return true;
            }
            return false;
        }

        /// <summary>Tourne le bouton vers le joueur (rotation horizontale seulement).</summary>
        private void Orienter()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 regard = transform.position - cam.transform.position;
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }
    }
}
