// =====================================================================================
//  VRHandMenu.cs — MENU AU POIGNET GAUCHE AUTONOME (DUPLIQUER / SUPPRIMER)
//
//  RÔLE
//    Menu 3D autonome attaché au poignet gauche du joueur :
//      - Suit le poignet gauche (main suivie en hand tracking ou manette gauche).
//      - S'affiche automatiquement lorsque le joueur regarde son poignet/sa main.
//      - Deux boutons clairs et ergonomiques :
//          * "Dupliquer (X)" (Bleu)
//          * "Supprimer (Y)" (Rouge)
//      - Déclenchable par :
//          * Contact physique (toucher du doigt / poke de l'autre main via VRMenuButtonTouch)
//          * Pointeur / laser UI
//          * Raccourcis physiques manette (X pour dupliquer, Y pour supprimer)
//          * Touche M au clavier (pour tester dans l'éditeur)
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.InputSystem;
using TMPro;

namespace RemodelerLeReel.VR
{
    [AddComponentMenu("Remodeler le Reel/VR/VR Hand Menu (Menu poignet Dupliquer / Supprimer)")]
    public class VRHandMenu : MonoBehaviour
    {
        [Header("--- Cibles & Détection ---")]
        [Tooltip("Transform du poignet/main gauche à suivre (trouvé automatiquement si vide)")]
        public Transform leftHandTarget;

        [Tooltip("Caméra de la tête (trouvée automatiquement si vide)")]
        public Transform headCamera;

        [Tooltip("Composant VRDuplicateDelete (trouvé automatiquement si vide)")]
        public VRDuplicateDelete duplicateDelete;

        [Header("--- Positionnement sur le poignet gauche ---")]
        [Tooltip("Décalage de position par rapport au poignet gauche")]
        public Vector3 positionOffset = new Vector3(0.02f, 0.08f, -0.03f);

        [Tooltip("Orienter le menu face au regard du joueur")]
        public bool faceCamera = true;

        [Tooltip("Ajustement de rotation supplémentaire du menu (degrés)")]
        public Vector3 menuRotationOffset = Vector3.zero;

        [Header("--- Détection du regard & Visibilité ---")]
        [Tooltip("Toujours afficher le menu sans condition d'orientation")]
        public bool alwaysVisible = false;

        [Tooltip("Durée minimale de maintien (secondes) avec la main devant le visage pour ouvrir le menu")]
        [Range(0.5f, 5f)] public float requiredGazeHoldTime = 2.0f;

        [Tooltip("Angle max (degrés) entre le regard et la main gauche pour afficher le menu")]
        [Range(15f, 60f)] public float gazeAngleThreshold = 35f;

        [Tooltip("Distance min entre la tête et la main pour activer le menu (m)")]
        public float minDistance = 0.15f;

        [Tooltip("Distance max entre la tête et la main pour activer le menu (m)")]
        public float maxDistance = 0.65f;

        [Tooltip("Exiger que la main soit levée devant le buste")]
        public bool requireHandRaised = true;

        [Tooltip("Vitesse d'apparition et de disparition")]
        public float animSpeed = 12f;

        [Header("--- Références UI ---")]
        [Tooltip("Racine de l'UI du menu (animée en échelle)")]
        public Transform menuRoot;

        [Tooltip("Bouton Dupliquer")]
        public Button duplicateButton;

        [Tooltip("Bouton Supprimer")]
        public Button deleteButton;

        [Header("--- Retours Sonores & Haptiques ---")]
        [Tooltip("Activer les vibrations haptiques au clic")]
        public bool enableHaptics = true;

        [Tooltip("Son joué lors d'un clic")]
        public AudioClip clickClip;

        [Tooltip("AudioSource pour jouer le son de clic")]
        public AudioSource audioSource;

        [Header("--- Bulle de rangement ---")]
        [Tooltip("Position de la bulle par rapport au menu (m, repère du menu : +Y = au-dessus)")]
        public Vector3 bubbleLocalOffset = new Vector3(0f, 0.11f, 0f);

        [Tooltip("Diamètre de la bulle (m)")]
        public float bubbleDiameter = 0.12f;

        [Tooltip("Taille max (m) de l'objet rangé, réduit pour tenir dans la bulle")]
        public float bubbleStoredSize = 0.08f;

        [Tooltip("Distance max (m) entre l'objet lâché et la bulle pour qu'il y soit rangé")]
        public float bubbleCaptureRadius = 0.15f;

        [Tooltip("Distance (m) entre le bout de l'index droit et un bouton pour l'activer (mode mains)")]
        public float fingerPressDistance = 0.015f;

        [Header("--- Raccourcis doigts (mode mains) ---")]
        [Tooltip("Remplacer les boutons par « toucher un doigt gauche avec l'index droit » : " +
                 "index = Dupliquer, majeur = Supprimer, annulaire = Récupérer")]
        public bool useFingerTaps = true;

