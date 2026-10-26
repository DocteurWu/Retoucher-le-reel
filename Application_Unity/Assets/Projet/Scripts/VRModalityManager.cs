// =====================================================================================
//  VRModalityManager.cs — GESTION DES MODALITÉS : MAINS (HAND TRACKING) VS MANETTES
//
//  RÔLE
//    Détecte automatiquement si l'utilisateur utilise le suivi des mains (Hand Tracking)
//    ou ses manettes physiques (Motion Controllers) :
//      - En mode MAINS :
//          * Active « Left Hand Interaction Visual » et « Right Hand Interaction Visual »
//            pour voir les maillages de mains animés et le mouvement des doigts.
//          * Masque les modèles 3D de manettes (« ControllerModel »).
//          * Désactive la locomotion continue (pour n'avoir que la téléportation).
//      - En mode MANETTES :
//          * Affiche les modèles 3D de manettes.
//          * Masque les visuels de mains.
//          * Autorise la locomotion continue au stick si configurée.
//
//    Désactive également l'XRInputModalityManager natif d'Unity qui force les manettes
//    dès qu'un casque Meta Quest Link est branché.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    [DefaultExecutionOrder(-120)]
    [AddComponentMenu("Remodeler le Reel/VR/VR Modality Manager (Gestion Mains et Manettes)")]
    public class VRModalityManager : MonoBehaviour
    {
        public static VRModalityManager Instance { get; private set; }

        [Header("--- Visuels des Mains (SkinnedMeshRenderer) ---")]
        [Tooltip("Visuel de la main gauche (avec os et maillage animé)")]
        public GameObject leftHandVisual;

        [Tooltip("Visuel de la main droite (avec os et maillage animé)")]
        public GameObject rightHandVisual;

        [Header("--- Contrôleurs / Manettes ---")]
        [Tooltip("Objet racine Left Controller")]
        public GameObject leftController;

        [Tooltip("Objet racine Right Controller")]
        public GameObject rightController;

        [Tooltip("Modèle 3D de la manette gauche à masquer en mode mains")]
        public GameObject leftControllerModel;

        [Tooltip("Modèle 3D de la manette droite à masquer en mode mains")]
        public GameObject rightControllerModel;

        [Header("--- Options & Détection ---")]
        [Tooltip("Forcer le mode mains même si aucune main n'est suivie (masque alors les manettes en permanence)")]
        public bool forceHandTrackingVisual = false;

        [Tooltip("Désactiver le déplacement continu (joystick) en mode mains pour n'autoriser QUE la téléportation")]
        public bool disableContinuousMoveInHandMode = true;

        [Tooltip("Délai de grâce avant de quitter le mode mains en cas de perte fugitive du suivi")]
        public float handTrackingGracePeriod = 0.6f;

        /// <summary>
        /// Vrai dès que le suivi des mains est actif (ou forcé).
        /// Lu par VRSmoothLocomotion, VRTeleportation et VRSnapTurn.
        /// </summary>
        public static bool IsHandMode { get; private set; } = false;

        private float _lastHandTrackedTime = -999f;
        private bool _isCurrentHandMode = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
            // Une instance posée dans une scène disparaît avec elle : on garantit qu'il en
            // existe toujours une après chaque chargement (s'exécute après les Awake de la scène).
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance == null) EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (Instance == null)
            {
                var existing = Object.FindFirstObjectByType<VRModalityManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    var go = new GameObject("VRModalityManager_Auto");
                    Instance = go.AddComponent<VRModalityManager>();
                    Object.DontDestroyOnLoad(go);
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // ⚠️ Ne JAMAIS détruire le GameObject : dans Scene.de.base1 ce composant est
                // posé sur « XR Origin (VR) ». Destroy(gameObject) supprimait tout le rig
                // (caméra comprise) en arrivant du Menu → image figée « No cameras rendering ».
                enabled = false;
                Destroy(this);
                return;
            }
            Instance = this;
            // Persistance inter-scènes uniquement pour l'instance autonome créée par
            // AutoInitialize (qui appelle elle-même DontDestroyOnLoad). Une instance posée
            // sur un rig de scène ne doit pas emporter ce rig dans les scènes suivantes.
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResolveReferences();
            DisableConflictingManagers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveReferences();
            DisableConflictingManagers();
            ApplyModality(CheckHandsTracked() || forceHandTrackingVisual, true);
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
            ResolveReferences();
            DisableConflictingManagers();
        }

        private void Start()
        {
            ResolveReferences();
            DisableConflictingManagers();
            ApplyModality(CheckHandsTracked() || forceHandTrackingVisual, true);
        }

        private void Update()
        {
            bool trackedNow = CheckHandsTracked();
            if (trackedNow)
            {
                _lastHandTrackedTime = Time.unscaledTime;
            }

            bool withinGrace = (Time.unscaledTime - _lastHandTrackedTime) < handTrackingGracePeriod;
            bool targetHandMode = forceHandTrackingVisual || trackedNow || withinGrace;

            if (targetHandMode != _isCurrentHandMode)
            {
                ApplyModality(targetHandMode, false);
            }
        }

        public void ResolveReferences()
        {
            if (leftHandVisual == null)
            {
                var go = GameObject.Find("Left Hand Interaction Visual");
                if (go != null) leftHandVisual = go;
            }
            if (rightHandVisual == null)
            {
                var go = GameObject.Find("Right Hand Interaction Visual");
                if (go != null) rightHandVisual = go;
            }

            if (leftController == null)
            {
                var go = GameObject.Find("Left Controller");
                if (go != null) leftController = go;
            }
            if (rightController == null)
            {
                var go = GameObject.Find("Right Controller");
                if (go != null) rightController = go;
            }

            if (leftControllerModel == null && leftController != null)
            {
                var t = leftController.transform.Find("ControllerModel");
                if (t != null) leftControllerModel = t.gameObject;
            }
            if (rightControllerModel == null && rightController != null)
            {
                var t = rightController.transform.Find("ControllerModel");
                if (t != null) rightControllerModel = t.gameObject;
            }
        }

        private void DisableConflictingManagers()
        {
            // Désactiver l'XRInputModalityManager d'Unity qui bloque les mains sous Oculus Link
            var allMono = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < allMono.Length; i++)
            {
                var mb = allMono[i];
                if (mb != null && mb != this && mb.GetType().Name == "XRInputModalityManager")
                {
                    mb.enabled = false;
                }
            }
        }

        private void ApplyModality(bool handMode, bool force)
        {
            _isCurrentHandMode = handMode;
            IsHandMode = handMode;

            ResolveReferences();
            AlignHandVisual(leftHandVisual);
            AlignHandVisual(rightHandVisual);

            // 1. Visuels des mains : affichés en mode mains, masqués en mode manettes
            if (leftHandVisual != null && (leftHandVisual.activeSelf != handMode || force))
            {
                leftHandVisual.SetActive(handMode);
            }
            if (rightHandVisual != null && (rightHandVisual.activeSelf != handMode || force))
            {
                rightHandVisual.SetActive(handMode);
            }

            // 2. Modèles 3D des manettes : TOUS les renderers sont masqués en mode mains
            SetControllerRenderersVisible(leftController, !handMode);
            SetControllerRenderersVisible(rightController, !handMode);

            // 3. Les GameObjects racines Left Controller / Right Controller doivent rester ACTIFS
            // afin que VRGrabHand continue de fonctionner (suivi 6-DOF du poignet + détection de proximité)
            if (leftController != null && !leftController.activeSelf)
            {
                leftController.SetActive(true);
            }
            if (rightController != null && !rightController.activeSelf)
            {
                rightController.SetActive(true);
            }
        }

        /// <summary>
        /// Les poses XR Hands sont exprimées dans l'espace de tracking, comme celle du casque :
        /// le visuel de main doit donc être un enfant DIRECT du parent de la caméra (Camera Offset),
        /// en pose locale identité. Dans Menu.unity il était enfant de la racine XROrigin_Menu avec
        /// z = -0,55 : les mains virtuelles apparaissaient décalées des mains réelles.
        /// </summary>
        private static void AlignHandVisual(GameObject visual)
        {
            if (visual == null) return;
            var cam = Camera.main;
            if (cam == null || cam.transform.parent == null) return;

            Transform space = cam.transform.parent;
            Transform t = visual.transform;
            if (t.parent != space) t.SetParent(space, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private void SetControllerRenderersVisible(GameObject controllerRoot, bool visible)
        {
            if (controllerRoot == null) return;

            var model = controllerRoot.transform.Find("ControllerModel");
            if (model != null) model.gameObject.SetActive(visible);

            // Désactiver tous les MeshRenderer des pièces de la manette (boutons, base, joystick)
            var renderers = controllerRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                // Ne jamais masquer les SkinnedMeshRenderer des mains, ni les lasers / arcs
                // (LineRenderer) que les scripts de menu et de téléportation pilotent eux-mêmes.
                if (r is SkinnedMeshRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (leftHandVisual != null && r.transform.IsChildOf(leftHandVisual.transform)) continue;
                if (rightHandVisual != null && r.transform.IsChildOf(rightHandVisual.transform)) continue;

                r.enabled = visible;
            }
        }

        private bool CheckHandsTracked()
        {
            return VRHandGestures.AnyHandTracked;
        }
    }
}
