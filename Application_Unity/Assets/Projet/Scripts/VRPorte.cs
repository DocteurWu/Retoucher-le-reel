// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRPorte.cs — PORTE À CHARNIÈRE OUVRABLE À LA MAIN
//
//  RÔLE
//    Une porte qu'on ouvre en l'attrapant (poing fermé ou pincement, comme pour un
//    objet) et en la tirant / poussant : elle pivote autour de sa charnière en
//    suivant la main, entre « fermée » et « angleOuverture ». Relâchée, elle reste
//    où elle est.
//
//  STRUCTURE À RESPECTER
//    Porte            ← CE COMPOSANT. Son origine = la CHARNIÈRE, son axe Y = l'axe de rotation.
//    ├── (BoxCollider)  zone de prise : englobe le vantail, du côté charnière vers le côté libre
//    └── Porte_Splat    le Gaussian Splat du vantail (enfant, il tourne avec la porte)
//
//  POURQUOI PAS UN Rigidbody + HingeJoint ?
//    La saisie du projet parente l'objet à la main : une articulation physique serait
//    cassée. Ici la main pilote directement l'angle (calcul d'angle autour de la
//    charnière), c'est fiable et sans physique.
//
//  EN MODE ESCALADE : une main qui tient la porte ne déplace pas le joueur
//    (VRGrabLocomotion lit VRPorte.UneMainTientUnePorte).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    /// <summary>Porte à charnière pilotée par la main.</summary>
    [AddComponentMenu("Remodeler le Reel/VR/Porte (charnière)")]
    public class VRPorte : MonoBehaviour
    {
        /// <summary>Levé quand la porte atteint (presque) son ouverture maximale.</summary>
        public static event Action<VRPorte> Ouverte;

        /// <summary>Levé quand une main essaie d'ouvrir une porte verrouillée (une fois par prise).</summary>
        public static event Action<VRPorte> TentativeVerrouillee;

        /// <summary>Vrai tant qu'une main tient une porte (suspend l'escalade).</summary>
        public static bool UneMainTientUnePorte { get; private set; }

        [Header("Mouvement")]
        [Range(10f, 170f)]
        [Tooltip("Ouverture maximale, en degrés.")]
        public float angleOuverture = 90f;

        [Tooltip("+1 : la porte s'ouvre en tournant vers +Y local (sens horaire vu du dessus) ; -1 : dans l'autre sens. "
                 + "Si elle s'ouvre du mauvais côté, changer le signe.")]
        public float sens = 1f;

        [Tooltip("Porte bloquée (ne s'ouvre pas). Appeler Deverrouiller() depuis une épreuve.")]
        public bool verrouillee = false;

        [Header("Prise")]
        [Tooltip("Zone de la porte où l'on peut la saisir. Vide = premier BoxCollider trouvé.")]
        public BoxCollider zonePrise;

        [Min(0.01f)]
        [Tooltip("Distance max entre la main et le vantail pour le saisir (m).")]
        public float rayonPrise = 0.22f;

        [Range(1f, 30f)]
        [Tooltip("Douceur du suivi de la main (plus grand = plus réactif).")]
        public float suivi = 18f;

        [Header("Événements")]
        public UnityEvent quandOuverte = new UnityEvent();
        public UnityEvent quandFermee = new UnityEvent();

        /// <summary>Vrai tant qu'une main tient CETTE porte.</summary>
        public bool EstTenue => _main != null;

        /// <summary>Ouverture de 0 (fermée) à 1 (ouverte à fond).</summary>
        public float Ouverture01 => angleOuverture > 0f ? _angle / angleOuverture : 0f;

        private Quaternion _rotationFermee;
        private float _angle;          // 0..angleOuverture
        private float _angleCible;
        private VRGrabHand _main;      // main qui tient la porte
        private Vector3 _vecteurDepart;
        private float _angleDepart;
        private bool _etaitOuverte;
        private bool _refusEnCours;    // une main tente d'ouvrir alors que c'est verrouillé

                // [Louai H.] Définition des contraintes physiques et charnières de porte
        private void Awake()
        {
            _rotationFermee = transform.localRotation;
            if (zonePrise == null) zonePrise = GetComponentInChildren<BoxCollider>();
            if (zonePrise == null) Debug.LogWarning("[VRPorte] « " + name + " » n'a pas de BoxCollider de prise : elle ne pourra pas être saisie.", this);
        }

        private static readonly List<VRPorte> s_toutesLesPortes = new List<VRPorte>();

        private void OnEnable()
        {
            if (!s_toutesLesPortes.Contains(this)) s_toutesLesPortes.Add(this);
        }

        /// <summary>Vrai si la position donnée est à proximité d'une porte ouvrable (désactive l'escalade).</summary>
        public static bool UneMainEstProcheDUnePorte(Vector3 positionMain, float rayon = 0.35f)
        {
            for (int i = 0; i < s_toutesLesPortes.Count; i++)
            {
                VRPorte p = s_toutesLesPortes[i];
                if (p != null && p.isActiveAndEnabled && p.zonePrise != null)
                {
                    if (p.DistanceAuVantail(positionMain) <= rayon) return true;
                }
            }
            return false;
        }

        private void OnDisable()
        {
            s_toutesLesPortes.Remove(this);
            if (_main != null) Lacher();
        }

        /// <summary>Débloque la porte (à brancher sur « À La Réussite » d'une épreuve).</summary>
        public void Deverrouiller() => verrouillee = false;

        /// <summary>Referme la porte (instantané).</summary>
        public void Fermer()
        {
            _angle = _angleCible = 0f;
            Appliquer();
        }

                // [L. Hamlat] Suivi de la poignee et resistance physique a la manipulation
        private void Update()
        {
            if (_main == null) ChercherUneMain();
            else SuivreLaMain();

            // Lissage vers la cible (la main bouge par à-coups de tracking).
            if (!Mathf.Approximately(_angle, _angleCible))
            {
                _angle = Mathf.Lerp(_angle, _angleCible, 1f - Mathf.Exp(-suivi * Time.deltaTime));
                if (Mathf.Abs(_angle - _angleCible) < 0.05f) _angle = _angleCible;
                Appliquer();
            }

            // Ouverte à fond : un événement ; refermée : un autre (hystérésis de 3° aux deux bouts).
            if (!_etaitOuverte && _angle >= angleOuverture - 3f)
            {
                _etaitOuverte = true;
                quandOuverte?.Invoke();
                Ouverte?.Invoke(this);
                VRSons.Jouer(VRSon.Impact, transform.position, 0.7f, 0.7f);
                _main?.TriggerHaptic(0.5f, 0.08f, 3);
            }
            else if (_etaitOuverte && _angle <= 3f)
            {
                _etaitOuverte = false;
                quandFermee?.Invoke();
                VRSons.Jouer(VRSon.Impact, transform.position, 0.5f, 0.6f);
            }
        }

        // ------------------------------------------------------------------
        // Prise et suivi de la main
        // ------------------------------------------------------------------
        private void ChercherUneMain()
        {
            if (zonePrise == null) return;

            bool tentative = false;
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main == null || !main.IsGrabInputActive || main.IsHoldingObject) continue;
                if (DistanceAuVantail(main.transform.position) > rayonPrise) continue;

                if (verrouillee)
                {
                    // Verrouillée : petit « clac » et vibration, une seule fois par prise.
                    tentative = true;
                    if (!_refusEnCours)
                    {
                        VRSons.Jouer(VRSon.Impact, main.transform.position, 0.6f, 0.5f);
                        main.TriggerHaptic(0.6f, 0.1f, 3);
                        TentativeVerrouillee?.Invoke(this);
                    }
                    continue;
                }

                Vector3 v = VecteurDepuisCharniere(main.transform.position);
                if (v.sqrMagnitude < 0.05f * 0.05f) continue; // trop près de l'axe : angle instable

                _main = main;
                _vecteurDepart = v;
                _angleDepart = _angleCible;
                UneMainTientUnePorte = true;
                VRSons.Jouer(VRSon.Saisie, main.transform.position, 0.6f, 0.8f);
                main.TriggerHaptic(0.3f, 0.05f, 2);
                return;
            }
            _refusEnCours = tentative;
        }

        private void SuivreLaMain()
        {
            if (_main == null || !_main.IsGrabInputActive || _main.IsHoldingObject)
            {
                Lacher();
                return;
            }

            Vector3 v = VecteurDepuisCharniere(_main.transform.position);
            if (v.sqrMagnitude < 0.05f * 0.05f) return;

            // Rotation de la main autour de la charnière depuis la prise → rotation de la porte.
            float delta = Vector3.SignedAngle(_vecteurDepart, v, transform.up);
            _angleCible = Mathf.Clamp(_angleDepart + delta * Mathf.Sign(sens == 0f ? 1f : sens), 0f, angleOuverture);
        }

        private void Lacher()
        {
            _main = null;
            UneMainTientUnePorte = false;
        }

        /// <summary>Vecteur charnière → main, à plat (perpendiculaire à l'axe de la charnière).</summary>
        private Vector3 VecteurDepuisCharniere(Vector3 pointMonde)
        {
            return Vector3.ProjectOnPlane(pointMonde - transform.position, transform.up);
        }

        /// <summary>Distance du point à la boîte de prise (0 s'il est dedans). Calcul direct : pas de dépendance à la physique.</summary>
        private float DistanceAuVantail(Vector3 pointMonde)
        {
            Transform t = zonePrise.transform;
            Vector3 local = t.InverseTransformPoint(pointMonde) - zonePrise.center;
            Vector3 demi = zonePrise.size * 0.5f;
            Vector3 echelle = t.lossyScale;
            Vector3 hors = new Vector3(
                Mathf.Max(0f, Mathf.Abs(local.x) - demi.x) * Mathf.Abs(echelle.x),
                Mathf.Max(0f, Mathf.Abs(local.y) - demi.y) * Mathf.Abs(echelle.y),
                Mathf.Max(0f, Mathf.Abs(local.z) - demi.z) * Mathf.Abs(echelle.z));
            return hors.magnitude;
        }

        private void Appliquer()
        {
            transform.localRotation = _rotationFermee * Quaternion.AngleAxis(Mathf.Sign(sens == 0f ? 1f : sens) * _angle, Vector3.up);
        }
    }
}