        [Tooltip("Distance (m) entre l'index droit et le bout d'un doigt gauche pour déclencher l'action")]
        public float fingerTapDistance = 0.02f;

        [Tooltip("Diamètre (m) des pastilles affichées au bout des doigts gauches")]
        public float fingerMarkerSize = 0.012f;

        private bool FingerTapMode => useFingerTaps && VRModalityManager.IsHandMode;

        // Correspondance doigt gauche → action (0 = Dupliquer, 1 = Supprimer, 2 = Récupérer)
        private static readonly XRHandFingerID[] s_TapFingers = { XRHandFingerID.Index, XRHandFingerID.Middle, XRHandFingerID.Ring };
        private static readonly Color[] s_TapColors =
        {
            new Color(0.20f, 0.65f, 1.00f, 1f), // Dupliquer : bleu (comme l'ancien bouton)
            new Color(0.95f, 0.25f, 0.25f, 1f), // Supprimer : rouge
            new Color(1.00f, 1.00f, 1.00f, 1f), // Récupérer : blanc
        };
        private Transform[] _tapMarkers;
        private int _tapInside = -1;

        private Transform _bubble;
        private Rigidbody _stored;
        private Vector3 _storedLossyScale;
        private readonly Dictionary<VRGrabHand, Rigidbody> _prevHeld = new Dictionary<VRGrabHand, Rigidbody>();
        private int _fingerInside;
        private float _lastFingerPress;

        private Vector3 _initialScale = new Vector3(0.0008f, 0.0008f, 0.0008f);
        private float _currentScaleFactor = 0f;
        private float _gazeHoldTimer = 0f;
        private float _gazeLostTimer = 0f;
        /// <summary>Interruption tolérée (s) de la posture avant de remettre le compteur à zéro.</summary>
        private const float GazeGrace = 0.35f;

        [Tooltip("Affiche dans la Console (1×/s) pourquoi le menu poignet est caché")]
        public bool debugVisibility = false;
        private float _lastVisibilityLog;

        private void LogVisibility(string reason)
        {
            if (!debugVisibility || Time.unscaledTime - _lastVisibilityLog < 1f) return;
            _lastVisibilityLog = Time.unscaledTime;
            Debug.Log($"[VRHandMenu] {reason} | maintien {_gazeHoldTimer:F1}/{requiredGazeHoldTime:F1}s");
        }
        private bool _isMenuDisplayed = false;
        private Canvas _canvas;

        private void Awake()
        {
            EnsureAudioSource();
            EnsureHierarchyAndUI();
            EnsureEventSystem();
            EnsureBubble();
        }

        // =====================================================================
        // BULLE DE RANGEMENT
        //   Lâcher un objet dans la bulle (au-dessus du menu ouvert) le range : il
        //   rétrécit et flotte, la main est libre pour appuyer sur Dupliquer /
        //   Supprimer, qui agissent alors sur lui. Le saisir le ressort à sa taille.
        // =====================================================================

