// =====================================================================================
//  VRThrowableRespawn.cs — REMISE EN PLACE D'UN OBJET (RESPAWN)
//
//  RÔLE
//    Deux déclencheurs :
//      1. CHUTE : si l'objet passe sous minHeightBeforeRespawn (−2 m), il est
//         remis exactement sur sa position/rotation de départ (« le socle »).
//      2. RAPPEL MANUEL (désactivé par défaut, enableButtonRecall) : bouton de
//         manette ou touche R. Déactivé car A/B/X/Y sont réservés à
//         dupliquer/supprimer.
//
//  À POSER SUR : le même objet que le Rigidbody à surveiller.
//
//  ⚠️ La position de rappel est capturée dans Start() (et non dans Awake()) :
//     sur un corps non cinématique, la physique réimpose sa propre pose au
//     frame suivant ; capturer trop tôt mémoriserait une pose erronée.
//
//  ⚠️ Le repositionnement passe par Rigidbody.position/rotation ET par le
//     Transform : le premier est autoritaire pour la physique, le second évite
//     le « flicker » d'une image.
//
//  RÉFÉRENCES : voir README.md (section 4)
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
    /// REMISE EN PLACE AUTOMATIQUE d'un objet (ex. le cylindre) — système « Respawn ».
    ///
    /// Deux déclencheurs :
    ///  1. CHUTE : si l'objet passe sous minHeightBelowRespawn (par défaut -2 m), on le
    ///     replace sur son socle. Évite qu'il tombe à l'infini et que le joueur le perde.
    ///  2. RAPPEL MANUEL (optionnel) : appui sur un bouton de manette ou touche R au
    ///     clavier. Désactivé par défaut pour ne pas entrer en conflit avec les actions
    ///     dupliquer/supprimer (X/Y). Réactivable via « enableButtonRecall ».
    ///
    /// LA POSITION DE RÉFÉRENCE est mémorisée dans Awake(), c'est-à-dire à l'instant
    /// où la scène se charge : c'est donc la position posée dans l'éditeur qui sert
    /// de « socle ».
    ///
    /// À poser sur le MÊME objet que le Rigidbody à surveiller (le cylindre).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VRThrowableRespawn : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // CONFIGURATION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Hauteur (Y monde) sous laquelle l'objet est considéré « perdu » et
        /// doit être replacé. -2 m = s'il tombe de 2 mètres sous le point de départ.
        /// </summary>
        public float minHeightBeforeRespawn = -2.0f;

        /// <summary>
        /// Autorise le rappel manuel via les boutons de la manette (A/B/X/Y) ou la touche R.
        /// Désactivé par défaut : A/B/X/Y sont réservés au DUPLIQUER / SUPPRIMER, et
        /// déclencher le respawn en même temps ferait revenir l'objet à son socle au lieu
        /// d'agir dessus. Le respawn automatique par chute reste toujours actif.
        /// </summary>
        [Tooltip("Rappel manuel via boutons manette A/B/X/Y ou touche R (décoché : X/Y libres pour dupliquer/supprimer)")]
        public bool enableButtonRecall = false;

        // --- Mémoire interne ------------------------------------------------

        /// <summary>Rigidbody de l'objet (garanti présent par le RequireComponent).</summary>
        private Rigidbody _rb;

        /// <summary>Position « socle » mémorisée au démarrage de la scène.</summary>
        private Vector3 _spawnPosition;

        /// <summary>Rotation « socle » mémorisée au démarrage de la scène.</summary>
        private Quaternion _spawnRotation;

        /// <summary>
        /// État du bouton de rappel à la frame précédente.
        /// Permet de détecter le FRONT MONTANT (appui) et pas seulement « bouton
        /// maintenu » : sans cela le rappel se déclencherait à chaque frame
        /// pendant tout l'appui.
        /// </summary>
        private bool _wasRecallPressed;

        // =====================================================================
        // INITIALISATION
        // =====================================================================

