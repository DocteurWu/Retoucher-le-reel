// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MainGuide.cs — LA MAIN QUI MONTRE LE CHEMIN (FAÇON « LA CHOSE » DE LA FAMILLE ADDAMS)
//
//  RÔLE
//    Une main coupée au poignet qui vit dans la pièce et emmène le joueur aux objets de
//    l'étape en cours. Son petit numéro, comme La Chose :
//      1. ARRIVÉE : elle surgit sur la table (ou devant le joueur), se dresse sur son poignet
//         et fait coucou ;
//      2. APPEL : debout, paume vers le joueur, elle replie les doigts (« viens ! ») ;
//      3. GUIDE : elle trottine sur ses doigts vers l'objet. Le joueur ne suit pas (plus de
//         distanceAttente) : elle s'arrête, se retourne et l'appelle à nouveau ;
//      4. MONTRE : arrivée, elle pointe l'objet de l'index (depuis le bord de la table ou
//         sur l'objet), une étiquette au-dessus d'elle (« La statue »). Plusieurs objets :
//         elle va de l'un à l'autre ;
//      5. RETOUR / FOUILLE : quand il n'y a rien à montrer (énigme de la clé, délai avant
//         aide), la main flâne et explore la table en trottinant de coin en coin avec des
//         mouvements aléatoires curieux, au lieu de trépigner sur place.
//
//  TABLE & DÉCOR :
//    Dans Scene 1, la table existe en gaussian splat et possède un collider de surface
//    invisible nommé 'table'. Quand 'resterSurLaTable' est actif (par défaut), la main reste
//    sur le plateau de la table : elle y trottine sur ses doigts, se déplace vers le bord orienté
//    vers l'objectif pour le désigner du doigt et afficher l'étiquette, et appelle le joueur
//    depuis le bord faisant face à lui, sans jamais sauter au sol ni s'enfoncer dans le bois.
//
//  CE QU'ELLE MONTRE : ProgressionEtapes.RemplirObjetsEnCours, comme la surbrillance.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Personnage qui mène le joueur aux objets de l'étape en cours.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Main guide (façon Addams)")]
    public class MainGuide : MonoBehaviour
    {
        /// <summary>La progression dont on montre l'étape en cours (vide = trouvée dans la scène).</summary>
        [Tooltip("Progression dont on montre l'étape en cours. Vide = celle de la scène.")]
        public ProgressionEtapes progression;

        [Tooltip("La main elle-même (enfant du prefab, avec ses matériaux). Vide = copie de la main droite du rig.")]
        public GameObject modele;

        /// <summary>
        /// Hors table : la main surgit à sa position posée dans l'éditeur (hauteur comprise) au lieu
        /// de devant le joueur. Scene3 : (0,9 ; 0,19 ; -1), au milieu de la Grange.
        /// </summary>
        [Tooltip("Hors table : surgir à la position posée dans l'éditeur (au lieu de devant le joueur).")]
        public bool surgirALaPositionPosee = false;

        public Color couleurPeau = new Color(0.82f, 0.74f, 0.66f);
        public Color couleurManchette = new Color(0.12f, 0.1f, 0.14f);

        /// <summary>Taille de la main : un peu plus grande que nature, pour la voir de loin.</summary>
        public float echelle = 1.4f;

        /// <summary>Vitesse de course (m/s) quand elle rejoint le joueur, et nombre de foulées par seconde.</summary>
        public float vitesse = 1.3f, foulees = 3.2f;

        [Tooltip("Vitesse (m/s) quand elle emmène le joueur : assez lente pour qu'il la suive des yeux.")]
        public float vitesseGuide = 0.7f;

        [Tooltip("Vitesse (m/s) quand elle fouille la table (recherche de clé) : lente et curieuse.")]
        public float vitesseFouille = 0.28f;

        [Tooltip("Pause minimale (s) sur la table entre deux déplacements pendant la recherche.")]
        public float pauseMinFouille = 7f;

        [Tooltip("Pause maximale (s) sur la table entre deux déplacements pendant la recherche.")]
        public float pauseMaxFouille = 10f;

        /// <summary>Durée (s) passée à pointer un objet avant de passer au suivant (ou de rechercher le joueur resté loin).</summary>
        public float dureeMontre = 6f;

        /// <summary>Écart (m) entre la main et l'objet montré.</summary>
        public float ecartObjet = 0.18f;

        /// <summary>Nombre d'objets visités : les copies suivantes n'apprendraient rien au joueur.</summary>
        public int maxCibles = 2;

        /// <summary>Distance (m) à partir de laquelle elle rejoint le joueur quand il n'y a rien à montrer.</summary>
        public float distanceAuJoueur = 2.5f;

        [Tooltip("En chemin, joueur plus loin que ça (m) : elle s'arrête et l'appelle.")]
        public float distanceAttente = 2.6f;

        [Tooltip("Durée maximale (s) d'un appel avant de repartir.")]
        public float dureeAppel = 4f;

        [Tooltip("Hors du champ de vision du joueur plus longtemps que ça (s), joueur loin : elle revient devant lui.")]
        public float delaiHorsChamp = 2.5f;

        [Header("Table")]
        [Tooltip("Surface (collider) de la table sur laquelle la main reste. Si vide, cherchée automatiquement.")]
        public Collider surfaceTable;

        [Tooltip("Vrai : la main trottine sur ses doigts et reste sur la table pour montrer la suite sans descendre au sol.")]
        public bool resterSurLaTable = true;

        [Tooltip("Marge (m) par rapport au bord de la table pour éviter que les doigts ne dépassent dans le vide.")]
        public float margeBordTable = 0.06f;

        [Tooltip("Ajustement vertical (m) si le visuel gaussian de la table dépasse son collider.")]
        public float decalageHauteurTable = 0f;

        private const float DureeSaut = 0.55f;
        private const float HauteurSautMin = 0.25f;
        private const float HauteurMinPerche = 0.3f;
        private const float MargeMurs = 0.15f;
        private const float DureeFonduPose = 0.2f;
        private const float DureeRedressement = 0.3f;
        private const float DouceurRotation = 9f;
        private const float InclinaisonMarche = 28f;
        private const float RebondMarche = 0.012f;
        private const float ElevationVisee = 38f;
        private const float PeriodeSursaut = 4f, DureeSursaut = 0.35f, HauteurSursaut = 0.06f;
        private const float DeplacementCibleMax = 0.6f;
        private const float PeriodeListe = 0.25f;
        private const float DistanceDevant = 1.3f, DistanceDevantMin = 0.8f;
        private const float ArriveeDevant = 0.3f;
        private const float DemiChampVue = 45f, DistanceVueMax = 6f;
        private const float DureeSalut = 2.2f, DureeAppelCourt = 1.6f, EntreDeuxAppels = 4f;
        private const float JoueurArrive = 1.8f;
        private const float PeriodeViens = 0.9f, PeriodeCoucou = 0.45f, AngleCoucou = 22f;
        private const float HauteurPoignetDebout = 0.035f;

        private enum Etat { Attente, Marche, Saut, Montre, Salut, Appel }

        private MainFantome _main;
        private TextMeshPro _etiquette;
        private Material _matManchette;
        private Etat _etat = Etat.Attente;
        private Vector3 _pied;             // point du sol, de la table ou de la perche sous la main
        private Vector3 _cap = Vector3.forward;
        private Quaternion _rotation = Quaternion.identity;
        private PoseMain _poseA = PoseMain.Griffe, _poseB = PoseMain.Griffe;
        private float _melange = 1f, _phase, _amplitudeMarche, _debout;
        private bool _visible;
        private Vector3 _positionPoseInitiale;
        private bool _aPositionPoseInitiale;
        private float _prochaineRechercheTable;
        private int _essaisTable; // recherches de table refaites après celle de Start (plafonnées)

        private readonly List<Collider> _cibles = new List<Collider>();
        private Collider _cible;
        private int _indice;
        private float _prochaineListe, _finMontre, _prochainSursaut;
        private Vector3 _centreCible;
        private Epreuve _etape;
        private float _debutEtape;

        /// <summary>En marche : vrai = elle va chercher le joueur, faux = elle va à l'objet ou fouille.</summary>
        private bool _versJoueur;
        private float _debutEtat, _finEtat, _prochainAppel, _horsChamp;
        private int _dernierRepli;

        private Vector3 _cibleFouille;
        private bool _aCibleFouille;
        private float _debutMarcheFouille;

        private Vector3 _departSaut, _arriveeSaut;
        private float _tempsSaut;
        private Etat _apresSaut;

        private float _sol;
        private Bounds _piece;
        private bool _pieceConnue;

        private void Awake()
        {
            // Mémorise la position placée dans l'éditeur (ex: au milieu de la table par l'utilisateur)
            _positionPoseInitiale = transform.position;
            _aPositionPoseInitiale = transform.position.sqrMagnitude > 1e-4f;
        }

        private void Start()
        {
            if (progression == null) progression = FindFirstObjectByType<ProgressionEtapes>();
            TrouverLaPiece();
            TrouverTable();

            if (SurLaTable)
            {
                Vector3 posDepart = _aPositionPoseInitiale ? _positionPoseInitiale : transform.position;
                if (surfaceTable != null)
                {
                    _pied = ClamperSurTable(surfaceTable, posDepart, margeBordTable, decalageHauteurTable);
                }
                else
                {
                    _pied = posDepart;
                    _pied.y = HauteurSurfaceTable();
                }
            }
            else
            {
                _pied = transform.position;
            }

            CreerLaMain();
            if (_main == null)
            {
                enabled = false;
                return;
            }
            CreerEtiquette();
            _main.gameObject.SetActive(false);
        }

        /// <summary>
        /// Vrai si elle vit sur la table : il faut un collider de table. Une main simplement posée
        /// ailleurs dans l'éditeur (au sol, dans une nouvelle scène) garde le comportement au sol.
        /// </summary>
        private bool SurLaTable => resterSurLaTable && surfaceTable != null;

        private void OnDestroy()
        {
            if (_main != null) Destroy(_main.gameObject);
            if (_etiquette != null) Destroy(_etiquette.gameObject);
            if (_matManchette != null) Destroy(_matManchette);
        }

        private void TrouverLaPiece()
        {
            // La pièce mesurée par VRLimitesPiece (créé au chargement de la scène), sinon le « Sol ».
            Rect? piece = VRLimitesPiece.Interieur;
            if (piece.HasValue)
            {
                Rect r = piece.Value;
                _piece = new Bounds(new Vector3(r.center.x, 0f, r.center.y), new Vector3(r.width, 0f, r.height));
                _sol = 0f;
                _pieceConnue = true;
                return;
            }
            foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name != "Sol") continue;
                _piece = c.bounds;
                _sol = _piece.max.y;
                _pieceConnue = true;
                return;
            }
            Debug.LogWarning("[MainGuide] Pas de collider « Sol » : la main n'est pas bornée à la pièce.", this);
        }

        /// <summary>
        /// Recherche la surface de la table dans la scène.
        /// Priorité 1 : collider nommé exactement "table" (l'objet invisible créé dans Scene 1).
        /// Priorité 2 : collider situé directement sous la main placée dans l'éditeur (raycast vertical).
        /// Priorité 3 : collider contenant "table" (en EXCLUANT explicitement "tableau" !).
        /// </summary>
        private void TrouverTable()
        {
            if (surfaceTable != null)
            {
                MasquerRenduTable(surfaceTable);
                return;
            }

            Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

            // 1. Priorité absolue : nom exact "table" (insensible à la casse)
            foreach (Collider c in colliders)
            {
                if (c == null || c.isTrigger) continue;
                string n = c.gameObject.name.Trim().ToLowerInvariant();
                if (n == "table")
                {
                    surfaceTable = c;
                    MasquerRenduTable(surfaceTable);
                    Debug.Log($"[MainGuide] Table trouvée (nom exact) : '{c.gameObject.name}' (hauteur surface = {c.bounds.max.y:F2}m)", this);
                    return;
                }
            }

            // 2. Si la main a été posée dans l'éditeur : raycast vers le bas pour trouver la surface directement sous elle
            if (_aPositionPoseInitiale)
            {
                RaycastHit[] hits = Physics.RaycastAll(_positionPoseInitiale + Vector3.up * 0.3f, Vector3.down, 2.5f);
                System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    string n = hit.collider.gameObject.name.Trim().ToLowerInvariant();
                    if (n == "sol" || n.StartsWith("mur") || n.StartsWith("plafond") || n.Contains("tableau")) continue;
                    if (hit.point.y > _sol + 0.35f)
                    {
                        surfaceTable = hit.collider;
                        MasquerRenduTable(surfaceTable);
                        Debug.Log($"[MainGuide] Table trouvée par raycast sous la main : '{hit.collider.gameObject.name}' (hauteur = {hit.point.y:F2}m)", this);
                        return;
                    }
                }
            }

            // 3. Nom contenant "table" mais NE CONTENANT PAS "tableau"
            foreach (Collider c in colliders)
            {
                if (c == null || c.isTrigger) continue;
                string n = c.gameObject.name.Trim().ToLowerInvariant();
                if (n.Contains("tableau")) continue; // Ne JAMAIS confondre avec le tableau au mur !
                if (n.Contains("table"))
                {
                    surfaceTable = c;
                    MasquerRenduTable(surfaceTable);
                    Debug.Log($"[MainGuide] Table trouvée par mot-clé : '{c.gameObject.name}' (hauteur surface = {c.bounds.max.y:F2}m)", this);
                    return;
                }
            }

            // Pas d'autre heuristique : un meuble deviné au hasard (bac de l'épreuve, coffre…)
            // verrait son rendu masqué par MasquerRenduTable. Sans table, elle marche au sol.
        }

        /// <summary>Désactive le MeshRenderer d'un collider pour le rendre transparent et laisser voir le décor Gaussian splatting.</summary>
        private static void MasquerRenduTable(Collider col)
        {
            if (col == null) return;
            MeshRenderer mr = col.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled)
            {
                mr.enabled = false;
                Debug.Log($"[MainGuide] MeshRenderer de '{col.gameObject.name}' désactivé (table rendue transparente).");
            }
        }

        /// <summary>Hauteur du plateau de la table (ou repli sur la position initiale posée).</summary>
        private float HauteurSurfaceTable()
        {
            if (surfaceTable != null) return surfaceTable.bounds.max.y + decalageHauteurTable;
            if (_aPositionPoseInitiale) return _positionPoseInitiale.y;
            return _sol;
        }

        /// <summary>Choisit un point aléatoire sur la table pour flâner et explorer (éloigné du point actuel).</summary>
        private Vector3 ChoisirPointFouille()
        {
            if (surfaceTable == null)
            {
                return _aPositionPoseInitiale ? _positionPoseInitiale : transform.position;
            }
            return PointAleatoireSurTable(surfaceTable, _pied, margeBordTable, decalageHauteurTable);
        }

        /// <summary>
        /// Calcule un point aléatoire sur le plateau supérieur de la table, strictement à l'intérieur
        /// des bords avec une marge de sécurité. Garanti d'être atteignable par la main.
        /// </summary>
        public static Vector3 PointAleatoireSurTable(Collider table, Vector3 positionActuelle, float margeBord, float decalageY = 0f)
        {
            if (table == null) return positionActuelle;

            float marge = Mathf.Max(margeBord, 0.08f);

            if (table is BoxCollider box)
            {
                Vector3 centre = box.center;
                Vector3 scale = box.transform.lossyScale;
                Vector3 demiBox = new Vector3(Mathf.Abs(box.size.x) * 0.5f, 0f, Mathf.Abs(box.size.z) * 0.5f);

                float margeX = Mathf.Abs(scale.x) > 1e-4f ? marge / Mathf.Abs(scale.x) : 0.05f;
                float margeZ = Mathf.Abs(scale.z) > 1e-4f ? marge / Mathf.Abs(scale.z) : 0.05f;

                float demiX = Mathf.Max(0.02f, demiBox.x - margeX);
                float demiZ = Mathf.Max(0.02f, demiBox.z - margeZ);

                Vector3 meilleurPoint = box.transform.TransformPoint(centre);
                meilleurPoint.y = box.bounds.max.y + decalageY;
                float maxDist = -1f;

                for (int essai = 0; essai < 12; essai++)
                {
                    float rx = Random.Range(-demiX, demiX);
                    float rz = Random.Range(-demiZ, demiZ);
                    Vector3 local = new Vector3(centre.x + rx, centre.y, centre.z + rz);
                    Vector3 monde = box.transform.TransformPoint(local);
                    monde.y = box.bounds.max.y + decalageY;

                    // Clamper avec un peu plus de marge pour que le point soit toujours atteignable sans coller au bord
                    Vector3 clp = ClamperSurTable(table, monde, margeBord + 0.02f, decalageY);
                    float dist = Horizontal(clp - positionActuelle).magnitude;

                    if (dist > 0.22f)
                    {
                        return clp;
                    }
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        meilleurPoint = clp;
                    }
                }
                return meilleurPoint;
            }
            else
            {
                Bounds b = table.bounds;
                float demiX = Mathf.Max(0.02f, b.extents.x - marge);
                float demiZ = Mathf.Max(0.02f, b.extents.z - marge);

                Vector3 meilleurPoint = b.center;
                meilleurPoint.y = b.max.y + decalageY;
                float maxDist = -1f;

                for (int essai = 0; essai < 12; essai++)
                {
                    float rx = Random.Range(-demiX, demiX);
                    float rz = Random.Range(-demiZ, demiZ);
                    Vector3 monde = new Vector3(b.center.x + rx, b.max.y + decalageY, b.center.z + rz);
                    Vector3 clp = ClamperSurTable(table, monde, margeBord + 0.02f, decalageY);
                    float dist = Horizontal(clp - positionActuelle).magnitude;

                    if (dist > 0.22f)
                    {
                        return clp;
                    }
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        meilleurPoint = clp;
                    }
                }
                return meilleurPoint;
            }
        }

        private void CreerLaMain()
        {
            // Modèle du prefab : on le copie tel quel (matériaux et manchette compris), puis on le
            // cache : c'est la copie, animée, qui joue.
            if (modele != null)
            {
                SkinnedMeshRenderer peau = modele.GetComponentInChildren<SkinnedMeshRenderer>(true);
                _main = MainFantome.Creer(modele, false, couleurPeau, transform,
                                          peau != null ? peau.sharedMaterial : MateriauOpaque(couleurPeau), echelle, peau != null);
                modele.SetActive(false);
                if (_main != null) _main.name = "Main_Guide";
                return;
            }

            VRModalityManager modalite = VRModalityManager.Instance;
            if (modalite == null) return;
            modalite.ResolveReferences();

            _main = MainFantome.Creer(modalite.rightHandVisual, false, couleurPeau, transform, MateriauOpaque(couleurPeau), echelle);
            if (_main == null) return;
            _main.name = "Main_Guide";

            GameObject manchette = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            manchette.name = "Manchette";
            DestroyImmediate(manchette.GetComponent<Collider>());
            manchette.transform.SetParent(_main.Poignet, false);
            manchette.transform.localPosition = new Vector3(0f, -0.005f, -0.012f);
            manchette.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            manchette.transform.localScale = new Vector3(0.068f, 0.018f, 0.05f);
            _matManchette = MateriauOpaque(couleurManchette);
            manchette.GetComponent<Renderer>().sharedMaterial = _matManchette;
        }

        /// <summary>Matériau éclairé (sa forme se lit, là où une couleur plate ne donnerait qu'une silhouette), toujours un peu lumineux.</summary>
        private static Material MateriauOpaque(Color couleur)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return VRUtils.CreateUnlitMaterial(couleur);

            Material m = new Material(shader) { color = couleur };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", couleur * 0.3f);
            return m;
        }

        private void CreerEtiquette()
        {
            GameObject go = new GameObject("Etiquette_Main_Guide");
            go.transform.SetParent(transform, false);
            _etiquette = go.AddComponent<TextMeshPro>();
            _etiquette.enableAutoSizing = true;
            _etiquette.fontSizeMin = 0.01f;
            _etiquette.fontSizeMax = 3f;
            _etiquette.alignment = TextAlignmentOptions.Center;
            _etiquette.textWrappingMode = TextWrappingModes.NoWrap;
            _etiquette.rectTransform.sizeDelta = new Vector2(0.6f, 0.12f);
            go.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Que montrer ?
        // ------------------------------------------------------------------
        private void ActualiserCibles(Vector3 tete)
        {
            if (Time.time < _prochaineListe) return;
            _prochaineListe = Time.time + PeriodeListe;

            Epreuve etape = progression != null ? progression.EtapeEnCours : null;
            if (etape != _etape)
            {
                _etape = etape;
                _debutEtape = Time.time;
            }

            // Une aide retardée : la main reste auprès du joueur tant que le délai court.
            if (progression == null || (etape != null && Time.time - _debutEtape < etape.DelaiAvantGuide)) _cibles.Clear();
            else progression.RemplirObjetsEnCours(_cibles);

            if (_cibles.Count > maxCibles) _cibles.RemoveRange(maxCibles, _cibles.Count - maxCibles);

            if (_cible == null || !_cibles.Contains(_cible))
            {
                _indice = 0;
                _cible = _cibles.Count > 0 ? _cibles[0] : null;
                if (_etat == Etat.Montre)
                {
                    // Étape suivante : le joueur est sans doute près d'elle, elle file vers l'objet suivant.
                    _versJoueur = Horizontal(tete - _pied).magnitude > distanceAttente;
                    Repartir();
                }
                else if (_cible != null && _etat == Etat.Marche && _aCibleFouille)
                {
                    // Un objet vient d'apparaître pendant la fouille : elle interrompt sa balade pour aller le montrer !
                    _aCibleFouille = false;
                    Repartir();
                }
            }
        }

        private void CibleSuivante()
        {
            _indice = (_indice + 1) % _cibles.Count;
            _cible = _cibles[_indice];
            Repartir();
        }

        /// <summary>Quitte l'objet montré : redescend d'abord s'il est perché, ou reste sur la table.</summary>
        private void Repartir()
        {
            _etiquette.gameObject.SetActive(false);
            if (SurLaTable)
            {
                _pied.y = HauteurSurfaceTable();
                if (_cible == null)
                {
                    _versJoueur = false;
                    _aCibleFouille = false;
                    Changer(Etat.Attente, Random.Range(pauseMinFouille, pauseMaxFouille));
                }
                else
                {
                    Changer(Etat.Marche);
                }
            }
            else if (_pied.y - _sol > 0.01f)
            {
                Vector3 auSol = _pied;
                auSol.y = _sol;
                Sauter(auSol, Etat.Marche);
            }
            else
            {
                Changer(Etat.Marche);
            }
        }

        private void Sauter(Vector3 arrivee, Etat ensuite)
        {
            _departSaut = _pied;
            _arriveeSaut = arrivee;
            _tempsSaut = 0f;
            _apresSaut = ensuite;
            Changer(Etat.Saut);
        }

        private void Changer(Etat etat, float duree = 0f)
        {
            _etat = etat;
            _debutEtat = Time.time;
            _finEtat = Time.time + duree;
        }

        /// <summary>Elle se dresse face au joueur et l'appelle de la main, puis (re)part vers l'objet.</summary>
        private void Appeler(float duree)
        {
            _etiquette.gameObject.SetActive(false);
            _versJoueur = false;
            _dernierRepli = 0;
            Changer(Etat.Appel, duree);
        }

        private void Tic(float pitch)
        {
            VRSons.Jouer(VRSon.NarrationLettre, _pied, 0.6f, pitch);
        }

        // ------------------------------------------------------------------
        // Comportement
        // ------------------------------------------------------------------
        /// <summary>
        /// Montrée seulement une fois la première consigne donnée, si la progression et le panneau
        /// de démo (G) le permettent.
        /// </summary>
        private bool DoitSeMontrer()
        {
            if (progression == null) progression = FindFirstObjectByType<ProgressionEtapes>();
            return progression != null && progression.mainGuide && progression.ConsignesLancees && ReglagesAide.MainGuide;
        }

                // [LH-Assist] Interpolation fluide de la main fantome guide vers la cible
        private void Update()
        {
            if (_main == null) return;
            Camera cam = Camera.main;
            Vector3 tete = cam != null ? cam.transform.position : _pied;
            Vector3 regard = cam != null ? cam.transform.forward : Vector3.forward;

            // Recherche de la table seulement quand la main est montrée, et 5 essais au plus :
            // sans table (Scene3), elle parcourait tous les colliders (une chaîne neuve par nom)
            // + un RaycastAll toutes les 2 s pendant toute la partie, même main éteinte.
            if (surfaceTable == null && resterSurLaTable && _visible && _essaisTable < 5 && Time.time >= _prochaineRechercheTable)
            {
                _prochaineRechercheTable = Time.time + 2f; // FindObjectsByType + RaycastAll : pas à chaque image
                _essaisTable++;
                TrouverTable();
            }
            if (SurLaTable && _etat != Etat.Saut)
            {
                _pied.y = HauteurSurfaceTable();
            }

            bool visible = DoitSeMontrer();
            if (visible != _visible)
            {
                _visible = visible;
                _main.gameObject.SetActive(visible);
                _etiquette.gameObject.SetActive(false);
                if (visible) Surgir(tete, regard);
            }
            if (!visible) return;
            ActualiserCibles(tete);

            bool vue = EstDansLeChamp(tete, regard, _main.Poignet.position, DemiChampVue, DistanceVueMax);
            _horsChamp = vue ? 0f : _horsChamp + Time.deltaTime;
            float distanceJoueur = Horizontal(tete - _pied).magnitude;
            Vector3 versJoueur = HorizontalVers(tete - _pied);

            Quaternion rotationVoulue = _rotation;
            Quaternion balancement = Quaternion.identity;
            PoseMain pose = PoseMain.Griffe;
            bool marche = false, debout = false;
            float sursaut = 0f, repli = 0f, vitesseMarche = vitesse;

            bool surTable = SurLaTable;

            switch (_etat)
            {
                case Etat.Attente:
                    if (_cible != null)
                    {
                        // Un objet est disponible : elle regarde le joueur et l'appelle ou va le chercher
                        rotationVoulue = Quaternion.LookRotation(versJoueur, Vector3.up) * Quaternion.Euler(InclinaisonMarche * 0.5f, 0f, 0f);
                        _phase += foulees * Mathf.PI * Time.deltaTime;
                        _amplitudeMarche = Mathf.MoveTowards(_amplitudeMarche, 0.35f, Time.deltaTime / DureeFonduPose);
                        if (distanceJoueur <= distanceAttente && vue) Appeler(DureeAppelCourt);
                        else AllerChercherLeJoueur();
                    }
                    else if (surTable)
                    {
                        // Rien à montrer (ex: énigme de la clé, délai d'aide) :
                        // En attente : la main reste posée AU REPOS sur la table (doigts immobiles, pas de sur-place !)
                        // et tourne le poignet pour regarder curieusement vers le joueur.
                        debout = true;
                        pose = PoseMain.SigneV;
                        rotationVoulue = Debout(versJoueur);
                        _amplitudeMarche = Mathf.MoveTowards(_amplitudeMarche, 0f, Time.deltaTime / DureeFonduPose);


                        // Après une pause aléatoire (7 à 10 s), elle repart trottiner vers un nouvel endroit
                        if (Time.time >= _finEtat)
                        {
                            _cibleFouille = ChoisirPointFouille();
                            _aCibleFouille = true;
                            _versJoueur = false;
                            _debutMarcheFouille = Time.time;
                            Changer(Etat.Marche);
                        }
                    }
                    else
                    {
                        // Hors table : comportement classique
                        rotationVoulue = Quaternion.LookRotation(versJoueur, Vector3.up) * Quaternion.Euler(InclinaisonMarche * 0.5f, 0f, 0f);
                        _phase += foulees * Mathf.PI * Time.deltaTime;
                        _amplitudeMarche = Mathf.MoveTowards(_amplitudeMarche, 0.35f, Time.deltaTime / DureeFonduPose);
                        if (distanceJoueur > distanceAuJoueur || _horsChamp > delaiHorsChamp)
                        {
                            AllerChercherLeJoueur();
                        }
                    }
                    break;

                case Etat.Salut:
                    // Coucou : debout sur le poignet, paume vers le joueur, la main se balance.
                    debout = true;
                    pose = PoseMain.Ouverte;
                    rotationVoulue = Debout(versJoueur);
                    balancement = Quaternion.AngleAxis(Mathf.Sin((Time.time - _debutEtat) * Mathf.PI * 2f / PeriodeCoucou) * AngleCoucou, versJoueur);
                    if (Time.time >= _finEtat)
                    {
                        if (_cible != null) Appeler(DureeAppelCourt);
                        else
                        {
                            _cibleFouille = ChoisirPointFouille();
                            _aCibleFouille = true;
                            _debutMarcheFouille = Time.time;
                            Changer(Etat.Attente, Random.Range(pauseMinFouille, pauseMaxFouille));
                        }
                    }
                    break;

                case Etat.Appel:
                {
                    // « Viens ! » : debout face au joueur, les doigts se replient et se rouvrent.
                    debout = true;
                    pose = PoseMain.Ouverte;
                    rotationVoulue = Debout(versJoueur);
                    float t = (Time.time - _debutEtat) / PeriodeViens;
                    repli = (1f - Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f * 0.8f;
                    int cycle = Mathf.FloorToInt(t + 0.5f);
                    if (cycle != _dernierRepli)
                    {
                        _dernierRepli = cycle;
                        Tic(1.4f);
                    }

                    bool joueurVenu = distanceJoueur < JoueurArrive && Time.time - _debutEtat > 1.2f;
                    if (_cible == null)
                    {
                        _cibleFouille = ChoisirPointFouille();
                        _aCibleFouille = true;
                        _debutMarcheFouille = Time.time;
                        Changer(Etat.Attente, Random.Range(pauseMinFouille, pauseMaxFouille));
                    }
                    else if (_horsChamp > delaiHorsChamp && distanceJoueur > distanceAttente) AllerChercherLeJoueur();
                    else if (joueurVenu || Time.time >= _finEtat)
                    {
                        _prochainAppel = Time.time + EntreDeuxAppels;
                        Changer(Etat.Marche);
                    }
                    break;
                }

                case Etat.Marche:
                {
                    bool chercheJoueur = _versJoueur && _cible != null;
                    bool fouilleTable = surTable && _cible == null;

                    Vector3 but;
                    if (surTable && surfaceTable != null)
                    {
                        if (fouilleTable)
                        {
                            if (!_aCibleFouille)
                            {
                                _cibleFouille = ChoisirPointFouille();
                                _aCibleFouille = true;
                                _debutMarcheFouille = Time.time;
                            }
                            but = _cibleFouille;
                        }
                        else if (chercheJoueur)
                        {
                            but = ClamperSurTable(surfaceTable, tete, margeBordTable, decalageHauteurTable);
                        }
                        else
                        {
                            but = DestinationSurTable(surfaceTable, _cible, tete, ecartObjet, margeBordTable, decalageHauteurTable);
                        }
                    }
                    else
                    {
                        but = (chercheJoueur || _cible == null) ? DevantLeJoueur(tete, regard) : Perche(_cible, tete);
                    }

                    Vector3 versBut = Horizontal(but - _pied);
                    float distanceAuBut = versBut.magnitude;
                    float arrivee = surTable ? 0.06f : (chercheJoueur ? ArriveeDevant : 0.05f);
                    vitesseMarche = (chercheJoueur || fouilleTable) ? (fouilleTable ? vitesseFouille : vitesse) : vitesseGuide;

                    const float DureeMarcheFouilleMax = 5.0f; // Marche pendant 5s puis pause obligatoire
                    bool tempsEcouleFouille = fouilleTable && (Time.time - _debutMarcheFouille >= DureeMarcheFouilleMax);

                    if (!chercheJoueur && !fouilleTable && distanceJoueur > distanceAttente && Time.time >= _prochainAppel && distanceAuBut > 0.6f)
                    {
                        // Le joueur ne suit pas : elle s'arrête et se retourne pour l'appeler.
                        Appeler(dureeAppel);
                    }
                    else if (distanceAuBut > arrivee && !tempsEcouleFouille)
                    {
                        marche = true;
                        _cap = versBut.sqrMagnitude > 1e-4f ? versBut.normalized : _cap;
                        _pied += _cap * Mathf.Min(vitesseMarche * Time.deltaTime, distanceAuBut);
                        if (surTable && surfaceTable != null) _pied = ClamperSurTable(surfaceTable, _pied, margeBordTable, decalageHauteurTable);
                        else if (surTable) _pied.y = HauteurSurfaceTable();
                        _phase += foulees * (vitesseMarche / vitesse) * Mathf.PI * 2f * Time.deltaTime;
                        rotationVoulue = Quaternion.LookRotation(_cap, Vector3.up) * Quaternion.Euler(InclinaisonMarche, 0f, 0f);
                    }
                    else if (fouilleTable)
                    {
                        // Arrivée sur un point de fouille aléatoire (ou fin du temps de marche) :
                        // Elle s'arrête, pose ses doigts au repos sur la table, et marque une pause de 7 à 10s.
                        _aCibleFouille = false;
                        Changer(Etat.Attente, Random.Range(pauseMinFouille, pauseMaxFouille));
                    }
                    else if (chercheJoueur)
                    {
                        _versJoueur = false;
                        if (_cible != null) Appeler(dureeAppel);
                        else
                        {
                            _cibleFouille = ChoisirPointFouille();
                            _aCibleFouille = true;
                            _debutMarcheFouille = Time.time;
                            Changer(Etat.Attente, Random.Range(pauseMinFouille, pauseMaxFouille));
                        }
                    }
                    else if (!surTable && but.y - _sol > HauteurMinPerche)
                    {
                        Sauter(but, Etat.Montre);
                    }
                    else
                    {
                        Changer(Etat.Montre);
                        CommencerAMontrer();
                    }
                    break;
                }

                case Etat.Saut:
                    _tempsSaut += Time.deltaTime;
                    float k = Mathf.Clamp01(_tempsSaut / DureeSaut);
                    _pied = PositionSaut(_departSaut, _arriveeSaut, k, HauteurSautMin);
                    Vector3 direction = Horizontal(_arriveeSaut - _departSaut);
                    if (direction.sqrMagnitude > 1e-4f) rotationVoulue = Quaternion.LookRotation(direction, Vector3.up);
                    if (k >= 1f)
                    {
                        Tic(0.8f);
                        Changer(_apresSaut);
                        if (_etat == Etat.Montre) CommencerAMontrer();
                    }
                    break;

                case Etat.Montre:
                {
                    if (_cible == null)
                    {
                        Repartir();
                        break;
                    }
                    pose = PoseMain.Pointe;
                    rotationVoulue = Quaternion.LookRotation(DirectionVisee(_main.Poignet.position, _cible.bounds.center), Vector3.up);
                    sursaut = Sursaut();
                    MettreAJourEtiquette(tete);

                    float distRef = surTable ? distanceJoueur : Horizontal(tete - _cible.bounds.center).magnitude;
                    bool joueurLoin = distRef > distanceAttente + 0.5f;
                    if (joueurLoin && (_horsChamp > delaiHorsChamp || Time.time >= _finMontre))
                    {
                        _versJoueur = true;
                        Repartir();
                    }
                    // L'objet a bougé (jeté, décroché) : on le rejoint. Le joueur qui marche, lui, ne la dérange pas.
                    else if (Vector3.Distance(_cible.bounds.center, _centreCible) > DeplacementCibleMax) Repartir();
                    else if (_cibles.Count > 1 && Time.time >= _finMontre) CibleSuivante();
                    break;
                }
            }

            AppliquerPose(pose, rotationVoulue, balancement, marche, debout, sursaut, repli);
        }

        /// <summary>Première apparition (ou retour après la touche G) : sur la table ou devant le joueur, et coucou.</summary>
        private void Surgir(Vector3 tete, Vector3 regard)
        {
            if (SurLaTable)
            {
                // Si la main a été positionnée dans l'éditeur (ex: au milieu de la table), elle surgit là
                Vector3 pointDepart = _aPositionPoseInitiale ? _positionPoseInitiale : (tete + HorizontalVers(regard) * DistanceDevant);
                if (surfaceTable != null)
                {
                    _pied = ClamperSurTable(surfaceTable, pointDepart, margeBordTable, decalageHauteurTable);
                }
                else
                {
                    _pied = pointDepart;
                    _pied.y = HauteurSurfaceTable();
                }
            }
            else if (surgirALaPositionPosee && _aPositionPoseInitiale)
            {
                _pied = _positionPoseInitiale; // position et hauteur posées dans l'éditeur
            }
            else
            {
                _pied = DevantLeJoueur(tete, regard);
            }
            Vector3 versJoueur = HorizontalVers(tete - _pied);
            _cap = versJoueur;
            _rotation = Debout(versJoueur);
            _debout = 1f;
            _versJoueur = false;
            _horsChamp = 0f;
            Changer(Etat.Salut, DureeSalut);
            Tic(1.2f);
        }

        private void AllerChercherLeJoueur()
        {
            _versJoueur = true;
            _etiquette.gameObject.SetActive(false);
            Changer(Etat.Marche);
        }

        private void CommencerAMontrer()
        {
            if (_cible != null) _centreCible = _cible.bounds.center;
            _finMontre = Time.time + dureeMontre;
            _prochainSursaut = Time.time + 1f;
            _etiquette.text = Etiquette(NomDe(_cible));
            _etiquette.gameObject.SetActive(true);
            Tic(1.2f);
        }

        /// <summary>Petit bond régulier pour attirer l'œil, avec un son qui aide à la situer dans la pièce.</summary>
        private float Sursaut()
        {
            if (Time.time >= _prochainSursaut)
            {
                _prochainSursaut = Time.time + PeriodeSursaut;
                Tic(1.2f);
            }
            float depuis = Time.time - (_prochainSursaut - PeriodeSursaut);
            return depuis < DureeSursaut ? Mathf.Sin(depuis / DureeSursaut * Mathf.PI) * HauteurSursaut : 0f;
        }

        /// <summary>
        /// Pose la main : fondu entre les poses, marche, repli des doigts (« viens »), puis
        /// hauteur du poignet : couchée sur le bout des doigts, ou debout sur le poignet.
        /// Empêche strictement le poignet de s'enfoncer sous la table quand la main pointe vers le haut.
        /// </summary>
        private void AppliquerPose(PoseMain pose, Quaternion rotationVoulue, Quaternion balancement,
                                   bool marche, bool debout, float sursaut, float repli)
        {
            if (pose != _poseB)
            {
                _poseA = _poseB;
                _poseB = pose;
                _melange = 0f;
            }
            _melange = Mathf.MoveTowards(_melange, 1f, Time.deltaTime / DureeFonduPose);
            if (_etat != Etat.Attente) _amplitudeMarche = Mathf.MoveTowards(_amplitudeMarche, marche ? 1f : 0f, Time.deltaTime / DureeFonduPose);
            _debout = Mathf.MoveTowards(_debout, debout ? 1f : 0f, Time.deltaTime / DureeRedressement);
            _rotation = Quaternion.Slerp(_rotation, rotationVoulue, 1f - Mathf.Exp(-DouceurRotation * Time.deltaTime));

            _main.Placer(_pied, balancement * _rotation, _poseA, _poseB, Mathf.SmoothStep(0f, 1f, _melange));
            if (_amplitudeMarche > 0f) _main.Marcher(_phase, _amplitudeMarche);
            if (repli > 0f) _main.Replier(repli);

            // Couchée : le bout de doigt le plus bas touche le sol ou la table, le corps rebondit à chaque foulée.
            float rebond = Mathf.Abs(Mathf.Sin(_phase)) * RebondMarche * _amplitudeMarche;
            float surfaceCible = _pied.y + sursaut + rebond;
            _main.PoserSur(surfaceCible);

            // Sécurité anti-enfoncement : quand la main pointe vers le haut, le poignet pivote vers le bas.
            // On garantit que la manchette / poignet ne descend JAMAIS sous le plateau de la table.
            float rayonPoignet = 0.026f * _main.transform.lossyScale.y;
            float hauteurMinPoignet = surfaceCible + rayonPoignet;
            if (_main.Poignet.position.y < hauteurMinPoignet)
            {
                Vector3 posPoignet = _main.Poignet.position;
                posPoignet.y = hauteurMinPoignet;
                _main.Poignet.position = posPoignet;
            }

            float hauteurCouchee = _main.Poignet.position.y;
            // Debout : posée sur sa manchette.
            float hauteurDebout = surfaceCible + HauteurPoignetDebout * _main.transform.lossyScale.y;
            Vector3 p = _main.Poignet.position;
            p.y = Mathf.Lerp(hauteurCouchee, hauteurDebout, Mathf.SmoothStep(0f, 1f, _debout));
            _main.Poignet.position = p;
        }

        private void MettreAJourEtiquette(Vector3 tete)
        {
            Vector3 position = _main.Poignet.position + Vector3.up * 0.28f;
            Vector3 regard = position - tete;
            float distance = regard.magnitude;
            _etiquette.transform.position = position;
            _etiquette.transform.localScale = Vector3.one * Mathf.Max(1f, distance / 2.5f); // reste lisible de loin
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) _etiquette.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        // ------------------------------------------------------------------
        // Géométrie
        // ------------------------------------------------------------------
        /// <summary>Main dressée sur le poignet : doigts vers le haut, paume vers le joueur.</summary>
        private static Quaternion Debout(Vector3 versJoueur)
        {
            return Quaternion.LookRotation(Vector3.up, -versJoueur);
        }

        /// <summary>Où se tenir pour montrer cet objet : côté joueur, au niveau du pied de l'objet.</summary>
        private Vector3 Perche(Collider cible, Vector3 tete)
        {
            if (cible == null) return DevantLeJoueur(tete, Vector3.forward);
            Vector3 p = PositionPerche(cible.bounds, tete, ecartObjet, _sol, HauteurMinPerche);
            Vector3 borne = DansLaPiece(p);
            borne.y = p.y;
            return borne;
        }

        /// <summary>Au sol, devant le joueur, dans son champ de vision (vers le centre de la pièce s'il regarde un mur).</summary>
        private Vector3 DevantLeJoueur(Vector3 tete, Vector3 regard)
        {
            Vector3 p = DansLaPiece(tete + HorizontalVers(regard) * DistanceDevant);
            if (_pieceConnue && Horizontal(p - tete).magnitude < DistanceDevantMin)
            {
                p = DansLaPiece(tete + HorizontalVers(_piece.center - tete) * DistanceDevant);
            }
            p.y = _sol;
            return p;
        }

        private Vector3 DansLaPiece(Vector3 p)
        {
            if (!_pieceConnue) return p;
            p.x = Mathf.Clamp(p.x, _piece.min.x + MargeMurs, _piece.max.x - MargeMurs);
            p.z = Mathf.Clamp(p.z, _piece.min.z + MargeMurs, _piece.max.z - MargeMurs);
            return p;
        }

        private static Vector3 Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static Vector3 HorizontalVers(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>Direction de visée, limitée en élévation : vers un objet juste au-dessus, la main ne se met pas à la verticale.</summary>
        private static Vector3 DirectionVisee(Vector3 depuis, Vector3 vers)
        {
            Vector3 d = vers - depuis;
            Vector3 h = HorizontalVers(d);
            float elevation = Mathf.Clamp(Mathf.Atan2(d.y, Horizontal(d).magnitude) * Mathf.Rad2Deg, -ElevationVisee, ElevationVisee);
            return Quaternion.AngleAxis(-elevation, Vector3.Cross(Vector3.up, h)) * h;
        }

        // ------------------------------------------------------------------
        // Calculs purs (testés en EditMode : Editor/Tests)
        // ------------------------------------------------------------------
        /// <summary>
        /// Projette un point sur le plateau supérieur de la table et le borne à l'intérieur
        /// des bords avec une marge de sécurité pour que la main ne glisse pas du bord.
        /// </summary>
        public static Vector3 ClamperSurTable(Collider table, Vector3 point, float margeBord, float decalageY = 0f)
        {
            if (table == null) return point;

            if (table is BoxCollider box)
            {
                Vector3 local = box.transform.InverseTransformPoint(point);
                Vector3 demi = new Vector3(Mathf.Abs(box.size.x) * 0.5f, Mathf.Abs(box.size.y) * 0.5f, Mathf.Abs(box.size.z) * 0.5f);
                Vector3 centre = box.center;
                Vector3 scale = box.transform.lossyScale;

                float margeX = Mathf.Abs(scale.x) > 1e-4f ? margeBord / Mathf.Abs(scale.x) : 0f;
                float margeZ = Mathf.Abs(scale.z) > 1e-4f ? margeBord / Mathf.Abs(scale.z) : 0f;

                float demiX = Mathf.Max(0.01f, demi.x - margeX);
                float demiZ = Mathf.Max(0.01f, demi.z - margeZ);

                local.x = Mathf.Clamp(local.x, centre.x - demiX, centre.x + demiX);
                local.z = Mathf.Clamp(local.z, centre.z - demiZ, centre.z + demiZ);

                Vector3 monde = box.transform.TransformPoint(new Vector3(local.x, centre.y, local.z));
                monde.y = box.bounds.max.y + decalageY;
                return monde;
            }
            else
            {
                Bounds b = table.bounds;
                float demiX = Mathf.Max(0.01f, b.extents.x - margeBord);
                float demiZ = Mathf.Max(0.01f, b.extents.z - margeBord);
                float x = Mathf.Clamp(point.x, b.center.x - demiX, b.center.x + demiX);
                float z = Mathf.Clamp(point.z, b.center.z - demiZ, b.center.z + demiZ);
                return new Vector3(x, b.max.y + decalageY, z);
            }
        }

        /// <summary>
        /// Calcule la position de destination de la main sur la table :
        /// - Si l'objet est posé sur la table : la main se place à côté de lui, côté joueur.
        /// - Si l'objet est ailleurs dans la pièce (tableau au mur, porte, statue...) :
        ///   la main rejoint le bord de la table faisant face à cet objet pour le pointer.
        /// </summary>
        public static Vector3 DestinationSurTable(Collider table, Collider cible, Vector3 tete, float ecartObjet, float margeBord, float decalageY = 0f)
        {
            if (table == null)
            {
                return cible != null ? cible.bounds.center : tete;
            }
            if (cible == null)
            {
                return ClamperSurTable(table, tete, margeBord, decalageY);
            }

            Bounds tableBounds = table.bounds;
            Bounds cibleBounds = cible.bounds;

            bool surLaTable = cibleBounds.center.x >= tableBounds.min.x - 0.15f &&
                              cibleBounds.center.x <= tableBounds.max.x + 0.15f &&
                              cibleBounds.center.z >= tableBounds.min.z - 0.15f &&
                              cibleBounds.center.z <= tableBounds.max.z + 0.15f &&
                              cibleBounds.min.y >= tableBounds.min.y - 0.15f &&
                              cibleBounds.min.y <= tableBounds.max.y + 0.8f;

            if (surLaTable)
            {
                Vector3 versJoueur = HorizontalVers(tete - cibleBounds.center);
                float rayon = Mathf.Max(cibleBounds.extents.x, cibleBounds.extents.z);
                Vector3 pos = cibleBounds.center + versJoueur * (rayon + ecartObjet);
                return ClamperSurTable(table, pos, margeBord, decalageY);
            }
            else
            {
                return ClamperSurTable(table, cibleBounds.center, margeBord, decalageY);
            }
        }

        /// <summary>
        /// Place devant un objet, côté joueur. Pied de l'objet bas (moins de
        /// <paramref name="hauteurMinPerche"/> au-dessus du sol) : on reste au sol.
        /// </summary>
        public static Vector3 PositionPerche(Bounds objet, Vector3 tete, float ecart, float sol, float hauteurMinPerche)
        {
            Vector3 versJoueur = HorizontalVers(tete - objet.center);
            float rayon = Mathf.Max(objet.extents.x, objet.extents.z);
            Vector3 p = objet.center + versJoueur * (rayon + ecart);
            p.y = objet.min.y - sol > hauteurMinPerche ? objet.min.y : sol;
            return p;
        }

        /// <summary>Trajectoire d'un saut : ligne droite plus une cloche, d'autant plus haute que le dénivelé est grand.</summary>
        public static Vector3 PositionSaut(Vector3 depart, Vector3 arrivee, float k, float hauteurMin)
        {
            float cloche = 4f * k * (1f - k) * (hauteurMin + Mathf.Abs(arrivee.y - depart.y) * 0.5f);
            return Vector3.Lerp(depart, arrivee, k) + Vector3.up * cloche;
        }

        /// <summary>
        /// Le point est-il dans le champ de vision du joueur : moins de <paramref name="demiAngle"/>
        /// degrés de son regard (angle HORIZONTAL : la main au sol, juste devant, est loin sous un
        /// regard à l'horizontale mais le joueur la voit en baissant les yeux) et moins de
        /// <paramref name="distanceMax"/> mètres ?
        /// </summary>
        public static bool EstDansLeChamp(Vector3 tete, Vector3 regard, Vector3 point, float demiAngle, float distanceMax)
        {
            Vector3 versPoint = point - tete;
            if (versPoint.magnitude > distanceMax) return false;
            Vector3 h = Horizontal(versPoint);
            if (h.sqrMagnitude < 0.01f) return true; // juste sous ses pieds
            return Vector3.Angle(HorizontalVers(regard), h) <= demiAngle;
        }

        private static string NomDe(Collider c)
        {
            if (c == null) return "";
            return c.attachedRigidbody != null ? c.attachedRigidbody.name : c.name;
        }

        /// <summary>Ce qu'affiche l'étiquette pour un objet de la scène, d'après son nom.</summary>
        public static string Etiquette(string nom)
        {
            string n = nom.ToLowerInvariant();
            if (n.Contains("tableau")) return "Le tableau";
            if (n.Contains("plante")) return "La plante";
            if (n.Contains("statue")) return "La statue";
            if (n.Contains("porte")) return "La porte";
            if (n.Contains("pot")) return "Le pot";
            if (n.Contains("fraise") || n.Contains("strawberry")) return "La fraise";
            if (n.Contains("zone") || n.Contains("recipient")) return "Le bac";
            if (n.Contains("cle")) return "La clé";
            return "C'est ici !";
        }
    }
}
