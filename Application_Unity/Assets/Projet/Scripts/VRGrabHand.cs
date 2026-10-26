// =====================================================================================
//  VRGrabHand.cs — MANETTE : SUIVI 6-DOF + SAISIE + LANCER
//
//  RÔLE (3 choses sur le GameObject de la manette)
//    1. SUIVI : écrit la pose réelle de la manette dans son Transform (local).
//    2. SAISIE : une sphère de grabRadius (18 cm) entoure la main ; à l'appui
//       du grip ou du trigger, l'objet le plus proche est parenté à la main,
//       devient cinématique et perd la gravité (il ne tombe plus en main).
//    3. LANCER : au relâchement, l'objet repart avec la MOYENNE des 8 dernières
//       vitesses de la main (on peut donc lancer en remuant le poignet).
//
//  À POSER SUR : chaque manette (Left Controller / Right Controller).
//  Parameters : handNode (LeftHand/RightHand), grabRadius, throwForceMultiplier,
//               snapToHandOnGrab, gripOffset.
//
//  ⚠️ RÈGLES IMPORTANTES
//    - Physics.SyncTransforms() après le parentage : le projet a
//      autoSyncTransforms = 0, sans quoi le collider de l'objet reste en retard.
//    - Au relâchement, écrire la pose sur le RIGIDBODY (position/rotation)
//      avant de le repasser en dynamique, sinon la physique réimpose son
//      ancienne pose et l'objet « revient en arrière ».
//    - Un objet déjà tenu par l'autre main est EXCLU du classement (sinon la
//      main ne pouvait plus rien attraper).
//    - Ordre d'exécution : 0 (après VRScaleGrab en −50, qui arme le verrou
//      d'entrée que ce script doit voir dans la MÊME frame).
//
//  RÉFÉRENCES : voir README.md (sections 3, 4 et 6)
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// CONTRÔLEUR VR COMPLET : suivi 6-DOF de la manette + saisie + lancer physique.
    ///
    /// Ce script fait trois choses, sur le GameObject de la manette :
    ///
    ///  1. SUIVI 6-DOF — il écrit à chaque frame la position et la rotation réelles
    ///     de la manette dans son propre Transform (via 4 niveaux de lecture, voir
    ///     UpdateHandTracking). C'est ce qui fait que la main virtuelle colle à la
    ///     main physique, sans retard visible.
    ///
    ///  2. SAISIE (GRAB) — une sphère de rayon grabRadius est centrée sur la main.
    ///     À l'appui du grip ou du trigger, l'objet le plus proche est « attrapé » :
    ///     on le_met en cinématique, on coupe la gravité, et surtout on le
    ///     PARENTE à la main. Ce parentage est la mécanique clé de tout le reste :
    ///     c'est lui qui fait suivre l'objet à la main, et c'est aussi ce que
    ///     VRScaleGrab utilise pour retrouver l'objet tenu.
    ///
    ///  3. LANCER (THROW) — à chaque frame on mémorise la vitesse de la main.
    ///     Au relâchement, l'objet repart avec la MOYENNE des 8 dernières vitesses :
    ///     le joueur peut donc lancer un objet en remuant le poignet, comme
    ///     dans la vraie vie, et non pas seulement en translation de la main.
    ///
    /// Compatible Quest 3 standalone (OpenXR) et PCVR grâce aux 4 niveaux de
    /// lecture du suivi et du bouton.
    /// </summary>
    /// <remarks>
    /// ORDRE D'EXÉCUTION : ce composant garde l'ordre par défaut (0), donc il
    /// s'exécute APRÈS VRScaleGrab (-50). C'est indispensable : c'est
    /// VRScaleGrab qui arme le verrou d'entrée, et la saisie doit voir ce
    /// verrou dans la MÊME frame pour interdire à la main libre de voler
    /// l'objet en cours de redimensionnement.
    /// En revanche VRDuplicateDelete (+10) s'exécute après ce script, ce qui
    /// lui permet de dupliquer/supprimer l'objet venant d'être saisi dans la
    /// même frame.
    /// </remarks>
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Remodeler le Reel/VR/VR Grab Hand (Contrôleur et Saisie)")]
    public class VRGrabHand : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // SUIVI SPATIAL DE LA MANETTE (6-DOF)
        // ---------------------------------------------------------------------

        [Header("--- Suivi Spatial de la Manette (6-DOF) ---")]

        /// <summary>
        /// Quelle main ce contrôleur représente. Indispensable : c'est le critère
        /// utilisé pour retrouver le bon périphérique XR parmi tous ceux branchés.
        /// </summary>
        [Tooltip("Main ciblée (LeftHand ou RightHand)")]
        public XRNode handNode = XRNode.RightHand;

        // ---------------------------------------------------------------------
        // INTERACTION PHYSIQUE & SAISIE
        // ---------------------------------------------------------------------

        [Header("--- Interaction Physique & Saisie ---")]

        /// <summary>
        /// Rayon de la sphère de détection autour de la main, en mètres.
        /// C'est à la fois le rayon du SphereCollider (pour les événements de
        /// contact) et la distance de recherche des objets attrapables.
        /// 0,18 m ≈ une portée de main naturelle.
        /// </summary>
        [Tooltip("Rayon de détection pour attraper les objets")]
        public float grabRadius = 0.18f;

        /// <summary>
        /// Multiplicateur appliqué à la vitesse mesurée de la main au moment du
        /// lancer. 1,0 = réaliste, &gt; 1 = plus spectaculaire (souvent plus
        /// agréable en VR que le réalisme strict, car les collisions de la main
        /// « volent » de l'énergie).
        /// </summary>
        [Tooltip("Multiplicateur d'impulsion lors du lancer")]
        public float throwForceMultiplier = 1.35f;

        // --- Accroche : position de l'objet dans la main --------------------------

        /// <summary>
        /// Coller l'objet exactement sous la main au moment de la saisie.
        ///
        /// Sans cette option, l'objet est simplement parenté en conservant sa position
        /// monde : il reste donc figé à l'écart qu'il avait quand le bouton a été
        /// pressé — jusqu'à <see cref="grabRadius"/> (18 cm par défaut). Cet écart ne
        /// bouge plus ensuite, et l'objet semble alors « ne pas suivre la manette ».
        /// </summary>
        [Tooltip("Coller l'objet exactement sous la main au moment de la saisie")]
        public bool snapToHandOnGrab = false;

        /// <summary>
        /// Décalage local appliqué à l'objet une fois collé dans la main (en mètres,
        /// repère de la main). Utile pour placer l'objet légèrement devant ou au-dessus
        /// du pivot plutôt qu'au centre. Ignoré si <see cref="snapToHandOnGrab"/> est décoché.
        /// </summary>
        [Tooltip("Décalage local de l'objet collé dans la main (mètres)")]
        public Vector3 gripOffset = Vector3.zero;

        // --- Retours haptiques (vibrations) -----------------------------------

        [Header("Haptique (Vibrations)")]
        [Tooltip("Activer les retours haptiques sur cette manette")]
        public bool enableHaptics = true;

        [Range(0f, 1f), Tooltip("Intensité de la vibration lors de la saisie (grab)")]
        public float grabHapticIntensity = 0.5f;

        [Tooltip("Durée de la vibration lors de la saisie (secondes)")]
        public float grabHapticDuration = 0.08f;

        [Range(0f, 1f), Tooltip("Intensité de la vibration lors du relâchement / lancer")]
        public float releaseHapticIntensity = 0.25f;

        [Tooltip("Durée de la vibration lors du relâchement (secondes)")]
        public float releaseHapticDuration = 0.05f;

        // --- État de la saisie ------------------------------------------------

        /// <summary>Objet actuellement tenu par cette main (null si main vide).</summary>
        private Rigidbody _grabbedBody;

        /// <summary>Profil haptique spécifique de l'objet actuellement saisi (ou null si aucun composant).</summary>
        private VRHapticInteractable _heldHaptic;

        /// <summary>Priorité de la vibration en cours de jeu sur la manette.</summary>
        private int _currentHapticPriority = -1;

        /// <summary>Temps de fin prévu de la vibration actuelle.</summary>
        private float _hapticEndTime = 0f;

        /// <summary>
        /// Parent d'origine de l'objet, mémorisé au moment de la saisie.
        /// Indispensable pour le rendre à sa place au relâchement : sans ça, un
        /// objet du décor deviendrait définitivement enfant de la main.
        /// </summary>
        private Transform _originalParent;

        /// <summary>
        /// Mode d'interpolation du Rigidbody AVANT la saisie, restitué au relâchement.
        ///
        /// Précaution, pas un correctif de bug : pendant la saisie l'objet devient
        /// CINÉMATIQUE, et la recommandation pour un corps cinématique est de ne pas
        /// l'interpoler. La pose affichée suit alors le Transform, qui colle exactement
        /// à la main via la hiérarchie. (Mesuré : avec Interpolate l'écart reste nul,
        /// le parenting étant exact — mais on ne laisse pas la physique avoir le rôle
        /// de la main pendant la saisie.)
        /// </summary>
        private RigidbodyInterpolation _interpolationBeforeGrab = RigidbodyInterpolation.None;

        /// <summary>
        /// État du bouton de saisie à la frame précédente.
        /// Permet de distinguer l'appui (front montant) du relâchement (front
        /// descendant) — un maintien ne doit grabber qu'une seule fois.
        /// </summary>
        private bool _wasGrabPressed;

        /// <summary>
        /// Objet actuellement tenu par cette main (null si la main est vide). Exposé en
        /// lecture seule pour les composants frères qui travaillent sur l'objet saisi
        /// (ex. <see cref="VRDuplicateDelete"/>).
        /// </summary>
        public Rigidbody GrabbedBody => _grabbedBody;

        /// <summary>Vrai si cette main tient actuellement un objet.</summary>
        public bool IsHoldingObject => _grabbedBody != null;

        /// <summary>Vrai si l'entrée de saisie (grip, bouton ou pincement) est active ce frame.</summary>
        public bool IsGrabInputActive { get; private set; }

        /// <summary>
        /// Parent d'origine de l'objet tenu, mémorisé au moment de la saisie (null si
        /// aucun objet n'est tenu). Permet à un autre composant de replacer ou de
        /// dupliquer l'objet dans la bonne partie de la hiérarchie, plutôt qu'à la
        /// racine de la scène.
        /// </summary>
        public Transform GrabbedOriginalParent => _originalParent;

        /// <summary>Liste active de toutes les mains VRGrabHand enregistrees.</summary>
        public static readonly List<VRGrabHand> AllHands = new List<VRGrabHand>(2);

        /// <summary>
        /// Un objet vient d'être saisi (main, objet). Écouté par la narration
        /// (<c>NarrationSequence</c>) : penser à se désabonner dans OnDisable.
        /// </summary>
        public static event System.Action<VRGrabHand, Rigidbody> ObjetSaisi;

        /// <summary>Un objet vient d'être relâché (main, objet).</summary>
        public static event System.Action<VRGrabHand, Rigidbody> ObjetLache;

        /// <summary>Vrai si au moins une main tient un objet.</summary>
        public static bool AnyHandGrabbing()
        {
            for (int i = 0; i < AllHands.Count; i++)
            {
                if (AllHands[i] != null && AllHands[i]._grabbedBody != null) return true;
            }
            return false;
        }

        /// <summary>Retourne le Rigidbody tenu par l'une des mains (null si rien n'est tenu).</summary>
        public static Rigidbody GetAnyGrabbedBody(out VRGrabHand holdingHand)
        {
            for (int i = 0; i < AllHands.Count; i++)
            {
                if (AllHands[i] != null && AllHands[i]._grabbedBody != null)
                {
                    holdingHand = AllHands[i];
                    return AllHands[i]._grabbedBody;
                }
            }
            holdingHand = null;
            return null;
        }

        /// <summary>Retourne le composant VRGrabHand associé à un nœud XR donné (LeftHand ou RightHand).</summary>
        public static VRGrabHand GetHandForNode(XRNode node)
        {
            for (int i = 0; i < AllHands.Count; i++)
            {
                if (AllHands[i] != null && AllHands[i].handNode == node)
                {
                    return AllHands[i];
                }
            }
            return null;
        }

        // --- Historique des vitesses (pour le lancer) -------------------------

        // Historique des vitesses pour un lancer fluide et naturel

        /// <summary>Nombre d'échantillons conservés (fenêtre de lissage du lancer).</summary>
        private const int VelocitySampleCount = 8;

        /// <summary>Vitesse linéaire de la main sur les 8 dernières frames (m/s).</summary>
        private readonly Queue<Vector3> _velocitySamples = new Queue<Vector3>(VelocitySampleCount);

        /// <summary>Vitesse angulaire de la main sur les 8 dernières frames (rad/s).</summary>
        private readonly Queue<Vector3> _angularVelocitySamples = new Queue<Vector3>(VelocitySampleCount);

        /// <summary>
        /// Position de la main à la frame précédente, dans le repère de TRACKING
        /// (et non en monde) : un déplacement du rig (téléportation, escalade,
        /// locomotion) ne doit pas être compté comme un mouvement de la main.
        /// </summary>
        private Vector3 _lastTrackingPos;

        /// <summary>Rotation de la main à la frame précédente, dans le repère de tracking.</summary>
        private Quaternion _lastTrackingRot;

        /// <summary>Repère de tracking utilisé à la frame précédente (détecte un changement de rig).</summary>
        private Transform _lastTrackingSpace;

        /// <summary>
        /// Vitesse maximale retenue pour un échantillon (m/s). Un bras humain dépasse
        /// rarement 10 m/s : au-delà, c'est un saut de tracking (perte puis reprise
        /// de la main), qui sinon projetterait l'objet à l'autre bout de la scène.
        /// </summary>
        private const float MaxSampleSpeed = 20f;

        /// <summary>Tampon réutilisé pour les retours haptiques (évite une allocation par vibration).</summary>
        private static readonly List<XRInputDevice> s_HapticDevices = new List<XRInputDevice>();

        // --- Input Actions (secours) -----------------------------------------

        /// <summary>Action analogique du bouton de maintien (grip).</summary>
        private InputAction _gripAction;

        /// <summary>Action analogique de la détente (trigger).</summary>
        private InputAction _triggerAction;

        /// <summary>Action de position de la manette (poignée).</summary>
        private InputAction _posAction;

        /// <summary>Action de rotation de la manette (poignée).</summary>
        private InputAction _rotAction;

        /// <summary>Action de position du pointeur (référence alternative).</summary>
        private InputAction _posPointerAction;

        /// <summary>Action de rotation du pointeur (référence alternative).</summary>
        private InputAction _rotPointerAction;

        // --- Tampons réutilisables (zéro allocation par frame) ----------------

        /// <summary>Liste réutilisée pour lire les XRNodeState.</summary>
        private readonly List<XRNodeState> _nodeStates = new List<XRNodeState>();

        /// <summary>
        /// Tampon pour Physics.OverlapSphereNonAlloc : évite de créer un tableau
        /// à chaque frame. 16 = nombre max de colliders testés simultanément.
        /// </summary>
        private readonly Collider[] _overlapResults = new Collider[16];

        // =====================================================================
        // INITIALISATION
        // =====================================================================

        /// <summary>
        /// APPELÉ UNE SEULE FOIS au démarrage.
        ///
        /// 1. Configure le SphereCollider de la main : Trigger + rayon de détection.
        ///    (Le collider sert de « volume de préhension » autour de la main.)
        ///
        /// 2. Crée toutes les Input Actions nécessaires, chacune avec TROIS familles
        ///    de liaisons, car le chemin exact dépend du casque et du pilote :
        ///       &lt;XRController&gt;{RightHand}/grip        → forme avec accolade de main
        ///       &lt;XRController&gt;{rightHand}/grip        → variante minuscule
        ///       &lt;RightHandXRController&gt;/grip          → layout Meta Quest
        ///    En enregistrant les trois, le même code fonctionne sur Quest, Index, Vive…
        /// </summary>
        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;         // Un trigger ne pousse pas physiquement l'objet.
            col.radius = grabRadius;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            string handSide = handNode == XRNode.LeftHand ? "leftHand" : "rightHand";
            string handPrefix = handNode == XRNode.LeftHand ? "Left" : "Right";

            // Actions Boutons de saisie
            _gripAction = new InputAction("Grip", InputActionType.Value);
            _gripAction.AddBinding($"<XRController>{handTag}/grip");
            _gripAction.AddBinding($"<XRController>{handTag}/gripPressed");
            _gripAction.AddBinding($"<XRController>{{{handSide}}}/grip");
            _gripAction.AddBinding($"<XRController>{{{handSide}}}/gripPressed");
            _gripAction.AddBinding($"<{handPrefix}HandXRController>/grip");
            _gripAction.AddBinding($"<{handPrefix}HandXRController>/gripPressed");

            _triggerAction = new InputAction("Trigger", InputActionType.Value);
            _triggerAction.AddBinding($"<XRController>{handTag}/trigger");
            _triggerAction.AddBinding($"<XRController>{handTag}/triggerPressed");
            _triggerAction.AddBinding($"<XRController>{{{handSide}}}/trigger");
            _triggerAction.AddBinding($"<XRController>{{{handSide}}}/triggerPressed");
            _triggerAction.AddBinding($"<{handPrefix}HandXRController>/trigger");
            _triggerAction.AddBinding($"<{handPrefix}HandXRController>/triggerPressed");

            // Actions Pose (Grip & Pointer)
            _posAction = new InputAction("Position", InputActionType.Value, expectedControlType: "Vector3");
            _posAction.AddBinding($"<XRController>{handTag}/devicePosition");
            _posAction.AddBinding($"<XRController>{{{handSide}}}/devicePosition");
            _posAction.AddBinding($"<{handPrefix}HandXRController>/devicePosition");

            _rotAction = new InputAction("Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotAction.AddBinding($"<XRController>{handTag}/deviceRotation");
            _rotAction.AddBinding($"<XRController>{{{handSide}}}/deviceRotation");
            _rotAction.AddBinding($"<{handPrefix}HandXRController>/deviceRotation");

            _posPointerAction = new InputAction("PointerPosition", InputActionType.Value, expectedControlType: "Vector3");
            _posPointerAction.AddBinding($"<XRController>{handTag}/pointerPosition");
            _posPointerAction.AddBinding($"<XRController>{{{handSide}}}/pointerPosition");
            _posPointerAction.AddBinding($"<{handPrefix}HandXRController>/pointerPosition");

            _rotPointerAction = new InputAction("PointerRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotPointerAction.AddBinding($"<XRController>{handTag}/pointerRotation");
            _rotPointerAction.AddBinding($"<XRController>{{{handSide}}}/pointerRotation");
            _rotPointerAction.AddBinding($"<{handPrefix}HandXRController>/pointerRotation");
        }

        /// <summary>
        /// APPELÉ quand le composant devient actif.
        /// Allume les actions, s'abonne au rendu (pour un suivi sans latence) et
        /// initialise la position de référence servant au calcul des vitesses :
        /// sans ça, la toute première frame donnerait une vitesse énorme.
        /// </summary>
        private void OnEnable()
        {
            _gripAction?.Enable();
            _triggerAction?.Enable();
            _posAction?.Enable();
            _rotAction?.Enable();
            _posPointerAction?.Enable();
            _rotPointerAction?.Enable();

            // Rendu : appelé juste avant chaque image → suivi plus fluide que Update seul.
            Application.onBeforeRender += OnBeforeRenderHand;

            ResetVelocityTracking();
            if (!AllHands.Contains(this)) AllHands.Add(this);
        }

        /// <summary>
        /// APPELÉ quand le composant est désactivé ou détruit.
        /// Point critique : si on tient un objet, on le RELÂCHE. Sans cela l'objet
        /// resterait parenté à une main désactivée (il disparaîtrait de la scène).
        /// </summary>
        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderHand;
            AllHands.Remove(this);

            if (_grabbedBody != null)
            {
                ReleaseObject();
            }

            _gripAction?.Disable();
            _triggerAction?.Disable();
            _posAction?.Disable();
            _rotAction?.Disable();
            _posPointerAction?.Disable();
            _rotPointerAction?.Disable();
        }

        /// <summary>
        /// Mise à jour de suivi avant CHAQUE image affichée.
        /// Priorité -20000 : exécutée très tôt dans la phase de rendu pour que la
        /// position de la main corresponde à l'image réellement montrée.
        /// </summary>
        [BeforeRenderOrder(-20000)]
        private void OnBeforeRenderHand()
        {
            UpdateHandTracking();
        }

        // =====================================================================
        // BOUCLE PRINCIPALE
        // =====================================================================

        /// <summary>
        /// Chaque frame, dans cet ordre :
        ///  1. suivi de la manette ;
        ///  2. échantillonnage de la vitesse (à faire AVANT la saisie, sinon le
        ///     déplacement dû à l'objet lui-même polluerait la mesure) ;
        ///  3. lecture du bouton et gestion des fronts (appui → saisie, relâchement → lancer).
        /// </summary>
        private void Update()
        {
            UpdateHandTracking();
            SampleHandVelocity();

            bool isPressed = IsGrabPressed();
            IsGrabInputActive = isPressed;

            // ARBITRAGE : si le joueur est en train de redimensionner un objet
            // (poignée de la main libre serrée), cette main ne doit PAS
            // pouvoir attraper. Sans ce garde-fou, la main qui sert à
            // pincer l'échelle vole l'objet qu'on est en train de mettre à
            // l'échelle, et le geste devient impossible à réaliser.
            // Une main qui tient DÉJÀ un objet n'est pas affectée : le
            // relâchement doit rester possible à tout moment.
            bool canGrab = !VRInputLock.GrabSuppressed || _grabbedBody != null;

            // FRONT MONTANT : appui -> on attrape si main libre.
            if (isPressed && !_wasGrabPressed && canGrab)
            {
                if (_grabbedBody == null) TryGrabClosestObject();
            }
            // FRONT DESCENDANT : le bouton vient d'être relâché → on lance.
            // Toujours autorisé, même en mode échelle : sinon on ne pourrait
            // jamais se débarrasser d'un objet.
            else if (!isPressed && _wasGrabPressed)
            {
                if (_grabbedBody != null)
                {
                    ReleaseObject();
                }
            }

            // Retour continu si l'objet tenu requiert une vibration continue (ex: moteur, outil actif)
            if (enableHaptics && _grabbedBody != null && _heldHaptic != null && _heldHaptic.continuousHeldIntensity > 0f)
            {
                TriggerHaptic(_heldHaptic.continuousHeldIntensity, Time.deltaTime * 1.5f, 0);
            }

            _wasGrabPressed = isPressed;
        }

        // =====================================================================
        // SUIVI 6-DOF — 4 NIVEAUX DE LECTURE
        // =====================================================================

        /// <summary>
        /// Écrit la pose réelle de la manette dans le Transform de ce GameObject.
        ///
        /// Les 4 niveaux sont essayés dans l'ordre (le premier qui renvoie une
        /// position/rotation non nulle gagne) car selon le casque, le runtime et le
        /// mode (Play / Editor sans casque), l'API disponible change :
        ///
        ///  1. XRNodeStates      → API moderne, la plus fiable sous OpenXR
        ///  2. Input System Devices → XRController / TrackedDevice
        ///  3. InputDevices legacy → si TryGetFeatureValue est disponible
        ///  4. Input Actions        → liaisons par chemin (<XRController>...)
        ///
        /// ÉCRITURE EN LOCAL : la manette est enfant de l'XR Origin, qui peut être
        /// tourné (snap turn) ou déplacé (téléportation). Écrire la pose en local
        /// garde la main correctement à sa place dans ce repère.
        /// </summary>
        private void UpdateHandTracking()
        {
            Vector3 pos;
            Quaternion rot;

            // 0. HAND TRACKING : si la main réelle est suivie, c'est ELLE qui fait foi.
            // Sans ça, les nœuds de manette renvoient la dernière pose connue de la
            // manette posée sur la table : la sphère de saisie, l'arc de téléportation
            // et le menu poignet restaient ancrés au bureau.
            // Conversion via le repère de tracking (parent de la caméra) et non via le parent
            // de la manette : dans XROrigin_Menu les manettes et la caméra n'ont pas le même parent.
            if (VRHandGestures.TryGetPalmPose(handNode, out Pose palm))
            {
                Transform space = VRHandGestures.TrackingSpace;
                if (space != null)
                {
                    transform.SetPositionAndRotation(space.TransformPoint(palm.position), space.rotation * palm.rotation);
                }
                else
                {
                    transform.localPosition = palm.position;
                    transform.localRotation = palm.rotation;
                }
                return;
            }

            // Système de suivi à 4 couches prioritaires :
            // 1. XRNodeStates (Subsystem natif bas niveau OpenXR)
            // 2. New Input System Devices (XRController / TrackedDevice)
            // 3. InputDevices legacy XR (si TryGetFeatureValue disponible)
            // 4. Input Actions (Bindings par path)
            if (TryGetPoseFromNodeStates(out pos, out rot) ||
                TryGetPoseFromInputSystemDevices(out pos, out rot) ||
                TryGetPoseFromInputDevice(out pos, out rot) ||
                TryGetPoseFromInputActions(out pos, out rot))
            {
                if (pos != Vector3.zero)
                {
                    transform.localPosition = pos;
                }
                if (rot != Quaternion.identity)
                {
                    transform.localRotation = rot;
                }
            }
#if UNITY_EDITOR
            else
            {
                // Aucun casque détecté → on simule une main à l'écran pour pouvoir
                // tester la saisie / le lancer à la souris dans l'éditeur.
                HandleEditorSimulation();
            }
#endif
        }

        /// <summary>
        /// NIVEAU 1 : parcourt les XRNodeState et cherche la pose du nœud de cette
        /// main (RightHand / LeftHand). Chemin recommandé sous OpenXR.
        /// </summary>
        private bool TryGetPoseFromNodeStates(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            _nodeStates.Clear();
            InputTracking.GetNodeStates(_nodeStates);

            for (int i = 0; i < _nodeStates.Count; i++)
            {
                var state = _nodeStates[i];
                if (state.nodeType == handNode)
                {
                    bool hasPos = state.TryGetPosition(out pos);
                    bool hasRot = state.TryGetRotation(out rot);
                    if ((hasPos && pos != Vector3.zero) || (hasRot && rot != Quaternion.identity))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// NIVEAU 2 : parcourt les périphériques du New Input System et lit les
        /// contrôles de pose. On retient « devicePosition » (poignée) et on tente
        /// « pointerPosition » en secours — certains pilotes ne fournissent que l'un.
        /// </summary>
        private bool TryGetPoseFromInputSystemDevices(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            bool isLeft = handNode == XRNode.LeftHand;

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!dev.added) continue;

                // On ne regarde que le périphérique de la bonne main.
                if (!DeviceMatchesHand(dev, isLeft)) continue;

                // ⚠️ Lecteur SÛR : GetChildControl lèverait une exception sur
                // les périphériques qui n'ont pas « devicePosition » (le casque,
                // certains claviers…), ce qui interrompait le suivi de la main.
                var posControl = VRInputControls.FindAny<UnityEngine.InputSystem.Controls.Vector3Control>(
                    dev, "devicePosition", "pointerPosition", "position", "centerEyePosition");
                var rotControl = VRInputControls.FindAny<UnityEngine.InputSystem.Controls.QuaternionControl>(
                    dev, "deviceRotation", "pointerRotation", "rotation", "centerEyeRotation");

                if (posControl != null)
                {
                    Vector3 p = posControl.ReadValue();
                    if (p != Vector3.zero) pos = p;
                }
                if (rotControl != null)
                {
                    Quaternion r = rotControl.ReadValue();
                    if (r != Quaternion.identity) rot = r;
                }

                if (pos != Vector3.zero || rot != Quaternion.identity)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// NIVEAU 3 : ancienne API Unity XR. Si le nœud de main ne donne pas de
        /// périphérique valide, on élargit la recherche aux contrôleurs du bon côté.
        /// </summary>
        private bool TryGetPoseFromInputDevice(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

            XRInputDevice device = VRStickReader.GetDevice(handNode);
            if (device.isValid)
            {
                bool hasP = device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
                bool hasR = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
                if ((hasP && pos != Vector3.zero) || (hasR && rot != Quaternion.identity))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// NIVEAU 4 (dernier recours) : lecture par les Input Actions créées dans
        /// Awake, avec repli de « devicePose » vers « pointerPose ».
        /// </summary>
        private bool TryGetPoseFromInputActions(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

            if (_posAction != null && _posAction.enabled)
            {
                Vector3 p = _posAction.ReadValue<Vector3>();
                if (p != Vector3.zero) pos = p;
            }
            if (pos == Vector3.zero && _posPointerAction != null && _posPointerAction.enabled)
            {
                Vector3 p = _posPointerAction.ReadValue<Vector3>();
                if (p != Vector3.zero) pos = p;
            }

            if (_rotAction != null && _rotAction.enabled)
            {
                Quaternion r = _rotAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) rot = r;
            }
            if (rot == Quaternion.identity && _rotPointerAction != null && _rotPointerAction.enabled)
            {
                Quaternion r = _rotPointerAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) rot = r;
            }

            return pos != Vector3.zero || rot != Quaternion.identity;
        }

        // =====================================================================
        // MESURE DE LA VITESSE DE LA MAIN (pour le lancer)
        // =====================================================================

        /// <summary>
        /// Calcule et mémorise la vitesse linéaire ET angulaire de la main pour
        /// cette frame, dans des files de 8 échantillons.
        ///
        /// - Vitesse linéaire : (position actuelle − position précédente) / dt.
        /// - Vitesse angulaire : on passe par le quaternion d'écart
        ///   delta = rotation actuelle × inverse(rotation précédente), puis on
        ///   le convertit en axe + angle (ToAngleAxis). L'angle est ramené dans
        ///   [-180°, 180°] car ToAngleAxis peut renvoyer un angle équivalent
        ///   mais faux (ex. 350° au lieu de -10°) — sans cette correction, le
        ///   lancer vrillerait dans le mauvais sens.
        ///
        /// Le moyennage sur 8 frames sert à lisser le bruit du tracking : sans ça,
        /// le lancer serait haché et amplifié par les micro-tremblements.
        ///
        /// ⚠️ REPÈRE DE TRACKING : la vitesse est mesurée en local du Camera Offset
        /// puis reconvertie en monde. Mesurée directement en monde, une téléportation
        /// (saut de plusieurs mètres en UNE frame) devenait une vitesse de plusieurs
        /// centaines de m/s : un objet lâché juste après partait comme un boulet.
        /// Conséquence assumée : la vitesse de déplacement du joueur n'est pas ajoutée
        /// au lancer.
        /// </summary>
        private void SampleHandVelocity()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return; // dt = 0 → division impossible, on ignore la frame.

            Transform space = VRHandGestures.TrackingSpace;
            if (space != _lastTrackingSpace)
            {
                // Nouveau rig (changement de scène) : l'ancienne référence n'a plus de sens.
                ResetVelocityTracking();
                return;
            }

            GetTrackingPose(space, out Vector3 pos, out Quaternion rot);

            Vector3 localVelocity = (pos - _lastTrackingPos) / dt;
            if (localVelocity.sqrMagnitude > MaxSampleSpeed * MaxSampleSpeed)
            {
                // Saut de tracking : on repart de zéro plutôt que de polluer la moyenne.
                ResetVelocityTracking();
                return;
            }

            Quaternion deltaRot = rot * Quaternion.Inverse(_lastTrackingRot);
            deltaRot.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
            if (angleInDegrees > 180f) angleInDegrees -= 360f;
            Vector3 localAngularVelocity = (angleInDegrees * Mathf.Deg2Rad / dt) * rotationAxis.normalized;

            // Retour en monde avec l'orientation ACTUELLE du rig (gère le snap turn).
            Vector3 velocity = space != null ? space.TransformVector(localVelocity) : localVelocity;
            Vector3 angularVelocity = space != null ? space.rotation * localAngularVelocity : localAngularVelocity;

            // File circulaire : on retire le plus ancien, on ajoute le nouveau.
            if (_velocitySamples.Count >= VelocitySampleCount) _velocitySamples.Dequeue();
            _velocitySamples.Enqueue(velocity);

            if (_angularVelocitySamples.Count >= VelocitySampleCount) _angularVelocitySamples.Dequeue();
            _angularVelocitySamples.Enqueue(angularVelocity);

            _lastTrackingPos = pos;
            _lastTrackingRot = rot;
        }

        /// <summary>Pose de la main exprimée dans le repère de tracking (ou en monde si aucun repère).</summary>
        private void GetTrackingPose(Transform space, out Vector3 pos, out Quaternion rot)
        {
            if (space != null)
            {
                pos = space.InverseTransformPoint(transform.position);
                rot = Quaternion.Inverse(space.rotation) * transform.rotation;
            }
            else
            {
                pos = transform.position;
                rot = transform.rotation;
            }
        }

        /// <summary>
        /// Vide l'historique des vitesses et reprend la pose actuelle comme référence.
        /// Public : à appeler après tout déplacement brusque du rig si besoin.
        /// </summary>
        public void ResetVelocityTracking()
        {
            _velocitySamples.Clear();
            _angularVelocitySamples.Clear();
            _lastTrackingSpace = VRHandGestures.TrackingSpace;
            GetTrackingPose(_lastTrackingSpace, out _lastTrackingPos, out _lastTrackingRot);
        }

        // =====================================================================
        // LECTURE DU BOUTON DE SAISIE (3 NIVEAUX)
        // =====================================================================

        /// <summary>
        /// Vrai si le grip OU le trigger est enfoncé. Trois niveaux de lecture,
        /// du plus fiable au plus large, car selon le pilote les Returning
        /// touches analogiques, booléennes ou les deux peuvent être présentes.
        /// </summary>
        private bool IsGrabPressed()
        {
            // 1. XRInputDevice legacy
            XRInputDevice device = VRStickReader.GetDevice(handNode);
            if (device.isValid)
            {
                // Touches analogiques (grip / trigger > 0,5)…
                if (device.TryGetFeatureValue(CommonUsages.grip, out float gripVal) && gripVal > 0.5f) return true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f) return true;
                // …puis touches numériques, si le pilote ne fournit pas d'analogique.
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn) return true;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn) return true;
            }

            // 2. Input Actions
            if (_gripAction != null && _gripAction.enabled && _gripAction.ReadValue<float>() > 0.5f) return true;
            if (_triggerAction != null && _triggerAction.enabled && _triggerAction.ReadValue<float>() > 0.5f) return true;

            // 3. New Input System Devices directs
            bool isLeft = handNode == XRNode.LeftHand;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!DeviceMatchesHand(dev, isLeft)) continue;

                var gripControl = VRInputControls.Find<UnityEngine.InputSystem.Controls.AxisControl>(dev, "grip");
                if (gripControl != null && gripControl.ReadValue() > 0.5f) return true;

                var triggerControl = VRInputControls.Find<UnityEngine.InputSystem.Controls.AxisControl>(dev, "trigger");
                if (triggerControl != null && triggerControl.ReadValue() > 0.5f) return true;

                var gripBtn = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(dev, "gripPressed");
                if (gripBtn != null && gripBtn.isPressed) return true;

                var trigBtn = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(dev, "triggerPressed");
                if (trigBtn != null && trigBtn.isPressed) return true;
            }

            // 4. Hand Tracking pur : poing fermé ou pincement pouce-index (avec hystérésis)
            if (VRHandGestures.IsGrabGesture(handNode, _wasGrabPressed)) return true;

