// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuveMemoire.cs — ÉPREUVE « JEU DE MÉMOIRE » (Scene3)
//
//  DÉROULÉ
//    1. Observation (dureeObservation) : les objets sont à leur place dans le décor,
//       intouchables (colliders coupés). Un panneau au-dessus du tas affiche le compte
//       à rebours.
//    2. Mélange : chaque objet s'envole en arc vers le milieu de la pièce (position de
//       CE GameObject) et tombe dans un tas.
//    3. Replacement : le joueur reprend chaque objet et le repose à sa place. Lâché à
//       moins de toleranceDistance de sa place (et, si toleranceAngle > 0, assez bien
//       tourné), l'objet se cale tout seul et se fige ; le reprendre l'enlève de sa place.
//    4. Tous à leur place → son, félicitation, étape réussie.
//
//  OBJETS (pas de réglage à la main)
//    Chaque ENFANT DIRECT de « conteneur » (vide = enfants de ce GameObject) est un objet
//    du jeu. Un enfant sans Rigidbody (splat segmenté importé tel quel, origine = celle
//    de la pièce) est enveloppé au lancement dans un parent « <nom> (mémoire) » : Rigidbody
//    + BoxCollider calé sur les bornes du splat (asset Gaussian) ou des Renderer. Un objet
//    déjà préparé (parent Rigidbody + collider, comme dans la scène céramique) est pris tel quel.
//
//  NIVEAUX (`niveaux`, coché par défaut ; idée et évaluation de taille de Yassine, branche
//  feature/memoire-visuelle, adaptées au mélange en tas)
//    Les étapes 1 à 3 ci-dessus se répètent trois fois ; entre deux niveaux, les objets
//    restent à leur place. Ni copie ni suppression des objets du jeu, à aucun niveau.
//      Niveau 1 : replacer seulement, taille bloquée (VRScalable.scalingEnabled = faux).
//      Niveau 2 : les objets atterrissent à ×0,8 / ×1,25 : il faut aussi retrouver leur taille
//                 (à toleranceTaille près, EvaluationMemoire.TailleRetrouvee) pour qu'ils se calent.
//      Niveau 3 : ×0,65 / ×1,5 et observation deux fois plus courte.
//    Victoire de l'épreuve à la fin du niveau 3.
//
//  AIDE AUTOMATIQUE (`aideApresTenue`, 20 s)
//    Un objet tenu 20 s (cumulées depuis le mélange) sans être remis à sa place : la main
//    guide surgit (Scene3 : à sa position posée, au milieu de la Grange) et va montrer la
//    place de CET objet. Lâché ou calé : elle repart. Le temps repart de zéro une fois calé.
//
//  MAIN GUIDE (option développeur, `guideVersLaPlace`, désactivée par défaut)
//    Quand le joueur tient un objet du jeu, la main guide va montrer sa place
//    (« C'est ici ! ») : un repère invisible (trigger, calque Ignore Raycast) posé à la
//    place d'origine, actif seulement pendant que l'objet est tenu. Rien n'est montré
//    pendant l'observation ni quand le joueur a les mains vides. La touche G du panneau
//    de démo coupe / remet la main (ReglagesAide.MainGuide).
//
//  NARRATION
//    • Avec un ProgressionEtapes qui liste cette épreuve : il annonce `consigne` (à écrire
//      comme l'explication de l'observation) puis `felicitation`.
//    • Sans : l'épreuve annonce elle-même `consigne` au début de l'observation.
//    • Au mélange, l'épreuve annonce toujours `consigneReplacement`.
//
//  À POSER SUR : un objet vide « Epreuve_Memoire » posé au sol au MILIEU de la pièce.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using GaussianSplatting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using RemodelerLeReel.VR;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Jeu de mémoire : retenir la place des objets, puis les remettre en place après le mélange.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Jeu de mémoire")]
    public class EpreuveMemoire : Epreuve
    {
        private enum Phase { Attente, Observation, Melange, Replacement, Terminee }

        [Header("Objets")]
        [Tooltip("Parent des objets du jeu : chaque enfant direct = un objet. Vide = les enfants de cet objet.")]
        public Transform conteneur;

        [Header("Déroulé")]
        [Tooltip("Secondes avant le début de l'observation (laisser la narration d'accueil se dire).")]
        [Min(0f)] public float delaiDepart = 6f;
        [Tooltip("Secondes pour observer la pièce avant le mélange.")]
        [Min(1f)] public float dureeObservation = 20f;
        [Tooltip("Durée du vol de chaque objet vers le tas (s).")]
        [Min(0.1f)] public float dureeVol = 1.4f;
        [Tooltip("Décalage entre deux départs d'objets (s).")]
        [Min(0f)] public float ecartDeparts = 0.2f;
        [Tooltip("Rayon du tas au milieu de la pièce (m).")]
        [Min(0.05f)] public float rayonTas = 0.35f;

        [Header("Replacement")]
        [Tooltip("Distance max entre l'objet lâché et sa place pour qu'il s'y cale (m).")]
        [Min(0.01f)] public float toleranceDistance = 0.2f;
        [Tooltip("Écart d'orientation max (degrés). 0 = l'orientation ne compte pas (l'objet se remet droit en se calant).")]
        [Range(0f, 180f)] public float toleranceAngle = 0f;
        [Tooltip("Tolérance ajoutée selon la taille de l'objet (fraction de sa demi-diagonale) : on pose « à côté » d'un gros objet plus loin de son centre.")]
        [Range(0f, 2f)] public float margeTaille = 0.75f;
        [Tooltip("Plafond de cette marge liée à la taille (m).")]
        [Min(0f)] public float margeTailleMax = 0.4f;
        [Tooltip("Objet perdu (tombé sous le sol ou parti à plus de cette distance du tas, en m) : ramené sur le tas.")]
        [Min(1f)] public float distancePerdu = 12f;

        [Header("Narration")]
        [TextArea(1, 3)]
        public string consigneReplacement = "Tout s'est mélangé ! Reprends chaque objet au milieu de la pièce et remets-le exactement à sa place.";

        [Tooltip("Panneau au-dessus du tas : compte à rebours, puis objets replacés.")]
        public bool afficherPanneau = true;
        [Min(0.5f)] public float hauteurPanneau = 1.7f;

        [Header("Niveaux")]
        [Tooltip("Trois niveaux enchaînés : 1 = replacer (taille bloquée) ; 2 = replacer ET retrouver la taille (×0,8 / ×1,25) ; 3 = tailles plus fausses (×0,65 / ×1,5), observation deux fois plus courte. Décoché = une seule manche, comme avant.")]
        public bool niveaux = true;
        [Tooltip("Écart de taille toléré pour se caler aux niveaux 2 et 3 (0,15 = entre ×0,87 et ×1,15).")]
        [Range(0.02f, 0.5f)] public float toleranceTaille = 0.15f;
        [Tooltip("Pause entre deux niveaux (s), objets à leur place.")]
        [Min(0f)] public float pauseEntreNiveaux = 3f;

        /// <summary>Niveau en cours (1 à 3), 0 sans niveaux.</summary>
        private int _niveau;

        /// <summary>Niveau en cours (1 à NombreNiveaux), 0 sans niveaux ou avant le début.</summary>
        public int Niveau => _niveau;

        [Header("Aide automatique")]
        [Tooltip("Un objet tenu ce temps (s, cumulé) sans être remis à sa place : la main guide vient montrer sa place. 0 = jamais.")]
        [Min(0f)] public float aideApresTenue = 20f;

        [Header("Aide (développeur)")]
        [Tooltip("La main guide montre la place de l'objet que le joueur tient. Désactivé par défaut : c'est un jeu de mémoire.")]
        public bool guideVersLaPlace = false;

        public UnityEvent aLaReussite = new UnityEvent();

        /// <summary>Un objet du jeu et sa place d'origine.</summary>
        private class Objet
        {
            public Rigidbody corps;
            public int famille;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 echelle;
            public bool place;
            public BoxCollider repere; // place d'origine, montrée par la main guide (guideVersLaPlace)
            public float tolerance;    // distance de calage de CET objet (toleranceDistance + marge selon sa taille)
            public bool aPorteeEnMain; // pour le petit son « c'est ici » quand l'objet tenu entre dans sa zone
            public float prochaineRecherche; // original détruit : prochaine recherche d'une copie (pas à chaque contrôle)
            public float tempsTenu;          // secondes passées en main depuis le mélange (cumulé), remis à 0 une fois calé
        }

        /// <summary>Vrai si l'aide automatique a allumé la main guide (elle l'éteindra quand l'objet sera lâché).</summary>
        private bool _aideAuto;
        private float _dernierControle;

        /// <summary>Temps tenu à partir duquel la main guide vient aider (fonction pure : testée en EditMode).</summary>
        public static bool AideNecessaire(float tempsTenu, float seuil) => seuil > 0f && tempsTenu >= seuil;

        /// <summary>
        /// Un Transform a été déplacé à la main cette image : Physics.SyncTransforms() une seule fois
        /// en fin d'image (LateUpdate) au lieu d'une fois par objet et par image (10 objets en vol
        /// pendant le mélange = 10 synchronisations de toute la scène physique par image).
        /// </summary>
        private bool _syncDemandee;
        private void DemanderSync() => _syncDemandee = true;

        private void LateUpdate()
        {
            if (!_syncDemandee) return;
            _syncDemandee = false;
            Physics.SyncTransforms();
        }

        private readonly List<Objet> _objets = new List<Objet>();
        private Phase _phase = Phase.Attente;
        private float _finObservation;
        private float _prochainControle;
        private TextMeshPro _panneau;

        /// <summary>Objets remis à leur place / nombre total.</summary>
        public int NombrePlaces { get { int n = 0; foreach (Objet o in _objets) if (o.place) n++; return n; } }

        // ------------------------------------------------------------------
        // Préparation
        // ------------------------------------------------------------------
                // [L. Hamlat] Parametrage du protocole de memorisation visuelle et des ancres
        private void Start()
        {
            Transform racine = conteneur != null ? conteneur : transform;
            var enfants = new List<Transform>();
            foreach (Transform t in racine) enfants.Add(t);

            foreach (Transform t in enfants)
            {
                if (_panneau != null && t == _panneau.transform) continue;
                Rigidbody corps = t.GetComponent<Rigidbody>();
                if (corps == null) corps = Envelopper(t, racine);
                if (corps == null) continue;

                _objets.Add(new Objet
                {
                    corps = corps,
                    famille = VRCloneOrigin.FamilleDe(corps.gameObject),
                    position = corps.transform.position,
                    rotation = corps.transform.rotation,
                    echelle = corps.transform.localScale,
                    tolerance = ToleranceEffective(toleranceDistance, DemiDiagonale(corps), margeTaille, margeTailleMax),
                });
                corps.isKinematic = true;
                Saisissable(corps, false); // pas touche pendant l'observation
                InterdireCopie(corps);
            }
            Physics.SyncTransforms();

            if (_objets.Count == 0)
            {
                Debug.LogWarning("[Memoire] « " + name + " » : aucun objet sous « " + racine.name + " ». Glisser les splats segmentés dedans.", this);
                enabled = false;
                return;
            }
            Debug.Log("[Memoire] " + _objets.Count + " objets à retenir.", this);

            PreparerReperes(); // toujours : la main guide peut être allumée en cours de partie (case ou touche G)

            if (afficherPanneau)
            {
                _panneau = CreerPanneau("Panneau_Memoire", new Vector2(1.4f, 0.3f));
                _panneau.color = Color.white;
                _panneau.text = "";
            }
            StartCoroutine(Deroule());
        }

        /// <summary>
        /// Repères de place (un par objet), montrés par la main guide quand elle est allumée. Ils
        /// sont toujours créés : la main peut être allumée en cours de partie (case de l'Inspector
        /// ou touche G du panneau de démo), pas seulement au lancement.
        /// </summary>
        private void PreparerReperes()
        {
            int calque = LayerMask.NameToLayer("Ignore Raycast"); // ni téléportation ni saisie
            foreach (Objet o in _objets)
            {
                GameObject go = new GameObject("Place " + o.corps.name.Replace(" (mémoire)", ""));
                go.transform.SetParent(transform, true);
                go.transform.SetPositionAndRotation(o.position, o.rotation);
                if (calque >= 0) go.layer = calque;
                o.repere = go.AddComponent<BoxCollider>();
                o.repere.isTrigger = true;
                BoxCollider forme = o.corps.GetComponent<BoxCollider>();
                if (forme != null)
                {
                    o.repere.center = Vector3.Scale(forme.center, o.corps.transform.lossyScale);
                    o.repere.size = Vector3.Scale(forme.size, o.corps.transform.lossyScale);
                }
                o.repere.enabled = false;
            }
        }

        /// <summary>Dernier état de guideVersLaPlace transmis à la progression (suivi de la case pendant le jeu).</summary>
        private bool _guideApplique;
        private ProgressionEtapes _progression;

        /// <summary>
        /// La case guideVersLaPlace est suivie pendant le jeu : la cocher dans l'Inspector allume la
        /// main guide (avant, elle n'était lue qu'au lancement : la cocher en jeu ne faisait rien).
        /// </summary>
        private void SuivreCaseGuide()
        {
            if (guideVersLaPlace == _guideApplique) return;
            _guideApplique = guideVersLaPlace;
            if (_progression == null) _progression = FindFirstObjectByType<ProgressionEtapes>();
            if (_progression != null && _progression.etapes.Contains(this))
            {
                _progression.ActiverMainGuide(guideVersLaPlace);
                Debug.Log("[Memoire] Main guide " + (guideVersLaPlace ? "allumée" : "éteinte") + " (case guideVersLaPlace).", this);
            }
            else Debug.LogWarning("[Memoire] guideVersLaPlace : pas de ProgressionEtapes qui liste l'épreuve, pas de main guide.", this);
        }

        /// <summary>Splat sans Rigidbody : parent Rigidbody + BoxCollider calé sur ses bornes en monde.</summary>
        private Rigidbody Envelopper(Transform objet, Transform racine)
        {
            if (!BornesMonde(objet, out Bounds b))
            {
                Debug.LogWarning("[Memoire] « " + objet.name + " » ignoré : ni splat ni Renderer pour en mesurer la taille.", objet);
                return null;
            }
            GameObject parent = new GameObject(objet.name + " (mémoire)");
            parent.transform.SetParent(racine, false);
            parent.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            parent.transform.localScale = Vector3.one;
            objet.SetParent(parent.transform, true);

            BoxCollider boite = parent.AddComponent<BoxCollider>();
            Vector3 l = parent.transform.lossyScale;
            boite.size = new Vector3(b.size.x / Mathf.Abs(l.x), b.size.y / Mathf.Abs(l.y), b.size.z / Mathf.Abs(l.z));

            Rigidbody corps = parent.AddComponent<Rigidbody>();
            corps.mass = 1f;
            corps.interpolation = RigidbodyInterpolation.Interpolate;
            corps.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return corps;
        }

        /// <summary>Boîte englobante en monde : bornes de l'asset Gaussian (8 coins transformés) et des Renderer.</summary>
        private static bool BornesMonde(Transform objet, out Bounds b)
        {
            b = default;
            bool trouve = false;
            foreach (GaussianSplatRenderer splat in objet.GetComponentsInChildren<GaussianSplatRenderer>())
            {
                if (splat.asset == null) continue;
                Vector3 min = splat.asset.boundsMin, max = splat.asset.boundsMax;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 coin = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = splat.transform.TransformPoint(coin);
                    if (!trouve) { b = new Bounds(p, Vector3.zero); trouve = true; }
                    else b.Encapsulate(p);
                }
            }
            foreach (Renderer r in objet.GetComponentsInChildren<Renderer>())
            {
                if (!trouve) { b = r.bounds; trouve = true; }
                else b.Encapsulate(r.bounds);
            }
            // Un objet tout plat (tableau) garde une épaisseur saisissable.
            if (trouve) b.size = Vector3.Max(b.size, Vector3.one * 0.03f);
            return trouve;
        }

        /// <summary>
        /// Un objet du jeu ne se duplique pas et ne se supprime pas : une copie posée à la place
        /// n'était pas comptée (seul l'original l'est) et le joueur restait bloqué à 9 / 10.
        /// </summary>
        private static void InterdireCopie(Rigidbody corps)
        {
            VRDuplicable marque = corps.GetComponent<VRDuplicable>();
            if (marque == null) marque = corps.gameObject.AddComponent<VRDuplicable>();
            marque.duplicationEnabled = false;
            marque.deletionEnabled = false;
        }

        /// <summary>Colliders coupés = la main ne peut pas l'attraper.</summary>
        private static void Saisissable(Rigidbody corps, bool oui)
        {
            if (corps == null) return;
            foreach (Collider c in corps.GetComponentsInChildren<Collider>()) c.enabled = oui;
        }

        // ------------------------------------------------------------------
        // Déroulé
        // ------------------------------------------------------------------
        /// <summary>Vrai tant qu'on attend le départ ou qu'on observe : l'attente peut être passée depuis le PC.</summary>
        public bool PeutPasserAttente => _phase == Phase.Attente || _phase == Phase.Observation;

        /// <summary>Démo (panneau du PC, touche P) : saute le délai de départ et le compte à rebours, le mélange part tout de suite.</summary>
        public void PasserAttente()
        {
            if (!PeutPasserAttente) return;
            Debug.Log("[Demo] Jeu de mémoire : attente passée depuis le PC.", this);
            _passerAttente = true;
        }

        private bool _passerAttente;

        private IEnumerator Deroule()
        {
            for (float fin = Time.time + delaiDepart; Time.time < fin && !_passerAttente;) yield return null;
            if (EstReussie) yield break;

            int nombre = niveaux ? NombreNiveaux : 1;
            for (int n = 1; n <= nombre; n++)
            {
                _niveau = niveaux ? n : 0;
                PreparerNiveau();

                _phase = Phase.Observation;
                _finObservation = Time.time + DureeObservationNiveau(_niveau, dureeObservation);
                if (n == 1 && !AnnonceeParLaProgression()) Dire(consigne);
                int secondesAffichees = -1;
                while (Time.time < _finObservation && !_passerAttente)
                {
                    if (EstReussie) yield break;
                    // Texte refait une fois par seconde seulement (une chaîne neuve à chaque image sinon).
                    int secondes = Mathf.CeilToInt(_finObservation - Time.time);
                    if (secondes != secondesAffichees)
                    {
                        secondesAffichees = secondes;
                        Afficher(PrefixeNiveau() + "Retiens la place des objets : " + secondes);
                    }
                    yield return null;
                }
                _passerAttente = false; // la touche P ne passe que l'attente EN COURS (pas celles des niveaux suivants)

                _phase = Phase.Melange;
                Afficher(PrefixeNiveau() + "Mélange !");
                VRSons.Jouer(VRSon.Teleportation, transform.position + Vector3.up, 0.8f);
                for (int i = 0; i < _objets.Count; i++)
                {
                    StartCoroutine(Envoler(_objets[i], i));
                    yield return new WaitForSeconds(ecartDeparts);
                }
                yield return new WaitForSeconds(dureeVol + 0.5f);
                if (EstReussie) yield break;

                _phase = Phase.Replacement;
                Dire(consigneReplacement);
                AfficherCompte();

                if (!niveaux || n == nombre) yield break; // dernier niveau : la victoire vient du dernier objet calé (Placer)
                while (NombrePlaces < _objets.Count && !EstReussie) yield return null;
                if (EstReussie) yield break;

                // Niveau réussi : petite pause, les objets restent à leur place pour le niveau suivant.
                _phase = Phase.Melange;
                Afficher("Niveau " + n + " réussi !");
                VRSons.Jouer(VRSon.Reussite, transform.position + Vector3.up, 0.8f, 1.2f);
                yield return new WaitForSeconds(pauseEntreNiveaux);
            }
        }

        // ------------------------------------------------------------------
        // Niveaux (idée de Yassine, adaptée au mélange en tas)
        // ------------------------------------------------------------------
        /// <summary>Nombre de niveaux enchaînés quand `niveaux` est coché.</summary>
        public const int NombreNiveaux = 3;

        /// <summary>
        /// Taille d'un objet à l'atterrissage sur le tas, en multiple de sa taille d'origine.
        /// Niveau 1 (ou sans niveaux) : inchangée. Niveau 2 : ×0,8 / ×1,25 un objet sur deux.
        /// Niveau 3 : ×0,65 / ×1,5. (Fonction pure : testée en EditMode.)
        /// </summary>
        public static float RatioTailleMelange(int niveau, int indice)
        {
            if (niveau <= 1) return 1f;
            bool petit = indice % 2 == 0;
            if (niveau == 2) return petit ? 0.8f : 1.25f;
            return petit ? 0.65f : 1.5f;
        }

        /// <summary>Durée d'observation d'un niveau : moitié au niveau 3. (Fonction pure.)</summary>
        public static float DureeObservationNiveau(int niveau, float duree) => niveau >= 3 ? duree * 0.5f : duree;

        /// <summary>La taille compte-t-elle pour se caler ? Seulement aux niveaux 2 et 3. (Fonction pure.)</summary>
        public static bool TailleACompte(int niveau) => niveau >= 2;

        /// <summary>
        /// Début d'un niveau : plus rien n'est calé, temps tenu à zéro, objets intouchables pendant
        /// l'observation. Taille modifiable seulement là où elle compte (niveau 1 : on place juste).
        /// </summary>
        private void PreparerNiveau()
        {
            bool tailleLibre = !niveaux || TailleACompte(_niveau);
            foreach (Objet o in _objets)
            {
                o.place = false;
                o.tempsTenu = 0f;
                o.aPorteeEnMain = false;
                Rigidbody corps = Representant(o);
                if (corps == null) continue;
                Saisissable(corps, false);
                VRScalable echelle = corps.GetComponent<VRScalable>();
                if (echelle == null) echelle = corps.gameObject.AddComponent<VRScalable>(); // taille de référence = celle d'origine
                echelle.scalingEnabled = tailleLibre;
            }
        }

        private string PrefixeNiveau() => _niveau > 0 ? "Niveau " + _niveau + " · " : "";

        /// <summary>Vrai au dernier niveau (ou sans niveaux) : tous les objets calés = épreuve gagnée.</summary>
        private bool DernierNiveau => !niveaux || _niveau >= NombreNiveaux;

        /// <summary>Vol en arc vers le tas, puis l'objet tombe (physique) et devient saisissable.</summary>
        private IEnumerator Envoler(Objet o, int indice)
        {
            Rigidbody corps = o.corps;
            if (corps == null) yield break;
            Liberer(corps);
            corps.isKinematic = true;

            // Points du tas en spirale (angle d'or) pour qu'ils ne tombent pas tous au même endroit.
            Vector3 arrivee = transform.position + DecalageTas(indice, _objets.Count, rayonTas);
            Quaternion rotArrivee = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * o.rotation;

            Vector3 depart = corps.transform.position;
            Quaternion rotDepart = corps.transform.rotation;
            // Niveaux 2 et 3 : l'objet change de taille en vol (à retrouver en le replaçant).
            Vector3 echelleDepart = corps.transform.localScale;
            Vector3 echelleArrivee = o.echelle * RatioTailleMelange(_niveau, indice);
            float hauteurArc = 0.4f + 0.3f * Vector3.Distance(depart, arrivee);
            for (float t = 0f; t < 1f; t += Time.deltaTime / dureeVol)
            {
                if (corps == null || EstReussie) yield break;
                float s = Mathf.SmoothStep(0f, 1f, t);
                corps.transform.SetPositionAndRotation(
                    Vector3.Lerp(depart, arrivee, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * hauteurArc,
                    Quaternion.Slerp(rotDepart, rotArrivee, s));
                corps.transform.localScale = Vector3.Lerp(echelleDepart, echelleArrivee, s);
                DemanderSync(); // autoSyncTransforms = 0 : une seule synchro par image pour tous les objets
                yield return null;
            }
            if (corps == null) yield break;
            corps.transform.SetPositionAndRotation(arrivee, rotArrivee);
            corps.transform.localScale = echelleArrivee;
            Saisissable(corps, true);
            corps.isKinematic = false;
            Physics.SyncTransforms();
            VRSons.Jouer(VRSon.Lacher, arrivee, 0.6f);
        }

        // ------------------------------------------------------------------
        // Replacement
        // ------------------------------------------------------------------
        private void Update()
        {
            SuivreCaseGuide();
            if (_panneau != null && _panneau.gameObject.activeSelf)
            {
                PlacerPanneau(_panneau, transform.position + Vector3.up * hauteurPanneau);
            }
            if (_phase != Phase.Replacement || EstReussie || Time.time < _prochainControle) return;
            _prochainControle = Time.time + 0.15f;
            float ecoule = _dernierControle > 0f ? Time.time - _dernierControle : 0f;
            _dernierControle = Time.time;

            bool change = false;
            bool aideNecessaire = false;
            foreach (Objet o in _objets)
            {
                Rigidbody corps = Representant(o);
                if (corps == null) continue;

                // Repris en main : il n'est plus à sa place.
                if (EstTenu(corps))
                {
                    o.tempsTenu += ecoule;
                    if (AideNecessaire(o.tempsTenu, aideApresTenue)) aideNecessaire = true;
                    if (o.place) { o.place = false; change = true; }
                    // Petit son quand l'objet tenu entre dans sa zone : « lâche ici ».
                    bool aPortee = APortee(o, corps);
                    if (aPortee && !o.aPorteeEnMain) VRSons.Jouer(VRSon.Saisie, corps.transform.position, 0.35f, 1.6f);
                    o.aPorteeEnMain = aPortee;
                    continue;
                }
                o.aPorteeEnMain = false;
                if (o.place) continue;
                if (DansLaBulle(corps)) continue; // la bulle le tient : ni calé ni « perdu »
                if (EstPerdu(corps.transform.position)) { Ramener(corps); continue; }
                if (!APortee(o, corps)) continue;

                Placer(o, corps);
                if (EstReussie) return;
            }
            GererAideAuto(aideNecessaire);
            if (!change) return;
            AfficherCompte();

            if (NombrePlaces == _objets.Count && DernierNiveau) Gagner();
        }

        /// <summary>
        /// Aide automatique : un objet tenu depuis aideApresTenue secondes (cumulées) sans être remis
        /// à sa place → la main guide surgit et va montrer sa place ; plus rien à aider (lâché, calé)
        /// → elle repart. Sans effet si le dev l'a déjà allumée (touche G, case guideVersLaPlace).
        /// </summary>
        private void GererAideAuto(bool necessaire)
        {
            ProgressionEtapes p = _progression != null ? _progression : (_progression = FindFirstObjectByType<ProgressionEtapes>());
            if (p == null || !p.etapes.Contains(this)) return;

            if (necessaire && !_aideAuto && !p.MainGuideVisible)
            {
                _aideAuto = true;
                p.ActiverMainGuide(true);
                Debug.Log("[Memoire] Objet tenu " + aideApresTenue + " s sans être remis : la main guide vient aider.", this);
            }
            else if (!necessaire && _aideAuto)
            {
                _aideAuto = false;
                if (!guideVersLaPlace) p.ActiverMainGuide(false);
            }
        }

        /// <summary>L'objet du jeu, ou une de ses copies si l'original a été supprimé.</summary>
        private Rigidbody Representant(Objet o)
        {
            if (o.corps != null) return o.corps;
            // FindObjectsByType parcourt toute la scène : au plus une fois par seconde par objet
            // (avant : à chaque contrôle et à chaque image de la surbrillance / main guide).
            if (Time.time < o.prochaineRecherche) return null;
            o.prochaineRecherche = Time.time + 1f;
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie != null && copie.famille == o.famille && copie.TryGetComponent(out Rigidbody rb))
                {
                    o.corps = rb;
                    return rb;
                }
            }
            return null;
        }

        /// <summary>
        /// Assez près de sa place (et assez bien tourné si toleranceAngle > 0) ; aux niveaux 2 et 3,
        /// il faut aussi avoir retrouvé sa taille (EvaluationMemoire, module de Yassine).
        /// </summary>
        private bool APortee(Objet o, Rigidbody corps) =>
            EstAPortee(corps.transform.position, corps.transform.rotation, o.position, o.rotation, o.tolerance, toleranceAngle) &&
            (!TailleACompte(_niveau) || EvaluationMemoire.TailleRetrouvee(RatioTaille(corps, o), toleranceTaille));

        /// <summary>Taille actuelle / taille d'origine (le redimensionnement est uniforme).</summary>
        private static float RatioTaille(Rigidbody corps, Objet o) =>
            o.echelle.x != 0f ? Mathf.Abs(corps.transform.localScale.x / o.echelle.x) : 1f;

        /// <summary>
        /// Distance de calage d'un objet : la tolérance de base plus une marge liée à sa taille
        /// (plafonnée). Posé « à côté » de sa place, un gros objet a son centre plus loin que celui
        /// d'une assiette. (Fonction pure : testée en EditMode.)
        /// </summary>
        public static float ToleranceEffective(float toleranceBase, float demiDiagonale, float marge, float margeMax) =>
            toleranceBase + Mathf.Min(Mathf.Max(0f, demiDiagonale) * marge, margeMax);

        /// <summary>Demi-diagonale de la boîte de l'objet en monde (0 sans BoxCollider).</summary>
        private static float DemiDiagonale(Rigidbody corps)
        {
            BoxCollider b = corps.GetComponent<BoxCollider>();
            return b != null ? Vector3.Scale(b.size, corps.transform.lossyScale).magnitude * 0.5f : 0f;
        }

        // ------------------------------------------------------------------
        // Lâcher : vérifié tout de suite (sans attendre le contrôle périodique)
        // ------------------------------------------------------------------
        private void OnEnable()
        {
            VRGrabHand.ObjetLache += AuLacher;
            VRGrabHand.ObjetSaisi += ALaSaisie;
            VRHandMenu.ObjetRange += AuRangement;
        }

        private void OnDisable()
        {
            VRGrabHand.ObjetLache -= AuLacher;
            VRGrabHand.ObjetSaisi -= ALaSaisie;
            VRHandMenu.ObjetRange -= AuRangement;
        }

        /// <summary>
        /// Rangé dans la bulle du poignet : plus à sa place. Lâché dans la bulle tout près de sa
        /// place, l'objet était d'abord calé (AuLacher) puis rangé par la bulle à l'image suivante,
        /// et restait compté « à sa place » en flottant dans la bulle.
        /// </summary>
        private void AuRangement(Rigidbody corps) => ALaSaisie(null, corps);

        private VRHandMenu _menu;
        private float _prochaineRechercheMenu;

        /// <summary>Vrai si l'objet est rangé dans la bulle du poignet (on ne le cale pas : la bulle le garde).</summary>
        private bool DansLaBulle(Rigidbody corps)
        {
            if (_menu == null && Time.time >= _prochaineRechercheMenu)
            {
                _prochaineRechercheMenu = Time.time + 1f;
                _menu = FindFirstObjectByType<VRHandMenu>();
            }
            return _menu != null && _menu.ObjetDansLaBulle == corps;
        }

        /// <summary>
        /// Un objet calé repris en main n'est plus à sa place, TOUT DE SUITE : avant, il ne l'était
        /// qu'au contrôle suivant (0,15 s). Repris et relâché entre deux contrôles, il restait compté
        /// « à sa place » alors qu'il tombait (il n'était plus jamais revérifié).
        /// </summary>
        private void ALaSaisie(VRGrabHand main, Rigidbody corps)
        {
            if (_phase != Phase.Replacement || EstReussie || corps == null) return;
            foreach (Objet o in _objets)
            {
                if (!o.place || Representant(o) != corps) continue;
                o.place = false;
                AfficherCompte();
                return;
            }
        }

        /// <summary>
        /// Un objet lâché près de sa place s'y cale à l'instant du lâcher : sans ça, un objet posé
        /// en hauteur (étagère, mur : le scan n'a pas de collider) tombait avant le contrôle suivant
        /// et ressortait de sa zone.
        /// </summary>
        private void AuLacher(VRGrabHand main, Rigidbody corps)
        {
            if (_phase != Phase.Replacement || EstReussie || corps == null) return;
            foreach (Objet o in _objets)
            {
                if (Representant(o) != corps) continue;
                o.aPorteeEnMain = false;
                o.place = false; // filet : un objet lâché redevient dynamique, il n'est plus calé
                if (APortee(o, corps)) Placer(o, corps);
                else AfficherCompte();
                return;
            }
        }

        /// <summary>Cale l'objet à sa place, son, compteur, victoire si c'était le dernier.</summary>
        private void Placer(Objet o, Rigidbody corps)
        {
            o.place = true; // AVANT la coroutine : sa 1re image s'exécute tout de suite et s'arrête si l'objet n'est pas placé
            o.tempsTenu = 0f; // retrouvé : l'aide repart de zéro s'il est repris
            StartCoroutine(Caler(o, corps));
            VRSons.Jouer(VRSon.Reussite, o.position, 0.45f, 0.9f + 0.6f * NombrePlaces / _objets.Count);
            AfficherCompte();
            if (NombrePlaces == _objets.Count && DernierNiveau) Gagner(); // niveaux 1-2 : le déroulé passe au suivant
        }

        /// <summary>
        /// Vrai si l'objet est assez près de sa place pour s'y caler. <paramref name="toleranceAngleDeg"/> ≤ 0 :
        /// l'orientation ne compte pas. (Fonction pure : testée en EditMode.)
        /// </summary>
        public static bool EstAPortee(Vector3 position, Quaternion rotation, Vector3 place, Quaternion rotationPlace,
                                      float toleranceDistance, float toleranceAngleDeg)
        {
            if (Vector3.Distance(position, place) > toleranceDistance) return false;
            return toleranceAngleDeg <= 0f || Quaternion.Angle(rotation, rotationPlace) <= toleranceAngleDeg;
        }

        /// <summary>
        /// Point d'arrivée du n-ième objet dans le tas, relatif au centre : spirale à l'angle d'or (les
        /// objets ne tombent pas tous au même endroit), rayon ≤ <paramref name="rayon"/>, lâchés un peu
        /// plus haut à chaque objet pour qu'ils s'empilent. (Fonction pure : testée en EditMode.)
        /// </summary>
        public static Vector3 DecalageTas(int indice, int total, float rayon)
        {
            float angle = indice * 2.39996f;
            float r = rayon * Mathf.Sqrt((indice + 0.5f) / Mathf.Max(1, total));
            return new Vector3(Mathf.Cos(angle) * r, 0.3f + 0.1f * indice, Mathf.Sin(angle) * r);
        }

        /// <summary>Tombé sous le sol (traversé, lancé par la fenêtre) ou parti trop loin du tas.</summary>
        private bool EstPerdu(Vector3 p)
        {
            Vector3 c = transform.position;
            return p.y < c.y - 1f || new Vector2(p.x - c.x, p.z - c.z).magnitude > distancePerdu;
        }

        /// <summary>Remet un objet perdu sur le tas, sans vitesse.</summary>
        private void Ramener(Rigidbody corps)
        {
            Debug.Log("[Memoire] « " + corps.name + " » perdu : ramené sur le tas.", corps);
            if (!corps.isKinematic)
            {
                corps.linearVelocity = Vector3.zero;
                corps.angularVelocity = Vector3.zero;
            }
            corps.transform.position = transform.position + DecalageTas(Random.Range(0, _objets.Count), _objets.Count, rayonTas);
            Physics.SyncTransforms();
            VRSons.Jouer(VRSon.Lacher, corps.transform.position, 0.5f);
        }

        /// <summary>Glisse l'objet exactement à sa place (position, orientation, taille d'origine) et le fige.</summary>
        private IEnumerator Caler(Objet o, Rigidbody corps)
        {
            corps.isKinematic = true;
            Transform t = corps.transform;
            Vector3 p0 = t.position, e0 = t.localScale;
            Quaternion r0 = t.rotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.25f)
            {
                if (corps == null || !o.place) yield break; // repris en main entre-temps
                float s = Mathf.SmoothStep(0f, 1f, k);
                t.SetPositionAndRotation(Vector3.Lerp(p0, o.position, s), Quaternion.Slerp(r0, o.rotation, s));
                t.localScale = Vector3.Lerp(e0, o.echelle, s);
                DemanderSync();
                yield return null;
            }
            if (corps == null || !o.place) yield break;
            t.SetPositionAndRotation(o.position, o.rotation);
            t.localScale = o.echelle;
            Physics.SyncTransforms();
        }

        private void Gagner()
        {
            _phase = Phase.Terminee;
            GererAideAuto(false); // la main venue aider repart (plus de contrôle après la victoire)
            Afficher("Bravo !");
            VRSons.Jouer(VRSon.Reussite, transform.position + Vector3.up, 1f);
            aLaReussite?.Invoke();
            if (!AnnonceeParLaProgression()) Dire(felicitation);
            SignalerReussite();
        }

        /// <summary>Validé depuis le PC : chaque objet retourne à sa place.</summary>
        protected override void Forcer()
        {
            StopAllCoroutines();
            foreach (Objet o in _objets)
            {
                Rigidbody corps = Representant(o);
                if (corps == null) continue;
                Liberer(corps);
                corps.isKinematic = true;
                corps.transform.SetPositionAndRotation(o.position, o.rotation);
                corps.transform.localScale = o.echelle;
                Saisissable(corps, true);
                o.place = true;
            }
            Physics.SyncTransforms();
            _phase = Phase.Terminee;
            GererAideAuto(false);
            Afficher("Bravo !");
            aLaReussite?.Invoke();
        }

        /// <summary>Les objets pas encore à leur place (rien pendant l'observation : il faut regarder, pas toucher).</summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste)
        {
            if (_phase != Phase.Replacement) return;
            // La main guide n'existe que si le dev l'a allumée (case guideVersLaPlace ou touche G) :
            // elle montre alors la place de l'objet tenu, et rien quand les mains sont vides.
            // La surbrillance (montrerLesObjets), elle, montre les objets pas encore à leur place.
            ProgressionEtapes p = _progression != null ? _progression : (_progression = FindFirstObjectByType<ProgressionEtapes>());
            bool parLaMain = p == null || p.MainGuideVisible;
            if (parLaMain)
            {
                foreach (Objet o in _objets)
                {
                    Rigidbody corps = Representant(o);
                    // Aide automatique : seulement l'objet tenu trop longtemps ; allumée par le dev : tout objet tenu.
                    bool tenu = corps != null && EstTenu(corps) && (!_aideAuto || AideNecessaire(o.tempsTenu, aideApresTenue));
                    if (o.repere == null) continue;
                    o.repere.enabled = tenu; // un collider coupé n'a pas de bornes : actif seulement quand il sert
                    if (tenu) liste.Add(o.repere);
                }
                return;
            }
            foreach (Objet o in _objets)
            {
                if (o.place) continue;
                Collider c = ColliderDe(Representant(o));
                if (c != null) liste.Add(c);
            }
        }

        // ------------------------------------------------------------------
        // Affichage et narration
        // ------------------------------------------------------------------
        private void AfficherCompte() =>
            Afficher(PrefixeNiveau() + NombrePlaces + " / " + _objets.Count + (TailleACompte(_niveau) ? " à leur place et à leur taille" : " à leur place"));

        private void Afficher(string texte)
        {
            if (_panneau != null && _panneau.text != texte) _panneau.text = texte;
        }

        /// <summary>Vrai si un ProgressionEtapes de la scène liste cette épreuve (il en dit la consigne et la félicitation).</summary>
        private bool AnnonceeParLaProgression()
        {
            ProgressionEtapes p = FindFirstObjectByType<ProgressionEtapes>();
            return p != null && p.etapes.Contains(this);
        }

        /// <summary>Fait dire un texte à la bulle de narration (séquence « Manuel » retirée une fois dite).</summary>
        private void Dire(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte)) return;
            NarrationBulle bulle = FindFirstObjectByType<NarrationBulle>();
            if (bulle == null) return;
            NarrationSequence annonce = bulle.gameObject.AddComponent<NarrationSequence>();
            annonce.declencheur = DeclencheurNarration.Manuel; // avant son Start : ne part pas toute seule
            annonce.nom = "Mémoire";
            annonce.delai = 0.3f;
            annonce.repliques = new List<Replique> { new Replique { texte = texte } };
            annonce.Terminee += s => Destroy(s);
            annonce.Declencher();
        }
    }
}
