// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRTeleportation.cs — TÉLÉPORTATION BALISTIQUE (arc parabolique)
//
//  RÔLE
//    Pousser le stick gauche vers l'avant affiche un arc et un réticule ; le
//    relâcher téléporte le joueur sur la surface visée. Geste à deux temps
//    (viser → relâcher) : c'est le standard VR, il évite les téléportations
//    involontaires.
//
//  À POSER SUR : l'XR Origin. Câbler rayOrigin (la manette qui vise),
//  headCamera, xrOriginTransform.
//
//  ⚠️ Le masque de layers de la téléportation est le layer 6 « Ground » UNIQUEMENT.
//     Avec « Everything », le joueur se téléportait sur la manette qu'il tenait
//     ou sur un objet sans surface sous les pieds.
//
//  ⚠️ Le joueur est placé à « cible MOINS l'offset de tête » : sans cette
//     compensation, il atterrit décalé d'autant que sa tête l'était.
//
//  ⚠️ Le mode est piloté par VRLocomotionSettings : en mode « Mouvement Continu »,
//     ce script ne fait rien (le stick gauche déplace le joueur).
//
//  RÉFÉRENCES : voir README.md (sections 3 et 4)
// =====================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>Geste de téléportation en mode mains.</summary>
    public enum HandTeleportGesture
    {
        /// <summary>Signe du pistolet main gauche + visée stable (2 s).</summary>
        PistoletGauche,
        /// <summary>Microgeste Meta : poing droit, tap du pouce sur l'index pour viser puis confirmer.</summary>
        MicrogesteDroit,
        /// <summary>Les deux gestes sont actifs.</summary>
        LesDeux
    }

    /// <summary>
    /// TÉLÉPORTATION VR par trajectoire balistique (arc parabolique).
    ///
    /// GESTE : pousser le joystick vers l'avant Affiche l'arc de prévisualisation
    /// et une cible au sol. Relâcher le joystick téléporte le joueur vers la cible.
    /// Ce geste à deux temps (viser → relâcher) est le standard VR : il évite les
    /// téléportations involontaires.
    ///
    /// FONCTIONNEMENT DE L'ARC : on simule une balle lancée depuis la manette
    /// (rayOrigin) vers l'avant, avec la gravité du monde. À chaque pas on lance un
    /// petit rayon vers le point suivant ; au premier impact on s'arrête et on
    /// vérifie que la surface est assez plate pour s'y poser.
    ///
    /// DÉTAIL CRITIQUE — COMPENSATION DE TÊTE : on ne déplace pas l'XR Origin à la
    /// cible, mais à « cible MOINS l'offset de la tête ». Sinon le joueur atterrit
    /// décalé d'autant que sa tête était décalée du centre de l'Origin. Voir
    /// PerformTeleport().
    ///
    /// À poser sur l'XR Origin. Références à câbler :
    ///   rayOrigin        = la manette (gauche par défaut) — auto-détecté si vide
    ///   headCamera       = la caméra du casque
    ///   xrOriginTransform= l'XR Origin à déplacer
    ///   arcRenderer      = un LineRenderer (créé automatiquement si absent)
    ///   reticleTransform = le disque au sol (créé automatiquement si absent)
    /// </summary>
    public class VRTeleportation : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // RÉFÉRENCES DE SCÈNE
        // ---------------------------------------------------------------------

        [Header("Références")]

        /// <summary>
        /// Origine du rayon de visée : la main qui lance la parabole (gauche par défaut).
        /// Si laissé vide, on cherche automatiquement un VRGrabHand du bon côté.
        /// </summary>
        [Tooltip("Origine du rayon de visée (généralement la manette gauche)")]
        public Transform rayOrigin;

        /// <summary>Caméra du casque (tête du joueur). Si vide → Camera.main.</summary>
        [Tooltip("La caméra VR (tête du joueur)")]
        public Camera headCamera;

        /// <summary>Transform de l'XR Origin à déplacer lors de la téléportation.</summary>
        [Tooltip("Le repère XR Origin à déplacer")]
        public Transform xrOriginTransform;

        // ---------------------------------------------------------------------
        // CONFIGURATION ENTRÉE
        // ---------------------------------------------------------------------

        [Header("Configuration Entrée")]

        /// <summary>Manette utilisée pour viser (par défaut la gauche).</summary>
        public XRNode handNode = XRNode.LeftHand;

        /// <summary>Seuil d'activation : stick poussé au-delà de cette valeur → on vise.</summary>
        [Tooltip("Seuil de déclenchement du joystick (plus haut = moins sensible aux fausses manips)")]
        public float activationThreshold = 0.75f;

        /// <summary>Seuil de relâchement : stick ramené sous cette valeur → on téléporte.</summary>
        [Tooltip("Seuil de relâchement du joystick pour valider")]
        public float releaseThreshold = 0.35f;

        /// <summary>Durée minimale de visée (secondes) avant de pouvoir valider la téléportation.</summary>
        [Tooltip("Temps de maintien minimum de la visée pour éviter les à-coups")]
        public float minAimDuration = 0.18f;

        private float _aimDuration = 0f;

        // ---------------------------------------------------------------------
        // CALIBRAGE DE LA VISÉE
        // ---------------------------------------------------------------------

        [Header("Calibrage de la visée")]

        /// <summary>
        /// Viser avec la tête (caméra) au lieu de la main.
        ///
        /// Par défaut on vise avec la main, plus naturel en VR, mais cela dépend
        /// directement de l'orientation rapportée pour la manette. Si cette orientation
        /// n'est pas celle qu'on attend (pointe vers le haut, décalée d'un angle...), la
        /// parabole part de travers et le joueur atterrit très loin. Cocher cette case
        /// supprime complètement cette dépendance : on vise droit devant soi.
        /// </summary>
        [Tooltip("Viser avec la caméra du casque au lieu de la manette")]
        public bool aimFromHead = false;

        /// <summary>
        /// Correction d'orientation de la visée, EN DEGRÉS, appliquée dans le repère
        /// monde (X = tangage, Y = lacet, Z = roulis).
        ///
        /// C'est le réglage de calibration à utiliser quand la manette n'est pas
        /// orientée comme on l'attend. Attention au signe : en Unity une rotation X
        /// positive incline l'axe « avant » vers le BAS. Une manette pointée vers le
        /// haut se corrige donc avec une valeur X POSITIVE (+45 si elle pointe 45° plus
        /// haut que prévu). Valeur par défaut 0 = aucune correction, c'est-à-dire
        /// l'axe « avant » brut du transform de la main.
        ///
        /// ASTUCE : à régler en Play Mode directement dans l'Inspector (le champ est
        /// public), en poussant devant soi jusqu'à ce que l'arc parte droit, puis
        /// recopier les valeurs dans la scène.
        /// </summary>
        [Tooltip("Correction de l'axe de visée, en degrés (X tangage, Y lacet, Z roulis)")]
        public Vector3 aimEulerCorrection = Vector3.zero;

        /// <summary>
        /// Distance maximale de téléportation, en mètres, mesurée depuis la main.
        ///
        /// Filet de sécurité : si l'axe de visée est mal calibré, la parabole part très
        /// haut et retombe très loin. Au-delà de cette distance la cible est refusée
        /// (arc rouge) plutôt que d'envoyer le joueur à l'autre bout du niveau.
        /// </summary>
        [Tooltip("Distance maximale de téléportation (mètres)")]
        public float maxTeleportDistance = 10f;

        // ---------------------------------------------------------------------
        // PARAMÈTRES BALISTIQUES
        // ---------------------------------------------------------------------

        [Header("Paramètres Balistiques")]

        /// <summary>
        /// Vitesse initiale de la balle simulée, en m/s. Plus c'est fort, plus la
        /// parabole va loin et plat. 9 m/s ≈ 8-10 m de portée sur un sol horizontal.
        /// </summary>
        public float initialVelocity = 9.0f;

        /// <summary>Nombre de points simulés = nombre de segments de l'arc affiché.</summary>
        public int maxArcPoints = 35;

        /// <summary>
        /// Durée d'un pas de simulation, en secondes.
        /// 35 points × 0.04 s ≈ 1.4 s de vol : c'est la distance affichée max.
        /// Plus petit = arc plus précis mais plus de calculs.
        /// </summary>
        public float timeStep = 0.04f;

        /// <summary>
        /// Layers traversés par les rayons de détection du sol.
        ///
        /// IMPORTANT : ne laissez PAS « Everything ». Avec ~0, le rayon
        /// s'arrête sur le premier objet碰 rencontré — y compris le socle,
        /// une machine, un objet tenu en main (qui voyage avec la manette) ou
        /// le cylindre : le joueur peut se téléporter sur un meuble, ou pire,
        /// là où il a aucune surface sous les pieds.
        ///
        /// Default = layer 6 « Ground » : seules les surfaces réellement
        /// marchables sont acceptées. Les objets à saisir vont sur le layer
        /// « Grabbable », qui est exclu du masque.
        /// </summary>
        [Tooltip("Layers où l'on peut se téléporter (mettre seulement le sol, PAS Everything)")]
        public LayerMask teleportLayerMask = 1 << 6; // Ground

        [Tooltip("Layers qui bloquent l'arc de téléportation sans permettre d'y atterrir (murs, obstacles).")]
        public LayerMask blockingLayerMask = (1 << 0) | (1 << 8); // Default + Environment

        /// <summary>Tampon des impacts d'un segment de l'arc (pas d'allocation pendant la visée).</summary>
        private static readonly RaycastHit[] s_impacts = new RaycastHit[16];

        /// <summary>
        /// Impact le plus proche sur ce segment, en ignorant le rig lui-même : le calque Default
        /// bloque l'arc, et les mains, les manettes ou l'objet tenu (enfants du rig) y sont souvent.
        /// Sans ce filtre, l'arc s'arrêtait dans la main qui vise.
        /// </summary>
        private bool ImpactHorsDuRig(Vector3 origine, Vector3 direction, float distance, LayerMask masque, out RaycastHit impact)
        {
            impact = default;
            int n = Physics.RaycastNonAlloc(origine, direction, s_impacts, distance, masque, QueryTriggerInteraction.Ignore);
            float meilleure = float.MaxValue;
            bool trouve = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = s_impacts[i];
                if (h.distance >= meilleure) continue;
                if (xrOriginTransform != null && h.collider.transform.IsChildOf(xrOriginTransform)) continue;
                meilleure = h.distance;
                impact = h;
                trouve = true;
            }
            return trouve;
        }

        /// <summary>
        /// Pente maximale acceptable du sol, en degrés par rapport à l'horizontale.
        /// Au-delà (mur, meuble incliné), la destination est refusée (arc rouge).
        /// </summary>
        public float maxSurfaceSlopeAngle = 45f;

        // ---------------------------------------------------------------------
        // VISUEL
        // ---------------------------------------------------------------------

        [Header("Visuel")]

        /// <summary>LineRenderer qui dessine l'arc. Créé automatiquement si absent.</summary>
        public LineRenderer arcRenderer;

        /// <summary>Disque posé au sol à l'endroit de destination. Créé si absent.</summary>
        public Transform reticleTransform;

        /// <summary>Couleur de l'arc quand la destination est valide.</summary>
        public Color validArcColor = new Color(0.1f, 0.8f, 1.0f, 0.8f);

        /// <summary>Couleur de l'arc quand la destination est refusée.</summary>
        public Color invalidArcColor = new Color(1.0f, 0.2f, 0.2f, 0.4f);

        // --- État interne ----------------------------------------------------

        /// <summary>
        /// Vrai si la dernière frame a trouvé un sol valide au bout de l'arc.
        /// </summary>
        private bool _hasValidTarget;

        /// <summary>Point d'arrivée validé (surface touchée par l'arc).</summary>
        private Vector3 _targetPosition;

        /// <summary>Normale de la surface touchée (sert à poser le réticule à plat).</summary>
        private Vector3 _targetNormal;

        /// <summary>Action de secours pour lire le joystick (2e niveau de secours).</summary>
        private InputAction _thumbstickAction;

        /// <summary>
        /// Vrai entre le push du stick et son relâchement (on est en train de viser).
        /// Exposé publiquement : la locomotion continue s'en sert pour se mettre
        /// en pause, pour que le joueur ne dérive pas pendant qu'il vise.
        /// </summary>
        public static bool IsAiming { get; private set; }

        public static void ResetAiming()
        {
            IsAiming = false;
        }

        /// <summary>
        /// Points de l'arc réutilisés d'une frame à l'autre. La capacité est
        /// préallouée à maxArcPoints pour ne jamais.resize() pendant la visée.
        /// </summary>
        private readonly List<Vector3> _arcPoints = new List<Vector3>(64);

        // =====================================================================
        // INITIALISATION
        // =====================================================================

        /// <summary>
        /// APPELÉ UNE SEULE FOIS au démarrage.
        /// 1. Complète les références manquantes.
        /// 2. Crée l'Input Action du joystick avec plusieurs liaisons (compatibilité casque).
        /// 3. Prépare l'affichage : ligne et réticule.
        /// </summary>
        private void Awake()
        {
            ResetAiming();
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            string handSide = handNode == XRNode.LeftHand ? "leftHand" : "rightHand";
            _thumbstickAction = new InputAction("TeleportThumbstick", InputActionType.Value);
            _thumbstickAction.AddBinding($"<XRController>{handTag}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{handTag}/primary2DAxis");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/primary2DAxis");

            if (blockingLayerMask.value == 0)
            {
                int env = LayerMask.NameToLayer("Environment");
                int envMask = env >= 0 ? (1 << env) : 0;
                blockingLayerMask = (1 << 0) | envMask;
            }

            SetupLineRenderer();
            SetupReticle();
        }

        /// <summary>Active l'Input Action quand le composant est activé.</summary>
        private void OnEnable()
        {
            ResetAiming();
            _thumbstickAction?.Enable();
        }

        /// <summary>
        /// Désactive l'Input Action et range l'affichage quand le composant est désactivé
        /// (sinon l'arc resterait figé à l'écran).
        /// </summary>
        private void OnDisable()
        {
            ResetAiming();
            _thumbstickAction?.Disable();
            SetVisualsActive(false);
        }

        // =====================================================================
        // BOUCLE PRINCIPALE
        // =====================================================================

        /// <summary>
        /// Machine à états de la téléportation, chaque frame :
        ///   REPOS      : le stick n'est pas poussé → rien ne se passe.
        ///   VISÉE      : stick poussé au-delà du seuil → on affiche l'arc.
        ///                pendant ce temps on recalcule l'arc à chaque frame.
        ///   RELÂCHEMENT: stick ramené sous le seuil → on téléporte si la cible est
        ///                valide, puis on range l'affichage.
        /// </summary>
                // [Louai H.] Boucle d actualisation de l arc de visee et detection d impact
        private void Update()
        {
            // Vérification du mode de locomotion configuré dans le menu.
            // En mode MAINS la téléportation est toujours disponible : le déplacement
            // continu (défaut « Smooth ») exige un stick, inexistant sans manette.
            if (VRLocomotionSettings.Mode != LocomotionMode.Teleport && !VRModalityManager.IsHandMode)
            {
                if (IsAiming)
                {
                    IsAiming = false;
                    SetVisualsActive(false);
                }
                return;
            }

            // Auto-recherche du contrôleur si non renseigné
            // (registre VRGrabHand.AllHands : pas de parcours de scène à chaque frame)
            if (rayOrigin == null)
            {
                var hand = VRGrabHand.GetHandForNode(handNode);
                if (hand != null) rayOrigin = hand.transform;
            }

            // En mode MAINS : utiliser la téléportation par visée et geste franc (ignore les sticks fantômes)
            if (VRModalityManager.IsHandMode)
            {
                UpdateHandTrackingTeleport();
                return;
            }

            Vector2 stick = ReadThumbstick();

            // Pousser le stick vers l'avant active la visée
            // (sauf si le stick est réquisitionné par l'échelle de l'objet)
            bool stickReserved = VRInputLock.IsStickReserved(handNode);

            if (!IsAiming && !stickReserved && stick.y > activationThreshold)
            {
                IsAiming = true;
                _aimDuration = 0f;
                SetVisualsActive(true);
            }

            if (IsAiming)
            {
                _aimDuration += Time.deltaTime;
                // On recalcule la parabole en continu : la cible bouge avec le joueur.
                UpdateArcAndTarget();

                // Relâcher le stick (ou le perdre au profit de l'échelle) téléporte.
                if (stick.y < releaseThreshold || stickReserved)
                {
                    // On ne téléporte QUE si la destination a été validée ET maintenue assez longtemps
                    if (_hasValidTarget && !stickReserved && _aimDuration >= minAimDuration)
                    {
                        PerformTeleport(_targetPosition);
                    }

                    IsAiming = false;
                    _aimDuration = 0f;
                    SetVisualsActive(false);
                }
            }
        }

        [Header("Téléportation aux Mains (Hand Tracking)")]
        [Tooltip("Geste de téléportation en mode mains. Microgeste = comme Meta : main droite en poing lâche, "
                 + "le pouce tape sur le côté de l'index une fois pour viser, une seconde fois pour partir.")]
        public HandTeleportGesture handTeleportGesture = HandTeleportGesture.MicrogesteDroit;

        [Tooltip("Microgeste : distance pouce ↔ côté de l'index (m) en dessous de laquelle le « bouton » est appuyé.")]
        [Range(0.01f, 0.04f)] public float microPressDistance = 0.022f;

        [Tooltip("Microgeste : distance (m) au-delà de laquelle le « bouton » est relâché (doit être > appui).")]
        [Range(0.02f, 0.06f)] public float microReleaseDistance = 0.035f;

        [Tooltip("Microgeste : temps (s) pendant lequel on tolère la perte du poing avant d'annuler la visée.")]
        [Range(0.1f, 1.5f)] public float microPoseGrace = 0.4f;

        [Tooltip("Microgeste : glissement latéral du pouce (m) qui vaut un cran de rotation (lu par VRSnapTurn).")]
        [Range(0.005f, 0.04f)] public float microSwipeDistance = 0.015f;

        [Tooltip("Durée de maintien du grab main gauche (secondes) sans objet avant d'activer la visée")]
        [Range(1f, 4f)] public float handGrabActivationDelay = 2.0f;

        [Tooltip("Durée (s) de visée stable avec le pistolet main gauche avant de téléporter (l'arc s'affiche immédiatement)")]
        [Range(0.5f, 6f)] public float handGunConfirmTime = 2.0f;

        [Tooltip("Déplacement max (m) de la cible pendant la confirmation avant de relancer le compteur")]
        public float handGunConfirmRadius = 0.35f;

        private float _gunConfirmTimer;
        private Vector3 _gunConfirmAnchor;
        private bool _gunNeedsRelease;

        private const float HandGunGrace = 0.25f;
        private float _lastGunTime = -999f;
        private Vector3 _gunAimPos;
        private Vector3 _gunAimDir = Vector3.forward;
        private bool _useGunAim;

        private float _handGrabTimer = 0f;

        /// <summary>Geste qui pilote la visée en cours (un seul à la fois).</summary>
        private enum HandAimSource { None, Gun, Micro }
        private HandAimSource _handAimSource;

        private float _lastMicroPoseTime = -999f;

        /// <summary>
        /// Vérifie si la main gauche maintient un geste de saisie (poing / grab / pinch)
        /// SANS tenir d'objet dans la main.
        /// </summary>
        private bool IsLeftHandGrabbingWithoutObject()
        {
            // 1. Si un objet est tenu par la main gauche, aucune téléportation ne doit s'activer
            var leftGrab = VRGrabHand.GetHandForNode(XRNode.LeftHand);
            if (leftGrab != null && leftGrab.IsHoldingObject)
            {
                return false;
            }

            // 2. État de grab rapporté par VRGrabHand
            if (leftGrab != null && leftGrab.IsGrabInputActive)
            {
                return true;
            }

            // 3. Détection directe XRHandSubsystem (poing fermé ou pincement index/majeur)
            // Hystérésis : une fois le geste engagé (minuterie ou visée), on le garde
            // avec des seuils plus bas pour ne pas annuler la visée au moindre tremblement.
            if (VRHandGestures.IsGrabGesture(XRNode.LeftHand, IsAiming || _handGrabTimer > 0f))
            {
                return true;
            }

            // 4. Manette gauche (secours si manette utilisée)
            XRInputDevice dev = VRStickReader.GetDevice(XRNode.LeftHand);
            if (dev.isValid)
            {
                if (dev.TryGetFeatureValue(CommonUsages.grip, out float grip) && grip > 0.5f) return true;
                if (dev.TryGetFeatureValue(CommonUsages.trigger, out float trig) && trig > 0.5f) return true;
            }

            return false;
        }

        private void UpdateHandTrackingTeleport()
        {
            if (headCamera == null) return;

            // Réglages d'Inspector → détecteur partagé (aussi lu par VRSnapTurn).
            VRThumbMicrogestures.PressDistance = microPressDistance;
            VRThumbMicrogestures.ReleaseDistance = microReleaseDistance;
            VRThumbMicrogestures.SwipeDistance = microSwipeDistance;

            // Mode « Escalade » choisi dans le menu : pas de téléportation aux mains.
            if (VRLocomotionSettings.HandMode != HandLocomotionMode.Teleport)
            {
                if (IsAiming) StopHandAim();
                return;
            }

            // Les deux gestes coexistent, mais une visée commencée par l'un ne peut
            // pas être annulée ou confirmée par l'autre.
            if (handTeleportGesture != HandTeleportGesture.PistoletGauche && _handAimSource != HandAimSource.Gun)
            {
                UpdateMicrogestureTeleport();
            }
            if (handTeleportGesture != HandTeleportGesture.MicrogesteDroit && _handAimSource != HandAimSource.Micro)
            {
                UpdateGunTeleport();
            }
        }

        /// <summary>
        /// MICROGESTE (main droite, façon Meta) :
        ///   1. poing lâche, le pouce tape sur le côté de l'index → l'arc apparaît ;
        ///   2. on vise avec le poing (axe poignet → jointures) ;
        ///   3. second tap → téléportation si la cible est valide (sinon annulation).
        /// Ouvrir la main plus de <see cref="microPoseGrace"/> s annule la visée.
        /// </summary>
        private void UpdateMicrogestureTeleport()
        {
            // Perte fugitive du suivi pendant la visée : on gèle l'état.
            if (_handAimSource == HandAimSource.Micro && !VRHandGestures.IsTracked(XRNode.RightHand)) return;

            var rightGrab = VRGrabHand.GetHandForNode(XRNode.RightHand);
            bool holding = rightGrab != null && rightGrab.IsHoldingObject;
            bool pose = VRThumbMicrogestures.TryGetAim(XRNode.RightHand, out Vector3 aimPos, out Vector3 aimDir) && !holding;
            if (pose)
            {
                _lastMicroPoseTime = Time.time;
                _gunAimPos = aimPos;
                _gunAimDir = aimDir;
            }

            // Tap = pouce posé puis relevé SANS glisser (un glissement = rotation).
            bool tap = pose && VRThumbMicrogestures.TapThisFrame(XRNode.RightHand);

            if (_handAimSource != HandAimSource.Micro)
            {
                if (!tap) return;
                _handAimSource = HandAimSource.Micro;
                IsAiming = true;
                _aimDuration = 0f;
                SetVisualsActive(true);
                // Pas de vibration sans manette : un petit son confirme le clic.
                VRSons.Jouer(VRSon.Saisie, aimPos, 0.6f, 1.3f);
                return;
            }

            if (Time.time - _lastMicroPoseTime > microPoseGrace)
            {
                StopHandAim();
                return;
            }

            _aimDuration += Time.deltaTime;
            _useGunAim = true;
            UpdateArcAndTarget();
            _useGunAim = false;

            if (tap && _aimDuration >= minAimDuration)
            {
                if (_hasValidTarget) PerformTeleport(_targetPosition);
                StopHandAim();
            }
        }

        /// <summary>Range l'arc et libère la visée mains.</summary>
        private void StopHandAim()
        {
            IsAiming = false;
            _aimDuration = 0f;
            _gunConfirmTimer = 0f;
            _handAimSource = HandAimSource.None;
            SetVisualsActive(false);
        }

        /// <summary>
        /// SIGNE DU PISTOLET (main gauche) : arc immédiat, téléportation après
        /// <see cref="handGunConfirmTime"/> s de visée stable.
        /// </summary>
        private void UpdateGunTeleport()
        {
            // Perte fugitive du suivi pendant la visée : on gèle l'état.
            if (IsAiming && !VRHandGestures.IsTracked(XRNode.LeftHand)) return;

            // Geste : SIGNE DU PISTOLET main gauche, main vide. L'arc apparaît
            // IMMÉDIATEMENT ; la téléportation se déclenche après handGunConfirmTime
            // secondes de visée stable sur la même zone. Relâcher la posture annule.
            var leftGrab = VRGrabHand.GetHandForNode(XRNode.LeftHand);
            bool holding = leftGrab != null && leftGrab.IsHoldingObject;
            bool gun = VRHandGestures.TryGetFingerGun(XRNode.LeftHand, out Vector3 gunPos, out Vector3 gunDir, out _) && !holding;
            if (gun)
            {
                _lastGunTime = Time.time;
                _gunAimPos = gunPos;
                _gunAimDir = gunDir;
            }
            bool gunHeld = gun || (Time.time - _lastGunTime) < HandGunGrace;

            if (!gunHeld)
            {
                _gunNeedsRelease = false;
                if (IsAiming) StopHandAim();
                return;
            }

            // Après une téléportation, il faut défaire puis refaire le pistolet.
            if (_gunNeedsRelease) return;

            if (!IsAiming)
            {
                IsAiming = true;
                _handAimSource = HandAimSource.Gun;
                _aimDuration = 0f;
                _gunConfirmTimer = 0f;
                SetVisualsActive(true);
            }

            _aimDuration += Time.deltaTime;
            _useGunAim = true;
            UpdateArcAndTarget();
            _useGunAim = false;

            if (!_hasValidTarget)
            {
                _gunConfirmTimer = 0f;
                return;
            }

            // Confirmation : la cible doit rester dans un rayon de tolérance.
            if (_gunConfirmTimer <= 0f || Vector3.Distance(_targetPosition, _gunConfirmAnchor) > handGunConfirmRadius)
            {
                _gunConfirmAnchor = _targetPosition;
                _gunConfirmTimer = 0f;
            }
            _gunConfirmTimer += Time.deltaTime;

            if (_gunConfirmTimer >= handGunConfirmTime)
            {
                PerformTeleport(_targetPosition);
                StopHandAim();
                _gunNeedsRelease = true;
            }
        }

        // =====================================================================
        // LECTURE DU JOYSTICK (3 NIVEAUX DE SECOURS + CLAVIER)
        // =====================================================================

        /// <summary>
        /// Lit le joystick 2D. Ici on s'intéresse surtout à la composante Y
        /// (avant / arrière), le X servant au snap turn.
        /// </summary>
        private Vector2 ReadThumbstick()
        {
            // 1. Essai via XR InputDevice (repli par caractéristiques inclus, 0 allocation)
            XRInputDevice device = VRStickReader.GetDevice(handNode);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis;
            }

            // 2. Fallback via Input System Action
            if (_thumbstickAction != null && _thumbstickAction.enabled)
            {
                Vector2 val = _thumbstickAction.ReadValue<Vector2>();
                if (val.sqrMagnitude > 0.01f) return val;
            }

            // 3. Fallback direct New Input System Devices : on parcourt les
            //    périphériques et on garde celui de la bonne main.
            bool isLeft = handNode == XRNode.LeftHand;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!dev.added) continue;

                if (!VRStickReader.DeviceMatchesHand(dev, isLeft)) continue;

                var stick = VRInputControls.FindAny<UnityEngine.InputSystem.Controls.Vector2Control>(
                    dev, "thumbstick", "primary2DAxis", "stick");
                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (v.sqrMagnitude > 0.01f) return v;
                }
            }

            // 4. Fallback Clavier / Éditeur (Flèche haut ou touche T) — uniquement
            //    sans casque actif, comme partout ailleurs dans le projet.