        private void EnsureBubble()
        {
            if (_bubble != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Storage_Bubble";
            Destroy(go.GetComponent<Collider>()); // purement visuelle : ne doit ni bloquer la saisie ni toucher les boutons
            go.transform.SetParent(transform, false);
            go.transform.localPosition = bubbleLocalOffset;
            go.transform.localScale = Vector3.zero;

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = VRUtils.CreateUnlitMaterial(new Color(0.45f, 0.85f, 1f, 0.18f));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bubble = go.transform;
        }

        private void UpdateBubble()
        {
            if (_bubble == null) return;

            // La bulle est visible quand le menu est ouvert, ou tant qu'elle contient un objet.
            float show = _stored != null ? 1f : _currentScaleFactor;
            _bubble.localPosition = bubbleLocalOffset;
            _bubble.localScale = Vector3.one * (bubbleDiameter * show);
            _bubble.gameObject.SetActive(show > 0.02f);
            Vector3 center = _bubble.position;

            // 1. Capture : un objet vient d'être lâché près de la bulle, menu ouvert.
            for (int i = 0; i < VRGrabHand.AllHands.Count; i++)
            {
                VRGrabHand hand = VRGrabHand.AllHands[i];
                if (hand == null) continue;
                Rigidbody now = hand.GrabbedBody;
                _prevHeld.TryGetValue(hand, out Rigidbody prev);
                if (prev != null && now == null && _stored == null && _bubble.gameObject.activeSelf)
                {
                    // Distance à la SURFACE de l'objet (et non à son centre) : un objet un peu
                    // grand lâché « dans » la bulle a souvent son centre hors du rayon.
                    float dist = DistanceToBody(prev, center);
                    if (debugVisibility) Debug.Log($"[VRHandMenu] Objet « {prev.name} » lâché à {dist:F2} m de la bulle (rayon {bubbleCaptureRadius:F2} m)");
                    if (dist < bubbleCaptureRadius) Store(prev);
                }
                _prevHeld[hand] = now;
            }

            UpdateCloseButton();
            UpdateButtonStates();
            if (_stored == null) return;

            // 2. Flottement au centre de la bulle (lente rotation).
            Transform t = _stored.transform;
            t.rotation = Quaternion.AngleAxis(40f * Time.unscaledDeltaTime, Vector3.up) * t.rotation;
            t.position += center - GetBoundsCenter(_stored);
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : le collider doit suivre pour rester saisissable
        }

        private void Store(Rigidbody body)
        {
            _stored = body;
            _storedLossyScale = body.transform.lossyScale;
            // Boîte mémorisée AVANT de couper les colliders (seule mesure d'un splat, sans Renderer).
            _storedHasLocalBounds = false;
            _storedLocalBounds = WorldToLocalBounds(body.transform, GetBounds(body));
            _storedHasLocalBounds = true;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;

            // Colliders coupés tant que l'objet est rangé : il ne gêne rien, ne touche pas les
            // boutons et ne peut pas être ressaisi par erreur (on le récupère avec la croix).
            _storedColliders.Clear();
            foreach (var c in body.GetComponentsInChildren<Collider>())
            {
                if (c.enabled) { _storedColliders.Add(c); c.enabled = false; }
            }

            ShrinkStored();
            PlayFeedback();
            VRSons.Jouer(VRSon.Rangement, body.position);
            ObjetRange?.Invoke(body);
        }

        /// <summary>Un objet vient d'être rangé dans la bulle. Écouté par la narration.</summary>
        public static event System.Action<Rigidbody> ObjetRange;

        /// <summary>L'objet de la bulle vient d'en être sorti. Écouté par la narration.</summary>
        public static event System.Action<Rigidbody> ObjetRecupere;

        /// <summary>Croix : l'objet reprend sa taille et retourne dans la main droite.</summary>
        public void OnReleaseStoredClicked()
        {
            if (_stored == null) return;
            PlayFeedback();

            Rigidbody body = _stored;
            _stored = null;
            VRSons.Jouer(VRSon.Recuperation, body.position);
            ObjetRecupere?.Invoke(body);
            SetLossyScale(body.transform, _storedLossyScale);
            foreach (var c in _storedColliders) if (c != null) c.enabled = true;
            _storedColliders.Clear();

            var right = VRGrabHand.GetHandForNode(XRNode.RightHand);
            if (right != null && !right.IsHoldingObject)
            {
                body.transform.position += right.transform.position - GetBoundsCenter(body);
                Physics.SyncTransforms();
                right.GrabBody(body); // reste en main jusqu'au prochain « fermer puis ouvrir » la main
            }
            else
            {
                body.isKinematic = false;
                body.useGravity = true;
                Physics.SyncTransforms();
            }
        }

        /// <summary>Un objet tenu est-il approché de la bulle (dépôt en cours) ?</summary>
        private bool IsDepositInProgress()
        {
            if (_bubble == null || _stored != null) return false;
            for (int i = 0; i < VRGrabHand.AllHands.Count; i++)
            {
                VRGrabHand hand = VRGrabHand.AllHands[i];
                if (hand != null && hand.GrabbedBody != null
                    && DistanceToBody(hand.GrabbedBody, _bubble.position) < 0.35f)
                    return true;
            }
            return false;
        }

        private float DistanceToBody(Rigidbody body, Vector3 point)
        {
            Bounds b = GetBounds(body);
            return Mathf.Sqrt(b.SqrDistance(point));
        }

        private readonly List<Collider> _storedColliders = new List<Collider>();
        private Bounds _storedLocalBounds;      // boîte de l'objet rangé, dans son repère local
        private bool _storedHasLocalBounds;
        private Transform _closeButton;
        private BoxCollider _closeCollider;
        private readonly Dictionary<Button, Color> _buttonBaseColors = new Dictionary<Button, Color>();

        /// <summary>Mini croix rouge en haut à droite de la bulle, créée par code (aucun asset).</summary>
        private void EnsureCloseButton()
        {
            if (_closeButton != null) return;
            var root = new GameObject("Bubble_Close");
            root.transform.SetParent(transform, false);
            _closeCollider = root.AddComponent<BoxCollider>();
            _closeCollider.isTrigger = true;
            _closeCollider.size = new Vector3(0.04f, 0.04f, 0.03f);

            var red = VRUtils.CreateUnlitMaterial(new Color(0.95f, 0.25f, 0.25f, 1f));
            var back = VRUtils.CreateUnlitMaterial(new Color(0.08f, 0.10f, 0.16f, 0.9f));

            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(0.035f, 0.035f, 1f);
            disc.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = back;

            for (int k = 0; k < 2; k++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(root.transform, false);
                bar.transform.localScale = new Vector3(0.028f, 0.005f, 0.004f);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 45f : -45f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = red;
            }
            _closeButton = root.transform;
            root.SetActive(false);
        }

        private void UpdateCloseButton()
        {
            EnsureCloseButton();
            bool visible = _stored != null && _currentScaleFactor > 0.5f && !FingerTapMode;
            _closeButton.localPosition = bubbleLocalOffset + new Vector3(bubbleDiameter * 0.65f, bubbleDiameter * 0.45f, 0f);
            if (_closeButton.gameObject.activeSelf != visible) _closeButton.gameObject.SetActive(visible);
        }

        /// <summary>Dupliquer / Supprimer grisés tant que la bulle est vide.</summary>
        private void UpdateButtonStates()
        {
            SetButtonEnabledLook(duplicateButton, _stored != null);
            SetButtonEnabledLook(deleteButton, _stored != null);
        }

        private void SetButtonEnabledLook(Button button, bool enabledLook)
        {
            if (button == null) return;
            var img = button.GetComponent<Image>();
            if (img == null) return;
            if (!_buttonBaseColors.TryGetValue(button, out Color baseColor))
            {
                baseColor = img.color;
                _buttonBaseColors[button] = baseColor;
            }
            img.color = enabledLook ? baseColor : new Color(baseColor.r * 0.4f, baseColor.g * 0.4f, baseColor.b * 0.4f, 0.6f);
        }

        private void ShrinkStored()
        {
            Bounds b = GetBounds(_stored);
            float maxExtent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float factor = maxExtent > 0.0001f ? Mathf.Min(1f, bubbleStoredSize / maxExtent) : 1f;
            SetLossyScale(_stored.transform, _storedLossyScale * factor);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Boîte englobante de l'objet, en monde. Renderers d'abord ; sinon les colliders :
        /// un Gaussian Splat est dessiné par code, sans Renderer, et son pivot peut être loin
        /// de ce qu'on voit (le fruit test : ~14 cm). L'objet rangé a ses colliders coupés :
        /// on reprend la boîte mémorisée au rangement.
        /// </summary>
        private Bounds GetBounds(Rigidbody body)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds rb = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) rb.Encapsulate(renderers[i].bounds);
                return rb;
            }