/// <summary>
        /// (Re)mémorise le point de rappel à la position ACTUELLE de l'objet.
        ///
        /// Appelée automatiquement au <see cref="Start"/> (donc APRÈS le
        /// Awake), ce qui est indispensable pour les clones :
        /// <c>VRDuplicateDelete</c> instancie l'objet déjà positionné, mais un
        /// Rigidbody non cinématique est replacé par la physique au frame
        /// suivant. Capturer dans l'Awake revenait à mémoriser la pose
        /// paritaire (l'objet « planté » à l'origine de la scène), d'où des
        /// clones qui réapparaissaient au mauvais endroit après une chute.
        ///
        /// Appelable explicitement par un script qui vient de déplacer
        /// l'objet (voir <c>CaptureSpawnPoint</c>).
        /// </summary>
        public void CaptureSpawnPoint()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Capture tardive, après que la physique a eu l'occasion de replacer
        /// l'objet. Start est le premier moment où la pose est fiable.
        /// </summary>
        private void Start()
        {
            CaptureSpawnPoint();
        }

        // =====================================================================
        // BOUCLE PRINCIPALE
        // =====================================================================

        /// <summary>
        /// Chaque frame : on teste d'abord la chute, puis le bouton de rappel.
        /// </summary>
        private void Update()
        {
            // 1. L'objet est tombé trop bas → remise en place immédiate.
            if (transform.position.y < minHeightBeforeRespawn)
            {
                Respawn();
                return; // On quitte : inutile de tester le bouton dans la même frame.
            }

            // 2. Détection du front montant du bouton de rappel.
            bool recallPressed = IsRecallButtonPressed();
            if (recallPressed && !_wasRecallPressed)
            {
                Respawn();
            }
            _wasRecallPressed = recallPressed; // Mémorisation pour la prochaine frame.
        }

        // =====================================================================
        // LECTURE DU BOUTON DE RAPPEL
        // =====================================================================

        /// <summary>
        /// Vrai si le bouton de rappel est enfoncé (peut être lu sur AU MOINS
        /// une manette). On teste successivement le clavier, puis toutes les
        /// manettes, ce qui rend le système agnostique du matériel.
        /// </summary>
        private bool IsRecallButtonPressed()
        {
            // 1. Clavier (touche R) — pratique pour tester dans l'éditeur. Toujours actif.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;

            // 2. Boutons de manette : désactivés par défaut (enableButtonRecall) car
            //    A/B/X/Y sont réservés aux actions dupliquer/supprimer. Réactivables
            //    depuis l'Inspector si besoin.
            if (!enableButtonRecall)
                return false;

            var devices = new List<XRInputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
            foreach (var dev in devices)
            {
                if (dev.isValid)
                {
                    if (dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool btn) && btn)
                        return true;
                    if (dev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool btn2) && btn2)
                        return true;
                }
            }

            // 3. Sécurité : test explicite des deux mains (une manette peut ne pas
            //    apparaître dans la liste « Controller » selon le runtime).
            XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed) && aPressed)
                return true;

            XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.primaryButton, out bool xPressed) && xPressed)
                return true;

            return false;
        }

        // =====================================================================
        // ACTION : REMISE EN PLACE
        // =====================================================================

        /// <summary>
        /// Remet l'objet exactement à sa position et rotation de départ.
        ///
        /// Étapes, dans l'ordre :
        ///  1. Si une main tient encore l'objet, on le relâche SANS lui donner de
        ///     vitesse de lancer (sinon il repartirait en l'air). Indispensable pour
        ///     éviter que l'objet « glisse » dans la main.
        ///  2. On repasse le Rigidbody en physique normale (non cinématique, gravité active).
        ///  3. On remet les vitesses à zéro.
        ///  4. On replace la transformation au point d'origine.
        /// </summary>
        public void Respawn()
        {
            // Détacher si une main le tient encore
            VRGrabHand[] hands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                // IsChildOf : vrai si l'objet est quelque part sous la main dans la
                // hiérarchie (cas d'un objet composé placé en enfant de la main).
                if (transform.IsChildOf(hand.transform))
                {
                    hand.ReleaseObject(applyThrowVelocity: false);
                }
            }

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // On passe par Rigidbody.position/rotation et NON par
            // transform.SetPositionAndRotation : sur un corps NON cinématique,
            // déplacer le Transform est ignoré au frame suivant par la
            // physique (qui réimpose sa propre pose), ce qui faisait
            // « flicker » l'objet à sa position d'origine au lieu de le
            // replacer. Les propriétés Rigidbody, elles, sont autoritaires.
            _rb.position = _spawnPosition;
            _rb.rotation = _spawnRotation;

            // Le Transform suit immédiatement, sans attendre le step physique :
            // sinon le joueur voit l'objet à l'ancien endroit pendant une image.
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }
    }
}