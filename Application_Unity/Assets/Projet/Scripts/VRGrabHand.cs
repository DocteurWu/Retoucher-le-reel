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
        public bool snapToHandOnGrab = true;

        /// <summary>
        /// Décalage local appliqué à l'objet une fois collé dans la main (en mètres,
        /// repère de la main). Utile pour placer l'objet légèrement devant ou au-dessus
        /// du pivot plutôt qu'au centre. Ignoré si <see cref="snapToHandOnGrab"/> est décoché.
        /// </summary>
        [Tooltip("Décalage local de l'objet collé dans la main (mètres)")]
        public Vector3 gripOffset = Vector3.zero;

        // --- État de la saisie ------------------------------------------------

        /// <summary>Objet actuellement tenu par cette main (null si main vide).</summary>
        private Rigidbody _grabbedBody;

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

        // --- Historique des vitesses (pour le lancer) -------------------------

        // Historique des vitesses pour un lancer fluide et naturel

        /// <summary>Nombre d'échantillons conservés (fenêtre de lissage du lancer).</summary>
        private const int VelocitySampleCount = 8;

        /// <summary>Vitesse linéaire de la main sur les 8 dernières frames (m/s).</summary>
        private readonly Queue<Vector3> _velocitySamples = new Queue<Vector3>(VelocitySampleCount);

        /// <summary>Vitesse angulaire de la main sur les 8 dernières frames (rad/s).</summary>
        private readonly Queue<Vector3> _angularVelocitySamples = new Queue<Vector3>(VelocitySampleCount);

        /// <summary>Position mondiale de la main à la frame précédente.</summary>
        private Vector3 _lastWorldPos;

        /// <summary>Rotation mondiale de la main à la frame précédente.</summary>
        private Quaternion _lastWorldRot;

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

            _lastWorldPos = transform.position;
            _lastWorldRot = transform.rotation;
        }

        /// <summary>
        /// APPELÉ quand le composant est désactivé ou détruit.
        /// Point critique : si on tient un objet, on le RELÂCHE. Sans cela l'objet
        /// resterait parenté à une main désactivée (il disparaîtrait de la scène).
        /// </summary>
        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderHand;

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

            // FRONT MONTANT : le bouton vient d'être enfoncé → on attrape.
            if (isPressed && !_wasGrabPressed)
            {
                TryGrabClosestObject();
            }
            // FRONT DESCENDANT : le bouton vient d'être relâché → on lance.
            else if (!isPressed && _wasGrabPressed)
            {
                if (_grabbedBody != null)
                {
                    ReleaseObject();
                }
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

                var posControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("devicePosition") ??
                                 dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("pointerPosition");
                var rotControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("deviceRotation") ??
                                 dev.GetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointerRotation");

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

            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (!device.isValid)
            {
                var inputDevices = new List<XRInputDevice>();
                InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                    (handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
                InputDevices.GetDevicesWithCharacteristics(characteristics, inputDevices);
                if (inputDevices.Count > 0)
                {
                    device = inputDevices[0];
                }
            }

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
        /// </summary>
        private void SampleHandVelocity()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return; // dt = 0 → division impossible, on ignore la frame.

            Vector3 velocity = (transform.position - _lastWorldPos) / dt;
            Quaternion deltaRot = transform.rotation * Quaternion.Inverse(_lastWorldRot);
            deltaRot.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
            if (angleInDegrees > 180f) angleInDegrees -= 360f;
            Vector3 angularVelocity = (angleInDegrees * Mathf.Deg2Rad / dt) * rotationAxis.normalized;

            // File circulaire : on retire le plus ancien, on ajoute le nouveau.
            if (_velocitySamples.Count >= VelocitySampleCount) _velocitySamples.Dequeue();
            _velocitySamples.Enqueue(velocity);

            if (_angularVelocitySamples.Count >= VelocitySampleCount) _angularVelocitySamples.Dequeue();
            _angularVelocitySamples.Enqueue(angularVelocity);

            _lastWorldPos = transform.position;
            _lastWorldRot = transform.rotation;
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
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (!device.isValid)
            {
                var inputDevices = new List<XRInputDevice>();
                InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                    (handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
                InputDevices.GetDevicesWithCharacteristics(characteristics, inputDevices);
                if (inputDevices.Count > 0)
                {
                    device = inputDevices[0];
                }
            }

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

                var gripControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("grip");
                if (gripControl != null && gripControl.ReadValue() > 0.5f) return true;

                var triggerControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                if (triggerControl != null && triggerControl.ReadValue() > 0.5f) return true;

                var gripBtn = dev.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("gripPressed");
                if (gripBtn != null && gripBtn.isPressed) return true;

                var trigBtn = dev.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed");
                if (trigBtn != null && trigBtn.isPressed) return true;
            }

#if UNITY_EDITOR
            // Simulation pour l'éditeur : souris (bouton droit = main droite,
            // gauche = main gauche) ou Espace. Uniquement si aucun casque n'est actif.
            if (!XRSettings.isDeviceActive)
            {
                if (Mouse.current != null)
                {
                    bool isRight = handNode == XRNode.RightHand;
                    if (isRight && Mouse.current.rightButton.isPressed) return true;
                    if (!isRight && Mouse.current.leftButton.isPressed) return true;
                }
                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
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
        /// Attrape l'objet le plus proche et l'attache à la main.
        ///
        /// Étapes :
        ///  1. Rien à portée de main → on sort.
        ///  2. Si l'objet est déjà tenu par l'AUTRE main, on l'en détache d'abord
        ///     (sans vitesse de lancer) : un objet ne peut pas être dans deux mains.
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
            Rigidbody target = FindClosestGrabbable();
            if (target == null) return;

            VRGrabHand[] allHands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
            foreach (var otherHand in allHands)
            {
                if (otherHand != this && otherHand._grabbedBody == target)
                {
                    // applyThrowVelocity: false → pas de projection de l'objet.
                    otherHand.ReleaseObject(applyThrowVelocity: false);
                }
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

            _velocitySamples.Clear();
            _angularVelocitySamples.Clear();
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

            // Restauration de la position dans la hiérarchie et de la physique.
            _grabbedBody.transform.SetParent(_originalParent, true);
            _grabbedBody.isKinematic = false;
            _grabbedBody.useGravity = true;

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

            _grabbedBody = null;
            _originalParent = null;
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
            if (!XRSettings.isDeviceActive)
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
        /// Indique si un périphérique Input System correspond à la main demandée.
        /// Deux critères testés, dans l'ordre :
        ///   1. les « usages » déclarés du périphérique contiennent LeftHand / RightHand ;
        ///   2. à défaut, son NOM contient « left » / « right ».
        /// Le 2e critère est un filet de sécurité pour les pilotes qui ne déclarent
        /// pas les usages.
        /// </summary>
        private static bool DeviceMatchesHand(UnityEngine.InputSystem.InputDevice dev, bool isLeft)
        {
            if (dev == null || !dev.added) return false;

            string targetUsage = isLeft ? "LeftHand" : "RightHand";
            string targetName = isLeft ? "left" : "right";

            var usages = dev.usages;
            for (int j = 0; j < usages.Count; j++)
            {
                if (usages[j].ToString().IndexOf(targetUsage, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (dev.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }
    }
}