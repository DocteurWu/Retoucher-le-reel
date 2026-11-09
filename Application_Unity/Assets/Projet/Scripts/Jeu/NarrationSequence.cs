// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  NarrationSequence.cs — UN MOMENT DE NARRATION ET SON DÉCLENCHEUR
//
//  RÔLE
//    Une séquence = une ou plusieurs répliques + QUAND les dire :
//      • au démarrage (après un délai, 4 s par défaut) ;
//      • à la suite d'une autre séquence (enchaînement) ;
//      • sur un événement du joueur : saisir, lâcher, toucher, regarder un objet,
//        entrer dans une zone, dupliquer, supprimer, ranger dans la bulle,
//        se téléporter, réussir une épreuve, ouvrir une porte ;
//      • à la main (« Manuel ») : appeler Declencher() depuis un UnityEvent.
//
//  À POSER SUR : l'objet « Narration ». On en met AUTANT QU'ON VEUT sur le même
//    objet : toute l'histoire de la scène tient dans un seul GameObject. Toutes
//    les séquences partagent la même <see cref="NarrationBulle"/> (ajoutée
//    automatiquement) : elles passent chacune leur tour, sans se chevaucher.
//
//  ENCHAÎNER : déclencheur « Après Une Autre Narration » + champ « Précédente »
//    vide = la séquence placée JUSTE AU-DESSUS dans l'Inspector. L'ordre des
//    composants devient l'ordre de l'histoire.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Ce qui lance une séquence de narration.</summary>
    public enum DeclencheurNarration
    {
        [Tooltip("Au lancement de la scène, après le délai.")]
        AuDemarrage,
        [Tooltip("Quand la séquence « Précédente » se termine.")]
        ApresUneAutreNarration,
        ObjetSaisi,
        ObjetLache,
        [Tooltip("Une main (paume, manette ou bout d'index) touche l'objet cible.")]
        ObjetTouche,
        [Tooltip("Le joueur regarde l'objet cible pendant « Durée Regard ».")]
        ObjetRegarde,
        [Tooltip("La tête du joueur entre à moins de « Rayon Zone » de l'objet cible.")]
        ZoneAtteinte,
        ObjetDuplique,
        ObjetSupprime,
        [Tooltip("Un objet est rangé dans la bulle du menu poignet.")]
        ObjetRange,
        [Tooltip("L'objet de la bulle du menu poignet en est ressorti.")]
        ObjetRecupere,
        [Tooltip("Le joueur se téléporte (près de l'objet cible s'il est renseigné).")]
        Teleportation,
        [Tooltip("Uniquement par un appel à Declencher() (UnityEvent, autre script).")]
        Manuel,
        [Tooltip("Une épreuve (récipient OU tableau) est réussie. Objet cible = l'épreuve, vide = n'importe laquelle.")]
        EpreuveReussie,
        [Tooltip("Une porte (VRPorte) est ouverte à fond. Objet cible = la porte, vide = n'importe laquelle.")]
        PorteOuverte,
        [Tooltip("Toutes les étapes de ProgressionEtapes sont réussies (la porte de sortie se déverrouille).")]
        EtapesTerminees
    }

    /// <summary>Une phrase de la narration.</summary>
    [System.Serializable]
    public class Replique
    {
        [TextArea(2, 6)]
        public string texte = "";

        [Tooltip("Voix enregistrée (optionnel). L'écriture se cale sur sa durée.")]
        public AudioClip voix;

        [Tooltip("Temps de lecture après écriture, en secondes. 0 = automatique selon la longueur.")]
        [Min(0f)]
        public float duree = 0f;
    }

    /// <summary>
    /// Un moment de l'histoire : des répliques et la condition qui les déclenche.
    /// </summary>
    [RequireComponent(typeof(NarrationBulle))]
    [AddComponentMenu("Remodeler le Reel/Jeu/Narration - Séquence")]
    public class NarrationSequence : MonoBehaviour
    {
        [Tooltip("Nom de la séquence (pour s'y retrouver et pour les logs).")]
        public string nom = "Introduction";

        [Header("Quand")]
        public DeclencheurNarration declencheur = DeclencheurNarration.AuDemarrage;

        [Min(0f)]
        [Tooltip("Attente entre le déclenchement et l'affichage, en secondes.")]
        public float delai = 4f;

        [Tooltip("« Après Une Autre Narration » : séquence à attendre. Vide = celle juste au-dessus dans l'Inspector.")]
        public NarrationSequence precedente;

        [Tooltip("Objet concerné par l'événement. Vide = n'importe quel objet "
                 + "(obligatoire pour Touché, Regardé, Zone).")]
        public GameObject objetCible;

        [Tooltip("Compter aussi les clones (duplications) de l'objet cible.")]
        public bool inclureClones = true;

        [Tooltip("« Objet Touché » : distance main-surface, en mètres.")]
        public float distanceToucher = 0.04f;

        [Tooltip("« Objet Regardé » : temps de regard continu, en secondes.")]
        public float dureeRegard = 1.5f;

        [Range(2f, 45f)]
        [Tooltip("« Objet Regardé » : tolérance du regard, en degrés.")]
        public float angleRegard = 12f;

        [Tooltip("« Zone Atteinte » / « Téléportation » / « Regardé » : distance max, en mètres.")]
        public float rayonZone = 1.5f;

        [Tooltip("Ne jouer qu'une fois. Décoché = rejouable à chaque événement (après la fin de l'affichage).")]
        public bool uneSeuleFois = true;

        [Tooltip("Couper la narration en cours au lieu d'attendre son tour.")]
        public bool interrompre = false;

        [Header("Quoi")]
        public List<Replique> repliques = new List<Replique>();

        [Header("Après")]
        [Tooltip("Appelé à la fin de l'affichage (lancer un son, activer un objet, déclencher une séquence « Manuel »…).")]
        public UnityEvent aLaFin = new UnityEvent();

        /// <summary>Levé à la fin de l'affichage (ou si la séquence est interrompue).</summary>
        public event System.Action<NarrationSequence> Terminee;

        /// <summary>Vrai une fois la séquence affichée au moins une fois.</summary>
        public bool DejaJouee { get; private set; }

        private NarrationBulle _bulle;
        private NarrationSequence _precedenteEcoutee;
        private bool _armee;           // déclenchée, en attente ou en cours d'affichage
        private float _regardCumule;
        private Collider[] _colliders;
        private int _familleCible; // famille de l'objet cible (survit à la suppression de l'original)

        private void Awake()
        {
            // RequireComponent ne s'applique qu'à l'ajout dans l'éditeur : une scène
            // écrite autrement (outil, fusion git) peut ne pas avoir la bulle.
            _bulle = GetComponent<NarrationBulle>();
            if (_bulle == null) _bulle = gameObject.AddComponent<NarrationBulle>();
        }

        private void OnEnable()
        {
            VRGrabHand.ObjetSaisi += SurSaisie;
            VRGrabHand.ObjetLache += SurLacher;
            VRDuplicateDelete.ObjetDuplique += SurDuplication;
            VRDuplicateDelete.ObjetSupprime += SurSuppression;
            VRHandMenu.ObjetRange += SurRangement;
            VRHandMenu.ObjetRecupere += SurRecuperation;
            VRTeleportation.Teleporte += SurTeleportation;
            Epreuve.UneEpreuveReussie += SurEpreuveReussie;
            ProgressionEtapes.Terminees += SurEtapesTerminees;
            VRPorte.Ouverte += SurPorteOuverte;
        }

        private void OnDisable()
        {
            VRGrabHand.ObjetSaisi -= SurSaisie;
            VRGrabHand.ObjetLache -= SurLacher;
            VRDuplicateDelete.ObjetDuplique -= SurDuplication;
            VRDuplicateDelete.ObjetSupprime -= SurSuppression;
            VRHandMenu.ObjetRange -= SurRangement;
            VRHandMenu.ObjetRecupere -= SurRecuperation;
            VRTeleportation.Teleporte -= SurTeleportation;
            Epreuve.UneEpreuveReussie -= SurEpreuveReussie;
            ProgressionEtapes.Terminees -= SurEtapesTerminees;
            VRPorte.Ouverte -= SurPorteOuverte;
            if (_precedenteEcoutee != null) _precedenteEcoutee.Terminee -= SurPrecedenteTerminee;
            _precedenteEcoutee = null;
        }

        private void Start()
        {
            if (objetCible != null)
            {
                _colliders = objetCible.GetComponentsInChildren<Collider>();
                _familleCible = VRCloneOrigin.FamilleDe(objetCible);
            }

            switch (declencheur)
            {
                case DeclencheurNarration.AuDemarrage:
                    Declencher();
                    break;

                case DeclencheurNarration.ApresUneAutreNarration:
                    _precedenteEcoutee = precedente != null ? precedente : SequenceAuDessus();
                    if (_precedenteEcoutee == null || _precedenteEcoutee == this)
                    {
                        Debug.LogWarning("[Narration] « " + nom + " » doit suivre une autre séquence, "
                                         + "mais aucune n'est trouvée au-dessus d'elle.", this);
                        _precedenteEcoutee = null;
                    }
                    else
                    {
                        _precedenteEcoutee.Terminee += SurPrecedenteTerminee;
                    }
                    break;

                case DeclencheurNarration.ObjetTouche:
                case DeclencheurNarration.ObjetRegarde:
                case DeclencheurNarration.ZoneAtteinte:
                    if (objetCible == null)
                    {
                        Debug.LogWarning("[Narration] « " + nom + " » (" + declencheur + ") a besoin d'un « Objet Cible ».", this);
                    }
                    break;
            }
        }

        /// <summary>
        /// Lance la séquence (après <see cref="delai"/>). Sans effet si elle est déjà en
        /// attente ou en cours, ou déjà jouée avec <see cref="uneSeuleFois"/>.
        /// Public : branchable sur n'importe quel UnityEvent.
        /// </summary>
        public void Declencher()
        {
            if (!isActiveAndEnabled || _armee) return;
            if (uneSeuleFois && DejaJouee) return;
            if (repliques == null || repliques.Count == 0)
            {
                Debug.LogWarning("[Narration] « " + nom + " » n'a aucune réplique.", this);
                return;
            }

            _armee = true;
            StartCoroutine(Lancer());
        }

        private IEnumerator Lancer()
        {
            // Au moins une frame d'attente : un enchaînement déclenché depuis la fin
            // d'une autre séquence ne doit pas s'insérer au milieu de son nettoyage.
            if (delai > 0f) yield return new WaitForSeconds(delai);
            else yield return null;

            _bulle.Demander(this);
        }

        /// <summary>Appelé par la bulle quand l'affichage est fini (ou interrompu).</summary>
        internal void NotifierFin()
        {
            DejaJouee = true;
            _armee = false;
            aLaFin?.Invoke();
            Terminee?.Invoke(this);
        }

        // ------------------------------------------------------------------
        // Événements du module VR
        // ------------------------------------------------------------------
        private void SurPrecedenteTerminee(NarrationSequence _) => Declencher();
        private void SurSaisie(VRGrabHand main, Rigidbody corps) => Si(DeclencheurNarration.ObjetSaisi, corps);
        private void SurLacher(VRGrabHand main, Rigidbody corps) => Si(DeclencheurNarration.ObjetLache, corps);
        private void SurDuplication(Rigidbody original, GameObject clone) => Si(DeclencheurNarration.ObjetDuplique, original);
        private void SurSuppression(Rigidbody corps) => Si(DeclencheurNarration.ObjetSupprime, corps);
        private void SurRangement(Rigidbody corps) => Si(DeclencheurNarration.ObjetRange, corps);
        private void SurRecuperation(Rigidbody corps) => Si(DeclencheurNarration.ObjetRecupere, corps);
        private void SurEpreuveReussie(Epreuve epreuve) => Si(DeclencheurNarration.EpreuveReussie, epreuve);
        private void SurEtapesTerminees(ProgressionEtapes progression) => Si(DeclencheurNarration.EtapesTerminees, progression);
        private void SurPorteOuverte(VRPorte porte) => Si(DeclencheurNarration.PorteOuverte, porte);

        private void SurTeleportation(Vector3 destination)
        {
            if (declencheur != DeclencheurNarration.Teleportation) return;
            if (objetCible != null && DistanceA(destination) > rayonZone) return;
            Declencher();
        }

        private void Si(DeclencheurNarration type, Component objet)
        {
            if (declencheur == type && Concerne(objet)) Declencher();
        }

        /// <summary>L'objet de l'événement est-il l'objet cible (ou un de ses enfants / clones) ?</summary>
        private bool Concerne(Component objet)
        {
            if (objetCible == null) return true;
            if (objet == null) return false;

            Transform t = objet.transform;
            Transform cible = objetCible.transform;
            if (t == cible || t.IsChildOf(cible) || cible.IsChildOf(t)) return true;

            return inclureClones && _familleCible != 0 && VRCloneOrigin.FamilleDe(objet.gameObject) == _familleCible;
        }

        // ------------------------------------------------------------------
        // Déclencheurs surveillés à chaque frame (toucher, regard, zone)
        // ------------------------------------------------------------------
        private void Update()
        {
            if (_armee || objetCible == null || (uneSeuleFois && DejaJouee)) return;

            switch (declencheur)
            {
                case DeclencheurNarration.ObjetTouche:
                    if (MainTouche()) Declencher();
                    break;

                case DeclencheurNarration.ZoneAtteinte:
                    Camera cam = Camera.main;
                    if (cam != null && DistanceA(cam.transform.position) <= rayonZone) Declencher();
                    break;

                case DeclencheurNarration.ObjetRegarde:
                    _regardCumule = Regarde() ? _regardCumule + Time.deltaTime : 0f;
                    if (_regardCumule >= dureeRegard)
                    {
                        _regardCumule = 0f;
                        Declencher();
                    }
                    break;
            }
        }

        private bool MainTouche()
        {
            // Manettes / poignets (Transform des VRGrabHand)…
            for (int i = 0; i < VRGrabHand.AllHands.Count; i++)
            {
                VRGrabHand main = VRGrabHand.AllHands[i];
                if (main != null && DistanceA(main.transform.position) <= distanceToucher) return true;
            }
            // … et bouts d'index en mode mains.
            if (VRHandGestures.TryGetIndexTipWorld(UnityEngine.XR.XRNode.LeftHand, out Vector3 gauche)
                && DistanceA(gauche) <= distanceToucher) return true;
            if (VRHandGestures.TryGetIndexTipWorld(UnityEngine.XR.XRNode.RightHand, out Vector3 droite)
                && DistanceA(droite) <= distanceToucher) return true;
            return false;
        }

        private bool Regarde()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 centre = CentreCible();
            Vector3 versCible = centre - cam.transform.position;
            if (versCible.magnitude > Mathf.Max(rayonZone, 0.5f) * 4f) return false;
            return Vector3.Angle(cam.transform.forward, versCible) <= angleRegard;
        }

        private Vector3 CentreCible()
        {
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null && _colliders[i].enabled) return _colliders[i].bounds.center;
                }
            }
            return objetCible.transform.position;
        }

        /// <summary>Distance d'un point à la surface de l'objet cible (à son centre s'il n'a pas de collider).</summary>
        private float DistanceA(Vector3 point)
        {
            float meilleure = float.MaxValue;
            bool trouve = false;
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    Collider c = _colliders[i];
                    if (c == null || !c.enabled) continue;
                    trouve = true;
                    // ClosestPoint n'accepte pas les MeshCollider concaves : on prend la boîte englobante.
                    float d = c is MeshCollider mc && !mc.convex
                        ? Mathf.Sqrt(c.bounds.SqrDistance(point))
                        : Vector3.Distance(c.ClosestPoint(point), point);
                    if (d < meilleure) meilleure = d;
                }
            }
            return trouve ? meilleure : Vector3.Distance(objetCible.transform.position, point);
        }

        private NarrationSequence SequenceAuDessus()
        {
            NarrationSequence[] toutes = GetComponents<NarrationSequence>();
            int index = System.Array.IndexOf(toutes, this);
            return index > 0 ? toutes[index - 1] : null;
        }

        /// <summary>Exemple pré-rempli quand on ajoute le composant.</summary>
        private void Reset()
        {
            repliques = new List<Replique>
            {
                new Replique { texte = "Le silence est total. Quelqu'un est parti sans éteindre la machine." }
            };
        }
    }
}
