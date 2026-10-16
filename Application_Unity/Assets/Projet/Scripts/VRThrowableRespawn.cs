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
    ///  2. RAPPEL MANUEL : appui sur le bouton A / X de la manette (ou touche R au clavier).
    ///     Permet au joueur de « recharger » l'objet à tout moment.
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
        /// APPELÉ UNE SEULE FOIS au chargement.
        /// On capture la position et la rotation actuelles comme point de rappel.
        /// </summary>
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
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
            // 1. Clavier (touche R) — pratique pour tester dans l'éditeur.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;

            // 2. Parcours de toutes les manettes connectées :
            //    bouton principal (A sur la droite, X sur la gauche) ou secondaire (B / Y).
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
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }
    }
}