// =====================================================================================
//  VRHeadTracker.cs — POSE DE LA TÊTE (casque)
//
//  RÔLE
//    Écrit, à chaque frame, la pose réelle du casque dans le Transform de la
//    caméra (en LOCAL, car la caméra est enfant de l'XR Origin que la
//    locomotion, la téléportation et le snap turn déplacent / font tourner).
//    Demande aussi au sous-système XR le mode d'origine « Floor » (y = 0 au sol),
//    sans lequel la hauteur du joueur est fausse.
//
//  À POSER SUR : la Main Camera (et câbler le sous-système XR si l'éiteur le
//  demande : le script démarre lui-même la pile XR s'il faut).
//
//  ⚠️ 4 niveaux de lecture de la pose (XRNodeStates → Input System → ancienne
//     API XR → Input Actions) : le premier qui répond gagne.
//
//  RÉFÉRENCES : voir README.md (sections 2 et 6)
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
    /// SUITE DE POSITION ET ROTATION DU CASQUE VR (tête du joueur).
    ///
    /// À poser sur la Caméra de la scène. Chaque frame, ce script écrit la pose réelle
    /// du casque dans le Transform de la caméra. C'est ce qui donne la sensation
    /// de « vraie tête » dans le casque, et ce qui permet aux autres scripts
    /// (VRSnapTurn, VRTeleportation) de connaître la position de la tête en monde.
    ///
    /// DeuxPoints techniques importants :
    ///  1. ORIGINE FLOOR : on demande au sous-système XR de fixer l'origine de la pose
    ///     au niveau du SOL (y = 0 au sol, pas au niveau des yeux). Sans cela, la hauteur
    ///     du joueur est fausse et la téléportation / le snap turn sautent. Quand c'est
    ///     accepté, on remet aussi le parent (Camera Offset) à y = 0.
    ///
    ///  2. SUIVI À 4 NIVEAUX : le casque peut être lu de 4 manières différentes selon
    ///     le fournisseur (OpenXR,plugin SteamVR, simulateur d'éditeur...). On essaie
    ///     dans l'ordre et on s'arrête au premier qui répond :
    ///        1. XRNodeStates      → API moderne, la plus fiable sous OpenXR
    ///        2. XRInputDevice      → ancienne API Unity XR
    ///        3. Input Actions      → liaisons déclarées en chemin (<XRHMD>/...)
    ///     (le 4e niveau de VRGrabHand n'a pas lieu d'être ici : la tête n'est pas un contrôleur)
    ///
    /// Le résultat est appliqué en LOCAL (localPosition / localRotation) pour que la
    /// caméra reste correctement placée si elle est enfant d'un XR Origin.
    /// </summary>
    public class VRHeadTracker : MonoBehaviour
    {
        // --- Actions de secours (niveau 3 de la chaîne) ---------------------

        /// <summary>Lit la position du casque (Vector3) via les liaisons XRHMD.</summary>
        private InputAction _posAction;

        /// <summary>Lit la rotation du casque (Quaternion) via les liaisons XRHMD.</summary>
        private InputAction _rotAction;

        // --- Mémoire interne ------------------------------------------------

        /// <summary>
        /// Vrai une fois que le mode d'origine SOL a été accepté par le XR Loader.
        /// Évite de retenter (et de spammer) TrySetTrackingOriginMode à chaque frame.
        /// </summary>
        private bool _floorOriginConfigured;

        /// <summary>
        /// Liste réutilisée pour lire les XRNodeState (évite une allocation par frame).
        /// </summary>
        private readonly List<XRNodeState> _nodeStates = new List<XRNodeState>();

        // =====================================================================
        // INITIALISATION
        // =====================================================================

        /// <summary>
        /// APPELÉ UNE SEULE FOIS au chargement de la scène.
        /// 1. Démarre la pile XR si elle ne l'est pas déjà (certains casques, notamment
        ///    en PCVR ou avec OpenXR activé à la main, ne démarrent pas tout seuls).
        /// 2. Crée les Input Actions de secours avec DEUX liaisons chacune :
        ///    « centerEye… » (API moderne) et « device… » (API historique).
        ///    Les deux sont enregistrées : le runtime ignorera celle qui n'existe pas
        ///    sur le matériel branché, et gardera l'autre.
        /// </summary>
        private void Awake()
        {
            // 1. S'assurer que le runtime XR tourne avant toute interrogation du casque.
            EnsureXRStarted();

            // 2. Action « position de la tête ».
            _posAction = new InputAction("HeadPosition", InputActionType.Value, expectedControlType: "Vector3");
            _posAction.AddBinding("<XRHMD>/centerEyePosition");
            _posAction.AddBinding("<XRHMD>/devicePosition");

            // 3. Action « rotation de la tête ».
            _rotAction = new InputAction("HeadRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotAction.AddBinding("<XRHMD>/centerEyeRotation");
            _rotAction.AddBinding("<XRHMD>/deviceRotation");
        }

        /// <summary>
        /// Démarre la pile XR Management si besoin (loader + sous-systèmes).
        /// Tout est protégé par un try/catch : sur une machine sans casque on veut
        /// seulement un message dans la console, pas une exception qui casse la scène.
        /// </summary>
        private void EnsureXRStarted()
        {
            try
            {
                var xrGeneral = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (xrGeneral != null && xrGeneral.Manager != null)
                {
                    // Initialisation synchrone du loader (headset plugged / OpenXR link)…
                    if (!xrGeneral.Manager.isInitializationComplete)
                    {
                        xrGeneral.Manager.InitializeLoaderSync();
                    }
                    // …puis démarrage des sous-systèmes qui fournissent le tracking.
                    if (xrGeneral.Manager.activeLoader != null)
                    {
                        xrGeneral.Manager.StartSubsystems();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VRHeadTracker] Note initialisation XR: " + ex.Message);
            }
        }

        /// <summary>
        /// APPELÉ quand le composant devient actif (objet activé, scène chargée).
        /// On allume les actions, on s'abonne à l'événement de rendu, et on tente
        /// immédiatement de mettre l'origine au sol.
        /// </summary>
        private void OnEnable()
        {
            _posAction?.Enable();
            _rotAction?.Enable();

            // onBeforeRender = appelé juste AVANT l'affichage de l'image (donc plus de
            // fois que Update) : c'est ce qui réduit la latence et le "swim" du casque.
            Application.onBeforeRender += OnBeforeRenderHead;

            TryConfigureFloorOrigin();
        }

        /// <summary>
        /// APPELÉ quand le composant est désactivé ou détruit.
        /// Important de se désabonner : sinon le callback continuerait de tourner
        /// sur un objet désactivé (fuite mémoire + erreurs).
        /// </summary>
        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderHead;
            _posAction?.Disable();
            _rotAction?.Disable();
        }

        // =====================================================================
        // BOUCLE DE MISE À JOUR
        // =====================================================================

        /// <summary>
        /// Mise à jour « logique », 1 fois par frame.
        /// Si l'origine SOL n'a pas pu être configurée au démarrage (casque pas encore
        /// prêt), on réessaie ici. Ensuite on applique la pose du casque.
        /// </summary>
        private void Update()
        {
            if (!_floorOriginConfigured)
            {
                TryConfigureFloorOrigin();
            }
            UpdateHeadPose();
        }

        /// <summary>
        /// Mise à jour « rendu », appelée autant de fois que nécessaire AVANT chaque image.
        /// Priorité très négative (-30000) : exécutée en tout premier, avant les autres
        /// scripts qui consumes la position de la tête dans la même frame.
        /// </summary>
        [BeforeRenderOrder(-30000)]
        private void OnBeforeRenderHead()
        {
            UpdateHeadPose();
        }

        // =====================================================================
        // ORIGINE FLOOR
        // =====================================================================

        /// <summary>
        /// Demande à tous les sous-systèmes XR actifs de placer l'origine de suivi au SOL.
        ///
        /// En mode Floor, Unity considère que y = 0 est le plancher et que la hauteur
        /// des yeux du joueur est gérée par le casque : c'est le seul mode qui donne
        /// une hauteur de joueur cohérente.
        ///
        /// Si le mode est accepté, on remet aussi le parent de la caméra (souvent
        /// « Camera Offset ») à (0,0,0), car en mode Floor il ne doit PAS ajouter
        /// une hauteur manuelle en plus, sinon la caméra se retrouve trop haute.
        /// </summary>
        private void TryConfigureFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var sub in subsystems)
            {
                if (sub != null && sub.running)
                {
                    // Retourne true si le mode a été accepté par le matériel.
                    if (sub.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    {
                        _floorOriginConfigured = true;

                        // En mode Floor, le parent (Camera Offset) doit être à y = 0
                        if (transform.parent != null)
                        {
                            transform.parent.localPosition = Vector3.zero;
                        }
                    }
                }
            }
        }

        // =====================================================================
        // LECTURE DE LA POSE DU CASQUE
        // =====================================================================

        /// <summary>
        /// Lit la pose du casque et l'écrit dans le Transform de la caméra.
        ///
        /// On essaie 3 sources dans l'ordre et on sort dès que l'une donne une
        /// information. À chaque étage, une valeur « nulle » (Vector3.zero /
        /// Quaternion.identity) est considérée comme « aucune donnée » : c'est
        /// la valeur neutre renvoyée quand le casque n'est pas suivi.
        ///
        /// ÉCRITURE EN LOCAL : la caméra est enfant d'un XR Origin qui peut être
        /// déplacé (téléportation, snap turn). Écrire en local garantit que le
        /// casque reste au bon endroit à l'intérieur de cet Origin.
        /// </summary>
        private void UpdateHeadPose()
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            // ---------------------------------------------------------------
            // NIVEAU 1 — XRNodeStates (API moderne, la plus fiable sous OpenXR)
            // ---------------------------------------------------------------
            _nodeStates.Clear();
            InputTracking.GetNodeStates(_nodeStates);

            for (int i = 0; i < _nodeStates.Count; i++)
            {
                var state = _nodeStates[i];

                // CenterEye = position « milieu des yeux », avec correction de l'IPD.
                if (state.nodeType == XRNode.CenterEye)
                {
                    bool hasPos = state.TryGetPosition(out pos);
                    bool hasRot = state.TryGetRotation(out rot);

                    // Une des deux valeurs est exploitable → on applique ce qu'on a.
                    if ((hasPos && pos != Vector3.zero) || (hasRot && rot != Quaternion.identity))
                    {
                        if (pos != Vector3.zero) transform.localPosition = pos;
                        if (rot != Quaternion.identity) transform.localRotation = rot;
                        return; // Succès : inutile d'aller plus loin.
                    }
                }
            }

            // ---------------------------------------------------------------
            // NIVEAU 2 — XRInputDevice (ancienne API Unity XR)
            // ---------------------------------------------------------------
            XRInputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);

            if (headDevice.isValid)
            {
                // On tente centerEye… puis device… : le premier qui répond gagne.
                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out pos) ||
                    headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                {
                    if (pos != Vector3.zero) transform.localPosition = pos;
                }

                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rot) ||
                    headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                {
                    if (rot != Quaternion.identity) transform.localRotation = rot;
                }

                // Rien reçu de ce périphérique → on laisse la place au niveau 3.
                if (pos != Vector3.zero || rot != Quaternion.identity) return;
            }

            // ---------------------------------------------------------------
            // NIVEAU 3 — Input Actions (liaisons <XRHMD>/...)
            // ---------------------------------------------------------------
            if (_posAction != null && _posAction.enabled)
            {
                Vector3 p = _posAction.ReadValue<Vector3>();
                if (p != Vector3.zero) transform.localPosition = p;
            }

            if (_rotAction != null && _rotAction.enabled)
            {
                Quaternion r = _rotAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) transform.localRotation = r;
            }
        }
    }
}