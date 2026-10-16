using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
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
        public float activationThreshold = 0.5f;

        /// <summary>Seuil de relâchement : stick ramené sous cette valeur → on téléporte.</summary>
        public float releaseThreshold = 0.2f;

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

        /// <summary>Layers traversés par les rayons de détection du sol. ~0 = tous.</summary>
        public LayerMask teleportLayerMask = ~0; // Tous les layers par défaut

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

        /// <summary>Vrai entre le push du stick et son relâchement (on est en train de viser).</summary>
        private bool _isAiming;

        /// <summary>Vrai si la dernière frame a trouvé un sol valide au bout de l'arc.</summary>
        private bool _hasValidTarget;

        /// <summary>Point d'arrivée validé (surface touchée par l'arc).</summary>
        private Vector3 _targetPosition;

        /// <summary>Normale de la surface touchée (sert à poser le réticule à plat).</summary>
        private Vector3 _targetNormal;

        /// <summary>Action de secours pour lire le joystick (2e niveau de secours).</summary>
        private InputAction _thumbstickAction;

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
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            string handSide = handNode == XRNode.LeftHand ? "leftHand" : "rightHand";
            _thumbstickAction = new InputAction("TeleportThumbstick", InputActionType.Value);
            _thumbstickAction.AddBinding($"<XRController>{handTag}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{handTag}/primary2DAxis");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/primary2DAxis");

            SetupLineRenderer();
            SetupReticle();
        }

        /// <summary>Active l'Input Action quand le composant est activé.</summary>
        private void OnEnable()
        {
            _thumbstickAction?.Enable();
        }

        /// <summary>
        /// Désactive l'Input Action et range l'affichage quand le composant est désactivé
        /// (sinon l'arc resterait figé à l'écran).
        /// </summary>
        private void OnDisable()
        {
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
        private void Update()
        {
            // Auto-recherche du contrôleur si non renseigné
            if (rayOrigin == null)
            {
                var hands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
                foreach (var h in hands)
                {
                    if (h.handNode == handNode)
                    {
                        rayOrigin = h.transform;
                        break;
                    }
                }
            }

            Vector2 stick = ReadThumbstick();

            // Pousser le stick vers l'avant active la visée
            if (!_isAiming && stick.y > activationThreshold)
            {
                _isAiming = true;
                SetVisualsActive(true);
            }

            if (_isAiming)
            {
                // On recalcule la parabole en continu : la cible bouge avec le joueur.
                UpdateArcAndTarget();

                // Relâcher le stick déclenche la téléportation
                if (stick.y < releaseThreshold)
                {
                    // On ne téléporte QUE si la destination a été validée (sol plat).
                    if (_hasValidTarget)
                    {
                        PerformTeleport(_targetPosition);
                    }

                    _isAiming = false;
                    SetVisualsActive(false);
                }
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
            // 1. Essai via XR InputDevice
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (!device.isValid)
            {
                // Nœud non résolu → recherche par caractéristiques (contrôleur + côté).
                var inputDevices = new List<XRInputDevice>();
                InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                    (handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
                InputDevices.GetDevicesWithCharacteristics(characteristics, inputDevices);
                if (inputDevices.Count > 0)
                {
                    device = inputDevices[0];
                }
            }

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

                bool isTarget = isLeft
                    ? (dev.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand) || dev.name.ToLower().Contains("left"))
                    : (dev.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand) || dev.name.ToLower().Contains("right"));
                if (!isTarget) continue;

                var stick = dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") ??
                            dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (v.sqrMagnitude > 0.01f) return v;
                }
            }

            // 4. Fallback Clavier / Éditeur (Flèche haut ou touche T)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    return new Vector2(0f, 1f);
                }
            }

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

            // Conditions initiales de la balle : elle part de la main et part vers l'avant.
            Vector3 startVelocity = aimDirection.normalized * initialVelocity;
            Vector3 gravity = Physics.gravity;

            // Liste des points qui composeront la ligne affichée.
            List<Vector3> points = new List<Vector3> { startPos };
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
                    points.Add(startPos + (nextPos - startPos).normalized * maxTeleportDistance);
                    break;
                }

                // Direction et longueur du segment juste calculé (pour le rayon de détection).
                Vector3 segmentDir = nextPos - currentPos;
                float segmentDist = segmentDir.magnitude;

                // Rayon du point courant vers le point suivant, sur les layers autorisés.
                if (Physics.Raycast(currentPos, segmentDir.normalized, out RaycastHit hit, segmentDist, teleportLayerMask))
                {
                    points.Add(hit.point); // L'arc s'arrête exactement sur l'impact.

                    // Vérifier si la surface est suffisamment plate pour y marcher
                    float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                    if (slopeAngle <= maxSurfaceSlopeAngle)
                    {
                        _hasValidTarget = true;
                        _targetPosition = hit.point;
                        _targetNormal = hit.normal;
                    }
                    break; // Fin de la simulation : on a touché quelque chose.
                }

                // Pas d'impact : on continue la parabole.
                points.Add(nextPos);
                currentPos = nextPos;
            }

            // Affichage de la ligne
            if (arcRenderer != null)
            {
                arcRenderer.positionCount = points.Count;
                arcRenderer.SetPositions(points.ToArray());

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

            Vector3 newPosition = new Vector3(
                destination.x - cameraOffset.x,
                destination.y,
                destination.z - cameraOffset.z
            );

            xrOriginTransform.position = newPosition;
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

            // Shader unlit : pas d'éclairage, visible quel que soit l'éclairage de la scène.
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material lineMat = new Material(unlit);
            lineMat.color = validArcColor;
            arcRenderer.material = lineMat;
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

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material reticleMat = new Material(unlit);
            reticleMat.color = new Color(0.1f, 0.8f, 1.0f, 0.7f);
            reticle.GetComponent<Renderer>().sharedMaterial = reticleMat;

            reticleTransform = reticle.transform;
            reticle.SetActive(false);
        }
    }
}