            if (body == _stored && _storedHasLocalBounds)
                return LocalToWorldBounds(body.transform, _storedLocalBounds);

            bool found = false;
            Bounds b = new Bounds(body.position, Vector3.zero);
            foreach (var c in body.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;
                if (!found) { b = c.bounds; found = true; }
                else b.Encapsulate(c.bounds);
            }
            return b;
        }

        private Vector3 GetBoundsCenter(Rigidbody body) => GetBounds(body).center;

        private static Bounds WorldToLocalBounds(Transform t, Bounds world)
        {
            Bounds local = new Bounds(t.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++) local.Encapsulate(t.InverseTransformPoint(Corner(world, i)));
            return local;
        }

        private static Bounds LocalToWorldBounds(Transform t, Bounds local)
        {
            Bounds world = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++) world.Encapsulate(t.TransformPoint(Corner(local, i)));
            return world;
        }

        private static Vector3 Corner(Bounds b, int i) => b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));

        private static void SetLossyScale(Transform t, Vector3 lossy)
        {
            Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(
                ps.x != 0f ? lossy.x / ps.x : lossy.x,
                ps.y != 0f ? lossy.y / ps.y : lossy.y,
                ps.z != 0f ? lossy.z / ps.z : lossy.z);
        }

        /// <summary>
        /// Mode mains : un bouton s'active quand le BOUT DE L'INDEX DROIT le touche.
        /// (Les déclencheurs physiques réagissaient à n'importe quel collider, dont
        /// l'objet tenu : appuyer était impossible, ou se faisait tout seul.)
        /// </summary>
        private void PollFingerPress()
        {
            if (!VRModalityManager.IsHandMode || _currentScaleFactor < 0.9f
                || !VRHandGestures.TryGetIndexTipWorld(XRNode.RightHand, out Vector3 tip))
            {
                _fingerInside = 0;
                return;
            }

            // 0 = rien, 1 = Dupliquer, 2 = Supprimer, 3 = croix de la bulle
            int hit = 0;
            if (IsFingerOn(duplicateButton, tip)) hit = 1;
            else if (IsFingerOn(deleteButton, tip)) hit = 2;
            else if (_closeCollider != null && _closeCollider.gameObject.activeInHierarchy
                     && (_closeCollider.ClosestPoint(tip) - tip).sqrMagnitude <= fingerPressDistance * fingerPressDistance)
                hit = 3;

            // Front montant uniquement : il faut ressortir le doigt pour appuyer à nouveau.
            if (hit != 0 && hit != _fingerInside && Time.unscaledTime - _lastFingerPress > 0.35f)
            {
                _lastFingerPress = Time.unscaledTime;
                if (hit == 1) OnDuplicateClicked();
                else if (hit == 2) OnDeleteClicked();
                else OnReleaseStoredClicked();
            }
            _fingerInside = hit;
        }

        /// <summary>
        /// Mode doigts : avec un objet dans la bulle, toucher du bout de l'index droit
        /// le bout d'un doigt gauche déclenche l'action associée. Une pastille colorée
        /// marque chaque doigt actif. Pas besoin de regarder sa paume.
        /// </summary>
        private void PollFingerTaps()
        {
            EnsureTapMarkers();
            bool active = _stored != null;
            Vector3 rightTip = Vector3.zero;
            bool hasRight = active && VRHandGestures.TryGetIndexTipWorld(XRNode.RightHand, out rightTip);

            int hit = -1;
            float best = fingerTapDistance;
            for (int i = 0; i < s_TapFingers.Length; i++)
            {
                bool hasTip = VRHandGestures.TryGetFingerTipWorld(XRNode.LeftHand, s_TapFingers[i], out Vector3 leftTip);
                bool show = active && hasTip;
                if (_tapMarkers[i].gameObject.activeSelf != show) _tapMarkers[i].gameObject.SetActive(show);
                if (!show) continue;

                _tapMarkers[i].position = leftTip;
                if (!hasRight) continue;

                // Doigt le plus proche sous le seuil : les bouts de doigts ne sont qu'à ~2 cm
                // les uns des autres, on évite ainsi de déclencher le voisin.
                float d = Vector3.Distance(rightTip, leftTip);
                if (d < best) { best = d; hit = i; }
            }

            // Front montant uniquement : il faut écarter l'index pour toucher à nouveau.
            if (hit >= 0 && hit != _tapInside && Time.unscaledTime - _lastFingerPress > 0.35f)
            {
                _lastFingerPress = Time.unscaledTime;
                if (hit == 0) OnDuplicateClicked();
                else if (hit == 1) OnDeleteClicked();
                else OnReleaseStoredClicked();
            }
            _tapInside = hit;
        }

        private void EnsureTapMarkers()
        {
            if (_tapMarkers != null) return;
            _tapMarkers = new Transform[s_TapFingers.Length];
            for (int i = 0; i < s_TapFingers.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "FingerTap_" + s_TapFingers[i];
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * fingerMarkerSize;
                var rend = go.GetComponent<MeshRenderer>();
                rend.sharedMaterial = VRUtils.CreateUnlitMaterial(s_TapColors[i]);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _tapMarkers[i] = go.transform;
            }
        }

        private bool IsFingerOn(Button button, Vector3 tip)
        {
            if (button == null) return false;
            var col = button.GetComponent<BoxCollider>();
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) return false;
            return (col.ClosestPoint(tip) - tip).sqrMagnitude <= fingerPressDistance * fingerPressDistance;
        }

        private void Start()
        {
            ResolveReferences();
            WireButtons();

            if (_canvas != null && headCamera != null)
            {
                var cam = headCamera.GetComponent<Camera>();
                if (cam != null) _canvas.worldCamera = cam;
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }
        }

        private void EnsureHierarchyAndUI()
        {
            _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvas == null)
            {
                var canvasGO = new GameObject("Wrist_Canvas");
                canvasGO.transform.SetParent(transform, false);
                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            var rt = _canvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(280f, 100f);
                rt.localScale = Vector3.one;
            }

            menuRoot = _canvas.transform;
            _initialScale = new Vector3(0.0008f, 0.0008f, 0.0008f);

            // Vérifier le fond
            Transform bgT = _canvas.transform.Find("Background");
            if (bgT == null)
            {
                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(_canvas.transform, false);
                var bgRT = bgGO.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;
                var img = bgGO.AddComponent<Image>();
                img.color = new Color(0.08f, 0.10f, 0.16f, 0.92f);
            }

            // Bouton Dupliquer
            if (duplicateButton == null)
            {
                Transform dupT = _canvas.transform.Find("DuplicateButton");
                if (dupT == null)
                {
                    var dupGO = new GameObject("DuplicateButton");
                    dupGO.transform.SetParent(_canvas.transform, false);
                    var dupRT = dupGO.AddComponent<RectTransform>();
                    dupRT.anchorMin = new Vector2(0.04f, 0.12f);
                    dupRT.anchorMax = new Vector2(0.48f, 0.88f);
                    dupRT.offsetMin = Vector2.zero;
                    dupRT.offsetMax = Vector2.zero;

                    var img = dupGO.AddComponent<Image>();
                    img.color = new Color(0.12f, 0.58f, 0.95f, 1f);

                    duplicateButton = dupGO.AddComponent<Button>();
                    var colors = duplicateButton.colors;
                    colors.normalColor = new Color(0.12f, 0.58f, 0.95f, 1f);
                    colors.highlightedColor = new Color(0.30f, 0.72f, 1.0f, 1f);
                    colors.pressedColor = new Color(0.08f, 0.40f, 0.75f, 1f);
                    duplicateButton.colors = colors;

                    CreateButtonLabel(dupGO, "Dupliquer\n<size=70%>(X)</size>");
                }
                else
                {
                    duplicateButton = dupT.GetComponent<Button>();
                }
            }

            // Bouton Supprimer
            if (deleteButton == null)
            {
                Transform delT = _canvas.transform.Find("DeleteButton");
                if (delT == null)
                {
                    var delGO = new GameObject("DeleteButton");
                    delGO.transform.SetParent(_canvas.transform, false);
                    var delRT = delGO.AddComponent<RectTransform>();
                    delRT.anchorMin = new Vector2(0.52f, 0.12f);
                    delRT.anchorMax = new Vector2(0.96f, 0.88f);
                    delRT.offsetMin = Vector2.zero;
                    delRT.offsetMax = Vector2.zero;

                    var img = delGO.AddComponent<Image>();
                    img.color = new Color(0.90f, 0.22f, 0.21f, 1f);

                    deleteButton = delGO.AddComponent<Button>();
                    var colors = deleteButton.colors;
                    colors.normalColor = new Color(0.90f, 0.22f, 0.21f, 1f);
                    colors.highlightedColor = new Color(1.0f, 0.38f, 0.38f, 1f);
                    colors.pressedColor = new Color(0.70f, 0.15f, 0.15f, 1f);
                    deleteButton.colors = colors;

                    CreateButtonLabel(delGO, "Supprimer\n<size=70%>(Y)</size>");
                }
                else
                {
                    deleteButton = delT.GetComponent<Button>();
                }
            }
        }

        private void CreateButtonLabel(GameObject btnGO, string text)
        {
            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(btnGO.transform, false);
            var lRT = labelGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero;
            lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero;
            lRT.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
        }

        private void WireButtons()
        {
            if (duplicateButton != null)
            {
                duplicateButton.onClick.RemoveListener(OnDuplicateClicked);
                duplicateButton.onClick.AddListener(OnDuplicateClicked);
                EnsureTouchCollider(duplicateButton.gameObject, true);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(OnDeleteClicked);
                deleteButton.onClick.AddListener(OnDeleteClicked);
                EnsureTouchCollider(deleteButton.gameObject, false);
            }
        }

        private void EnsureTouchCollider(GameObject btnObj, bool isDuplicate)
        {
            if (btnObj == null) return;
            var col = btnObj.GetComponent<BoxCollider>();
            if (col == null) col = btnObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            var rt = btnObj.GetComponent<RectTransform>();
            float w = rt != null ? Mathf.Max(rt.rect.width, 100f) : 100f;
            float h = rt != null ? Mathf.Max(rt.rect.height, 60f) : 60f;
            col.size = new Vector3(w, h, 40f);

            var touch = btnObj.GetComponent<VRMenuButtonTouch>();
            if (touch == null) touch = btnObj.AddComponent<VRMenuButtonTouch>();
            touch.handMenu = this;
            touch.isDuplicate = isDuplicate;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            }
        }

        public void ResolveReferences()
        {
            if (headCamera == null)
            {
                if (Camera.main != null) headCamera = Camera.main.transform;
                else
                {
                    var tracker = FindFirstObjectByType<VRHeadTracker>();
                    if (tracker != null) headCamera = tracker.transform;
                }
            }

            if (duplicateDelete == null)
            {
                duplicateDelete = FindFirstObjectByType<VRDuplicateDelete>();
            }

            // Recherche dynamique de la main gauche active.
            // 1. VRGrabHand gauche : il suit la PAUME réelle en hand tracking (et la manette sinon).
            //    ⚠️ Ne plus viser la racine de « Left Hand Interaction Visual » : elle reste fixée au
            //    Camera Offset (au sol) — le menu mesurait ~1,1 m main-tête en permanence.
            var h = VRGrabHand.GetHandForNode(XRNode.LeftHand);
            if (h != null && h.gameObject.activeInHierarchy)
            {
                leftHandTarget = h.transform;
                return;
            }

            // Les replis suivants parcourent toute la scène (GameObject.Find) : au plus
            // une fois par seconde, LateUpdate rappelant cette méthode tant qu'aucune
            // cible n'est trouvée.
            if (Time.unscaledTime < _nextSceneLookup) return;
            _nextSceneLookup = Time.unscaledTime + 1f;

            // 2. Os du poignet du visuel de main (recherche récursive : la hiérarchie des os est imbriquée)
            var visual = GameObject.Find("Left Hand Interaction Visual");
            if (visual != null && visual.activeInHierarchy)
            {
                var wrist = VRUtils.FindDeep(visual.transform, "L_Wrist") ?? VRUtils.FindDeep(visual.transform, "L_Palm");
                if (wrist != null)
                {
                    leftHandTarget = wrist;
                    return;
                }
            }

            // 3. Left Controller
            var leftCtrl = GameObject.Find("Left Controller");
            if (leftCtrl != null && leftCtrl.activeInHierarchy)
            {
                leftHandTarget = leftCtrl.transform;
            }
        }

        /// <summary>Prochain instant (s) où les replis par GameObject.Find sont autorisés.</summary>
        private float _nextSceneLookup;

        private void Update()
        {
            // Raccourcis manette physique gauche (X pour dupliquer, Y pour supprimer).
            // Ignorés en mode mains : l'émulation de gestes d'ALVR génère de faux X/Y.
            if (!VRModalityManager.IsHandMode && VRButtonReader.WasPressed(VRButton.X))
            {
                OnDuplicateClicked();
            }
            if (!VRModalityManager.IsHandMode && VRButtonReader.WasPressed(VRButton.Y))
            {
                OnDeleteClicked();
            }

#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                alwaysVisible = !alwaysVisible;
            }