#if UNITY_EDITOR
            // Simulation pour l'éditeur : souris (bouton droit = main droite,
            // gauche = main gauche). Uniquement si aucun casque n'est actif.
            // ⚠️ ESPACE n'est PLUS utilisé ici : il servait de bouton de saisie
            // pour les DEUX mains, et c'est aussi le bouton A (saut) : le
            // joueur sautait à chaque fois qu'il essayait d'attraper un objet.
            if (VRDeviceState.EditorFallbackAllowed)
            {
                if (Mouse.current != null)
                {
                    bool isRight = handNode == XRNode.RightHand;
                    if (isRight && Mouse.current.rightButton.isPressed) return true;
                    if (!isRight && Mouse.current.leftButton.isPressed) return true;
                }
            }
#endif

            return false;
        }

        // =====================================================================
        // SAISIE
        // =====================================================================

        /// <summary>
        /// Cherche l'objet attrapable le plus proche de la main.
        ///
        /// On utilise Physics.OverlapSphereNonAlloc autour de la main, puis on ne
        /// garde que les colliders ayant un Rigidbody, et on retient celui dont le
        /// point le plus proche est le plus proche de la main.
        /// On compare des distances AU CARRÉ (sqrMagnitude) : pas de racine carrée
        /// inutile, c'est une optimisation classique sur du code exécuté à chaque frame.
        ///
        /// ⚠️ Les objets DÉJÀ TENUS par une autre main sont EXCLUS du classement
        /// (et non plus un motif d'abandon de la saisie). Avant, si l'objet
        /// tenu par l'autre main passait dans la sphère de préhension, la
        /// main abandonnait TOUTE sa saisie : elle ne pouvait plus rien
        /// attraper tant que l'autre main tenait son objet.
        /// </summary>
        private Rigidbody FindClosestGrabbable()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, _overlapResults);
            Rigidbody closest = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null || col.transform == transform) continue;

                Rigidbody rb = col.attachedRigidbody; // Ignorer les colliders sans corps physique.
                if (rb == null) continue;

                // Déjà tenu par une main : on l'ignore et on continue à
                // chercher un autre objet attrapable.
                if (IsHeldByAnyHand(rb)) continue;

                float distSq = (col.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closest = rb;
                }
            }

            return closest;
        }

        /// <summary>
        /// L'objet est-il déjà tenu par l'une des mains ? (Un objet ne peut pas
        /// être dans deux mains à la fois.)
        /// </summary>
        private static bool IsHeldByAnyHand(Rigidbody body)
        {
            if (body == null) return false;

            for (int i = 0; i < AllHands.Count; i++)
            {
                VRGrabHand hand = AllHands[i];
                if (hand != null && hand._grabbedBody == body) return true;
            }
            return false;
        }

        /// <summary>
        /// Attrape l'objet le plus proche et l'attache à la main.
        ///
        /// Étapes :
        ///  1. Rien à portée de main → on sort.
        ///  2. Les objets déjà tenus par l'autre main sont ignorés (ils ne sont
        ///     plus dans le classement : voir FindClosestGrabbable).
        ///  3. On mémorise le parent d'origine (pour pouvoir le restaurer).
        ///  4. On rend le Rigidbody CINÉMATIQUE et on coupe la gravité : l'objet
        ///     ne tombe plus pendant qu'il est en main.
        ///  5. On le PARENTE à la main (le paramètre true conserve sa position
        ///     mondiale : pas de saut à la saisie).
        ///  6. On vide l'historique de vitesses : le lancer ne doit pas prendre en
        ///     compte les frames précédant la saisie.
        /// </summary>
