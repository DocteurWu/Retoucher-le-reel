// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  TutorielGestes.cs — APPRENDRE LES GESTES AVEC DES MAINS FANTÔMES
//
//  RÔLE
//    Avant les étapes du niveau, le joueur apprend chaque geste sur un cube d'entraînement.
//    Pour chaque geste : une consigne (bulle de narration + panneau) et des mains
//    translucides qui le montrent en boucle (voir GestesTutoriel, MainFantome). La suite
//    n'arrive que lorsque le joueur a VRAIMENT fait le geste :
//      se déplacer → saisir → agrandir → ranger dans la bulle → dupliquer → récupérer → supprimer.
//
//  PAR MORCEAUX : Demarrer reçoit les gestes à enseigner. ProgressionEtapes s'en sert pour
//    n'apprendre, avant chaque étape, que les gestes qu'elle demande et que le joueur ne
//    connaît pas encore (Epreuve.GestesRequis). Un geste réussi, sauté ou passé est retenu.
//
//  VALIDATION : les événements du jeu (ObjetSaisi, ObjetRange, ObjetDuplique…), uniquement
//    sur le cube d'entraînement et ses copies ; agrandir et se déplacer sont mesurés.
//
//  LE CUBE D'ENTRAÎNEMENT : créé par le code, retiré à la fin. Il flotte là où on le lâche
//    et revient devant le joueur s'il est trop loin : il ne peut pas se perdre.
//
//  MANETTES : pas de mains fantômes, une consigne propre aux manettes, et les étapes de la
//    bulle sont sautées (X et Y agissent directement sur l'objet tenu).
//
//  LES TEXTES sont ici, pas dans la scène : l'outil des voix (Outils/VoixNarration) ne les
//    lit pas, ils s'affichent donc sans voix.
//
//  CRÉÉ PAR : TutorielMenu, bouton « Tutoriel » du menu d'entrée. PC : N = geste suivant,
//    T = arrêter le tutoriel.
// =====================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Tutoriel des gestes : mains fantômes, cube d'entraînement, étapes validées par le geste.</summary>
    [AddComponentMenu("")]
    public class TutorielGestes : MonoBehaviour
    {
        /// <summary>Levé à la fin du tutoriel, terminé ou passé.</summary>
        public event Action Termine;

        /// <summary>Couleur et opacité des mains fantômes.</summary>
        public Color couleurMains = new Color(0.45f, 0.9f, 1f, 0.5f);

        /// <summary>Position de l'ancre de démonstration, depuis la tête (m).</summary>
        public float distanceDemo = 0.45f, hauteurDemo = -0.2f;

        /// <summary>Taille du cube d'entraînement (m).</summary>
        public float tailleCube = 0.12f;

        /// <summary>Agrandi ou rétréci d'au moins ce facteur : le geste est réussi.</summary>
        public float facteurTaille = 1.5f;

        /// <summary>Déplacement du joueur (m, sans compter ses pas réels) : le geste est réussi.</summary>
        public float distanceDeplacement = 0.5f;

        private const float PauseEntreBoucles = 0.8f;
        private const float DureeFondu = 0.3f;
        private const float DouceurAncre = 3f;
        private const float DistanceCubeMax = 1.6f;
        private const float HauteurCubeMin = 0.1f;
        private static readonly Color CouleurCube = new Color(1f, 0.75f, 0.2f);

        private class Etape
        {
            public GesteTutoriel geste;
            public string panneau, consigne, consigneManettes, bravo;
            public bool mainsSeulement;
            public Func<ClipGeste> clip;
            /// <summary>Prise enregistrée (Resources/Gestes/…) jouée à la place du clip si elle existe.</summary>
            public string prise;
        }

        private List<Etape> _etapes;
        private int _indice = -1;
        private Transform _ancre;
        private MainFantome _gauche, _droite;
        private ClipGeste _clip;
        private float _tempsClip;
        private PriseGeste _prise;
        private readonly Pose[] _poses = new Pose[26];

        /// <summary>
        /// Où rejouer une prise, depuis l'ancre : une prise est enregistrée depuis les yeux
        /// (l'ancre est 45 cm devant et 20 cm sous eux). On la décale de 22 cm vers l'avant :
        /// les mains fantômes se tiennent devant celles du joueur au lieu de s'y superposer.
        /// </summary>
        private static readonly Vector3 DecalagePrise = new Vector3(0f, 0.23f, -0.23f);
        private readonly List<Transform> _accessoires = new List<Transform>();
        private readonly List<Material> _matAccessoires = new List<Material>();
        private readonly List<Rigidbody> _cubes = new List<Rigidbody>();
        /// <summary>Cubes ni tenus ni rangés, recalculés à chaque image (liste réutilisée : pas d'allocation).</summary>
        private readonly List<Rigidbody> _cubesLibres = new List<Rigidbody>();
        private TextMeshPro _panneau;
        private VRHandMenu _menu;
        private Material _matCube;
        private Vector3 _departRig;
        private float _prochainDiagnostic;
        private NarrationSequence _annonce;
        private readonly HashSet<GesteTutoriel> _appris = new HashSet<GesteTutoriel>();
        private readonly List<string> _avant = new List<string>();

        /// <summary>Vrai entre Demarrer et la fin du tutoriel.</summary>
        public bool EnCours { get; private set; }

        /// <summary>Vrai si un tutoriel des gestes est actuellement en cours.</summary>
        public static bool EstEnCours { get; private set; }

        private static bool ModeMains => VRModalityManager.IsHandMode;

        /// <summary>Sans casque (test au PC), on affiche quand même les mains pour vérifier les démonstrations.</summary>
        private static bool MontrerLesMains => ModeMains || VRDeviceState.EditorFallbackAllowed;

        private Transform _racine;

        /// <summary>
        /// Parent de tout ce que crée le tutoriel (ancre, mains, cube, panneau) : un objet à la
        /// racine de la scène, à l'échelle 1. Le composant peut vivre sur un objet réduit (le
        /// canvas du menu est à l'échelle 0,0018) : y rattacher les mains les rendait invisibles.
        /// </summary>
        private Transform Racine
        {
            get
            {
                if (_racine == null)
                {
                    _racine = new GameObject("Tutoriel_Gestes").transform;
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_racine.gameObject, gameObject.scene);
                }
                return _racine;
            }
        }

        private Etape Courante => _indice >= 0 && _indice < _etapes.Count ? _etapes[_indice] : null;

        /// <summary>Dite après le dernier geste (ex. « clique sur Jouer »). Vide = rien.</summary>
        public string phraseFinale = "";

        /// <summary>Oublie les gestes appris : le prochain Demarrer reprend tout depuis le début.</summary>
        public void Oublier()
        {
            if (!EnCours) _appris.Clear();
        }

        /// <summary>Parmi ces gestes, ceux que le joueur n'a pas encore appris.</summary>
        public List<GesteTutoriel> AApprendre(IEnumerable<GesteTutoriel> requis)
        {
            var reste = new List<GesteTutoriel>();
            foreach (GesteTutoriel g in requis) if (!_appris.Contains(g) && !reste.Contains(g)) reste.Add(g);
            return reste;
        }

        /// <summary>
        /// Enseigne ces gestes (null = tous), dans l'ordre du tutoriel. <paramref name="avant"/> :
        /// répliques dites juste avant (félicitation de l'étape précédente…). Faux s'il n'y a
        /// rien à enseigner : Termine n'est alors pas levé.
        /// </summary>
        public bool Demarrer(ICollection<GesteTutoriel> gestes = null, IEnumerable<string> avant = null)
        {
            if (EnCours) return false;
            _etapes = CreerEtapes();
            _etapes.RemoveAll(e => _appris.Contains(e.geste) || (gestes != null && !gestes.Contains(e.geste)));
            if (_etapes.Count == 0) return false;

            EnCours = true;
            EstEnCours = true;
            _avant.Clear();
            if (avant != null) foreach (string t in avant) if (!string.IsNullOrWhiteSpace(t)) _avant.Add(t);
            _menu = FindFirstObjectByType<VRHandMenu>();
            _matCube = VRUtils.CreateUnlitMaterial(CouleurCube);
            _ancre = new GameObject("Ancre_Tutoriel").transform;
            _ancre.SetParent(Racine, false);
            RecalerAncre(true);
            CreerMains();
            CreerPanneau();
            Abonner(true);
            PasserA(0, _appris.Count == 0 ? PhraseIntroPremiere : PhraseIntroSuite);
            return true;
        }

        /// <summary>Valide le geste en cours depuis le PC.</summary>
        public void EtapeSuivante()
        {
            if (EnCours) Reussir();
        }

        /// <summary>Met fin au tutoriel : retire tout ce qu'il a créé et rend la main au niveau.</summary>
        public void Terminer()
        {
            if (!EnCours) return;
            EnCours = false;
            EstEnCours = false;
            Abonner(false);
            // Passé depuis le PC : ces gestes comptent comme vus, ils ne reviendront pas à l'étape suivante.
            foreach (Etape e in _etapes) _appris.Add(e.geste);
            foreach (Rigidbody cube in _cubes) RetirerCube(cube);
            _cubes.Clear();
            ViderAccessoires();
            if (_ancre != null) Destroy(_ancre.gameObject);
            if (_panneau != null) Destroy(_panneau.gameObject);
            if (_matCube != null) Destroy(_matCube);
            Debug.Log("[Tutoriel] Terminé.", this);
            Termine?.Invoke();
        }

        private void OnDisable()
        {
            if (EnCours)
            {
                Abonner(false);
                EstEnCours = false;
            }
        }

        private void OnDestroy()
        {
            if (EnCours)
            {
                EnCours = false;
                EstEnCours = false;
            }
        }

        // ------------------------------------------------------------------
        // Les étapes
        // ------------------------------------------------------------------
        private static List<Etape> CreerEtapes()
        {
            return new List<Etape>
            {
                new Etape
                {
                    geste = GesteTutoriel.Deplacer,
                    panneau = "Déplace-toi",
                    consigneManettes = "Pour te déplacer dans la pièce, utilise le stick.",
                    bravo = "Bien."
                },
                new Etape
                {
                    geste = GesteTutoriel.Saisir, clip = GestesTutoriel.Saisir, prise = "Saisir",
                    panneau = "Attrape le cube",
                    consigne = "Approche ta main du cube : un petit tic te dit qu'il est à portée. Ferme alors le poing ou pince-le.",
                    consigneManettes = "Approche ta manette du cube et appuie sur la gâchette ou le grip.",
                    bravo = "Bien."
                },
                new Etape
                {
                    geste = GesteTutoriel.Agrandir, clip = GestesTutoriel.Agrandir, prise = "Agrandir",
                    panneau = "Change la taille du cube",
                    consigne = "Tiens le cube d'une main, touche-le et pince-le avec l'autre, puis écarte tes mains.",
                    consigneManettes = "Tiens le cube et pousse le stick droit vers le haut ou vers le bas.",
                    bravo = "Parfait."
                },
                new Etape
                {
                    geste = GesteTutoriel.Ranger, clip = GestesTutoriel.Ranger, prise = "Ranger", mainsSeulement = true,
                    panneau = "Range le cube dans la bulle",
                    consigne = "Tourne ta paume gauche vers ton visage et attends la bulle de ton poignet. Lâche le cube dedans.",
                    bravo = "Rangé."
                },
                new Etape
                {
                    geste = GesteTutoriel.Dupliquer, clip = GestesTutoriel.Dupliquer, prise = "Dupliquer",
                    panneau = "Copie le cube",
                    consigne = "Le cube est dans la bulle : touche ton index gauche avec ton index droit pour le copier.",
                    consigneManettes = "Tiens le cube et appuie sur X pour le copier.",
                    bravo = "Le voilà en double."
                },
                new Etape
                {
                    geste = GesteTutoriel.Recuperer, clip = GestesTutoriel.Recuperer, prise = "Recuperer", mainsSeulement = true,
                    panneau = "Récupère le cube",
                    consigne = "Touche ton annulaire gauche avec ton index droit : le cube revient dans ta main droite.",
                    bravo = "De retour."
                },
                new Etape
                {
                    geste = GesteTutoriel.Supprimer, clip = GestesTutoriel.Supprimer, prise = "Supprimer",
                    panneau = "Fais disparaître un cube",
                    consigne = "Range un cube dans la bulle, puis touche ton majeur gauche avec ton index droit pour le supprimer.",
                    consigneManettes = "Tiens un cube et appuie sur Y pour le supprimer.",
                    bravo = "Disparu."
                }
            };
        }

        /// <summary>Le déplacement dépend du mode choisi dans le menu : on montre celui qui marche vraiment.</summary>
        private void PreparerDeplacement(Etape etape)
        {
            if (VRLocomotionSettings.HandMode == HandLocomotionMode.Climb)
            {
                etape.clip = GestesTutoriel.Escalader;
                etape.prise = "Escalader";
                etape.consigne = "Pour te déplacer : ferme le poing, main vide, et tire vers toi comme sur une corde.";
                return;
            }
            VRTeleportation teleport = FindFirstObjectByType<VRTeleportation>();
            if (teleport != null && teleport.handTeleportGesture == HandTeleportGesture.PistoletGauche)
            {
                etape.clip = GestesTutoriel.TeleporterPistolet;
                etape.prise = "TeleporterPistolet";
                etape.consigne = "Pour te déplacer : fais le signe du pistolet avec ta main gauche, vise le sol et attends sans bouger.";
                return;
            }
            etape.clip = GestesTutoriel.TeleporterMicrogeste;
            etape.prise = "TeleporterMicrogeste";
            etape.consigne = "Pour te déplacer : poing lâche, main droite, tape ton pouce sur le côté de ton index pour viser le sol, puis tape encore pour y aller.";
        }

        // Textes dits par la voix du tutoriel : Outils/VoixNarration les relève dans ce fichier
        // (champs consigne / consigneManettes / bravo et constantes « Phrase… »).
        private const string PhraseIntroPremiere = "Avant tout, apprends tes gestes : imite les mains fantômes devant toi.";
        private const string PhraseIntroSuite = "Pour la suite, un nouveau geste : imite les mains fantômes.";

        private void PasserA(int indice, string prefixe)
        {
            // Manettes : les étapes de la bulle n'ont pas de sens.
            while (indice < _etapes.Count && _etapes[indice].mainsSeulement && !ModeMains && !VRDeviceState.EditorFallbackAllowed)
            {
                _appris.Add(_etapes[indice].geste);
                indice++;
            }

            _indice = indice;
            Etape etape = Courante;
            if (etape == null)
            {
                Annoncer(prefixe, phraseFinale);
                Terminer();
                return;
            }

            if (etape.geste == GesteTutoriel.Deplacer)
            {
                PreparerDeplacement(etape);
                _departRig = PositionRig();
            }

            RecalerAncre(true);
            AssurerCube();
            RepositionnerCubesLibres(true);
            Debug.Log("[Tutoriel] Geste : " + etape.geste + ".", this);
            // Ton vrai geste s'il a été enregistré, sinon la démonstration calculée.
            _prise = PriseGeste.Charger(etape.prise);
            JouerClip(_prise == null && etape.clip != null ? etape.clip() : null);

            string consigne = ModeMains || etape.consigneManettes == null ? etape.consigne : etape.consigneManettes;
            Annoncer(prefixe, consigne);
        }

        private void Reussir()
        {
            Etape etape = Courante;
            if (etape == null) return;
            Camera cam = Camera.main;
            VRSons.Jouer(VRSon.Reussite, cam != null ? cam.transform.position : transform.position);
            _appris.Add(etape.geste);
            PasserA(_indice + 1, etape.bravo);
        }

        /// <summary>Remplace ce que dit la bulle : une consigne périmée n'a plus à être écoutée.</summary>
        private void Annoncer(params string[] textes)
        {
            NarrationBulle bulle = FindFirstObjectByType<NarrationBulle>();
            if (bulle == null) return;
            bulle.ToutArreter();
            if (_annonce != null) Destroy(_annonce);

            _annonce = bulle.gameObject.AddComponent<NarrationSequence>();
            _annonce.declencheur = DeclencheurNarration.Manuel; // avant son Start : ne part pas toute seule
            _annonce.nom = "Tutoriel";
            _annonce.delai = 0.2f;
            _annonce.repliques = new List<Replique>();
            foreach (string t in _avant) _annonce.repliques.Add(new Replique { texte = t });
            _avant.Clear();
            // Une réplique par phrase (félicitation, puis consigne) : chacune a sa voix pré-générée,
            // ce qui serait impossible pour leurs combinaisons.
            foreach (string t in textes)
            {
                if (!string.IsNullOrWhiteSpace(t)) _annonce.repliques.Add(new Replique { texte = t });
            }
            _annonce.Declencher();
        }

        // ------------------------------------------------------------------
        // Validation par le geste
        // ------------------------------------------------------------------
        private void Abonner(bool oui)
        {
            if (oui)
            {
                VRGrabHand.ObjetSaisi += SurSaisi;
                VRHandMenu.ObjetRange += SurRange;
                VRHandMenu.ObjetRecupere += SurRecupere;
                VRDuplicateDelete.ObjetDuplique += SurDuplique;
                VRDuplicateDelete.ObjetSupprime += SurSupprime;
            }
            else
            {
                VRGrabHand.ObjetSaisi -= SurSaisi;
                VRHandMenu.ObjetRange -= SurRange;
                VRHandMenu.ObjetRecupere -= SurRecupere;
                VRDuplicateDelete.ObjetDuplique -= SurDuplique;
                VRDuplicateDelete.ObjetSupprime -= SurSupprime;
            }
        }

        private void Valider(GesteTutoriel geste, Rigidbody corps)
        {
            Etape etape = Courante;
            if (etape != null && etape.geste == geste && _cubes.Contains(corps)) Reussir();
        }

        private void SurSaisi(VRGrabHand main, Rigidbody corps) => Valider(GesteTutoriel.Saisir, corps);
        private void SurRange(Rigidbody corps) => Valider(GesteTutoriel.Ranger, corps);
        private void SurRecupere(Rigidbody corps) => Valider(GesteTutoriel.Recuperer, corps);

        private void SurDuplique(Rigidbody corps, GameObject clone)
        {
            if (!_cubes.Contains(corps)) return;
            Rigidbody copie = clone != null ? clone.GetComponent<Rigidbody>() : null;
            if (copie != null) _cubes.Add(copie);
            Valider(GesteTutoriel.Dupliquer, corps);
        }

        private void SurSupprime(Rigidbody corps)
        {
            Etape etape = Courante;
            bool valide = etape != null && etape.geste == GesteTutoriel.Supprimer && _cubes.Contains(corps);
            _cubes.Remove(corps);
            if (valide) Reussir();
        }

                // [LH] Validation de la posture courante de la main par rapport au gabarit cible
        private void Update()
        {
            if (!EnCours) return;

            RecalerAncre(false);
            EntretenirCubes();
            MettreAJourPanneau();

            Etape etape = Courante;
            if (etape == null) return;
            if (etape.geste == GesteTutoriel.Agrandir && UnCubeEstRedimensionne()) Reussir();
            else if (etape.geste == GesteTutoriel.Deplacer && Vector3.Distance(PositionRig(), _departRig) >= distanceDeplacement) Reussir();
        }

        private void LateUpdate()
        {
            // LateUpdate : après le déplacement du rig, l'ancre n'a pas une frame de retard sur la tête.
            if (EnCours) AfficherClip();
        }

        private bool UnCubeEstRedimensionne()
        {
            foreach (Rigidbody cube in _cubes)
            {
                if (cube == null) continue;
                Vector3 e = cube.transform.lossyScale;
                float ratio = Mathf.Max(e.x, Mathf.Max(e.y, e.z)) / tailleCube;
                if (ratio >= facteurTaille || ratio <= 1f / facteurTaille) return true;
            }
            return false;
        }

        private static Vector3 PositionRig()
        {
            Transform rig = VRHandGestures.TrackingSpace;
            return rig != null ? rig.position : Vector3.zero;
        }

        // ------------------------------------------------------------------
        // Le cube d'entraînement
        // ------------------------------------------------------------------
        private Vector3 PlaceDuCube(int index = 0, int total = 1)
        {
            if (_ancre == null) return Vector3.zero;
            Vector3 centre = _ancre.position + _ancre.forward * 0.12f + Vector3.up * 0.05f;
            if (total <= 1) return centre;
            float espacement = 0.18f;
            float xOffset = (index - (total - 1) * 0.5f) * espacement;
            return centre + _ancre.right * xOffset;
        }

        private void AssurerCube()
        {
            _cubes.RemoveAll(c => c == null);
            if (_cubes.Count > 0 || Courante == null || Courante.geste == GesteTutoriel.Deplacer) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube_Entrainement";
            go.transform.SetParent(Racine, false);
            go.transform.position = PlaceDuCube();
            go.transform.rotation = _ancre != null ? _ancre.rotation : Quaternion.identity;
            go.transform.localScale = Vector3.one * tailleCube;
            go.GetComponent<Renderer>().sharedMaterial = _matCube;
            int calque = LayerMask.NameToLayer("Grabbable");
            if (calque >= 0) go.layer = calque;

            Rigidbody corps = go.AddComponent<Rigidbody>();
            corps.isKinematic = true;
            corps.useGravity = false;
            go.AddComponent<VRScalable>().maxSizeRatio = 3f;
            Physics.SyncTransforms(); // autoSyncTransforms = 0
            _cubes.Add(corps);
        }

        private bool EstDansLaBulle(Rigidbody cube) => _menu != null && _menu.ObjetDansLaBulle == cube;

        /// <summary>
        /// DIAGNOSTIC (le cube ne se laissait pas saisir) : pendant « Attrape le cube », écrit
        /// toutes les 2 s la distance de chaque main au cube et l'état de son geste de saisie.
        /// </summary>
        private void JournaliserSaisie()
        {
            if (Courante == null || Courante.geste != GesteTutoriel.Saisir || Time.time < _prochainDiagnostic || _cubes.Count == 0 || _cubes[0] == null) return;
            _prochainDiagnostic = Time.time + 2f;
            Collider boite = _cubes[0].GetComponent<Collider>();
            var texte = new System.Text.StringBuilder("[Tutoriel] Saisie du cube en " + _cubes[0].position.ToString("F2")
                + ", cinématique " + _cubes[0].isKinematic + ", mode mains " + ModeMains);
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main == null) continue;
                Vector3 p = main.transform.position;
                float distance = boite != null ? Vector3.Distance(boite.ClosestPoint(p), p) : Vector3.Distance(_cubes[0].position, p);
                texte.Append(" | ").Append(main.handNode).Append(" à ").Append((distance * 100f).ToString("F0")).Append(" cm, geste ")
                     .Append(main.IsGrabInputActive ? "fermé" : "ouvert").Append(", tient ").Append(main.GrabbedBody != null ? main.GrabbedBody.name : "rien");
            }
            Debug.Log(texte.ToString(), this);
        }

        /// <summary>
        /// Maintient les cubes libres confortablement devant les yeux du joueur.
        /// Suit doucement la vue quand aucune main n'est proche ; se fige dès qu'une main approche pour saisir.
        /// </summary>
        private void RepositionnerCubesLibres(bool direct)
        {
            if (_ancre == null) return;
            Camera cam = Camera.main;

            List<Rigidbody> libres = _cubesLibres;
            libres.Clear();
            foreach (Rigidbody c in _cubes)
            {
                if (c != null && !EstDansLaBulle(c) && !EstTenu(c)) libres.Add(c);
            }

            for (int i = 0; i < libres.Count; i++)
            {
                Rigidbody cube = libres[i];
                if (!cube.isKinematic)
                {
                    cube.isKinematic = true;
                    cube.useGravity = false;
                }

                Vector3 cible = PlaceDuCube(i, libres.Count);
                Quaternion rotCible = _ancre.rotation;
                Vector3 position = cube.transform.position;

                bool sousLeSol = position.y < HauteurCubeMin;
                bool horsChamp = false;
                if (cam != null)
                {
                    float dist = Vector3.Distance(position, cam.transform.position);
                    Vector3 versCube = (position - cam.transform.position).normalized;
                    float dotRegard = Vector3.Dot(cam.transform.forward, versCube);
                    horsChamp = dist > DistanceCubeMax || dotRegard < 0.35f;
                }

                if (direct || sousLeSol || horsChamp)
                {
                    cube.transform.position = cible;
                    cube.transform.rotation = rotCible;
                    Physics.SyncTransforms();
                }
                else
                {
                    // Si une main approche pour saisir le cube (< 28 cm), on le stabilise
                    bool mainProche = false;
                    foreach (VRGrabHand main in VRGrabHand.AllHands)
                    {
                        if (main != null && Vector3.Distance(main.transform.position, position) < 0.28f)
                        {
                            mainProche = true;
                            break;
                        }
                    }

                    if (!mainProche)
                    {
                        float k = 1f - Mathf.Exp(-4.5f * Time.deltaTime);
                        cube.transform.position = Vector3.Lerp(position, cible, k);
                        cube.transform.rotation = Quaternion.Slerp(cube.transform.rotation, rotCible, k);
                        Physics.SyncTransforms();
                    }
                }
            }
        }

        private void EntretenirCubes()
        {
            AssurerCube();
            JournaliserSaisie();
            RepositionnerCubesLibres(direct: false);

            // Rangé trop tôt : la bulle rend le cube, l'étape en a besoin devant le joueur.
            Etape etape = Courante;
            if (_menu == null || etape == null || (etape.geste != GesteTutoriel.Saisir && etape.geste != GesteTutoriel.Agrandir)) return;
            Rigidbody range = _menu.ObjetDansLaBulle;
            if (range == null || !_cubes.Contains(range)) return;
            _menu.SortirDeLaBulle(range);
            range.transform.position = PlaceDuCube();
            Physics.SyncTransforms();
        }

        private static bool EstTenu(Rigidbody corps)
        {
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null && main.GrabbedBody == corps) return true;
            }
            return false;
        }

        private void RetirerCube(Rigidbody cube)
        {
            if (cube == null) return;
            foreach (VRGrabHand main in VRGrabHand.AllHands)
            {
                if (main != null && main.GrabbedBody == cube) main.ReleaseObject(applyThrowVelocity: false);
            }
            if (_menu != null) _menu.SortirDeLaBulle(cube);
            Destroy(cube.gameObject);
        }

        // ------------------------------------------------------------------
        // Les mains fantômes et leur film
        // ------------------------------------------------------------------
        private void CreerMains()
        {
            VRModalityManager modalite = VRModalityManager.Instance;
            if (modalite == null) return;
            modalite.ResolveReferences();
            _gauche = MainFantome.Creer(modalite.leftHandVisual, true, couleurMains, _ancre);
            _droite = MainFantome.Creer(modalite.rightHandVisual, false, couleurMains, _ancre);
        }

        private void RecalerAncre(bool direct)
        {
            Camera cam = Camera.main;
            if (cam == null || _ancre == null) return;

            Vector3 regard = cam.transform.forward;
            regard.y = 0f;
            if (regard.sqrMagnitude < 1e-4f) return;
            regard.Normalize();

            Vector3 position = cam.transform.position + regard * distanceDemo + Vector3.up * hauteurDemo;
            Quaternion rotation = Quaternion.LookRotation(regard, Vector3.up);
            float k = direct ? 1f : 1f - Mathf.Exp(-DouceurAncre * Time.deltaTime);
            _ancre.SetPositionAndRotation(Vector3.Lerp(_ancre.position, position, k), Quaternion.Slerp(_ancre.rotation, rotation, k));
        }

        private void JouerClip(ClipGeste clip)
        {
            _clip = clip;
            _tempsClip = 0f;
            ViderAccessoires();
            if (clip == null) return;
            foreach (PisteAccessoire piste in clip.accessoires) CreerAccessoire(piste.forme);
        }

        private void CreerAccessoire(FormeAccessoire forme)
        {
            PrimitiveType primitive = forme == FormeAccessoire.Cube ? PrimitiveType.Cube
                : forme == FormeAccessoire.Cible ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = "Accessoire_" + forme;
            DestroyImmediate(go.GetComponent<Collider>()); // la saisie ne doit surtout pas l'attraper
            go.transform.SetParent(_ancre, false);
            Color couleur = forme == FormeAccessoire.Cube ? CouleurCube : couleurMains;
            couleur.a = OpaciteAccessoire(forme);
            Material materiau = VRUtils.CreateUnlitMaterial(couleur);
            go.GetComponent<Renderer>().sharedMaterial = materiau;
            _accessoires.Add(go.transform);
            _matAccessoires.Add(materiau);
        }

        private static float OpaciteAccessoire(FormeAccessoire forme)
        {
            switch (forme)
            {
                case FormeAccessoire.Cube: return 0.55f;
                case FormeAccessoire.Cible: return 0.45f;
                default: return 0.2f;
            }
        }

        private void ViderAccessoires()
        {
            foreach (Transform t in _accessoires) if (t != null) Destroy(t.gameObject);
            foreach (Material m in _matAccessoires) if (m != null) Destroy(m);
            _accessoires.Clear();
            _matAccessoires.Clear();
        }

        /// <summary>Rejoue la prise enregistrée en boucle, avec un fondu à chaque tour.</summary>
        private void AfficherPrise()
        {
            _tempsClip += Time.deltaTime;
            if (_tempsClip > _prise.Duree + PauseEntreBoucles) _tempsClip = 0f;
            float t = Mathf.Min(_tempsClip, _prise.Duree);
            float opacite = _tempsClip > _prise.Duree ? 0f : Mathf.Clamp01(Mathf.Min(t, _prise.Duree - t) / DureeFondu);

            Vector3 origine = _ancre.TransformPoint(DecalagePrise);
            AfficherMainPrise(_gauche, true, t, opacite, origine);
            AfficherMainPrise(_droite, false, t, opacite, origine);
        }

        private void AfficherMainPrise(MainFantome main, bool gauche, float t, float opacite, Vector3 origine)
        {
            if (main == null) return;
            bool visible = opacite > 0.001f && _prise.Echantillonner(t, gauche, _poses);
            if (main.gameObject.activeSelf != visible) main.gameObject.SetActive(visible);
            if (!visible) return;
            main.PlacerArticulations(_poses, origine, _ancre.rotation);
            main.Opacite(opacite);
        }

        private void AfficherClip()
        {
            if (_prise != null && MontrerLesMains)
            {
                AfficherPrise();
                return;
            }
            if (_clip == null || !MontrerLesMains)
            {
                AfficherMain(_gauche, null, 0f);
                AfficherMain(_droite, null, 0f);
                foreach (Transform accessoire in _accessoires) accessoire.gameObject.SetActive(false);
                return;
            }

            _tempsClip += Time.deltaTime;
            if (_tempsClip > _clip.duree + PauseEntreBoucles) _tempsClip = 0f;
            float t2 = Mathf.Min(_tempsClip, _clip.duree);

            // Fondu au début et à la fin de chaque boucle : le saut de retour ne se voit pas.
            float opacite = Mathf.Clamp01(Mathf.Min(t2, _clip.duree - t2) / DureeFondu);
            if (_tempsClip > _clip.duree) opacite = 0f;

            AfficherMain(_gauche, _clip.gauche, opacite);
            AfficherMain(_droite, _clip.droite, opacite);

            for (int i = 0; i < _clip.accessoires.Count; i++)
            {
                _clip.accessoires[i].Encadrer(t2, out CleAccessoire a, out CleAccessoire b, out float k);
                float echelle = Mathf.Lerp(a.echelle, b.echelle, k);
                Transform t = _accessoires[i];
                t.gameObject.SetActive(echelle > 0.001f && opacite > 0.001f);
                t.localPosition = Vector3.Lerp(a.position, b.position, k);
                bool disque = _clip.accessoires[i].forme == FormeAccessoire.Cible;
                t.localScale = disque ? new Vector3(echelle, 0.01f, echelle) : Vector3.one * echelle;
                Color c = _matAccessoires[i].color;
                c.a = OpaciteAccessoire(_clip.accessoires[i].forme) * opacite;
                _matAccessoires[i].color = c;
            }
        }

        private void AfficherMain(MainFantome main, PisteMain piste, float opacite)
        {
            if (main == null) return;
            bool visible = piste != null && opacite > 0.001f;
            if (main.gameObject.activeSelf != visible) main.gameObject.SetActive(visible);
            if (!visible) return;

            piste.Encadrer(Mathf.Min(_tempsClip, _clip.duree), out CleMain a, out CleMain b, out float k);
            main.Placer(_ancre.TransformPoint(Vector3.Lerp(a.position, b.position, k)),
                        _ancre.rotation * Quaternion.Slerp(a.rotation, b.rotation, k),
                        a.pose, b.pose, k);
            main.Opacite(opacite);
        }

        // ------------------------------------------------------------------
        // Le panneau : la consigne reste lisible une fois la bulle partie
        // ------------------------------------------------------------------
        private void CreerPanneau()
        {
            GameObject go = new GameObject("Panneau_Tutoriel");
            go.transform.SetParent(Racine, false);
            _panneau = go.AddComponent<TextMeshPro>();
            _panneau.enableAutoSizing = true;
            _panneau.fontSizeMin = 0.01f;
            _panneau.fontSizeMax = 1f;
            _panneau.alignment = TextAlignmentOptions.Center;
            _panneau.textWrappingMode = TextWrappingModes.NoWrap;
            _panneau.rectTransform.sizeDelta = new Vector2(0.5f, 0.09f);
        }

        private void MettreAJourPanneau()
        {
            Etape etape = Courante;
            if (_panneau == null || etape == null) return;

            bool bulleVide = _menu != null && _menu.ObjetDansLaBulle == null;
            bool aideBulle = ModeMains && bulleVide && (etape.geste == GesteTutoriel.Dupliquer || etape.geste == GesteTutoriel.Recuperer);
            _panneau.text = "<color=#7fe6ff>Geste " + (_indice + 1) + "/" + _etapes.Count + "</color>\n<b>" + etape.panneau + "</b>"
                            + (aideBulle ? "\n<color=#ffc34d>Range d'abord le cube dans la bulle</color>" : "");

            _panneau.transform.SetPositionAndRotation(_ancre.position + _ancre.forward * 0.25f + Vector3.up * 0.42f, _ancre.rotation);
        }
    }
}
