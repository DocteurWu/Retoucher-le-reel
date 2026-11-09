// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ProgressionEtapes.cs — LES ÉTAPES DU NIVEAU ET LA PORTE DE SORTIE
//
//  RÔLE
//    Liste les épreuves (étapes) du niveau. Tant qu'elles ne sont pas TOUTES
//    réussies, la porte de sortie reste verrouillée ; un panneau au-dessus d'elle
//    affiche la liste des étapes (faites en vert). Quand tout est fait :
//      • la porte se déverrouille (son, événement Terminees → narration
//        « Etapes Terminees ») ;
//      • ouvrir la porte en grand charge la scène suivante.
//    Les étapes se font dans l'ordre qu'on veut ; l'ordre de la liste est celui
//    de l'affichage.
//
//  NARRATION DES CONSIGNES
//    Après l'introduction (« Après La Narration »), la bulle présente le niveau puis
//    la consigne de la première étape. À chaque étape réussie : sa félicitation, puis
//    la consigne de la PREMIÈRE étape restante (dans l'ordre de la liste). Les textes
//    sont sur chaque épreuve (champs « Consigne » et « Felicitation »). Une étape réussie
//    coupe ce que dit la bulle : le joueur n'attend pas la fin d'une consigne périmée.
//
//  MAIN GUIDE
//    Dès la première consigne, une main (façon « La Chose » de la famille Addams) va
//    pointer les objets de l'étape en cours (voir MainGuide). Rien à poser dans la scène.
//
//  SURBRILLANCE
//    Les objets de l'étape annoncée (la première restante) sont entourés d'une boîte
//    lumineuse (voir Surbrillance) ; quand tout est fait, c'est la porte.
//    DÉSACTIVÉE pour l'instant : cocher « Montrer Les Objets » pour l'activer.
//
//  COMMANDES DE DÉMO
//    Panneau et raccourcis sur l'écran du PC pour valider une étape ratée ou impossible
//    pendant qu'un joueur est dans le casque (voir CommandesDemo).
//
//  À POSER SUR : un objet vide « Progression ». Glisser les épreuves dans « Etapes »
//    et la porte (VRPorte) dans « Porte ».
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Étapes du niveau : porte verrouillée jusqu'à ce que toutes soient réussies.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Progression des étapes")]
    public class ProgressionEtapes : MonoBehaviour
    {
        /// <summary>Levé quand toutes les étapes sont réussies (la porte se déverrouille).</summary>
        public static event Action<ProgressionEtapes> Terminees;

        [Tooltip("Épreuves à réussir, dans l'ordre d'affichage.")]
        public List<Epreuve> etapes = new List<Epreuve>();

        [Tooltip("Porte de sortie : verrouillée tant que des étapes restent à faire.")]
        public VRPorte porte;

        [Tooltip("Scène chargée quand la porte (déverrouillée) est ouverte en grand. Vide = rien.")]
        public string sceneSuivante = "Scene2";

        [Min(0f)] public float delaiAvantScene = 1.5f;

        [Header("Sortie : la scène brûle")]
        [Tooltip("Porte ouverte en grand : la scène brûle jusqu'à ses Gaussiennes (onde partie de la porte) avant de charger la scène suivante (voir VRBrulure).")]
        public bool brulerALaSortie = true;

        [Tooltip("Temps laissé à la brûlure avant de charger la scène suivante (s). Remplace « Delai Avant Scene ».")]
        [Min(0f)] public float dureeBrulure = 3.5f;

        [Tooltip("La scène suivante se reconstruit autour du joueur (onde cyan) à son arrivée.")]
        public bool reconstruireALArrivee = true;

        [Header("Narration des consignes")]
        [Tooltip("Les consignes commencent quand cette narration se termine (ex. l'introduction). Vide = après « Délai Première Consigne ».")]
        public NarrationSequence apresLaNarration;

        [Min(0f)] public float delaiPremiereConsigne = 4f;

        [Tooltip("Étape réussie : couper la narration en cours pour enchaîner tout de suite sur la suite.")]
        public bool couperNarrationSurReussite = true;

        [TextArea(2, 5)]
        [Tooltip("Dite une fois, avant la première consigne (présentation du niveau). Vide = rien.")]
        public string presentation = "";

        [Header("Main guide")]
        [Tooltip("Une main parcourt la pièce et pointe les objets de l'étape en cours.")]
        public bool mainGuide = true;

        [Header("Démo")]
        [Tooltip("Panneau sur l'écran du PC et raccourcis (1-9, N, O, H) pour valider des étapes pendant une démo.")]
        public bool commandesDemo = true;

        [Header("Surbrillance")]
        [Tooltip("Entourer d'une boîte lumineuse les objets de l'étape en cours, puis la porte. Désactivé pour l'instant.")]
        public bool montrerLesObjets = false;

        public Color couleurSurbrillance = new Color(0.3f, 0.9f, 1f);

        [Header("Affichage")]
        [Tooltip("Afficher la liste des étapes au-dessus de la porte.")]
        public bool afficherListe = true;

        [Tooltip("Où placer le panneau. Vide = au-dessus de l'origine (charnière) de la porte.")]
        public Transform ancrePanneau;

        [Tooltip("Sans ancre : hauteur du panneau au-dessus de l'origine de la porte (m).")]
        public float hauteurPanneau = 2.25f;

        /// <summary>Vrai quand toutes les étapes sont réussies.</summary>
        public bool EstTerminee { get; private set; }

        /// <summary>L'étape annoncée par la narration (null quand tout est fait).</summary>
        public Epreuve EtapeEnCours => EstTerminee ? null : ProchaineEtape();

        private MainGuide _mainGuide;
        private TextMeshPro _panneau;
        private readonly StringBuilder _texte = new StringBuilder();
        private float _alerteJusqua;
        private bool _chargement;
        private bool _consignesLancees;
        private bool _narrationCoupee;
        private Surbrillance _surbrillance;
        private float _prochaineSurbrillance;
        private readonly List<Collider> _aMontrer = new List<Collider>();
        private readonly List<NarrationSequence> _annonces = new List<NarrationSequence>();

                // [LH-State] Souscription aux événements de progression de scène (dev: LH)
        private void OnEnable()
        {
            Epreuve.UneEpreuveReussie += SurEpreuve;
            VRPorte.Ouverte += SurPorteOuverte;
            VRPorte.TentativeVerrouillee += SurPorteVerrouillee;
        }

        private void OnDisable()
        {
            Epreuve.UneEpreuveReussie -= SurEpreuve;
            VRPorte.Ouverte -= SurPorteOuverte;
            VRPorte.TentativeVerrouillee -= SurPorteVerrouillee;
        }

                // [LH-Scenario] Lancement de la premiere etape active du jeu
        private void Start()
        {
            _mainGuidePrevue = mainGuide;
            etapes.RemoveAll(e => e == null);
            if (porte != null) porte.verrouillee = etapes.Count > 0;
            if (afficherListe && porte != null) CreerPanneau();
            if (montrerLesObjets)
            {
                _surbrillance = new GameObject("Surbrillance").AddComponent<Surbrillance>();
                _surbrillance.transform.SetParent(transform, false);
                _surbrillance.couleur = couleurSurbrillance;
            }
            if (commandesDemo) gameObject.AddComponent<CommandesDemo>().progression = this;
            Rafraichir();

            if (apresLaNarration != null) apresLaNarration.Terminee += SurIntroTerminee;
            else StartCoroutine(PremiereConsigneApresDelai());
        }

        private void OnDestroy()
        {
            if (apresLaNarration != null) apresLaNarration.Terminee -= SurIntroTerminee;
        }

        // ------------------------------------------------------------------
        // Narration des consignes
        // ------------------------------------------------------------------
        private void SurIntroTerminee(NarrationSequence intro)
        {
            intro.Terminee -= SurIntroTerminee;
            Annoncer(null, true);
        }

        private IEnumerator PremiereConsigneApresDelai()
        {
            yield return new WaitForSeconds(delaiPremiereConsigne);
            if (!_narrationCoupee) Annoncer(null, true);
        }

        /// <summary>Vrai dès la première consigne (la main guide attend ce moment pour se montrer).</summary>
        public bool ConsignesLancees => _consignesLancees;

        /// <summary>La main guide posée dans la scène (prefab « Main Guide », ex. via le Kit), sinon une créée ici.</summary>
        /// <summary>Chemin du prefab de la main guide sous Resources (Assets/Projet/Resources/Prefabs/Main Guide.prefab).</summary>
        public const string CheminPrefabMainGuide = "Prefabs/Main Guide";

        private void CreerMainGuide()
        {
            if (_mainGuide != null) return;
            _mainGuide = FindFirstObjectByType<MainGuide>(FindObjectsInactive.Include);
            if (_mainGuide != null)
            {
                _mainGuide.progression = this;
                _mainGuide.gameObject.SetActive(true);
                return;
            }
            // Pas de main posée dans la scène (Scene3…) : le prefab « Main Guide » (Resources), avec
            // son apparence ; à défaut, une main fabriquée depuis celle du rig.
            GameObject prefab = Resources.Load<GameObject>(CheminPrefabMainGuide);
            if (prefab != null)
            {
                // À l'origine : MainGuide.Awake retient cette position comme « posée dans l'éditeur »
                // et chercherait une table dessous (un canapé de la Grange ferait l'affaire…).
                GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                instance.name = "Main Guide";
                _mainGuide = instance.GetComponent<MainGuide>();
            }
            if (_mainGuide == null)
            {
                _mainGuide = new GameObject("Main_Guide_Racine").AddComponent<MainGuide>();
                _mainGuide.transform.SetParent(transform, false);
            }
            _mainGuide.progression = this;
        }

        /// <summary>
        /// Panneau de démo : active ou coupe la main guide, tout de suite (elle n'apparaît
        /// qu'une fois les consignes lancées) et pour les parties suivantes.
        /// </summary>
        /// <summary>Vrai si la main guide est montrée : prévue pour ce niveau ET non coupée depuis le panneau de démo.</summary>
        public bool MainGuideVisible => mainGuide && ReglagesAide.MainGuide;

        /// <summary>
        /// Allume ou éteint la main guide (touche G, case guideVersLaPlace du jeu de mémoire).
        /// L'allumer l'autorise aussi dans un niveau où elle était prévue éteinte (Scene3 :
        /// mainGuide = faux pour ne pas tricher) : avant, G n'y faisait rien.
        /// </summary>
        public void ActiverMainGuide(bool active)
        {
            if (active)
            {
                ReglagesAide.MainGuide = true; // la main lit ce réglage et se cache / se montre seule
                mainGuide = true;
                if (_consignesLancees) CreerMainGuide();
            }
            else if (_mainGuidePrevue)
            {
                ReglagesAide.MainGuide = false; // choix du joueur / du dev, gardé d'une partie à l'autre
            }
            else
            {
                // Niveau où la main est prévue éteinte (Scene3) : on revient à cet état sans toucher
                // au réglage persistant. Avant, l'allumer puis l'éteindre ici la coupait aussi dans
                // le niveau céramique aux parties suivantes.
                mainGuide = false;
            }
        }

        /// <summary>Valeur de la case mainGuide au lancement (le niveau prévoit-il la main ?).</summary>
        private bool _mainGuidePrevue;

        /// <summary>
        /// Coupe ce que dit la bulle et retire les consignes en attente, devenues périmées (la
        /// bulle vide sa file sans les prévenir). Une étape réussie pendant l'introduction saute
        /// aussi la présentation : le joueur n'a pas besoin qu'on lui explique ce qu'il fait déjà.
        /// </summary>
        private void CouperNarration()
        {
            _narrationCoupee = true;
            if (apresLaNarration != null) apresLaNarration.Terminee -= SurIntroTerminee;

            NarrationBulle bulle = FindFirstObjectByType<NarrationBulle>();
            if (bulle != null) bulle.ToutArreter();

            foreach (NarrationSequence annonce in _annonces)
            {
                if (annonce != null) Destroy(annonce);
            }
            _annonces.Clear();
        }

        /// <summary>Fait dire à la bulle : félicitation (optionnelle), présentation (optionnelle), consigne de la prochaine étape.</summary>
        private void Annoncer(string felicitation, bool avecPresentation)
        {
            _consignesLancees = true; // la surbrillance démarre avec la première consigne
            if (mainGuide) CreerMainGuide();
            var repliques = new List<Replique>();
            if (!string.IsNullOrWhiteSpace(felicitation)) repliques.Add(new Replique { texte = felicitation, duree = 2.5f });
            if (avecPresentation && !string.IsNullOrWhiteSpace(presentation)) repliques.Add(new Replique { texte = presentation });
            Epreuve suivante = ProchaineEtape();
            if (suivante != null && !EstTerminee) repliques.Add(new Replique { texte = suivante.consigne });
            if (repliques.Count == 0) return;

            NarrationBulle bulle = FindFirstObjectByType<NarrationBulle>();
            if (bulle == null) return;

            // Une séquence « Manuel » par annonce : la bulle les enchaîne dans l'ordre (file
            // d'attente), puis chacune se retire une fois dite.
            NarrationSequence annonce = bulle.gameObject.AddComponent<NarrationSequence>();
            annonce.declencheur = DeclencheurNarration.Manuel; // avant son Start : ne part pas toute seule
            annonce.nom = suivante != null ? "Consigne : " + suivante.nomEtape : "Félicitation";
            annonce.delai = 0.5f;
            annonce.repliques = repliques;
            annonce.Terminee += s =>
            {
                _annonces.Remove(s);
                Destroy(s);
            };
            _annonces.Add(annonce);
            annonce.Declencher();
        }

        // ------------------------------------------------------------------
        // Commandes de démo
        // ------------------------------------------------------------------
        /// <summary>Valide une étape depuis le PC : la narration en cours laisse la place à la suite.</summary>
        public void ForcerEtape(Epreuve etape)
        {
            if (etape == null || etape.EstReussie) return;

            CouperNarration();
            etape.ForcerReussite();
        }

        /// <summary>Valide la première étape restante.</summary>
        public void ForcerSuivante()
        {
            ForcerEtape(etapes.Find(e => e != null && !e.EstReussie));
        }

        /// <summary>Charge la scène suivante sans attendre la fin des étapes.</summary>
        public void ChargerMaintenant()
        {
            if (_chargement || string.IsNullOrEmpty(sceneSuivante)) return;
            StartCoroutine(Charger());
        }

        /// <summary>L'étape annoncée par la narration : la première restante qui a une consigne.</summary>
        private Epreuve ProchaineEtape()
        {
            return etapes.Find(e => !e.EstReussie && !string.IsNullOrWhiteSpace(e.consigne));
        }

        /// <summary>Met à jour les objets montrés (5 fois par seconde : ils changent rarement).</summary>
        private void MettreAJourSurbrillance()
        {
            if (_surbrillance == null || !_consignesLancees || Time.time < _prochaineSurbrillance) return;
            _prochaineSurbrillance = Time.time + 0.2f;

            RemplirObjetsEnCours(_aMontrer);
            _surbrillance.Montrer(_aMontrer);
        }

        /// <summary>Les objets à montrer au joueur en ce moment : ceux de l'étape en cours, puis la porte quand tout est fait.</summary>
        public void RemplirObjetsEnCours(List<Collider> liste)
        {
            if (EstTerminee)
            {
                liste.Clear();
                if (porte != null && porte.zonePrise != null && !porte.EstTenue && !_chargement) liste.Add(porte.zonePrise);
            }
            else
            {
                Epreuve courante = ProchaineEtape();
                if (courante != null) courante.RemplirObjetsAMontrer(liste);
                else liste.Clear();
            }
        }

        private void Update()
        {
            MettreAJourSurbrillance();
            if (_panneau == null) return;
            Vector3 position = ancrePanneau != null ? ancrePanneau.position : porte.transform.position + Vector3.up * hauteurPanneau;
            _panneau.transform.position = position;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 regard = position - cam.transform.position;
                regard.y = 0f;
                if (regard.sqrMagnitude > 1e-6f) _panneau.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
            }
            // Fin de l'alerte « verrouillée » : on remet la liste.
            if (_alerteJusqua > 0f && Time.time > _alerteJusqua)
            {
                _alerteJusqua = 0f;
                Rafraichir();
            }
        }

        // ------------------------------------------------------------------
        // Événements
        // ------------------------------------------------------------------
        private void SurEpreuve(Epreuve epreuve)
        {
            if (!etapes.Contains(epreuve)) return;
            if (couperNarrationSurReussite) CouperNarration();
            Rafraichir();
            Annoncer(epreuve.felicitation, false);
        }

        private void SurPorteVerrouillee(VRPorte p)
        {
            if (p != porte || _panneau == null) return;
            _alerteJusqua = Time.time + 2f;
            int restantes = etapes.FindAll(e => !e.EstReussie).Count;
            _panneau.text = "<color=#ff6b5d><b>Fermée à clé</b></color>\n" + restantes
                            + (restantes > 1 ? " étapes restantes" : " étape restante");
        }

        private void SurPorteOuverte(VRPorte p)
        {
            if (p != porte || !EstTerminee || _chargement || string.IsNullOrEmpty(sceneSuivante)) return;
            StartCoroutine(Charger());
        }

        private void Rafraichir()
        {
            int faites = 0;
            foreach (Epreuve e in etapes) if (e.EstReussie) faites++;

            if (!EstTerminee && faites == etapes.Count && etapes.Count > 0)
            {
                EstTerminee = true;
                if (porte != null) porte.Deverrouiller();
                if (porte != null) VRSons.Jouer(VRSon.Reussite, porte.transform.position + Vector3.up);
                Debug.Log("[Progression] Toutes les étapes sont réussies : la porte est déverrouillée.", this);
                Terminees?.Invoke(this);
            }

            if (_panneau == null || _alerteJusqua > 0f) return;
            _texte.Length = 0;
            if (EstTerminee)
            {
                _texte.Append("<color=#5dff7a><b>Porte déverrouillée</b></color>");
            }
            else
            {
                _texte.Append("<b>Étapes ").Append(faites).Append('/').Append(etapes.Count).Append("</b>");
                foreach (Epreuve e in etapes)
                {
                    _texte.Append('\n').Append(e.EstReussie ? "<color=#5dff7a>OK  " : "<color=#d0d0d0>•  ")
                          .Append(e.nomEtape).Append("</color>");
                }
            }
            _panneau.text = _texte.ToString();
        }

        private IEnumerator Charger()
        {
            _chargement = true;
            bool peutCharger = Application.CanStreamedLevelBeLoaded(sceneSuivante);
            Vector3 foyer = FoyerBrulure();
            if (brulerALaSortie && peutCharger)
            {
                VRBrulure.Bruler(foyer);
                if (reconstruireALArrivee) VRBrulure.ReconstruireALaProchaineScene();
                yield return new WaitForSeconds(dureeBrulure);
            }
            else
            {
                yield return new WaitForSeconds(delaiAvantScene);
            }
            if (!peutCharger)
            {
                Debug.LogError("[Progression] La scène « " + sceneSuivante + " » n'est pas dans les Build Settings.", this);
                yield break;
            }
            SceneManager.LoadSceneAsync(sceneSuivante);
        }

        /// <summary>D'où part la brûlure : le centre de la porte (sa zone de prise), sinon devant le joueur.</summary>
        private Vector3 FoyerBrulure()
        {
            if (porte != null && porte.zonePrise != null) return porte.zonePrise.bounds.center;
            if (porte != null) return porte.transform.position + Vector3.up * 1.2f;
            Camera cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward : Vector3.up * 1.2f;
        }

        private void CreerPanneau()
        {
            GameObject go = new GameObject("Panneau_Etapes");
            go.transform.SetParent(transform, false);
            _panneau = go.AddComponent<TextMeshPro>();
            _panneau.enableAutoSizing = true;
            _panneau.fontSizeMin = 0.01f;
            _panneau.fontSizeMax = 10f;
            _panneau.alignment = TextAlignmentOptions.Center;
            _panneau.textWrappingMode = TextWrappingModes.NoWrap;
            _panneau.rectTransform.sizeDelta = new Vector2(0.9f, 0.45f);
        }
    }
}
