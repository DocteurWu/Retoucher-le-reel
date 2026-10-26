// =====================================================================================
//  EpreuveRecipient.cs — ÉPREUVE « REMPLIR LE RÉCIPIENT » PUIS SCÈNE SUIVANTE
//
//  RÔLE
//    Le joueur doit déposer dans un récipient un nombre donné d'exemplaires de
//    certains objets (ex. 4 fraises = l'originale + 3 duplications, 3 cylindres =
//    l'original + 2 duplications). Les clones comptent comme leur original (lien
//    VRCloneOrigin posé par VRDuplicateDelete). L'épreuve mémorise chaque famille :
//    supprimer l'original ne fait pas perdre ses copies (ni les copies de copies).
//    Quand le compte est bon (et stable une seconde), l'épreuve est réussie :
//    son, panneau « Bravo », événement pour la narration, puis chargement de la
//    scène suivante.
//
//  À POSER SUR : un objet vide placé AU SOL, là où doit se trouver le récipient.
//    « Zone » vide = le script construit lui-même un pied + un bac ouvert et la
//    zone de détection (visible dans la vue Scene grâce au gizmo vert).
//    Sinon, glisser le Collider (trigger) d'un récipient modélisé à la main.
//
//  ÉVÉNEMENTS : EpreuveRecipient.Reussie (statique) et « A La Reussite » (Inspector).
//    La narration s'y branche avec le déclencheur « Epreuve Reussie ».
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Un objet à déposer, et combien d'exemplaires (original + clones).</summary>
    [Serializable]
    public class ObjetRequis
    {
        [Tooltip("Nom affiché sur le panneau (« Fraises »).")]
        public string nom = "Objets";

        [Tooltip("Objet ORIGINAL de la scène. Ses duplications comptent aussi.")]
        public GameObject objet;

        [Min(1)]
        [Tooltip("Exemplaires à déposer, original compris (3 duplications = 4).")]
        public int nombre = 1;
    }

    /// <summary>
    /// Épreuve : remplir un récipient avec les bons objets, puis passer à la scène suivante.
    /// </summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Récipient")]
    public class EpreuveRecipient : MonoBehaviour
    {
        /// <summary>Levé une fois, quand le récipient contient tout ce qu'il faut.</summary>
        public static event Action<EpreuveRecipient> Reussie;

        [Header("Objectif")]
        public List<ObjetRequis> objetsRequis = new List<ObjetRequis>();

        [Tooltip("Ne compter que les objets posés (pas ceux encore tenus en main).")]
        public bool exigerLache = true;

        [Min(0f)]
        [Tooltip("Le compte doit rester bon pendant ce temps (s) : un objet qui rebondit ne valide pas.")]
        public float dureeStable = 1f;

        [Header("Récipient (généré si « Zone » est vide)")]
        [Tooltip("Collider de détection d'un récipient fait à la main. Vide = récipient construit au lancement.")]
        public Collider zone;

        [Tooltip("Intérieur du bac : largeur, hauteur des bords, profondeur (m).")]
        public Vector3 tailleInterieure = new Vector3(0.45f, 0.16f, 0.45f);

        [Min(0f)]
        [Tooltip("Hauteur du pied sous le bac (m) : 0 = bac posé au sol.")]
        public float hauteurPied = 0.8f;

        public Color couleurBac = new Color(0.55f, 0.36f, 0.2f);
        public Color couleurPied = new Color(0.3f, 0.3f, 0.32f);

        [Header("Suite")]
        [Tooltip("Scène chargée après la réussite (doit être dans les Build Settings). Vide = rester ici.")]
        public string sceneSuivante = "Scene2";

        [Min(0f)]
        [Tooltip("Attente après la réussite avant de changer de scène (s).")]
        public float delaiAvantScene = 4f;

        [Tooltip("Attendre aussi la fin de la narration en cours (bulle) avant de partir.")]
        public bool attendreNarration = true;

        [Min(0f)]
        [Tooltip("Attente maximale de la narration (s).")]
        public float attenteNarrationMax = 25f;

        public UnityEvent aLaReussite = new UnityEvent();

        /// <summary>Vrai une fois l'épreuve réussie.</summary>
        public bool EstReussie { get; private set; }

        private const float PeriodeControle = 0.2f;

        private int[] _comptes;
        // Famille de chaque objet requis : original + toutes ses copies, mémorisées au fil
        // des duplications. Indispensable : supprimer l'original vide le lien
        // VRCloneOrigin.source des copies, qui ne pourraient plus être reconnues.
        private List<HashSet<GameObject>> _familles;
        private int _totalPrecedent;
        private float _prochainControle;
        private float _completDepuis = -1f;
        private TextMeshPro _panneau;
        private Transform _ancrePanneau;
        private readonly StringBuilder _texte = new StringBuilder();

        private void Start()
        {
            if (zone == null) ConstruireRecipient();
            _comptes = new int[objetsRequis.Count];
            _familles = new List<HashSet<GameObject>>(objetsRequis.Count);
            foreach (ObjetRequis requis in objetsRequis)
            {
                var famille = new HashSet<GameObject>();
                if (requis.objet != null) famille.Add(requis.objet);
                _familles.Add(famille);
            }
            CreerPanneau();
            Controler();
        }

        private void OnEnable() => VRDuplicateDelete.ObjetDuplique += SurDuplication;
        private void OnDisable() => VRDuplicateDelete.ObjetDuplique -= SurDuplication;

        /// <summary>Une copie d'un membre de la famille (original OU copie) rejoint la famille.</summary>
        private void SurDuplication(Rigidbody original, GameObject clone)
        {
            if (_familles == null || original == null || clone == null) return;
            foreach (HashSet<GameObject> famille in _familles)
            {
                if (famille.Contains(original.gameObject)) famille.Add(clone);
            }
        }

        private void Update()
        {
            OrienterPanneau();
            if (EstReussie || Time.time < _prochainControle) return;
            _prochainControle = Time.time + PeriodeControle;
            Controler();
        }

        // ------------------------------------------------------------------
        // Comptage
        // ------------------------------------------------------------------
        private void Controler()
        {
            if (zone == null) return;

            // Filet de sécurité (clone créé par un autre chemin que l'événement) :
            // un clone dont la source est déjà dans une famille la rejoint.
            // Contains marche même si la source a été détruite (même référence C#).
            VRCloneOrigin[] clones = FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None);
            foreach (HashSet<GameObject> famille in _familles)
            {
                for (int c = 0; c < clones.Length; c++)
                {
                    if (clones[c] != null && !ReferenceEquals(clones[c].source, null) && famille.Contains(clones[c].source))
                    {
                        famille.Add(clones[c].gameObject);
                    }
                }
            }

            bool complet = objetsRequis.Count > 0;
            int total = 0;
            for (int i = 0; i < objetsRequis.Count; i++)
            {
                ObjetRequis requis = objetsRequis[i];
                int n = 0;
                foreach (GameObject membre in _familles[i])
                {
                    if (membre != null && EstDansLeBac(membre)) n++; // membre détruit = ignoré
                }
                _comptes[i] = n;
                total += Mathf.Min(n, requis.nombre);
                if (n < requis.nombre) complet = false;
            }

            if (total > _totalPrecedent) VRSons.Jouer(VRSon.Rangement, zone.bounds.center, 0.7f, 1f + 0.08f * total);
            _totalPrecedent = total;

            if (!complet) _completDepuis = -1f;
            else if (_completDepuis < 0f) _completDepuis = Time.time;

            MettreAJourPanneau(complet);

            if (complet && Time.time - _completDepuis >= dureeStable) Reussir();
        }

        /// <summary>L'objet est-il posé dans le bac ? (actif, collider actif, centre dans la zone, pas tenu)</summary>
        private bool EstDansLeBac(GameObject objet)
        {
            if (objet == null || !objet.activeInHierarchy) return false;

            // Rangé dans la bulle du menu poignet = colliders coupés : ne compte pas.
            Collider corps = PremierCollider(objet);
            if (corps == null) return false;

            if (exigerLache && EstTenu(objet)) return false;

            return Contient(corps.bounds.center);
        }

        private static Collider PremierCollider(GameObject objet)
        {
            Collider direct = objet.GetComponent<Collider>();
            if (direct != null && direct.enabled && !direct.isTrigger) return direct;
            foreach (Collider c in objet.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger) return c;
            }
            return null;
        }

        private static bool EstTenu(GameObject objet)
        {
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                Rigidbody tenu = main != null ? main.GrabbedBody : null;
                if (tenu != null && (tenu.gameObject == objet || objet.transform.IsChildOf(tenu.transform))) return true;
            }
            return false;
        }

        /// <summary>Point dans la zone : test exact pour une boîte (même tournée), approché sinon.</summary>
        private bool Contient(Vector3 point)
        {
            if (zone is BoxCollider boite)
            {
                Vector3 local = boite.transform.InverseTransformPoint(point) - boite.center;
                Vector3 demi = boite.size * 0.5f;
                return Mathf.Abs(local.x) <= demi.x && Mathf.Abs(local.y) <= demi.y && Mathf.Abs(local.z) <= demi.z;
            }
            return (zone.ClosestPoint(point) - point).sqrMagnitude < 1e-6f;
        }

        // ------------------------------------------------------------------
        // Réussite et changement de scène
        // ------------------------------------------------------------------
        private void Reussir()
        {
            EstReussie = true;
            Debug.Log("[Epreuve] Réussie : le récipient est plein. Scène suivante : "
                      + (string.IsNullOrEmpty(sceneSuivante) ? "(aucune)" : sceneSuivante), this);

            VRSons.Jouer(VRSon.Reussite, zone.bounds.center);
            if (_panneau != null)
            {
                _panneau.text = "<color=#5dff7a><b>Bravo !</b></color>";
            }

            Reussie?.Invoke(this);
            aLaReussite?.Invoke();

            if (!string.IsNullOrEmpty(sceneSuivante)) StartCoroutine(PasserALaSuite());
        }

        private IEnumerator PasserALaSuite()
        {
            // Une frame : laisser la narration « Epreuve Reussie » s'inscrire dans la bulle.
            yield return null;
            if (delaiAvantScene > 0f) yield return new WaitForSeconds(delaiAvantScene);

            if (attendreNarration)
            {
                float limite = Time.time + attenteNarrationMax;
                NarrationBulle[] bulles = FindObjectsByType<NarrationBulle>(FindObjectsSortMode.None);
                while (Time.time < limite && UneBulleParle(bulles)) yield return null;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneSuivante))
            {
                Debug.LogError("[Epreuve] La scène « " + sceneSuivante + " » n'est pas dans les Build Settings "
                               + "(File > Build Profiles > Scene List).", this);
                yield break;
            }

            AsyncOperation chargement = SceneManager.LoadSceneAsync(sceneSuivante);
            while (chargement != null && !chargement.isDone) yield return null;
        }

        private static bool UneBulleParle(NarrationBulle[] bulles)
        {
            foreach (NarrationBulle b in bulles)
            {
                if (b != null && b.EstOccupee) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Récipient généré : pied + bac ouvert + zone de détection
        // ------------------------------------------------------------------
        private void ConstruireRecipient()
        {
            const float epaisseur = 0.02f;
            Vector3 t = tailleInterieure;

            Transform racine = new GameObject("Recipient").transform;
            racine.SetParent(transform, false);

            if (hauteurPied > 0f)
            {
                Bloc(racine, "Pied", new Vector3(0f, hauteurPied * 0.5f, 0f),
                     new Vector3(t.x * 0.6f, hauteurPied, t.z * 0.6f), couleurPied);
            }

            float fond = hauteurPied + epaisseur * 0.5f;
            float milieuBords = hauteurPied + epaisseur + t.y * 0.5f;
            float largeurTotale = t.x + 2f * epaisseur;

            Bloc(racine, "Fond", new Vector3(0f, fond, 0f), new Vector3(largeurTotale, epaisseur, t.z + 2f * epaisseur), couleurBac);
            Bloc(racine, "Bord_Avant", new Vector3(0f, milieuBords, -(t.z + epaisseur) * 0.5f), new Vector3(largeurTotale, t.y, epaisseur), couleurBac);
            Bloc(racine, "Bord_Arriere", new Vector3(0f, milieuBords, (t.z + epaisseur) * 0.5f), new Vector3(largeurTotale, t.y, epaisseur), couleurBac);
            Bloc(racine, "Bord_Gauche", new Vector3(-(t.x + epaisseur) * 0.5f, milieuBords, 0f), new Vector3(epaisseur, t.y, t.z), couleurBac);
            Bloc(racine, "Bord_Droit", new Vector3((t.x + epaisseur) * 0.5f, milieuBords, 0f), new Vector3(epaisseur, t.y, t.z), couleurBac);

            // Zone : l'intérieur du bac, deux fois plus haute que les bords (un
            // cylindre debout dépasse, mais son centre reste dedans).
            GameObject detection = new GameObject("Zone_Detection");
            detection.transform.SetParent(racine, false);
            detection.transform.localPosition = new Vector3(0f, hauteurPied + epaisseur + t.y, 0f);
            BoxCollider boite = detection.AddComponent<BoxCollider>();
            boite.isTrigger = true;
            boite.size = new Vector3(t.x, t.y * 2f, t.z);
            zone = boite;

            _ancrePanneau = detection.transform;
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : colliders utilisables dès cette frame
        }

        private static void Bloc(Transform parent, string nom, Vector3 position, Vector3 taille, Color couleur)
        {
            GameObject bloc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bloc.name = nom;
            bloc.transform.SetParent(parent, false);
            bloc.transform.localPosition = position;
            bloc.transform.localScale = taille;
            Renderer rendu = bloc.GetComponent<Renderer>();
            // Copie du matériau par défaut (Lit en URP) : seule la couleur change.
            rendu.sharedMaterial = new Material(rendu.sharedMaterial) { color = couleur };
        }

        // ------------------------------------------------------------------
        // Panneau de progression au-dessus du bac
        // ------------------------------------------------------------------
        private void CreerPanneau()
        {
            if (_ancrePanneau == null && zone != null) _ancrePanneau = zone.transform;
            if (_ancrePanneau == null) return;

            GameObject go = new GameObject("Panneau_Epreuve");
            go.transform.SetParent(transform, false);
            _panneau = go.AddComponent<TextMeshPro>();
            _panneau.enableAutoSizing = true;
            _panneau.fontSizeMin = 0.01f;
            _panneau.fontSizeMax = 10f;
            _panneau.alignment = TextAlignmentOptions.Center;
            _panneau.textWrappingMode = TextWrappingModes.NoWrap;
            _panneau.rectTransform.sizeDelta = new Vector2(0.5f, 0.12f);
            OrienterPanneau();
        }

        private void OrienterPanneau()
        {
            if (_panneau == null || zone == null) return;

            Bounds b = zone.bounds;
            Vector3 position = new Vector3(b.center.x, b.max.y + 0.12f, b.center.z);
            _panneau.transform.position = position;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 regard = position - cam.transform.position;
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) _panneau.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        private void MettreAJourPanneau(bool complet)
        {
            if (_panneau == null) return;

            _texte.Length = 0;
            for (int i = 0; i < objetsRequis.Count; i++)
            {
                if (i > 0) _texte.Append("     ");
                bool ok = _comptes[i] >= objetsRequis[i].nombre;
                _texte.Append(ok ? "<color=#5dff7a>" : "<color=#ffffff>")
                      .Append(objetsRequis[i].nom).Append("  ")
                      .Append(Mathf.Min(_comptes[i], objetsRequis[i].nombre)).Append('/').Append(objetsRequis[i].nombre)
                      .Append("</color>");
            }
            if (complet) _texte.Append("\n<color=#ffd84a>Ne touche plus à rien…</color>");
            _panneau.text = _texte.ToString();
        }

        // ------------------------------------------------------------------
        // Aperçu dans l'éditeur
        // ------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            if (zone != null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 t = tailleInterieure;
            Gizmos.DrawWireCube(new Vector3(0f, hauteurPied + t.y * 0.5f, 0f), new Vector3(t.x, t.y, t.z));
            if (hauteurPied > 0f)
            {
                Gizmos.DrawWireCube(new Vector3(0f, hauteurPied * 0.5f, 0f), new Vector3(t.x * 0.6f, hauteurPied, t.z * 0.6f));
            }
        }
    }
}
