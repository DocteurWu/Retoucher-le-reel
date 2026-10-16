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
    /// ROTATION PAR CRANS (Snap Turn) — pour se retourner dans l'espace en VR.
    ///
    /// PRINCIPE : pousser le joystick gauche/droite ne fait pas tourner la vue de façon
    /// continue ; ça bascule d'un cran fixe (45° par défaut). C'est la méthode qui limite
    /// la cinétose (mal des transports) : le joueur contrôle lui-même le moment du mouvement.
    ///
    /// OÙ EST DÉPLACÉ QUOI ? On ne tourne PAS la caméra. On tourne l'XR ORIGIN
    /// (xrOriginTransform) autour de la tête (headCamera). Effet : le joueur reste
    /// exactement au même endroit, mais tout son « repère » pivote — comme si sa chaise
    /// avait tourné sur elle-même.
    ///
    /// ANTI-RÉPÉTITION (hystérésis) : deux seuils distincts sont utilisés.
    ///   - au-delà de activationThreshold (0.6) → un cran est joué
    ///   - il faut revenir sous resetThreshold (0.2) pour pouvoir rejouer un cran
    /// Sans cette zone morte, garder le stick poussé déclencherait des dizaines
    /// de rotations par seconde. Le joueur doit « lâcher » puis « repousser ».
    ///
    /// À poser sur l'XR Origin. Références à câbler dans l'inspecteur :
    ///   xrOriginTransform = l'objet XR Origin
    ///   headCamera        = la caméra (ou laisser vide : Camera.main est utilisé)
    /// </summary>
    public class VRSnapTurn : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // RÉFÉRENCES DE SCÈNE
        // ---------------------------------------------------------------------

        [Header("Références")]

        /// <summary>Transform de l'XR Origin à tourner. Si vide → ce GameObject.</summary>
        public Transform xrOriginTransform;

        /// <summary>Caméra du casque, sert de point de rotation. Si vide → Camera.main.</summary>
        public Camera headCamera;

        // ---------------------------------------------------------------------
        // CONFIGURATION
        // ---------------------------------------------------------------------

        [Header("Configuration")]

        /// <summary>Manette qui déclenche la rotation (par défaut la droite).</summary>
        public XRNode handNode = XRNode.RightHand;

        /// <summary>Angle d'un cran, en degrés. 45 est le standard VR.</summary>
        public float turnAngle = 45f;

        /// <summary>Seuil de déclenchement : le stick doit dépasser cette valeur (0-1).</summary>
        public float activationThreshold = 0.6f;

        /// <summary>
        /// Seuil de réarmement : le stick doit revenir SOUS cette valeur pour
        /// autoriser le prochain cran. Plus il est bas, plus le joueur doit «
        /// recentrer » nettement le stick entre deux rotations.
        /// </summary>
        public float resetThreshold = 0.2f;

        // --- État interne ----------------------------------------------------

        /// <summary>
        /// Vrai = le joueur peut déclencher un cran.
        /// Passe à false juste après une rotation, et redevient true quand le
        /// stick revient au centre. C'est toute la logique anti-répétition.
        /// </summary>
        private bool _canTurn = true;

        /// <summary>Action de secours pour lire le joystick (3e niveau de secours).</summary>
        private InputAction _thumbstickAction;

        // =====================================================================
        // INITIALISATION
        // =====================================================================

        /// <summary>
        /// APPELÉ UNE SEULE FOIS au démarrage.
        /// 1. Complète les références manquantes par des valeurs par défaut.
        /// 2. Crée l'Input Action du joystick avec plusieurs liaisons car le chemin
        ///    exact diffère selon le casque ({LeftHand} vs {leftHand} vs
        ///    LeftHandXRController). En registering les trois, on reste compatible
        ///    avec tous les pilotes.
        /// </summary>
        private void Awake()
        {
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;

            // Deux écritures possibles du côté de la main dans les liaisons Input System.
            string handTag = handNode == XRNode.RightHand ? "{RightHand}" : "{LeftHand}";
            string handSide = handNode == XRNode.RightHand ? "rightHand" : "leftHand";

            _thumbstickAction = new InputAction("SnapTurnThumbstick", InputActionType.Value);

            // Liaison principale : contrôleur générique + accolade de main.
            _thumbstickAction.AddBinding($"<XRController>{handTag}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{handTag}/primary2DAxis");

            // Variante avec accolades de côté.
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/primary2DAxis");
        }

        /// <summary>Active l'Input Action quand le composant est activé.</summary>
        private void OnEnable()
        {
            _thumbstickAction?.Enable();
        }

        /// <summary>Désactive l'Input Action quand le composant est désactivé.</summary>
        private void OnDisable()
        {
            _thumbstickAction?.Disable();
        }

        // =====================================================================
        // BOUCLE PRINCIPALE
        // =====================================================================

        /// <summary>
        /// Chaque frame :
        ///  1. lit la composante horizontale (X) du joystick ;
        ///  2. si on est armé ET que X dépasse le seuil d'activation → un cran ;
        ///  3. sinon, si X revient au centre → on se réarme pour le prochain cran.
        /// </summary>
        private void Update()
        {
            float axisX = ReadThumbstickX();

            if (_canTurn)
            {
                // Poussé à droite → rotation positive (sens horaire vu du dessus).
                if (axisX > activationThreshold)
                {
                    PerformTurn(turnAngle);
                    _canTurn = false; // Verrouillage immédiat : un seul cran par poussée.
                }
                // Poussé à gauche → rotation inverse.
                else if (axisX < -activationThreshold)
                {
                    PerformTurn(-turnAngle);
                    _canTurn = false;
                }
            }
            else
            {
                // Réarmer lorsque le stick revient au centre
                if (Mathf.Abs(axisX) < resetThreshold)
                {
                    _canTurn = true;
                }
            }
        }

        // =====================================================================
        // LECTURE DU JOYSTICK (3 NIVEAUX DE SECOURS + CLAVIER)
        // =====================================================================

        /// <summary>
        /// Lit la composante horizontale du joystick.
        /// On essaie plusieurs méthodes car le chemin d'accès dépend du runtime XR actif.
        /// Seule la composante X compte ici (le Y sert à la téléportation).
        /// </summary>
        private float ReadThumbstickX()
        {
            // --- NIVEAU 1 : XRInputDevice (ancienne API) ---------------------
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);

            // Si le nœud n'est pas directement résolu, on cherche le contrôleur
            // correspondant par caractéristiques (main gauche / droite).
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

            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis.x;
            }

            // --- NIVEAU 2 : Input Action (liaisons <XRController>/...) --------
            if (_thumbstickAction != null && _thumbstickAction.enabled)
            {
                Vector2 actionVal = _thumbstickAction.ReadValue<Vector2>();
                if (Mathf.Abs(actionVal.x) > 0.05f) return actionVal.x;
            }

            // Fallback direct New Input System Devices : on parcourt la liste des
            // périphériques et on sélectionne celui qui appartient à la bonne main
            // (par usage déclaré OU par nom du périphérique).
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

                // thumbstick (Quest) en priorité, primary2DAxis en secours.
                var stick = dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") ??
                            dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (Mathf.Abs(v.x) > 0.05f) return v.x;
                }
            }

            // Fallback Clavier (Q / D ou Flèches gauche / droite) : pour tester
            // dans l'éditeur sans casque branché.
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.qKey.isPressed) return -1f;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) return 1f;
            }

            // Aucun joystick détecté → neutre.
            return 0f;
        }

        // =====================================================================
        // ROTATION
        // =====================================================================

        /// <summary>
        /// Fait pivoter l'XR Origin autour de la tête du joueur.
        ///
        /// RotateAround(axis, point) = rotation d'un angle autour d'un point.
        /// On utilise la position de la CAMÉRA comme point de rotation (et non
        /// l'origine du XR Origin) : ainsi le joueur ne se sent jamais « baladé »
        /// latéralement pendant la rotation, ce qui supprime la cinétose.
        /// Si la caméra est absente, on retombe sur la position de l'Origin.
        /// </summary>
        private void PerformTurn(float angle)
        {
            if (xrOriginTransform == null) return;

            Vector3 pivotPoint = headCamera != null ? headCamera.transform.position : xrOriginTransform.position;
            xrOriginTransform.RotateAround(pivotPoint, Vector3.up, angle);
        }
    }
}