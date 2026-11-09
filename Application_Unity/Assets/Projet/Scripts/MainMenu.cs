// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MainMenu.cs — MENU D'ENTRÉE (canvas world-space, piloté au laser VR)
//
//  RÔLE
//    • Un laser part de CHAQUE manette et va jusqu'au bouton visé (ou à
//      rayMaxDistance si aucun bouton n'est visé) : le laser est TOUJOURS
//      allumé, il change de couleur quand un bouton est survolé. Avant, il
//      n'apparaissait qu'au survol d'un bouton : le joueur n'avait aucun repère
//      de visée et croyait le « pointeur » intermittent.
//    • Un clic (gâchette ou poignée) valide le bouton visé (front montant).
//      Mains : signe du pistolet pour viser, puis « tirer » (pouce rabattu sur
//      l'index) pour valider.
//    • Sol invisible créé au lancement si la scène n'en a pas (le splat n'a pas
//      de collider) : téléportation et escalade marchent aussi dans le menu.
//    • Bouton « Jouer » coupé en deux au lancement : « Jeu de base » (gameSceneName) à
//      gauche, « Jeu de mémoire » (memorySceneName, Scene3) à droite, copie du bouton.
//    • Bouton du mode : bascule Continu ↔ Téléportation (VRLocomotionSettings).
//    • Bouton « Tutoriel » : lance / arrête le tutoriel complet des gestes, ici, dans le
//      menu (TutorielMenu). Créé au lancement en copiant le bouton du mode, au-dessus de lui.
//
//  À POSER SUR : le Canvas du menu. Câbler locomotionButton, playButton et les
//  textes (TMP ou Text).
//
//  ⚠️ Repli souris/clavier dans l'éditeur, uniquement si AUCUN casque n'est
//     détecté (VRDeviceState.EditorFallbackAllowed).
//
//  RÉFÉRENCES : voir README.md (sections 3 et 4)
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using InputDevice = UnityEngine.InputSystem.InputDevice;
using RemodelerLeReel.VR;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.UI
{
    /// <summary>
    /// MENU D'ENTRÉE — Canvas VR interactif.
    ///
    /// Offre au joueur :
    ///   - Le titre du projet (« Retoucher le réel »),
    ///   - Un bouton pour basculer le mode de locomotion (« Mouvement Continu » vs « Téléportation »),
    ///   - Un bouton « Jouer » pour lancer la scène de jeu (Scene.de.base1),
    ///   - Le rappel complet des contrôles du jeu.
    ///
    /// Interactions gérées :
    ///   - Pointeur laser VR depuis chaque manette (LineRenderer),
    ///   - Clic à la gâchette ou au grip en VR,
    ///   - Repli à la souris dans l'éditeur (clic gauche).
    /// </summary>
    [AddComponentMenu("Remodeler le Reel/UI/Main Menu (Menu d'entree)")]
    public class MainMenu : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // SCÈNE DE JEU
        // ---------------------------------------------------------------------

        [Header("--- Scène de jeu ---")]
        [Tooltip("Nom exact de la scène chargée par le bouton Jouer (sans .unity)")]
        public string gameSceneName = "Scene.de.base1";

        [Tooltip("Scène du jeu de mémoire, lancée par la moitié droite du bouton Jouer. Vide = bouton Jouer entier.")]
        public string memorySceneName = "Scene3";

        // ---------------------------------------------------------------------
        // WIDGETS (Boutons et Textes)
        // ---------------------------------------------------------------------

        [Header("--- Widgets ---")]
        [Tooltip("Transform du bouton de locomotion (avec Collider)")]
        public Transform locomotionButton;

        [Tooltip("Libellé TextMeshPro du mode de locomotion")]
        public TextMeshProUGUI locomotionLabelTMP;

        [Tooltip("Libellé TextMesh classique (fallback)")]
        public TextMesh locomotionLabelLegacy;

        [Tooltip("Transform du bouton Jouer (avec Collider)")]
        public Transform playButton;

        [Tooltip("Libellé TextMeshPro du bouton Jouer")]
        public TextMeshProUGUI playLabelTMP;

        [Tooltip("Libellé TextMesh classique du bouton Jouer (fallback)")]
        public TextMesh playLabelLegacy;

        // ---------------------------------------------------------------------
        // COULEURS
        // ---------------------------------------------------------------------

        [Header("--- Couleurs Bouton Locomotion ---")]
        public Color locomotionNormalColor = new Color(0.18f, 0.45f, 0.85f);
        public Color locomotionHoverColor = new Color(0.30f, 0.65f, 1.0f);

        [Header("--- Couleurs Bouton Jouer ---")]
        public Color playNormalColor = new Color(0.14f, 0.65f, 0.38f);
        public Color playHoverColor = new Color(0.22f, 0.82f, 0.48f);

        // ---------------------------------------------------------------------
        // INTERACTION
        // ---------------------------------------------------------------------

        [Header("--- Interaction ---")]
        public float rayMaxDistance = 6f;
        [Range(0f, 1f)] public float pressThreshold = 0.5f;

        [Tooltip("Mains : valider en « tirant » (pouce rabattu sur l'index tendu). Le bouton validé est "
                 + "celui visé juste avant le tir : rabattre le pouce fait légèrement dévier l'index.")]
        public bool handShootClick = true;

        [Tooltip("Mains : valider aussi en gardant la visée sur un bouton pendant dwellClickTime (ancien mode).")]
        public bool handDwellClick = false;

        [Tooltip("Durée de visée continue (s) sur un bouton pour le valider (si handDwellClick).")]
        [Range(0.5f, 8f)] public float dwellClickTime = 5f;

        [Header("--- Sol du menu ---")]
        [Tooltip("Créer un sol invisible sous le joueur si la scène n'en a pas (téléportation / escalade).")]
        public bool createInvisibleFloor = true;

        [Tooltip("Côté du sol invisible (m) : borne aussi la zone où le joueur peut aller.")]
        [Range(2f, 40f)] public float invisibleFloorSize = 24f;

        [Tooltip("Couleur du laser et du bouton quand la validation par maintien est presque atteinte")]
        public Color dwellCompleteColor = new Color(0.3f, 1f, 0.45f, 1f);
        public string locomotionPrefix = "Mode : ";

        [Header("--- Visuel (laser) ---")]
        [Tooltip("Couleur du laser (hors survol d'un bouton)")]
        public Color pointerColor = new Color(0.2f, 0.8f, 1f, 0.85f);
        [Tooltip("Couleur du laser quand un bouton est visé")]
        public Color pointerHoverColor = new Color(1f, 0.85f, 0.25f, 1f);
        [Tooltip("Décalage du départ du laser devant la manette (m), pour ne pas être caché par le modèle")]
        public float laserStartOffset = 0.05f;

        // --- État interne ---

        private readonly List<Widget> _widgets = new List<Widget>(2);
        private VRGrabHand[] _hands;
        private LineRenderer[] _rayLines;
        private bool[] _wasPressed;
        private bool _loading;

        // Tir « pistolet » des mains : bouton visé récemment (verrou de visée).
        private bool[] _wasShooting;
        private Widget[] _recentHover;
        private float[] _recentHoverTime;
        /// <summary>Le tir valide le bouton visé dans ce délai (s) avant le rabattement du pouce.</summary>
        private const float ShootAimMemory = 0.3f;

        // Validation par maintien de la visée (dwell)
        private Widget _dwellWidget;
        private float _dwellTime;
        private float _dwellLastSeen;
        private bool _dwellConsumed;
        /// <summary>Perte de visée tolérée (s) avant de remettre le compteur à zéro (tremblements du tracking).</summary>
        private const float DwellGrace = 0.35f;

        private sealed class Widget
        {
            public Transform Transform;
            public Collider Collider;
            public Image Image;
            public Renderer Renderer;
            public Action OnActivate;
            public Color NormalColor;
            public Color HoverColor;
        }

        private void Awake()
        {
            BuildWidgets();
        }

        private void Start()
        {
            RefreshLocomotionLabel();
            SetupPointers();
            if (createInvisibleFloor) CreateInvisibleFloorIfNeeded();
        }

        /// <summary>
        /// Le menu n'a qu'un Gaussian Splat, sans collider : l'arc de téléportation ne
        /// trouve aucune cible et l'escalade se croit au bord du monde. On pose donc un
        /// sol invisible (calque Ground, le seul visé par la téléportation) sous le rig,
        /// seulement s'il n'y a déjà rien sous les pieds.
        /// </summary>
        private void CreateInvisibleFloorIfNeeded()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform space = cam.transform.parent;
            Transform rig = space != null && space.parent != null ? space.parent : space;
            if (rig == null) return;

            Vector3 feet = rig.position;
            if (Physics.Raycast(feet + Vector3.up, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)) return;

            GameObject floor = new GameObject("Sol_Invisible_Menu");
            int ground = LayerMask.NameToLayer("Ground");
            floor.layer = ground >= 0 ? ground : 6;
            floor.transform.position = feet - Vector3.up * 0.05f;
            BoxCollider box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(invisibleFloorSize, 0.1f, invisibleFloorSize);

            // 4 Murs de sécurité autour du sol pour empêcher de se téléporter en dehors
            int env = LayerMask.NameToLayer("Environment");
            int wallLayer = env >= 0 ? env : 0;
            float half = invisibleFloorSize * 0.5f;
            float wallH = 6f;
            float wallThick = 0.3f;
            CreerMurMenu(floor.transform, "Mur_Ouest_Menu", wallLayer, new Vector3(feet.x - half - wallThick * 0.5f, wallH * 0.5f, feet.z), new Vector3(wallThick, wallH, invisibleFloorSize + wallThick * 2f));
            CreerMurMenu(floor.transform, "Mur_Est_Menu", wallLayer, new Vector3(feet.x + half + wallThick * 0.5f, wallH * 0.5f, feet.z), new Vector3(wallThick, wallH, invisibleFloorSize + wallThick * 2f));
            CreerMurMenu(floor.transform, "Mur_Sud_Menu", wallLayer, new Vector3(feet.x, wallH * 0.5f, feet.z - half - wallThick * 0.5f), new Vector3(invisibleFloorSize + wallThick * 2f, wallH, wallThick));
            CreerMurMenu(floor.transform, "Mur_Nord_Menu", wallLayer, new Vector3(feet.x, wallH * 0.5f, feet.z + half + wallThick * 0.5f), new Vector3(invisibleFloorSize + wallThick * 2f, wallH, wallThick));

            Physics.SyncTransforms(); // autoSyncTransforms = 0 : les colliders doivent exister dès cette frame
            Debug.Log("[MainMenu] Sol et murs invisibles créés (" + invisibleFloorSize + " m) : téléportation confinée dans le menu.");
        }

        private static void CreerMurMenu(Transform parent, string nom, int layer, Vector3 position, Vector3 taille)
        {
            GameObject wall = new GameObject(nom);
            wall.layer = layer;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = taille;
        }

        private void BuildWidgets()
        {
            _widgets.Clear();
            RegisterWidget(locomotionButton, ToggleLocomotionMode, locomotionNormalColor, locomotionHoverColor);
            RegisterWidget(playButton, ActivatePlay, playNormalColor, playHoverColor);
            DiviserBoutonJouer();
            CreerBoutonTutoriel();
        }

        // =====================================================================
        // JEU DE BASE / JEU DE MÉMOIRE
        // =====================================================================

        [Header("--- Bouton Jeu de mémoire ---")]
        public Color memoireNormalColor = new Color(0.75f, 0.45f, 0.15f);
        public Color memoireHoverColor = new Color(0.95f, 0.62f, 0.25f);

        /// <summary>Écart entre les deux moitiés du bouton Jouer, en unités du canvas.</summary>
        private const float EcartBoutonsJeu = 20f;

        /// <summary>
        /// Coupe le bouton Jouer en deux : la moitié gauche garde son rôle (« Jeu de base »),
        /// une copie à droite lance le jeu de mémoire. Fait au lancement : la scène Menu
        /// n'a pas à être retouchée.
        /// </summary>
                // [Louai H.] Partitionnement dynamique du point d entree menu principal
        private void DiviserBoutonJouer()
        {
            if (string.IsNullOrWhiteSpace(memorySceneName) || playButton == null || !Application.isPlaying) return;
            if (!(playButton is RectTransform gauche))
            {
                Debug.LogWarning("[MainMenu] Bouton Jouer sans RectTransform : pas de bouton « Jeu de mémoire ».", this);
                return;
            }

            GameObject copie = Instantiate(playButton.gameObject, playButton.parent);
            copie.name = "Bouton_JeuMemoire";
            RectTransform droite = (RectTransform)copie.transform;

            Vector2 taille = gauche.sizeDelta;
            float moitie = (taille.x - EcartBoutonsJeu) * 0.5f;
            float decalage = (moitie + EcartBoutonsJeu) * 0.5f;
            Vector2 centre = gauche.anchoredPosition;
            foreach (RectTransform r in new[] { gauche, droite })
            {
                r.sizeDelta = new Vector2(moitie, taille.y);
                BoxCollider boite = r.GetComponent<BoxCollider>();
                if (boite != null) boite.size = new Vector3(moitie, taille.y, boite.size.z);
            }
            gauche.anchoredPosition = centre - new Vector2(decalage, 0f);
            droite.anchoredPosition = centre + new Vector2(decalage, 0f);

            Libeller(playButton, "Jeu de base");
            Libeller(droite, "Jeu de mémoire");
            RegisterWidget(droite, () => ChargerScene(memorySceneName), memoireNormalColor, memoireHoverColor);
        }

        /// <summary>Texte d'un bouton (TMP ou TextMesh), réduit si besoin pour tenir dans la moitié.</summary>
        private static void Libeller(Transform bouton, string texte)
        {
            TextMeshProUGUI tmp = bouton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = texte;
                tmp.textWrappingMode = TextWrappingModes.NoWrap; // une ligne : l'auto-taille réduit la police
                tmp.fontSizeMax = tmp.fontSize;
                tmp.fontSizeMin = Mathf.Min(tmp.fontSize, 12f);
                tmp.enableAutoSizing = true;
                return;
            }
            TextMesh legacy = bouton.GetComponentInChildren<TextMesh>(true);
            if (legacy != null) legacy.text = texte;
        }

        // =====================================================================
        // TUTORIEL DES GESTES
        // =====================================================================

        [Header("--- Bouton Tutoriel ---")]
        [Tooltip("Créer le bouton « Tutoriel » au-dessus du bouton du mode (tutoriel des gestes dans le menu).")]
        public bool boutonTutoriel = true;
        public Color tutorielNormalColor = new Color(0.45f, 0.3f, 0.75f);
        public Color tutorielHoverColor = new Color(0.62f, 0.45f, 0.95f);

        /// <summary>Place du bouton, en unités du canvas : entre le titre (y = 210) et le bouton du mode (y = 25).</summary>
        private static readonly Vector2 PositionBoutonTutoriel = new Vector2(0f, 120f), TailleBoutonTutoriel = new Vector2(560f, 70f);

        private TutorielMenu _tutoriel;
        private TextMeshProUGUI _libelleTutoriel;

        /// <summary>Vrai pendant le déroulement du tutoriel des gestes.</summary>
        public bool TutorielEnCours => (_tutoriel != null && _tutoriel.EnCours) || TutorielGestes.EstEnCours;

        private void CreerBoutonTutoriel()
        {
            if (!boutonTutoriel || locomotionButton == null || !Application.isPlaying) return;

            GameObject go = Instantiate(locomotionButton.gameObject, locomotionButton.parent);
            go.name = "Bouton_Tutoriel";
            if (go.transform is RectTransform rect)
            {
                rect.anchoredPosition = PositionBoutonTutoriel;
                rect.sizeDelta = TailleBoutonTutoriel;
            }
            BoxCollider boite = go.GetComponent<BoxCollider>();
            if (boite != null) boite.size = new Vector3(TailleBoutonTutoriel.x, TailleBoutonTutoriel.y, boite.size.z);

            _libelleTutoriel = go.GetComponentInChildren<TextMeshProUGUI>(true);
            _tutoriel = gameObject.AddComponent<TutorielMenu>();
            _tutoriel.Change += AfficherBoutonTutoriel;
            RegisterWidget(go.transform, () => _tutoriel.Basculer(), tutorielNormalColor, tutorielHoverColor);
            if (_widgets.Count > 0 && _widgets[_widgets.Count - 1].Transform == go.transform) _widgetTutoriel = _widgets[_widgets.Count - 1];
            AfficherBoutonTutoriel();
        }

        /// <summary>
        /// Validation d'un bouton : l'entrée (« tir » du pouce ou gâchette) vient d'être enfoncée
        /// (front montant : enfoncée maintenant, pas à l'image d'avant) ET le bouton est visé.
        /// Une entrée restée enfoncée ne revalide jamais ; d'où <c>dejaEnfoncee = vrai</c> au
        /// lancement du tutoriel, pour que le geste qui l'a lancé ne l'arrête pas aussitôt.
        /// Calcul pur, testé en EditMode.
        /// </summary>
        public static bool ValideAuFrontMontant(bool enfonceeMaintenant, bool dejaEnfoncee, bool boutonVise)
        {
            return enfonceeMaintenant && !dejaEnfoncee && boutonVise;
        }

        /// <summary>Le bouton « Tutoriel des gestes » / « Arrêter le tutoriel » (seul visable pendant le tutoriel).</summary>
        private Widget _widgetTutoriel;

        /// <summary>Fronts de la gâchette des manettes pendant le tutoriel (séparés de ceux du contact direct).</summary>
        private bool[] _gachetteTutoriel;

        /// <summary>
        /// Pendant le tutoriel : visée (signe du pistolet) et validation (« tir » du pouce, ou
        /// gâchette) du seul bouton du tutoriel, pour pouvoir l'arrêter depuis le casque. Le
        /// laser n'apparaît que s'il touche ce bouton.
        /// </summary>
        private void ViserBoutonTutoriel()
        {
            if (_hands == null || _widgetTutoriel == null || _widgetTutoriel.Collider == null) return;
            if (_gachetteTutoriel == null || _gachetteTutoriel.Length != _hands.Length)
            {
                _gachetteTutoriel = new bool[_hands.Length];
                for (int i = 0; i < _gachetteTutoriel.Length; i++) _gachetteTutoriel[i] = true; // relâcher d'abord
            }

            for (int i = 0; i < _hands.Length; i++)
            {
                if (_hands[i] == null) continue;
                XRNode node = _hands[i].handNode;
                bool pistolet = TryGetHandFingerGun(node, out Vector3 origine, out Vector3 direction, out bool tir);
                RaycastHit impact = default;
                bool vise = pistolet && _widgetTutoriel.Collider.Raycast(new Ray(origine, direction), out impact, rayMaxDistance);

                if (vise)
                {
                    ShowPointer(i, origine, impact.point, true);
                    SetWidgetColor(_widgetTutoriel, true);
                    _recentHover[i] = _widgetTutoriel;
                    _recentHoverTime[i] = Time.time;
                }
                else
                {
                    HidePointer(i);
                }

                bool suivie = VRHandGestures.IsTracked(node);
                bool declenche;
                if (suivie)
                {
                    // Rabattre le pouce fait dévier l'index : le bouton visé juste avant compte aussi.
                    bool viseRecemment = vise || (_recentHover[i] == _widgetTutoriel && Time.time - _recentHoverTime[i] <= ShootAimMemory);
                    declenche = handShootClick && ValideAuFrontMontant(pistolet && tir, _wasShooting[i], viseRecemment);
                    _wasShooting[i] = pistolet && tir;
                }
                else
                {
                    bool gachette = ReadPress(node);
                    declenche = ValideAuFrontMontant(gachette, _gachetteTutoriel[i], vise);
                    _gachetteTutoriel[i] = gachette;
                }

                if (declenche)
                {
                    // Le tutoriel s'arrête : la boucle normale reprend à l'image suivante avec la
                    // gâchette encore enfoncée, qui ne doit pas relancer le tutoriel aussitôt.
                    _wasPressed[i] = true;
                    _widgetTutoriel.OnActivate?.Invoke();
                    VRSons.Jouer(VRSon.Saisie, origine, 0.8f, 1.4f);
                    return; // le tutoriel vient de s'arrêter : une seule validation par image
                }
            }
        }

        private void AfficherBoutonTutoriel()
        {
            if (_libelleTutoriel != null) _libelleTutoriel.text = _tutoriel != null && _tutoriel.EnCours ? "Arrêter le tutoriel" : "Tutoriel des gestes";
            if (TutorielEnCours)
            {
                MasquerTousLesLasers();
            }
        }

        private void RegisterWidget(Transform widgetTransform, Action onActivate, Color normalColor, Color hoverColor)
        {
            if (widgetTransform == null) return;

            Collider col = widgetTransform.GetComponent<Collider>() ?? widgetTransform.GetComponentInChildren<Collider>();
            if (col == null)
            {
                Debug.LogWarning("[MainMenu] Le widget '" + widgetTransform.name + "' n'a aucun Collider : il ne sera pas cliquable.", this);
                return;
            }

            Image img = widgetTransform.GetComponent<Image>() ?? widgetTransform.GetComponentInChildren<Image>();
            Renderer rend = widgetTransform.GetComponent<Renderer>() ?? widgetTransform.GetComponentInChildren<Renderer>();

            var widget = new Widget
            {
                Transform = widgetTransform,
                Collider = col,
                Image = img,
                Renderer = rend,
                OnActivate = onActivate,
                NormalColor = normalColor,
                HoverColor = hoverColor
            };

            SetWidgetColor(widget, false);
            _widgets.Add(widget);
        }

        // =====================================================================
        // AFFICHAGE DU MODE
        // =====================================================================

        /// <summary>
        /// Le bouton de mode affiche (et change) le réglage de la modalité EN COURS :
        /// manettes → Téléportation / Mouvement continu ; mains → Téléportation / Escalade.
        /// </summary>
        public void RefreshLocomotionLabel()
        {
            _labelShowsHandMode = VRModalityManager.IsHandMode;
            string label = locomotionPrefix + (_labelShowsHandMode
                ? VRLocomotionSettings.HandLabel(VRLocomotionSettings.HandMode)
                : VRLocomotionSettings.Label());
            if (locomotionLabelTMP != null)
            {
                locomotionLabelTMP.text = label;
            }
            if (locomotionLabelLegacy != null)
            {
                locomotionLabelLegacy.text = label;
            }
        }

                // [LH-Loco] Bascule a chaud du mode de deplacement VR
        private void ToggleLocomotionMode()
        {
            if (VRModalityManager.IsHandMode)
            {
                HandLocomotionMode handMode = VRLocomotionSettings.CycleHand();
                Debug.Log("[MainMenu] Mode de déplacement mains sélectionné : " + VRLocomotionSettings.HandLabel(handMode));
            }
            else
            {
                LocomotionMode mode = VRLocomotionSettings.Cycle();
                Debug.Log("[MainMenu] Mode de locomotion sélectionné : " + VRLocomotionSettings.Label(mode));
            }
            RefreshLocomotionLabel();
        }

        /// <summary>Modalité affichée par le bouton de mode (pour le rafraîchir si on pose / reprend les manettes).</summary>
        private bool _labelShowsHandMode;

        // =====================================================================
        // RAYONS DE VISÉE VR
        // =====================================================================

                // [LH] Initialisation des lasers de pointage UI world-space
        private void SetupPointers()
        {
            if (!Application.isPlaying) return;

            // Registre tenu par VRGrabHand (OnEnable/OnDisable) : pas de parcours de scène.
            _hands = VRGrabHand.AllHands.ToArray();
            _wasPressed = new bool[_hands.Length];
            _wasShooting = new bool[_hands.Length];
            _recentHover = new Widget[_hands.Length];
            _recentHoverTime = new float[_hands.Length];
            _rayLines = new LineRenderer[_hands.Length];

            for (int i = 0; i < _hands.Length; i++)
            {
                GameObject rayGo = new GameObject("MenuRay_" + _hands[i].handNode);
                rayGo.transform.SetParent(transform, false);

                LineRenderer line = rayGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.startWidth = 0.005f;
                line.endWidth = 0.002f;
                line.positionCount = 0;
                line.enabled = false;

                line.material = VRUtils.CreateUnlitMaterial(pointerColor);
                line.startColor = pointerColor;
                line.endColor = new Color(pointerColor.r, pointerColor.g, pointerColor.b, 0.15f);

                _rayLines[i] = line;
            }
        }

        // =====================================================================
        // BOUCLE D'INTERACTION
        // =====================================================================

        private void Update()
        {
            if (!Application.isPlaying || _loading) return;

            // Manettes posées ou reprises : le bouton de mode change de réglage.
            if (_labelShowsHandMode != VRModalityManager.IsHandMode) RefreshLocomotionLabel();

            if ((_hands == null || _hands.Length == 0) && VRGrabHand.AllHands.Count > 0) SetupPointers();
            if (_widgets.Count == 0) return;

            // Réinitialiser les couleurs de repos au début de chaque frame
            for (int i = 0; i < _widgets.Count; i++)
            {
                SetWidgetColor(_widgets[i], false);
            }

            // Pendant le tutoriel, le laser ne vise QUE le bouton « Arrêter le tutoriel » et n'est
            // dessiné que s'il le touche : les gestes appris ne font pas surgir de laser, et les
            // autres boutons ne peuvent pas être déclenchés par accident.
            // Pas de MasquerTousLesLasers ici (il est appelé une fois, au lancement du tutoriel) :
            // appelé à chaque image, il remettait à zéro _wasPressed, et un doigt posé sur le
            // bouton du tutoriel le redéclenchait à chaque image (arrêter, relancer, arrêter…).
            if (TutorielEnCours)
            {
                ViserBoutonTutoriel();
                VerifierContactDirect();
                if (!AnyControllerPresent() && Camera.main != null && Mouse.current != null)
                {
                    HandleMousePointer();
                }
                return;
            }

            bool controllerHover = false;
            Widget dwellCandidate = null;
            int dwellHand = -1;

            // 1. Visée et clic par les mains VR ou manettes VR
            for (int i = 0; i < _hands.Length; i++)
            {
                if (_hands[i] == null) continue;

                XRNode node = _hands[i].handNode;
                Vector3 rayOriginPos;
                Vector3 rayDirection;
                bool isShootPressed = false;

                // 1. Détection Pistolet : exigée pour activer la visée
                // - En Hand Tracking pur : index tendu, autres doigts repliés
                // - En Émulation Manette / Quest Link : doigts serrés (grip > 0.3) et index levé (trigger < 0.25)
                // Si la posture n'est pas adoptée, LE LASER EST TOTALEMENT MASQUÉ.
                bool isGun = TryGetHandFingerGun(node, out Vector3 gunOrigin, out Vector3 gunDir, out bool gunShoot);
                if (isGun)
                {
                    rayOriginPos = gunOrigin;
                    rayDirection = gunDir;
                    isShootPressed = gunShoot;
                }
                else
                {
                    // Pas de posture de visée : masquage propre et immédiat du laser.
                    // _wasShooting est conservé : reprendre la posture pouce déjà
                    // rabattu ne doit pas valider un bouton.
                    HidePointer(i);
                    _wasPressed[i] = false;
                    continue;
                }

                Widget hitWidget = RaycastWidgets(rayOriginPos, rayDirection, out RaycastHit hit);
                bool hovering = hitWidget != null;
                Vector3 start = rayOriginPos;
                Vector3 end = hovering
                    ? hit.point
                    : rayOriginPos + rayDirection * rayMaxDistance;

                ShowPointer(i, start, end, hovering);

                if (hovering)
                {
                    controllerHover = true;
                    SetWidgetColor(hitWidget, true);
                    if (dwellCandidate == null)
                    {
                        dwellCandidate = hitWidget;
                        dwellHand = i;
                    }
                }

                if (hovering)
                {
                    _recentHover[i] = hitWidget;
                    _recentHoverTime[i] = Time.time;
                }

                // Mains : « tirer » (pouce rabattu sur l'index) valide, au front montant.
                // Rabattre le pouce fait dévier l'index : on valide le bouton visé dans
                // les ShootAimMemory dernières secondes, pas forcément celui de cette frame.
                bool handTracked = VRHandGestures.IsTracked(node);
                if (handTracked && handShootClick)
                {
                    if (isShootPressed && !_wasShooting[i])
                    {
                        Widget target = hovering ? hitWidget
                            : (Time.time - _recentHoverTime[i] <= ShootAimMemory ? _recentHover[i] : null);
                        if (target != null)
                        {
                            target.OnActivate?.Invoke();
                            _dwellConsumed = true;
                            VRSons.Jouer(VRSon.Saisie, rayOriginPos, 0.8f, 1.4f);
                        }
                    }
                    _wasShooting[i] = isShootPressed;
                }

                // Manettes : gâchette / grip, front montant.
                bool pressed = !handTracked && ReadPress(node);
                if (pressed && !_wasPressed[i] && hovering)
                {
                    hitWidget.OnActivate?.Invoke();
                    _dwellConsumed = true;
                }
                _wasPressed[i] = pressed;
            }

            // Maintien de la visée : manettes sans gâchette lisible, ou mains si activé.
            bool dwellAllowed = dwellHand >= 0
                && (handDwellClick || !VRHandGestures.IsTracked(_hands[dwellHand].handNode));
            UpdateDwell(dwellAllowed ? dwellCandidate : null, dwellHand);

            // 2. Repli souris dans l'éditeur
            if (!controllerHover && !AnyControllerPresent() && Camera.main != null && Mouse.current != null)
            {
                HandleMousePointer();
            }
        }

        /// <summary>
        /// Validation par maintien : viser le même bouton pendant dwellClickTime le déclenche.
        /// Le laser et le bouton virent progressivement vers dwellCompleteColor pour montrer
        /// la progression. Après une validation, il faut quitter le bouton pour réarmer.
        /// </summary>
        private void UpdateDwell(Widget hovered, int handIndex)
        {
            if (hovered == null)
            {
                if (Time.time - _dwellLastSeen > DwellGrace)
                {
                    _dwellWidget = null;
                    _dwellTime = 0f;
                    _dwellConsumed = false;
                }
                return;
            }

            _dwellLastSeen = Time.time;
            if (hovered != _dwellWidget)
            {
                _dwellWidget = hovered;
                _dwellTime = 0f;
                _dwellConsumed = false;
            }
            if (_dwellConsumed) return;

            _dwellTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_dwellTime / Mathf.Max(0.01f, dwellClickTime));

            Color widgetColor = Color.Lerp(hovered.HoverColor, dwellCompleteColor, progress);
            if (hovered.Image != null) hovered.Image.color = widgetColor;
            else if (hovered.Renderer != null && hovered.Renderer.material != null) hovered.Renderer.material.color = widgetColor;

            if (_rayLines != null && handIndex >= 0 && handIndex < _rayLines.Length && _rayLines[handIndex] != null)
            {
                LineRenderer line = _rayLines[handIndex];
                Color c = Color.Lerp(pointerHoverColor, dwellCompleteColor, progress);
                line.startColor = c;
                line.endColor = new Color(c.r, c.g, c.b, 0.4f + 0.6f * progress);
                line.startWidth = Mathf.Lerp(0.005f, 0.012f, progress);
                if (line.material != null) line.material.color = c;
            }

            if (progress >= 1f)
            {
                _dwellConsumed = true;
                hovered.OnActivate?.Invoke();
            }
        }

        private Widget RaycastWidgets(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
        {
            Ray ray = new Ray(origin, direction);
            Widget best = null;
            float bestDistance = float.MaxValue;
            closestHit = default;

            for (int i = 0; i < _widgets.Count; i++)
            {
                Widget w = _widgets[i];
                if (w.Collider == null) continue;

                if (w.Collider.Raycast(ray, out RaycastHit hit, rayMaxDistance) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = w;
                    closestHit = hit;
                }
            }

            return best;
        }

        /// <summary>
        /// Affiche (ou met à jour) le laser d'une manette.
        ///
        /// Le trait est TOUJOURS visible : il va du point de départ (léger
        /// décalage devant la manette pour ne pas être caché par le modèle
        /// de la manette) jusqu'au bouton visé, ou à rayMaxDistance si aucun
        /// bouton n'est visé. La couleur change quand un bouton est survolé
        /// (jaune) pour donner un retour immédiat.
        /// </summary>
        private void ShowPointer(int index, Vector3 origin, Vector3 target, bool hovering)
        {
            if (_rayLines == null || index >= _rayLines.Length || _rayLines[index] == null) return;

            LineRenderer line = _rayLines[index];
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, target);
            line.startWidth = 0.005f; // UpdateDwell l'élargit ensuite pendant la validation
            line.enabled = true;

            Color c = hovering ? pointerHoverColor : pointerColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, hovering ? 0.4f : 0.15f);
            if (line.material != null)
            {
                line.material.color = c;
            }
        }

        private void HidePointer(int index)
        {
            if (_rayLines != null && index >= 0 && index < _rayLines.Length && _rayLines[index] != null)
            {
                _rayLines[index].enabled = false;
            }
        }

        /// <summary>Masque et désactive immédiatement tous les lasers et réinitialise les états de visée.</summary>
        public void MasquerTousLesLasers()
        {
            if (_rayLines != null)
            {
                for (int i = 0; i < _rayLines.Length; i++)
                {
                    HidePointer(i);
                }
            }
            if (_wasPressed != null)
            {
                for (int i = 0; i < _wasPressed.Length; i++) _wasPressed[i] = false;
            }
            // « Déjà enfoncés » : le pouce (ou la gâchette) qui vient de valider le bouton doit être
            // relâché avant une nouvelle validation, sinon il arrêterait le tutoriel à l'image suivante.
            if (_wasShooting != null)
            {
                for (int i = 0; i < _wasShooting.Length; i++) _wasShooting[i] = true;
            }
            if (_gachetteTutoriel != null)
            {
                for (int i = 0; i < _gachetteTutoriel.Length; i++) _gachetteTutoriel[i] = true;
            }
            if (_recentHover != null)
            {
                for (int i = 0; i < _recentHover.Length; i++) _recentHover[i] = null;
            }
            _dwellWidget = null;
            _dwellTime = 0f;
            _dwellConsumed = false;
        }

        /// <summary>
        /// Permet de valider un bouton en le touchant directement avec la main ou la manette
        /// (utile notamment pendant le tutoriel où le laser est désactivé).
        /// </summary>
        private void VerifierContactDirect()
        {
            if (_hands == null || _widgets.Count == 0) return;

            for (int h = 0; h < _hands.Length; h++)
            {
                if (_hands[h] == null) continue;
                XRNode node = _hands[h].handNode;
                Vector3 handPos = _hands[h].transform.position;
                HandBones bones = GetHandBones(node);
                if (bones != null && bones.Visual != null && bones.Visual.activeInHierarchy && bones.IndexTip != null)
                {
                    handPos = bones.IndexTip.position;
                }

                bool nearAnyWidget = false;
                for (int w = 0; w < _widgets.Count; w++)
                {
                    Widget widget = _widgets[w];
                    if (widget.Collider == null) continue;

                    Vector3 closest = widget.Collider.ClosestPoint(handPos);
                    float dist = Vector3.Distance(handPos, closest);

                    if (dist < 0.04f)
                    {
                        nearAnyWidget = true;
                        SetWidgetColor(widget, true);
                        bool isTracked = VRHandGestures.IsTracked(node);
                        bool pressed = (!isTracked && ReadPress(node)) || dist < 0.015f;

                        if (pressed)
                        {
                            if (!_wasPressed[h])
                            {
                                _wasPressed[h] = true;
                                widget.OnActivate?.Invoke();
                                VRSons.Jouer(VRSon.Saisie, handPos, 0.8f, 1.4f);
                            }
                        }
                        else
                        {
                            _wasPressed[h] = false;
                        }
                        break;
                    }
                }

                if (!nearAnyWidget)
                {
                    _wasPressed[h] = false;
                }
            }
        }

        private Transform GetHandTransform(XRNode node)
        {
            if (_hands != null)
            {
                for (int i = 0; i < _hands.Length; i++)
                {
                    if (_hands[i] != null && _hands[i].handNode == node)
                        return _hands[i].transform;
                }
            }
            return null;
        }

        /// <summary>
        /// Détecte si la main effectue le "signe du pistolet" (index tendu pour viser,
        /// autres doigts repliés dans la paume).
        /// Compatible avec le Hand Tracking pur ET l'émulation manettes du Meta Quest Link.
        /// </summary>
        private bool TryGetHandFingerGun(XRNode node, out Vector3 origin, out Vector3 direction, out bool isShootPressed)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            isShootPressed = false;

            // Hand Tracking natif : la main suivie fait foi, on ne tombe PAS dans les
            // replis manette (la manette posée sur la table générait un laser fantôme).
            if (VRHandGestures.IsTracked(node))
            {
                return VRHandGestures.TryGetFingerGun(node, out origin, out direction, out isShootPressed);
            }

            // Repli GameObjects d'articulation (os du visuel de main, mis en cache)
            HandBones bones = GetHandBones(node);
            if (bones != null && bones.Visual.activeInHierarchy)
            {
                Transform tip = bones.IndexTip;
                Transform inter = bones.IndexIntermediate;
                Transform palm = bones.Palm;
                Transform midTip = bones.MiddleTip;
                Transform thumbTip = bones.ThumbTip;

                if (tip != null && palm != null)
                {
                    float idxDist = Vector3.Distance(tip.position, palm.position);
                    bool curled = false;
                    if (midTip != null)
                    {
                        float mDist = Vector3.Distance(midTip.position, palm.position);
                        curled = (idxDist > mDist + 0.012f) || (mDist < 0.065f);
                    }

                    if (idxDist > 0.040f && curled)
                    {
                        origin = tip.position;
                        direction = inter != null ? (tip.position - inter.position).normalized : tip.forward;
                        if (thumbTip != null && inter != null && Vector3.Distance(thumbTip.position, inter.position) < 0.050f)
                        {
                            isShootPressed = true;
                        }
                        return true;
                    }
                }
            }

            // Repli Émulation Manette / Quest Link (manettes simulées suivant les mains)
            XRInputDevice dev = VRStickReader.GetDevice(node);
            if (dev.isValid)
            {
                dev.TryGetFeatureValue(CommonUsages.grip, out float grip);
                dev.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
                dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool btnPrimary);
                dev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool btnSecondary);

                // Posture pistolet : doigts repliés (grip serré) et index relâché (gâchette libre)
                if (grip > 0.30f && trigger < 0.25f)
                {
                    var handTrans = GetHandTransform(node);
                    if (handTrans != null)
                    {
                        origin = handTrans.position + handTrans.forward * laserStartOffset;
                        direction = handTrans.forward;
                        isShootPressed = btnPrimary || btnSecondary;
                        return true;
                    }
                }
                // Moment du tir (pincement / gâchette enfoncée pendant le maintien)
                else if (grip > 0.30f && trigger > 0.45f)
                {
                    var handTrans = GetHandTransform(node);
                    if (handTrans != null)
                    {
                        origin = handTrans.position + handTrans.forward * laserStartOffset;
                        direction = handTrans.forward;
                        isShootPressed = true;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Os utiles d'un visuel de main, résolus une seule fois.</summary>
        private sealed class HandBones
        {
            public GameObject Visual;
            public Transform IndexTip, IndexIntermediate, Palm, MiddleTip, ThumbTip;
        }

        private readonly HandBones[] _handBones = new HandBones[2];
        private float _nextHandBonesLookup;

        /// <summary>
        /// Os du visuel de main (« Left/Right Hand Interaction Visual »). Les os sont
        /// IMBRIQUÉS dans le prefab : recherche récursive, puis cache. Tant que le
        /// visuel est introuvable, on ne retente qu'une fois par seconde
        /// (GameObject.Find parcourt toute la scène).
        /// </summary>
        private HandBones GetHandBones(XRNode node)
        {
            int idx = node == XRNode.LeftHand ? 0 : 1;
            HandBones cached = _handBones[idx];
            if (cached != null && cached.Visual != null) return cached;
            if (Time.unscaledTime < _nextHandBonesLookup) return null;

            string side = idx == 0 ? "Left" : "Right";
            string pfx = idx == 0 ? "L_" : "R_";
            GameObject visual = GameObject.Find(side + " Hand Interaction Visual");
            if (visual == null)
            {
                _nextHandBonesLookup = Time.unscaledTime + 1f;
                return null;
            }

            Transform root = visual.transform;
            cached = new HandBones
            {
                Visual = visual,
                IndexTip = VRUtils.FindDeep(root, pfx + "IndexTip"),
                IndexIntermediate = VRUtils.FindDeep(root, pfx + "IndexIntermediate"),
                Palm = VRUtils.FindDeep(root, pfx + "Palm"),
                MiddleTip = VRUtils.FindDeep(root, pfx + "MiddleTip"),
                ThumbTip = VRUtils.FindDeep(root, pfx + "ThumbTip"),
            };
            _handBones[idx] = cached;
            return cached;
        }

        private static void SetWidgetColor(Widget widget, bool hovered)
        {
            if (widget == null) return;
            Color target = hovered ? widget.HoverColor : widget.NormalColor;

            if (widget.Image != null)
            {
                if (widget.Image.color != target) widget.Image.color = target;
            }
            else if (widget.Renderer != null)
            {
                Material mat = widget.Renderer.material;
                if (mat != null && mat.color != target) mat.color = target;
            }
        }

        private bool AnyControllerPresent()
        {
            if (_hands == null) return false;
            for (int i = 0; i < _hands.Length; i++)
            {
                if (_hands[i] == null) continue;
                if (VRStickReader.GetDevice(_hands[i].handNode).isValid) return true;
            }
            return false;
        }

        private void HandleMousePointer()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            for (int i = 0; i < _widgets.Count; i++)
            {
                Widget w = _widgets[i];
                if (w.Collider == null) continue;

                if (w.Collider.Raycast(ray, out _, rayMaxDistance * 100f))
                {
                    SetWidgetColor(w, true);
                    if (Mouse.current.leftButton.wasPressedThisFrame && w.OnActivate != null)
                    {
                        w.OnActivate();
                    }
                    return;
                }
            }
        }

        // =====================================================================
        // ACTION : JOUER
        // =====================================================================

        private void ActivatePlay() => ChargerScene(gameSceneName);

        /// <summary>Charge une scène de jeu (bouton « Jeu de base » ou « Jeu de mémoire »).</summary>
        private void ChargerScene(string sceneName)
        {
            if (_loading) return;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[MainMenu] Aucune scène de jeu renseignée.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[MainMenu] La scène « " + sceneName + " » est introuvable dans les Build Settings.", this);
                return;
            }

            _loading = true;
            Debug.Log("[MainMenu] Lancement asynchrone de la scène : " + sceneName);
            StartCoroutine(LoadSceneAsyncRoutine(sceneName));
        }

        private System.Collections.IEnumerator LoadSceneAsyncRoutine(string sceneName)
        {
            // Laisser une frame pour rafraîchir l'affichage et éviter tout gel du thread de rendu OpenXR
            yield return null;

            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            asyncOp.allowSceneActivation = true;

            while (!asyncOp.isDone)
            {
                yield return null;
            }
        }

        // =====================================================================
        // LECTURE DU BOUTON DE CLIC (trigger / grip)
        // =====================================================================

        /// <summary>Tampon réutilisé par ReadPress (évite une allocation par frame et par main).</summary>
        private readonly List<XRInputDevice> _charDeviceBuffer = new List<XRInputDevice>();

        private bool ReadPress(XRNode node)
        {
            XRInputDevice device = VRStickReader.GetDevice(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigger) && trigger > pressThreshold) return true;
                if (device.TryGetFeatureValue(CommonUsages.grip, out float grip) && grip > pressThreshold) return true;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn) return true;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn) return true;
            }

            var charDevices = _charDeviceBuffer;
            charDevices.Clear();
            InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                (node == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
            InputDevices.GetDevicesWithCharacteristics(characteristics, charDevices);
            for (int i = 0; i < charDevices.Count; i++)
            {
                if (charDevices[i].TryGetFeatureValue(CommonUsages.trigger, out float t) && t > pressThreshold) return true;
                if (charDevices[i].TryGetFeatureValue(CommonUsages.grip, out float g) && g > pressThreshold) return true;
                if (charDevices[i].TryGetFeatureValue(CommonUsages.triggerButton, out bool tb) && tb) return true;
                if (charDevices[i].TryGetFeatureValue(CommonUsages.gripButton, out bool gb) && gb) return true;
            }

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice dev = devices[i];
                if (!VRStickReader.DeviceMatchesHand(dev, node == XRNode.LeftHand)) continue;

                var trigger = VRInputControls.Find<AxisControl>(dev, "trigger");
                if (trigger != null && trigger.ReadValue() > pressThreshold) return true;

                var grip = VRInputControls.Find<AxisControl>(dev, "grip");
                if (grip != null && grip.ReadValue() > pressThreshold) return true;
            }

            return false;
        }
    }
}