#endif
        }

        private void LateUpdate()
        {
            if (leftHandTarget == null || !leftHandTarget.gameObject.activeInHierarchy)
            {
                ResolveReferences();
                if (leftHandTarget == null) return;
            }

            // Placer le menu sur le poignet gauche
            Vector3 worldPos = leftHandTarget.TransformPoint(positionOffset);
            Quaternion worldRot = transform.rotation;

            if (faceCamera && headCamera != null)
            {
                Vector3 toCam = headCamera.position - worldPos;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    // LookRotation(-toCam, up) : fait regarder le Canvas droit vers le joueur à l'endroit
                    worldRot = Quaternion.LookRotation(-toCam, headCamera.up) * Quaternion.Euler(menuRotationOffset);
                }
            }
            else
            {
                worldRot = leftHandTarget.rotation * Quaternion.Euler(menuRotationOffset);
            }

            transform.position = worldPos;
            transform.rotation = worldRot;

            if (menuRoot != null && menuRoot != transform)
            {
                menuRoot.position = worldPos;
                menuRoot.rotation = worldRot;
            }

            // Visibilité par regard et orientation : exige un maintien de 2s devant le visage
            bool shouldShow = CheckShouldShow();

            // Menu déjà ouvert : on le GARDE ouvert pendant qu'on dépose un objet. Quand la
            // main droite passe devant la gauche, le casque perd le suivi de la main gauche
            // (occlusion) : le menu se refermait et l'objet n'était jamais rangé.
            if (!shouldShow && _isMenuDisplayed && IsDepositInProgress()) shouldShow = true;

            if (!shouldShow && _gazeHoldTimer > 0f)
            {
                // Micro-interruption (tremblement du tracking) : on tolère GazeGrace secondes.
                _gazeLostTimer += Time.unscaledDeltaTime;
                if (_gazeLostTimer < GazeGrace) shouldShow = true;
            }
            else if (shouldShow)
            {
                _gazeLostTimer = 0f;
            }

            if (shouldShow)
            {
                _gazeHoldTimer += Time.unscaledDeltaTime;
                if (_gazeHoldTimer >= requiredGazeHoldTime)
                {
                    if (!_isMenuDisplayed)
                    {
                        PlayFeedback();
                    }
                    _isMenuDisplayed = true;
                }
            }
            else
            {
                _gazeHoldTimer = 0f;
                _isMenuDisplayed = false;
            }

            float targetFactor = _isMenuDisplayed ? 1f : 0f;
            _currentScaleFactor = Mathf.MoveTowards(_currentScaleFactor, targetFactor, Time.unscaledDeltaTime * animSpeed);

            if (menuRoot != null)
            {
                menuRoot.localScale = _initialScale * _currentScaleFactor;
                bool isVisible = _currentScaleFactor > 0.02f && !FingerTapMode; // mode doigts : pas de panneau, seule la bulle s'affiche
                if (menuRoot.gameObject.activeSelf != isVisible)
                {
                    menuRoot.gameObject.SetActive(isVisible);
                }
            }

            UpdateBubble();
            if (FingerTapMode) PollFingerTaps();
            else
            {
                if (_tapMarkers != null) foreach (var m in _tapMarkers) m.gameObject.SetActive(false);
                PollFingerPress();
            }
        }

        private bool CheckShouldShow()
        {
            if (alwaysVisible) return true;
            if (headCamera == null || leftHandTarget == null) { LogVisibility("caméra ou main gauche introuvable"); return false; }

            Vector3 handPos = leftHandTarget.position;
            Vector3 camPos = headCamera.transform.position;
            float dist = Vector3.Distance(handPos, camPos);

            // 1. Distance naturelle de lecture de montre : entre 15 cm et 65 cm
            if (dist < minDistance || dist > maxDistance) { LogVisibility($"distance main-tête {dist:F2} m hors [{minDistance:F2}, {maxDistance:F2}]"); return false; }

            // 2. Hauteur de la main : la main doit être levée au niveau du torse (au-dessus du plexus)
            if (requireHandRaised && handPos.y < (camPos.y - 0.45f)) { LogVisibility("main trop basse"); return false; }

            // 3. Regard dirigé directement sur le poignet (angle serré de 35° max)
            Vector3 camForward = headCamera.transform.forward;
            Vector3 toHand = (handPos - camPos).normalized;
            float gazeAngle = Vector3.Angle(camForward, toHand);
            if (gazeAngle > Mathf.Max(gazeAngleThreshold, 45f)) { LogVisibility($"main hors du regard ({gazeAngle:F0}°)"); return false; }

            // 4. Orientation de la paume/poignet : l'intérieur du poignet doit faire face au visage
            Vector3 toCamera = (camPos - handPos).normalized;

            // Hand tracking : la normale de la PAUME est -Y de l'articulation Palm (XR Hands :
            // +Y sort du dos de la main). L'ancien test ci-dessous visait le dos de la main,
            // donc ouvrir la paume vers son visage n'ouvrait jamais le menu.
            if (VRHandGestures.TryGetPalmNormal(XRNode.LeftHand, out Vector3 palmNormal))
            {
                float palmDot = Vector3.Dot(palmNormal, toCamera);
                LogVisibility($"paume→visage dot={palmDot:F2} (seuil 0.40)");
                return palmDot > 0.40f;
            }

            float facingDot = Vector3.Dot(leftHandTarget.up, toCamera);
            bool isFacingPlayer = facingDot > 0.30f || Vector3.Dot(-leftHandTarget.right, toCamera) > 0.30f;
            if (!isFacingPlayer) return false;

            return true;
        }

        // Dupliquer / Supprimer n'agissent QUE sur l'objet rangé dans la bulle : on évite
        // ainsi toute action involontaire sur l'objet tenu en main.
        public void OnDuplicateClicked()
        {
            if (_stored == null) { LogEmptyBubble(); return; }
            if (duplicateDelete == null) duplicateDelete = FindFirstObjectByType<VRDuplicateDelete>();
            if (duplicateDelete == null) return;
            PlayFeedback();

            // Le clone est instancié depuis l'objet rangé : il faut lui rendre temporairement
            // sa taille réelle et ses colliders, sinon le clone hériterait de l'état « rangé ».
            SetLossyScale(_stored.transform, _storedLossyScale);
            foreach (var c in _storedColliders) if (c != null) c.enabled = true;
            Physics.SyncTransforms();

            duplicateDelete.DuplicateBody(_stored);

            foreach (var c in _storedColliders) if (c != null) c.enabled = false;
            ShrinkStored();
        }

        public void OnDeleteClicked()
        {
            if (_stored == null) { LogEmptyBubble(); return; }
            if (duplicateDelete == null) duplicateDelete = FindFirstObjectByType<VRDuplicateDelete>();
            if (duplicateDelete == null) return;
            PlayFeedback();

            if (duplicateDelete.DeleteBody(_stored))
            {
                _stored = null;
                _storedColliders.Clear();
            }
        }

        private void LogEmptyBubble()
        {
            if (debugVisibility) Debug.Log("[VRHandMenu] Bulle vide : range d'abord un objet dans la bulle (lâche-le dedans).");
        }

        private void PlayFeedback()
        {
            if (audioSource != null && clickClip != null)
            {
                audioSource.PlayOneShot(clickClip);
            }

            if (enableHaptics)
            {
                SendHaptic(XRNode.RightHand, 0.4f, 0.06f);
                SendHaptic(XRNode.LeftHand, 0.25f, 0.04f);
            }
        }

        private void SendHaptic(XRNode node, float amplitude, float duration)
        {
            var device = VRStickReader.GetDevice(node); // repli par caractéristiques (nœud souvent invalide sous OpenXR)
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }

    /// <summary>
    /// Permet de déclencher un clic sur le bouton par contact physique avec l'autre main ou contrôleur.
    /// </summary>
    public class VRMenuButtonTouch : MonoBehaviour
    {
        public VRHandMenu handMenu;
        public bool isDuplicate;
        private float _lastPressTime;

        private void OnTriggerEnter(Collider other)
        {
            // Mode mains : l'appui est détecté au bout de l'index (VRHandMenu.PollFingerPress).
            if (VRModalityManager.IsHandMode) return;
            if (Time.unscaledTime - _lastPressTime < 0.35f) return;

            // Ignorer les contacts avec la main gauche elle-même
            if (handMenu != null && handMenu.leftHandTarget != null)
            {
                if (other.transform == handMenu.leftHandTarget || other.transform.IsChildOf(handMenu.leftHandTarget))
                    return;
            }

            _lastPressTime = Time.unscaledTime;
            if (isDuplicate)
            {
                handMenu?.OnDuplicateClicked();
            }
            else
            {
                handMenu?.OnDeleteClicked();
            }
        }
    }
}
