// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ObjetMemoire.cs — UN OBJET DU JEU « MÉMOIRE VISUELLE »
//
//  RÔLE
//    Posé sur chaque objet que le joueur doit mémoriser puis replacer. Au démarrage il
//    enregistre la POSE D'ORIGINE de l'objet (centre, rotation, taille) : c'est elle que le
//    joueur doit retrouver. Il sait ensuite :
//      • calculer son écart à l'origine (MemoireVisuelle l'évalue à la validation) ;
//      • être déplacé par le jeu pendant le noir (Deplacer) ou remis en place (RemettreEnPlace) ;
//      • rester EXACTEMENT là où le joueur le lâche (figerAuLacher) : le scan n'a pas de
//        collisions, un objet lâché en l'air ou sur une étagère tomberait à travers le décor.
//
//  À POSER SUR : le parent de l'objet (celui qui porte le Rigidbody + le collider ; le splat
//    de l'objet est son enfant). Cube, balle ou splat segmenté : le jeu ne fait pas la différence.
//    Le menu « Retoucher le Reel > Memoire visuelle > Convertir la selection » l'ajoute pour toi.
//
//  AUTOMATIQUE
//    Par défaut la copie, la suppression et le redimensionnement sont INTERDITS sur cet objet
//    (VRDuplicable / VRScalable ajoutés au démarrage) : au niveau 1 ils fausseraient le jeu.
//
//  LE CENTRE de l'objet est le centre de ses colliders, pas l'origine de son Transform : un objet
//  segmenté garde souvent l'origine de la pièce, qui n'a aucun rapport avec sa place réelle.
//
//  EMPLACEMENTS (facultatif)
//    emplacementFixe : le jeu le met toujours sur cet emplacement ;
//    emplacementsAutorises : il n'a le droit d'aller que sur ceux-là (le livre sur les étagères).
//    Rien de renseigné : n'importe quel emplacement valide.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Objet à mémoriser puis replacer.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Remodeler le Reel/Memoire visuelle/Objet memoire")]
    public class ObjetMemoire : MonoBehaviour
    {
        /// <summary>Tous les objets actifs du jeu (évite une recherche dans la scène).</summary>
        public static readonly List<ObjetMemoire> Tous = new List<ObjetMemoire>();

        [Tooltip("Nom affiché dans le résultat (vide = nom de l'objet).")]
        public string nom = "";

        [Tooltip("Si renseigné : le jeu déplace toujours cet objet sur cet emplacement.")]
        public EmplacementMemoire emplacementFixe;

        [Tooltip("Si non vide : le jeu ne le déplace que sur ces emplacements.")]
        public List<EmplacementMemoire> emplacementsAutorises = new List<EmplacementMemoire>();

        [Tooltip("L'objet reste là où on le lâche (sans gravité). Indispensable avec un scan sans collisions.")]
        public bool figerAuLacher = true;

        [Tooltip("Cocher pour laisser le joueur copier et supprimer cet objet (niveaux avancés).")]
        public bool autoriserCopieEtSuppression = false;

        [Tooltip("Cocher pour laisser le joueur agrandir / rétrécir cet objet (niveaux avancés).")]
        public bool autoriserRedimensionnement = false;

        /// <summary>Le corps physique de l'objet.</summary>
        public Rigidbody Corps { get; private set; }

        /// <summary>Centre d'origine (monde).</summary>
        public Vector3 CentreOrigine { get; private set; }

        /// <summary>Taille (m) : la plus grande dimension de ses colliders au démarrage.</summary>
        public float Taille { get; private set; } = 0.2f;

        /// <summary>Nom lisible.</summary>
        public string NomAffiche => string.IsNullOrWhiteSpace(nom) ? name : nom;

        private Quaternion _rotationOrigine;
        private Vector3 _echelleOrigine;
        private Vector3 _centreLocal; // centre des colliders, dans le repère de l'objet
        private bool _origineEnregistree;

        /// <summary>Centre actuel (monde). Suit le Transform : pas besoin de synchroniser la physique.</summary>
        public Vector3 Centre => transform.TransformPoint(_centreLocal);

        private void Awake()
        {
            Corps = GetComponent<Rigidbody>();
            ConfigurerDroits();
        }

        private void OnEnable()
        {
            if (!Tous.Contains(this)) Tous.Add(this);
            VRGrabHand.ObjetLache += SurLache;
            VRGrabHand.ObjetSaisi += SurSaisie;
        }

        private void OnDisable()
        {
            Tous.Remove(this);
            VRGrabHand.ObjetLache -= SurLache;
            VRGrabHand.ObjetSaisi -= SurSaisie;
        }

        private void Start()
        {
            EnregistrerOrigine();
            // Figé dès le départ : sans collisions sous lui (scan), un objet posé sur une étagère
            // tomberait avant même que le joueur ait pu le voir.
            if (figerAuLacher) Refiger();

            int colliders = 0;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c != null && c.enabled && !c.isTrigger) colliders++;
            }
            Debug.Log("[Memoire] Objet prêt : « " + NomAffiche + " » — colliders " + colliders + ", calque "
                + LayerMask.LayerToName(gameObject.layer) + ", figé " + (Corps != null && Corps.isKinematic) + ", centre " + CentreOrigine.ToString("0.00"), this);
        }

        /// <summary>Mémorise la pose actuelle comme pose d'origine (appelé au démarrage).</summary>
        public void EnregistrerOrigine()
        {
            Bounds boite;
            bool aUnCollider = CalculerBoite(out boite);
            Vector3 centre = aUnCollider ? boite.center : transform.position;
            _centreLocal = transform.InverseTransformPoint(centre);
            Taille = aUnCollider ? Mathf.Max(boite.size.x, Mathf.Max(boite.size.y, boite.size.z)) : 0.2f;
            CentreOrigine = centre;
            _rotationOrigine = transform.rotation;
            _echelleOrigine = transform.localScale;
            _origineEnregistree = true;
        }

        /// <summary>Distance (m) entre le centre actuel et le centre d'origine.</summary>
        public float DistanceALOrigine() => Vector3.Distance(Centre, CentreOrigine);

        /// <summary>Taille actuelle ÷ taille d'origine (1 = inchangée, 2 = deux fois plus grand).</summary>
        public float RatioTaille
        {
            get
            {
                float origine = _echelleOrigine.magnitude;
                return _origineEnregistree && origine > 1e-6f ? transform.localScale.magnitude / origine : 1f;
            }
        }

        /// <summary>
        /// Met l'objet à « ratio » fois sa taille d'origine, en gardant son centre en place (il grandit
        /// ou rétrécit sur lui-même, pas autour de l'origine de son Transform). Le joueur doit l'avoir lâché.
        /// </summary>
        public void ChangerTaille(float ratio)
        {
            if (!_origineEnregistree || ratio <= 0f) return;
            Vector3 centre = Centre;
            transform.localScale = _echelleOrigine * ratio;
            Deplacer(centre); // recentre, synchronise la physique et refige
        }

        /// <summary>Autorise ou interdit au joueur de redimensionner cet objet (réglé par le niveau).</summary>
        public void DefinirRedimensionnement(bool autorise)
        {
            VRScalable echelle = GetComponent<VRScalable>();
            if (echelle != null) echelle.scalingEnabled = autorise || autoriserRedimensionnement;
        }

        /// <summary>Boîte englobante actuelle des colliders (pour les repères du résultat).</summary>
        public bool CalculerBoite(out Bounds boite)
        {
            boite = new Bounds(transform.position, Vector3.zero);
            bool trouve = false;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger) continue;
                if (!trouve) { boite = c.bounds; trouve = true; }
                else boite.Encapsulate(c.bounds);
            }
            return trouve;
        }

        /// <summary>Pose l'objet pour que SON CENTRE soit à cette position (monde). Le joueur doit l'avoir lâché.</summary>
        public void Deplacer(Vector3 nouveauCentre)
        {
            Vector3 decalage = nouveauCentre - Centre;
            transform.position += decalage;
            if (Corps != null) Corps.position = transform.position;
            Physics.SyncTransforms(); // autoSyncTransforms = 0
            Refiger();
        }

        /// <summary>Remet l'objet exactement comme au démarrage (position, rotation, taille).</summary>
        public void RemettreEnPlace()
        {
            if (!_origineEnregistree) return;
            transform.localScale = _echelleOrigine;
            transform.rotation = _rotationOrigine;
            if (Corps != null) Corps.rotation = _rotationOrigine;
            Deplacer(CentreOrigine);
        }

        /// <summary>Immobilise l'objet : sans vitesse, sans gravité, cinématique.</summary>
        public void Refiger()
        {
            if (Corps == null) return;
            if (!Corps.isKinematic)
            {
                // Les vitesses se remettent à zéro AVANT de passer en cinématique : Unity refuse
                // (et avertit) d'écrire une vitesse sur un corps cinématique.
                Corps.linearVelocity = Vector3.zero;
                Corps.angularVelocity = Vector3.zero;
            }
            Corps.useGravity = false;
            Corps.isKinematic = true;
        }

        private void SurLache(VRGrabHand main, Rigidbody corps)
        {
            if (corps != Corps) return;
            Debug.Log("[Memoire] Lâché : « " + NomAffiche + " » à " + Centre.ToString("0.00"), this);
            if (figerAuLacher) Refiger();
        }

        /// <summary>Journal de diagnostic : permet de voir dans la Console si la saisie arrive jusqu'à l'objet.</summary>
        private void SurSaisie(VRGrabHand main, Rigidbody corps)
        {
            if (corps != Corps) return;
            Debug.Log("[Memoire] Saisi : « " + NomAffiche + " » par " + (main != null ? main.name : "?"), this);
        }

        private void ConfigurerDroits()
        {
            VRDuplicable droits = GetComponent<VRDuplicable>();
            if (droits == null) droits = gameObject.AddComponent<VRDuplicable>();
            droits.duplicationEnabled = autoriserCopieEtSuppression;
            droits.deletionEnabled = autoriserCopieEtSuppression;

            VRScalable echelle = GetComponent<VRScalable>();
            if (echelle == null) echelle = gameObject.AddComponent<VRScalable>();
            echelle.scalingEnabled = autoriserRedimensionnement;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Place d'origine (en jeu) : sphère verte de la taille de la marge de base.
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(Application.isPlaying ? CentreOrigine : transform.position, 0.25f);
        }
#endif
    }
}