private void TryGrabClosestObject()
        {
            GrabBody(FindClosestGrabbable());
        }

        /// <summary>
        /// Saisit un objet précis (ex. rendu par la bulle du menu poignet). L'objet reste
        /// en main jusqu'au prochain relâchement du geste de saisie.
        /// </summary>
        public void GrabBody(Rigidbody target)
        {
            if (target == null || _grabbedBody != null) return;

            // TRANSFERT INTERDIT : on ne vole pas un objet déjà tenu par l'autre
            // main. Le transfert automatique entrait en conflit direct avec le
            // pincement à deux mains (la main libre réquisitionnait l'objet au
            // lieu de le redimensionner) et supprimait tout contrôle individuel
            // sur l'objet. Pour changer de main : on lâche d'abord, puis on
            // saisit avec l'autre main.
            if (IsHeldByAnyHand(target))
            {
                return;
            }

            _grabbedBody = target;
            _originalParent = _grabbedBody.transform.parent;

            _grabbedBody.isKinematic = true;  // La main pilote désormais l'objet.
            _grabbedBody.useGravity = false;  // Il ne doit plus tomber en main.

            // On coupe l'interpolation le temps de la saisie : la physique n'a plus le rôle
            // de la main, c'est le Transform qui fait foi (recommendation Unity pour
            // un corps cinématique). Valeur d'origine restituée au relâchement.
            _interpolationBeforeGrab = _grabbedBody.interpolation;
            _grabbedBody.interpolation = RigidbodyInterpolation.None;

            _grabbedBody.transform.SetParent(transform, true); // Attache à la main, sans téléportation.

            // ACCROCHE AIMANTÉE : on écrase la position locale conservée ci-dessus pour
            // supprimer l'écart résiduel. Sans cela l'objet reste figé à la distance
            // qu'il avait quand le bouton a été pressé (jusqu'à grabRadius), et le
            // joueur a l'impression que l'objet ne suit pas la manette.
            if (snapToHandOnGrab)
            {
                _grabbedBody.transform.localPosition = gripOffset;
                _grabbedBody.transform.localRotation = Quaternion.identity;
            }

            // ⚠️ SYNC PHYSIQUE : le projet a « Physics.autoSyncTransforms = 0 ».
            // Sans cet appel, le COLLIDER de l'objet saisi reste sur sa
            // ancienne position pendant une frame (ou plus) : l'objet
            // « colle » visuellement mais ne pousse rien, et se fait
            // pousser. On synchronise donc immédiatement.
            Physics.SyncTransforms();

            _velocitySamples.Clear();
            _angularVelocitySamples.Clear();

            // Son de choc ajouté à la première saisie : tout objet manipulé
            // « sonne » ensuite quand il retombe, sans réglage objet par objet.
            if (_grabbedBody.GetComponent<VRSonImpact>() == null)
            {
                _grabbedBody.gameObject.AddComponent<VRSonImpact>();
            }
            VRSons.Jouer(VRSon.Saisie, transform.position, 1f, VRSons.PitchAleatoire());

            // Profil haptique spécifique de l'objet (ou retour par défaut de la manette)
            _heldHaptic = _grabbedBody.GetComponent<VRHapticInteractable>() ?? _grabbedBody.GetComponentInParent<VRHapticInteractable>();
            if (enableHaptics)
            {
                if (_heldHaptic != null)
                {
                    _heldHaptic.PlayFeedback(this, _heldHaptic.onGrab);
                }
                else if (grabHapticIntensity > 0f && grabHapticDuration > 0f)
                {
                    TriggerHaptic(grabHapticIntensity, grabHapticDuration, 2);
                }
            }

            ObjetSaisi?.Invoke(this, _grabbedBody);
        }

        /// <summary>
        /// Relâche l'objet : il retrouve son parent d'origine, la physique, et
        /// repart avec la vitesse moyenne de la main (le « lancer »).
        ///
        /// Le paramètre applyThrowVelocity permet de relâcher SANS lancer — c'est
        /// utilisé quand un autre script prend le contrôle (respawn, changement
        /// de main) et qu'un petit coup de vitesse serait unwelcome.
        /// </summary>
        public void ReleaseObject(bool applyThrowVelocity = true)
        {
            if (_grabbedBody == null) return;

            // ⚠️ AVANT DE REDEVENIR DYNAMIQUE : on fige la pose de l'objet là
            // où il se trouve réellement (sous la main). Le projet a
            // « Physics.autoSyncTransforms = 0 » : sur un corps non cinématique,
            // la physique réimpose sa propre pose au frame suivant, et
            // l'objet « repartait » à l'endroit d'où il avait été pris au
            // lieu de rester sous la main.
            Vector3 releasePosition = _grabbedBody.transform.position;
            Quaternion releaseRotation = _grabbedBody.transform.rotation;

            // Restauration de la position dans la hiérarchie et de la physique.
            _grabbedBody.transform.SetParent(_originalParent, true);
            _grabbedBody.isKinematic = false;
            _grabbedBody.useGravity = true;

            // La pose est authoritative : on l'écrit sur le Rigidbody (et pas
            // seulement sur le Transform) pour que la physique parte de là.
            _grabbedBody.position = releasePosition;
            _grabbedBody.rotation = releaseRotation;
            Physics.SyncTransforms();

            // L'objet redevient un corps physique : on lui restitue son interpolation
            // d'origine pour que sa chute et son lancer restent lisses.
            _grabbedBody.interpolation = _interpolationBeforeGrab;

            if (applyThrowVelocity)
            {
                // Vitesse linéaire moyenne sur les 8 dernières frames.
                Vector3 avgVel = Vector3.zero;
                foreach (var v in _velocitySamples) avgVel += v;
                if (_velocitySamples.Count > 0) avgVel /= _velocitySamples.Count;

                // Vitesse angulaire moyenne sur les 8 dernières frames.
                Vector3 avgAngVel = Vector3.zero;
                foreach (var av in _angularVelocitySamples) avgAngVel += av;
                if (_angularVelocitySamples.Count > 0) avgAngVel /= _angularVelocitySamples.Count;

                // Unity 6 a renommé « velocity » en « linearVelocity » : les deux
                // lignes sont compilées selon la version du moteur.
#if UNITY_6000_0_OR_NEWER
                _grabbedBody.linearVelocity = avgVel * throwForceMultiplier;
#else
                _grabbedBody.velocity = avgVel * throwForceMultiplier;
#endif
                _grabbedBody.angularVelocity = avgAngVel;
            }

            VRHapticInteractable releasedHaptic = _heldHaptic;
            Rigidbody releasedBody = _grabbedBody;
            _grabbedBody = null;
            _originalParent = null;
            _heldHaptic = null;

            if (enableHaptics)
            {
                if (releasedHaptic != null)
                {
                    releasedHaptic.PlayFeedback(this, releasedHaptic.onRelease);
                }
                else if (releaseHapticIntensity > 0f && releaseHapticDuration > 0f)
                {
                    TriggerHaptic(releaseHapticIntensity, releaseHapticDuration, 1);
                }
            }

            VRSons.Jouer(VRSon.Lacher, transform.position, 1f, VRSons.PitchAleatoire());
            ObjetLache?.Invoke(this, releasedBody);
        }

        // =====================================================================
        // SIMULATION ÉDITEUR
        // =====================================================================

