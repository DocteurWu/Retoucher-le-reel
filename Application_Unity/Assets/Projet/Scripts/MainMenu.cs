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

        public void RefreshLocomotionLabel()
        {
            string label = locomotionPrefix + VRLocomotionSettings.Label();
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
            LocomotionMode mode = VRLocomotionSettings.Cycle();
            Debug.Log("[MainMenu] Mode de locomotion sélectionné : " + VRLocomotionSettings.Label(mode));
            RefreshLocomotionLabel();
        }

        // =====================================================================
        // RAYONS DE VISÉE VR
        // =====================================================================

        private void SetupPointers()
        {
            if (!Application.isPlaying) return;

            _hands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
            _wasPressed = new bool[_hands.Length];
            _rayLines = new LineRenderer[_hands.Length];

            for (int i = 0; i < _hands.Length; i++)
            {
                GameObject rayGo = new GameObject("MenuRay_" + _hands[i].handNode);
                rayGo.transform.SetParent(transform, false);

                LineRenderer line = rayGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.startWidth = 0.008f;
                line.endWidth = 0.003f;
                line.positionCount = 0;
                line.enabled = false;

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                Material mat = new Material(unlit);
                // Le material reste BLANC : la couleur du laser est portée par
                // les couleurs de sommets du LineRenderer (voir ShowPointer),
                // ce qui permet de changer la couleur au survol.
                mat.color = Color.white;
                line.material = mat;
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

            if (_hands == null || _hands.Length == 0) SetupPointers();
            if (_widgets.Count == 0) return;

            // Réinitialiser les couleurs de repos au début de chaque frame
            for (int i = 0; i < _widgets.Count; i++)
            {
                SetWidgetColor(_widgets[i], false);
            }

            bool controllerHover = false;

            // 1. Visée et clic par les manettes VR
            for (int i = 0; i < _hands.Length; i++)
            {
                if (_hands[i] == null) continue;

                Transform hand = _hands[i].transform;
                Widget hitWidget = RaycastWidgets(hand.position, hand.forward, out RaycastHit hit);

                // ⚠️ LE LASER EST TOUJOURS AFFICHÉ (confort) : il part de la
                // manette et va jusqu'au bouton visé, ou jusqu'à rayMaxDistance
                // si aucun bouton n'est visé. AVANT, le laser n'apparaissait que
                // lorsqu'un bouton était survolé : il semblait « s'allumer par
                // intermittence » et le joueur n'avait aucun repère de visée
                // quand il approchait un bouton.
                bool hovering = hitWidget != null;
                Vector3 start = hand.position + hand.forward * laserStartOffset;
                Vector3 end = hovering
                    ? hit.point
                    : hand.position + hand.forward * rayMaxDistance;

                ShowPointer(i, start, end, hovering);

                if (hovering)
                {
                    controllerHover = true;
                    SetWidgetColor(hitWidget, true);
                }

                // Clic : uniquement si un bouton est visé (front montant).
                bool pressed = ReadPress(_hands[i].handNode);
                if (pressed && !_wasPressed[i] && hovering)
                {
                    hitWidget.OnActivate?.Invoke();
                }
                _wasPressed[i] = pressed;
            }

            // 2. Repli souris dans l'éditeur
            if (!controllerHover && !AnyControllerPresent() && Camera.main != null && Mouse.current != null)
            {
                HandleMousePointer();
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
            line.enabled = true;

            Color c = hovering ? pointerHoverColor : pointerColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, hovering ? 0.4f : 0.15f);
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
            Debug.Log("[MainMenu] Lancement de la scène : " + gameSceneName);
            SceneManager.LoadScene(gameSceneName);
        }

        // =====================================================================
        // LECTURE DU BOUTON DE CLIC (trigger / grip)
        // =====================================================================

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

            var charDevices = new List<XRInputDevice>();
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