#if UNITY_EDITOR
            if (VRDeviceState.EditorFallbackAllowed && Keyboard.current != null)
            {
                if (Keyboard.current.tKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    return new Vector2(0f, 1f);
                }

                // Flèches gauche/droite : utile pour tester le masque de
                // téléportation sans casque (on vise vers le haut, puis on
                // choisit la cible avec les flèches horizontales).
                if (Keyboard.current.leftArrowKey.isPressed) return new Vector2(-1f, 1f);
                if (Keyboard.current.rightArrowKey.isPressed) return new Vector2(1f, 1f);
            }
#endif

            return Vector2.zero;
        }

        // =====================================================================
        // CALCUL DE L'ARC BALISTIQUE
        // =====================================================================

        /// <summary>
        /// Simule la trajectoire parabolique et met à jour l'affichage (arc + réticule).
        ///
        /// Principe : on avance par petits pas de temps en utilisant les équations du
        /// mouvement uniformément accéléré (gravité) :
        ///     position suivante = position + vitesse·dt + ½·gravité·dt²
        ///     nouvelle vitesse   = vitesse + gravité·dt
        /// À chaque pas, un rayon est tiré du point courant vers le point suivant ; s'il
        /// touche quelque chose, on s'arrête : c'est le point d'impact.
        ///
        /// VALIDATION : l'impact n'est retenu comme destination que si la surface est
        /// assez horizontale (angle entre la normale et le haut ≤ maxSurfaceSlopeAngle).
        /// Sinon la cible est marquée invalide et l'arc passe en rouge.
        /// </summary>
        private void UpdateArcAndTarget()
        {
            if (rayOrigin == null && headCamera == null) return;

            // --- Point de départ et direction de visée --------------------------
            // La direction brute est l'axe « avant » du transform (manette ou tête selon
            // aimFromHead), puis on applique la correction de calibration en degrés.
            // Sans cette correction, une manette dont le runtime rapporte une
            // orientation pointée vers le haut envoie la parabole en cloche : le joueur
            // atterrit très loin, ce qui est le symptôme prévu ici.
            Transform aimTransform = (aimFromHead && headCamera != null) ? headCamera.transform : rayOrigin;
            if (aimTransform == null) return;

            Vector3 startPos = aimTransform.position;
            Vector3 aimDirection = Quaternion.Euler(aimEulerCorrection) * aimTransform.forward;
            if (_useGunAim)
            {
                // Hand tracking : l'arc part du bout de l'index, dans l'axe du doigt.
                startPos = _gunAimPos;
                aimDirection = _gunAimDir;
            }

            // Conditions initiales de la balle : elle part de la main et part vers l'avant.
            Vector3 startVelocity = aimDirection.normalized * initialVelocity;
            Vector3 gravity = Physics.gravity;

            // Liste des points qui composeront la ligne affichée.
            // Buffer réutilisé + capacite préallouée : la visée tourne à 72 Hz
            // en VR, une allocation par frame ici se traduit par des pics de GC
            // exactement au moment où le joueur en peut le moins les prévoir.
            _arcPoints.Clear();
            _arcPoints.Add(startPos);
            Vector3 currentPos = startPos;
            Vector3 currentVel = startVelocity;

            // On repart d'un état « pas de cible » à chaque nouveau calcul.
            _hasValidTarget = false;

            for (int i = 0; i < maxArcPoints; i++)
            {
                // Un pas de simulation.
                Vector3 nextPos = currentPos + currentVel * timeStep + 0.5f * gravity * (timeStep * timeStep);
                currentVel += gravity * timeStep;

                // Limite de portée : on coupe l'arc dès qu'on dépasse la distance
                // maximale. La cible reste invalide (pas de téléportation) : c'est le
                // filet de sécurité contre une visée mal calibrée qui enverrait le
                // joueur à l'autre bout du niveau.
                if (Vector3.Distance(nextPos, startPos) > maxTeleportDistance)
                {
                    _arcPoints.Add(startPos + (nextPos - startPos).normalized * maxTeleportDistance);
                    break;
                }

                // Direction et longueur du segment juste calculé (pour le rayon de détection).
                Vector3 segmentDir = nextPos - currentPos;
                float segmentDist = segmentDir.magnitude;
                if (segmentDist < 1e-5f) break;

                // Rayon du point courant vers le point suivant, sur les layers autorisés.
                //
                // QueryTriggerInteraction.Ignore : les-trigger (sphères de préhension
                // des manettes) ne doivent JAMAIS pouvoir servir de destination, ni
                // interrompre l'arc. Sinon on se téléporte sur une manette.
                LayerMask raycastMask = teleportLayerMask | blockingLayerMask;
                if (ImpactHorsDuRig(currentPos, segmentDir / segmentDist, segmentDist, raycastMask, out RaycastHit hit))
                {
                    _arcPoints.Add(hit.point); // L'arc s'arrête exactement sur l'impact (mur ou sol).

                    // Vérifier si la surface est autorisée (sol) et suffisamment plate pour y marcher
                    bool isGround = ((1 << hit.collider.gameObject.layer) & teleportLayerMask) != 0;
                    float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                    if (isGround && slopeAngle <= maxSurfaceSlopeAngle)
                    {
                        _hasValidTarget = true;
                        _targetPosition = hit.point;
                        _targetNormal = hit.normal;
                    }
                    break; // Fin de la simulation : on a touché quelque chose (mur ou sol).
                }

                // Pas d'impact : on continue la parabole.
                _arcPoints.Add(nextPos);
                currentPos = nextPos;
            }

            // Affichage de la ligne
            if (arcRenderer != null)
            {
                // SetPosition en boucle plutôt que SetPositions(list.ToArray()) :
                // évite de copier le tableau à chaque frame pendant la visée.
                arcRenderer.positionCount = _arcPoints.Count;
                for (int i = 0; i < _arcPoints.Count; i++)
                {
                    arcRenderer.SetPosition(i, _arcPoints[i]);
                }

                // Vert si valide, rouge sinon ; dégradé d'opacité pour un effet de fuite.
                Color col = _hasValidTarget ? validArcColor : invalidArcColor;
                arcRenderer.startColor = col;
                arcRenderer.endColor = new Color(col.r, col.g, col.b, 0.1f);
            }

            // Positionnement du réticule au sol
            if (reticleTransform != null)
            {
                reticleTransform.gameObject.SetActive(_hasValidTarget);
                if (_hasValidTarget)
                {
                    // Légèrement surélevé (1,5 cm) pour éviter le z-fighting avec le sol.
                    reticleTransform.position = _targetPosition + _targetNormal * 0.015f;
                    // Aligné sur la normale du sol : le disque colle à la pente.
                    reticleTransform.rotation = Quaternion.FromToRotation(Vector3.up, _targetNormal);
                }
            }
        }

        // =====================================================================
        // ACTION : TÉLÉPORTATION
        // =====================================================================

        /// <summary>Le joueur vient d'être téléporté (point visé). Écouté par la narration.</summary>
        public static event System.Action<Vector3> Teleporte;

        /// <summary>
        /// Déplace l'XR Origin pour que la TÊTE du joueur arrive exactement sur la cible.
        ///
        /// compensation : l'Origin est au sol, la tête est décalée à l'intérieur
        /// (le joueur a pu marcher). Si on posait l'Origin directement sur la cible,
        /// la tête se retrouverait décalée du même vecteur. On soustrait donc
        /// l'offset tête−Origin (en XZ uniquement : la hauteur reste celle de la cible)
        /// pour que ce soit la tête, et non le centre de la pièce, qui atterrit au bon endroit.
        /// </summary>
        private void PerformTeleport(Vector3 destination)
        {
            if (xrOriginTransform == null) return;

            // Formule précise : compenser la position de la tête du joueur dans la pièce
            // pour que la caméra arrive pile à la cible, pas le centre de la pièce.
            Vector3 cameraOffset = Vector3.zero;
            if (headCamera != null)
            {
                cameraOffset = headCamera.transform.position - xrOriginTransform.position;
                cameraOffset.y = 0f; // On ne compense que sur le plan horizontal XZ
            }

            if (VRLimitesPiece.Interieur.HasValue)
            {
                Vector2 cl = VRLimitesPiece.Contraindre(new Vector2(destination.x, destination.z), VRLimitesPiece.Interieur.Value, 0.2f);
                destination = new Vector3(cl.x, destination.y, cl.y);
            }

            Vector3 newPosition = new Vector3(
                destination.x - cameraOffset.x,
                destination.y,
                destination.z - cameraOffset.z
            );

            // Déplacement + Physics.SyncTransforms : les colliders des objets tenus
            // (enfants des mains) suivent immédiatement le rig.
            VRUtils.MoveRig(xrOriginTransform, newPosition);
            VRSons.Jouer(VRSon.Teleportation, null, 1f, VRSons.PitchAleatoire(0.04f));
            Teleporte?.Invoke(destination);

            // Le saut du rig ne doit pas compter comme un geste de lancer.
            for (int i = 0; i < VRGrabHand.AllHands.Count; i++)
            {
                if (VRGrabHand.AllHands[i] != null) VRGrabHand.AllHands[i].ResetVelocityTracking();
            }
        }

        // =====================================================================
        // AFFICHAGE
        // =====================================================================

        /// <summary>
        /// Allume / éteint l'affichage de l'arc et du réticule.
        /// Le réticule ne s'affiche que si la cible est valide.
        /// </summary>
        private void SetVisualsActive(bool active)
        {
            if (arcRenderer != null) arcRenderer.enabled = active;
            if (reticleTransform != null) reticleTransform.gameObject.SetActive(active && _hasValidTarget);
        }

        /// <summary>
        /// Récupère ou crée le LineRenderer de l'arc, puis le configure :
        /// espace monde (les points sont déjà en coordonnées monde), largeur dégressive,
        /// shader non éclairé (la ligne reste visible même dans le noir), ligne
        /// initialement vide et désactivée.
        /// </summary>
        private void SetupLineRenderer()
        {
            if (arcRenderer != null) return; // Déjà fourni dans l'inspecteur → on n'y touche pas.

            arcRenderer = GetComponent<LineRenderer>();
            if (arcRenderer == null)
            {
                arcRenderer = gameObject.AddComponent<LineRenderer>();
            }

            arcRenderer.useWorldSpace = true;
            arcRenderer.startWidth = 0.02f; // Épaisseur au départ de l'arc.
            arcRenderer.endWidth = 0.005f;   // Fin plus fine → effet de perspective.
            arcRenderer.positionCount = 0;
            arcRenderer.enabled = false;     // Cachée tant qu'on ne vise pas.

            // Shader unlit à couleurs de sommets (Sprites/Default) : matériau BLANC, ce
            // sont startColor/endColor qui colorent l'arc. Avec URP/Unlit, qui ignore
            // les couleurs de sommets, l'arc restait vert même sur une cible invalide.
            arcRenderer.material = VRUtils.CreateUnlitMaterial(Color.white);
        }

        /// <summary>
        /// Crée le réticule au sol si l'utilisateur n'en a pas fourni un :
        /// un cylindre aplati (disque), sans collider (pour ne pas fausser les
        /// rayons de détection), désactivé au départ.
        /// </summary>
        private void SetupReticle()
        {
            if (reticleTransform != null) return;

            // Création d'un réticule discret (disque plat) au sol
            GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "TeleportReticle";
            reticle.transform.SetParent(transform, false);
            // Échelle : 0,5 m de large, mais à peine 5 mm d'épaisseur → un disque.
            reticle.transform.localScale = new Vector3(0.5f, 0.005f, 0.5f);

            // Retirer le collider pour ne pas bloquer les rayons
            Collider c = reticle.GetComponent<Collider>();
            if (c != null) Destroy(c);

            reticle.GetComponent<Renderer>().sharedMaterial = VRUtils.CreateUnlitMaterial(new Color(0.1f, 0.8f, 1.0f, 0.7f));

            reticleTransform = reticle.transform;
            reticle.SetActive(false);
        }
    }
}