#if UNITY_EDITOR
        /// <summary>
        /// Utilisé UNIQUEMENT dans l'éditeur, et seulement si aucun casque n'est
        /// détecté : on place la main de façon ergonomique devant la caméra
        /// (à gauche ou à droite, légèrement en avant et en bas) pour pouvoir
        /// tester la saisie et le lancer à la souris sans Quest branché.
        /// </summary>
        private void HandleEditorSimulation()
        {
            // Fallback Éditeur : si aucun casque XR physique n'est branché,
            // positionner la manette de façon ergonomique devant la caméra
            if (VRDeviceState.EditorFallbackAllowed)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float side = (handNode == XRNode.LeftHand) ? -0.22f : 0.22f;
                    Vector3 defaultOffset = new Vector3(side, -0.15f, 0.45f);
                    transform.position = cam.transform.position + cam.transform.TransformDirection(defaultOffset);
                    transform.rotation = cam.transform.rotation;
                }
            }
        }
#endif

        /// <summary>
        /// Déclenche une vibration haptique sur cette manette physique via l'API OpenXR/XR.
        /// </summary>
        /// <param name="intensity">Intensité entre 0 et 1.</param>
        /// <param name="duration">Durée de l'impulsion en secondes.</param>
        /// <param name="priority">Niveau de priorité (un feedback de priorité inférieure n'écrase pas une vibration en cours).</param>
        public void TriggerHaptic(float intensity, float duration, int priority = 1)
        {
            if (!enableHaptics || intensity <= 0f || duration <= 0f) return;

            // Arbitrage par priorité : si une vibration de priorité supérieure est encore active, on refuse
            if (Time.time < _hapticEndTime && priority < _currentHapticPriority)
            {
                return;
            }

            _currentHapticPriority = priority;
            _hapticEndTime = Time.time + duration;

            // 1. Essai direct via l'API InputDevice (Standard OpenXR)
            XRInputDevice device = VRStickReader.GetDevice(handNode);
            if (device.isValid && device.SendHapticImpulse(0, Mathf.Clamp01(intensity), duration))
            {
                return;
            }

            // 2. Repli de recherche par caractéristiques si le mapping XRNode est incomplet
            s_HapticDevices.Clear();
            InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                (handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
            InputDevices.GetDevicesWithCharacteristics(characteristics, s_HapticDevices);
            for (int i = 0; i < s_HapticDevices.Count; i++)
            {
                if (s_HapticDevices[i].isValid && s_HapticDevices[i].SendHapticImpulse(0, Mathf.Clamp01(intensity), duration))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Indique si un périphérique Input System correspond à la main demandée.
        /// Délègue à <see cref="VRStickReader.DeviceMatchesHand"/> (implémentation unique).
        /// </summary>
        private static bool DeviceMatchesHand(UnityEngine.InputSystem.InputDevice dev, bool isLeft)
            => VRStickReader.DeviceMatchesHand(dev, isLeft);
    }
}