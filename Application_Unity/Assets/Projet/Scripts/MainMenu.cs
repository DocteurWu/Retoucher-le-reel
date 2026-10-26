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
//    • Bouton « Jouer » : charge la scène de jeu (gameSceneName).
//    • Bouton du mode : bascule Continu ↔ Téléportation (VRLocomotionSettings).
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
        [Range(2f, 40f)] public float invisibleFloorSize = 10f;

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
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : le collider doit exister dès cette frame
            Debug.Log("[MainMenu] Sol invisible créé (" + invisibleFloorSize + " m) : téléportation et escalade actives dans le menu.");
        }

        private void BuildWidgets()
        {
            _widgets.Clear();
            RegisterWidget(locomotionButton, ToggleLocomotionMode, locomotionNormalColor, locomotionHoverColor);
            RegisterWidget(playButton, ActivatePlay, playNormalColor, playHoverColor);
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

        private void ActivatePlay()
        {
            if (_loading) return;

            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogWarning("[MainMenu] Aucune scène de jeu renseignée.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError("[MainMenu] La scène « " + gameSceneName + " » est introuvable dans les Build Settings.", this);
                return;
            }

            _loading = true;
            Debug.Log("[MainMenu] Lancement asynchrone de la scène : " + gameSceneName);
            StartCoroutine(LoadSceneAsyncRoutine(gameSceneName));
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