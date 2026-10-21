// =====================================================================================
//  VRInput.cs — COUCHE D'ENTRÉE (fondation de tout le module VR)
//
//  RÔLE
//    Centralise TOUTE la lecture des entrées et fournit trois services :
//      1. VRInputLock      : verrou d'entrée « mode échelle » (durée de vie : 1 frame)
//      2. VRDeviceState    : « un casque est-il connecté ? » (remplace l'obsolète
//                            XRSettings.isDeviceActive qui mentait sous Unity 6)
//      3. VRInputControls  : LECTEUR DE CONTRÔLES SÛR (voir la règle ci-dessous)
//      4. VRButtonReader   : lecture des boutons X / Y / A / B
//      5. VRStickReader    : lecture des sticks (et helpers de deadzone)
//      6. VRInputLog       : journal d'exceptions (une seule fois par signature)
//
//  ⚠️ RÈGLE ABSOLUE DE CE FICHIER
//    NE JAMAIS utiliser InputControl.GetChildControl<T>(path) : cette API ne
//    renvoie PAS null quand le contrôle n'existe pas, elle LÈVE une
//    ArgumentException, et l'appel.interrompt la frame en cours (donc : plus de
//    gravité, plus de locomotion, plus de duplication...). Utiliser TOUJOURS
//    VRInputControls.Find<T>(device, nom) ou FindAny<T>(device, nom1, nom2…),
//    qui acceptent les alias et ne lèvent jamais.
//
//  ORDRE DE LECTURE DES BOUTONS (du plus fiable au plus large)
//    0. XRController.leftHand / .rightHand (Input System, direct)
//    1. Ancienne API XR (UnityEngine.XR.InputDevices)
//    2. Input Action dédiée (OpenXR)
//    3. Balayage des périphériques de l'Input System (nom ou alias)
//
//  RÉFÉRENCES : voir README.md (section 6 « Les pièges déjà payés »)
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using InputDevice = UnityEngine.InputSystem.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// VERROU D'ENTRÉE PARTAGÉ — arbitre les gestes qui se disputent le même
    /// stick. Sans lui, la préhension, l'échelle et la locomotion se marchent
    /// dessus et le joueur téléporte l'objet qu'il est en train de faire
    /// tourner.
    ///
    /// Le modèle est volontairement minimaliste : un petit contrat statique,
    /// alimenté par VRScaleGrab et lu par les autres scripts.
    ///
    /// RÈGLE UNIQUE : « poignée de la main libre serrée = mode échelle ».
    /// Dans ce mode :
    ///   - le stick de la main LIBRE sert à l'échelle (pas à la locomotion),
    ///   - une main qui ne tient RIEN ne peut pas attraper (sinon le joueur
    ///     vole l'objet qu'il est en train de mettre à l'échelle),
    ///   - la téléportation et la locomotion continue sont suspendues sur ce stick.
    ///
    /// ⚠️ DURÉE DE VIE : le verrou est armé POUR UNE SEULE FRAME. Il expire
    /// tout seul au frame suivant, qu'un purgeur externe existe ou non.
    ///
    /// C'est la correction d'un bug bloquant : le purgeur était un GameObject
    /// créé au chargement (RuntimeInitializeOnLoadMethod) qui disparaissait au
    /// changement de scène (Menu → Jeu). Le verrou restait alors armé POUR
    /// TOUJOURS : plus aucune saisie possible, snap turn mort, locomotion
    /// bloquée. Une durée de vie liée au frameCount supprime cette classe de
    /// bug : il n'y a plus rien à maintenir en vie entre deux scènes.
    /// </summary>
    public static class VRInputLock
    {
        /// <summary>Frame pendant lequel le verrou a été armé (-1 = jamais).</summary>
        private static int _armedFrame = -1;

        /// <summary>Interne : le mode échelle est-il conceptuellement actif ?</summary>
        private static bool _scaleModeActive;

        /// <summary>Interne : la préhension est-elle conceptuellement bloquée ?</summary>
        private static bool _grabSuppressed;

        /// <summary>
        /// Vrai tant que le mode échelle est actif (poignée main libre serrée)
        /// ET que le verrou n'a pas expiré.
        /// </summary>
        public static bool ScaleModeActive => IsArmedThisFrame && _scaleModeActive;

        /// <summary>
        /// Nœud du stick actuellement réservé à l'échelle (main libre).
        /// null = aucune réservation. (XRNode ne possède pas de valeur « None »,
        /// d'où le nullable.)
        /// </summary>
        public static XRNode? ScaleStickNode { get; private set; }

        /// <summary>
        /// Vrai si une préhension est interdite CE FRAME (mode échelle actif).
        /// Expire automatiquement avec le verrou : plus aucune préhension
        /// bloquée « dans le vide » d'une frame à l'autre.
        /// </summary>
        public static bool GrabSuppressed => IsArmedThisFrame && _grabSuppressed;

        /// <summary>
        /// Le verrou est-il encore valide ? Il expire dès que le frame change :
        /// le gesture qui l'a armé doit donc le ré-armer à chaque frame
        /// (voir VRScaleGrab.ContinuePinch / UpdateThumbstick).
        /// </summary>
        public static bool IsArmedThisFrame => _armedFrame == Time.frameCount;

        /// <summary>
        /// Le stick de ce nœud est-il actuellement réquisitionné par l'échelle ?
        /// Les scripts de locomotion l'interrogent pour s'effacer.
        /// </summary>
        public static bool IsStickReserved(XRNode node)
        {
            if (!IsArmedThisFrame) return false;
            return ScaleModeActive && ScaleStickNode.HasValue && node == ScaleStickNode.Value;
        }

        /// <summary>
        /// Signale qu'une préhension est bloquée ce frame (typiquement parce que
        /// le mode échelle vient d'être détecté).
        /// </summary>
        public static void SuppressGrabThisFrame()
        {
            if (!IsArmedThisFrame) return;
            _grabSuppressed = true;
        }

        /// <summary>Démarre (ou prolonge) le mode échelle sur un stick donné.</summary>
        public static void BeginScaleMode(XRNode stickNode)
        {
            _scaleModeActive = true;
            ScaleStickNode = stickNode;
            _grabSuppressed = true;
            _armedFrame = Time.frameCount;
        }

        /// <summary>Termine le mode échelle (poignée relâchée).</summary>
        public static void EndScaleMode()
        {
            _scaleModeActive = false;
            ScaleStickNode = null;
            _grabSuppressed = false;
            _armedFrame = -1;
        }
    }

    /// <summary>
    /// ÉTAT DU CASQUE CONNECTÉ — remplace XRSettings.isDeviceActive.
    ///
    /// <see cref="UnityEngine.XR.XRSettings"/> est OBSOLÈTE en Unity 6 et ne
    /// reflète pas l'état réel du runtime OpenXR : les raccourcis clavier de
    /// l'éditeur restaient donc actifs même casque branché (« A » sautait en
    /// même temps que la strafe, « D » faisait un snap turn, ESPACE saisissait
    /// les deux mains et sautait en même temps).
    ///
    /// La seule source de vérité fiable : la présence d'un périphérique XR
    /// (contrôleur ou casque) dans l'Input System. Si un casque est branché,
    /// le clavier n'est plus lu du tout.
    /// </summary>
    public static class VRDeviceState
    {
        private static bool _cached;
        private static int _cachedFrame = -1;

        /// <summary>Un casque / contrôleur XR est-il présent ?</summary>
        public static bool HmdConnected
        {
            get
            {
                // Cache par frame : la propriété est lue plusieurs fois par frame
                // (boutons, sticks, locomotion...) et la liste des périphériques
                // ne change pas d'une frame à l'autre.
                if (_cachedFrame == Time.frameCount) return _cached;

                bool connected = false;
                var devices = InputSystem.devices;
                for (int i = 0; i < devices.Count; i++)
                {
                    InputDevice dev = devices[i];
                    if (dev is UnityEngine.InputSystem.XR.XRController) { connected = true; break; }
                    if (dev is UnityEngine.InputSystem.XR.XRHMD) { connected = true; break; }
                }

                _cached = connected;
                _cachedFrame = Time.frameCount;
                return connected;
            }
        }

        /// <summary>
        /// Vrai quand les raccourcis clavier/souris de l'éditeur doivent être
        /// pris en compte (donc : aucun casque détecté).
        /// </summary>
        public static bool EditorFallbackAllowed => !HmdConnected;
    }

    /// <summary>
    /// LECTEUR DE CONTRÔLES SÛR — remplace tous les GetChildControl().
    ///
    /// ⚠️ POURQUOI CE HELPER EST INDISPENSABLE :
    /// <c>InputControl.GetChildControl&lt;T&gt;(path)</c> ne renvoie PAS null
    /// quand le contrôle n'existe pas : il LÈVE une ArgumentException
    /// (« Cannot find input control 'xButton' »). Dans notre projet, les
    /// périphériques n'ont pas tous les mêmes contrôles (le casque n'a pas de
    /// « grip », le profil Meta Quest Touch Plus n'a pas de « xButton » mais
    /// un alias « X », etc.). Chaque appel raté :
    ///   - interrompait la frame en cours (le Update qui appelait le lecteur
    ///     s'arrêtait net : le saut, la duplication et la suppression
    ///     ne fonctionnaient PLUS DU TOUT) ;
    ///   - polluait la console avec des dizaines d'exceptions par seconde.
    ///
    /// Ce helper ne lève JAMAIS d'exception : il renvoie null si le contrôle
    /// est absent, et il accepte aussi les ALIAS (c'est ce qui permet de
    /// lire « xButton », « X », « primaryButton »...).
    /// </summary>
    public static class VRInputControls
    {
        /// <summary>
        /// Cherche un contrôle enfant d'un périphérique par son NOM ou l'un de
        /// ses ALIAS. Renvoie null si absent ou si le type ne correspond pas.
        /// Ne lève jamais d'exception.
        /// </summary>
        public static T Find<T>(InputDevice dev, string nameOrAlias) where T : InputControl
        {
            if (dev == null || string.IsNullOrEmpty(nameOrAlias)) return null;

            // 1. Résolution par chemin (gère nativement les alias).
            try
            {
                InputControl control = dev.TryGetChildControl(nameOrAlias);
                if (control is T typed) return typed;
            }
            catch (Exception e)
            {
                VRInputLog.ExceptionOnce("VRInputControls.Find(" + nameOrAlias + ")", e);
            }

            // 2. Balayage exhaustif : nom exact ou alias, insensible à la casse.
            try
            {
                var controls = dev.allControls;
                for (int i = 0; i < controls.Count; i++)
                {
                    InputControl c = controls[i];
                    if (!(c is T t)) continue;

                    if (string.Equals(c.name, nameOrAlias, System.StringComparison.OrdinalIgnoreCase))
                        return t;

                    var aliases = c.aliases;
                    for (int a = 0; a < aliases.Count; a++)
                    {
                        if (string.Equals(aliases[a].ToString(), nameOrAlias, System.StringComparison.OrdinalIgnoreCase))
                            return t;
                    }
                }
            }
            catch (Exception e)
            {
                VRInputLog.ExceptionOnce("VRInputControls.Find(scan)", e);
            }

            return null;
        }

        /// <summary>
        /// Comme <see cref="Find{T}"/>, mais essaie plusieurs noms dans l'ordre
        /// (aliases + noms génériques) et renvoie le premier trouvé.
        /// </summary>
        public static T FindAny<T>(InputDevice dev, params string[] namesOrAliases) where T : InputControl
        {
            if (dev == null || namesOrAliases == null) return null;

            for (int i = 0; i < namesOrAliases.Length; i++)
            {
                T found = Find<T>(dev, namesOrAliases[i]);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Existe-t-il un contrôle de ce type (nom ou alias) ?</summary>
        public static bool Has<T>(InputDevice dev, string nameOrAlias) where T : InputControl
        {
            return Find<T>(dev, nameOrAlias) != null;
        }
    }

    /// <summary>
    /// JOURNAL DE DIAGNOSTIC — exceptions et messages uniques.
    ///
    /// But : une exception dans la couche d'entrée ne doit plus
    /// (a) casser la frame, (b) noyer la console sous des centaines de
    /// messages identiques. On ne signale chaque problème QU'UNE FOIS, avec
    /// la pile d'appels, pour pouvoir la transmettre telle quelle.
    /// </summary>
    public static class VRInputLog
    {
        private static readonly List<string> _reported = new List<string>();

        /// <summary>Nombre total d'exceptions neutralisées depuis le lancement.</summary>
        public static int ExceptionCount { get; private set; }

        /// <summary>Signale une exception (une seule fois par signature).</summary>
        public static void ExceptionOnce(string signature, Exception e)
        {
            if (e == null) return;
            ExceptionCount++;

            if (_reported.Contains(signature)) return;
            _reported.Add(signature);

            Debug.LogError(
                "[VRInput] " + signature + " : " + e.GetType().Name + " - " + e.Message +
                "\n" + e.StackTrace +
                "\n(Le message ne sera ré-affiché qu'une fois : cette exception est désormais neutralisée.)");
        }

        /// <summary>Signale un message unique (une seule fois par signature).</summary>
        public static void InfoOnce(string signature, string message)
        {
            if (_reported.Contains(signature)) return;
            _reported.Add(signature);
            Debug.Log("[VRInput] " + signature + " : " + message);
        }

        /// <summary>Remet le journal à zéro (utile dans les tests).</summary>
        public static void Reset()
        {
            _reported.Clear();
            ExceptionCount = 0;
        }
    }

    /// <summary>
    /// <summary>
    /// BOUTONS PHYSIQUES VR (standard Touch / OpenXR).
    /// X et Y sur la manette gauche.
    /// A et B sur la manette droite.
    /// </summary>
    public enum VRButton
    {
        X, // Manette gauche (primaire) - Dupliquer
        Y, // Manette gauche (secondaire) - Supprimer
        A, // Manette droite (primaire) - Sauter
        B  // Manette droite (secondaire)
    }

    /// <summary>
    /// LECTEUR DE BOUTONS VR UNIFIÉ
    /// Lit les boutons X, Y, A, B avec Input Actions dédiées OpenXR, lecture directe XRDevice et clavier.
    /// </summary>
    public static class VRButtonReader
    {
        private static readonly List<XRInputDevice> CharDeviceBuffer = new List<XRInputDevice>(4);

        private static InputAction _buttonXAction;
        private static InputAction _buttonYAction;
        private static InputAction _buttonAAction;
        private static InputAction _buttonBAction;
        private static bool _initialized;

        public static void EnsureActionsInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // X = Left Hand Primary
            _buttonXAction = new InputAction("VR_Button_X", InputActionType.Button);
            _buttonXAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            _buttonXAction.AddBinding("<XRController>{leftHand}/primaryButton");
            _buttonXAction.AddBinding("<OculusTouchController>{LeftHand}/primaryButton");
            _buttonXAction.AddBinding("<QuestTouchPlusController>{LeftHand}/primaryButton");
            _buttonXAction.AddBinding("<TouchController>{LeftHand}/primaryButton");
            _buttonXAction.AddBinding("<XRController>{LeftHand}/{PrimaryAction}");
#if UNITY_EDITOR
            _buttonXAction.AddBinding("<Keyboard>/x");
#endif
            _buttonXAction.Enable();

            // Y = Left Hand Secondary
            _buttonYAction = new InputAction("VR_Button_Y", InputActionType.Button);
            _buttonYAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
            _buttonYAction.AddBinding("<XRController>{leftHand}/secondaryButton");
            _buttonYAction.AddBinding("<OculusTouchController>{LeftHand}/secondaryButton");
            _buttonYAction.AddBinding("<QuestTouchPlusController>{LeftHand}/secondaryButton");
            _buttonYAction.AddBinding("<TouchController>{LeftHand}/secondaryButton");
            _buttonYAction.AddBinding("<XRController>{LeftHand}/{SecondaryAction}");
#if UNITY_EDITOR
            _buttonYAction.AddBinding("<Keyboard>/y");
#endif
            _buttonYAction.Enable();

            // A = Right Hand Primary
            _buttonAAction = new InputAction("VR_Button_A", InputActionType.Button);
            _buttonAAction.AddBinding("<XRController>{RightHand}/primaryButton");
            _buttonAAction.AddBinding("<XRController>{rightHand}/primaryButton");
            _buttonAAction.AddBinding("<OculusTouchController>{RightHand}/primaryButton");
            _buttonAAction.AddBinding("<QuestTouchPlusController>{RightHand}/primaryButton");
            _buttonAAction.AddBinding("<TouchController>{RightHand}/primaryButton");
            _buttonAAction.AddBinding("<XRController>{RightHand}/{PrimaryAction}");
#if UNITY_EDITOR
            _buttonAAction.AddBinding("<Keyboard>/a");
            _buttonAAction.AddBinding("<Keyboard>/space");
#endif
            _buttonAAction.Enable();

            // B = Right Hand Secondary
            _buttonBAction = new InputAction("VR_Button_B", InputActionType.Button);
            _buttonBAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            _buttonBAction.AddBinding("<XRController>{rightHand}/secondaryButton");
            _buttonBAction.AddBinding("<OculusTouchController>{RightHand}/secondaryButton");
            _buttonBAction.AddBinding("<QuestTouchPlusController>{RightHand}/secondaryButton");
            _buttonBAction.AddBinding("<TouchController>{RightHand}/secondaryButton");
            _buttonBAction.AddBinding("<XRController>{RightHand}/{SecondaryAction}");
#if UNITY_EDITOR
            _buttonBAction.AddBinding("<Keyboard>/b");
#endif
            _buttonBAction.Enable();
        }

        private static InputAction GetAction(VRButton button)
        {
            EnsureActionsInitialized();
            switch (button)
            {
                case VRButton.X: return _buttonXAction;
                case VRButton.Y: return _buttonYAction;
                case VRButton.A: return _buttonAAction;
                case VRButton.B: return _buttonBAction;
                default: return null;
            }
        }

        /// <summary>
        /// Renvoie true si le bouton a été pressé durant cette frame (front montant).
        /// </summary>
        public static bool WasPressed(VRButton button)
        {
            try
            {
                return WasPressedInternal(button);
            }
            catch (Exception e)
            {
                VRInputLog.ExceptionOnce("VRButtonReader.WasPressed(" + button + ")", e);
                return false;
            }
        }

        private static bool WasPressedInternal(VRButton button)
        {
            bool isLeft = button == VRButton.X || button == VRButton.Y;
            bool isPrimary = button == VRButton.X || button == VRButton.A;

            // 0. Direct New Input System XRController (sans latence)
            var xrCtrl = isLeft ? UnityEngine.InputSystem.XR.XRController.leftHand : UnityEngine.InputSystem.XR.XRController.rightHand;
            if (xrCtrl != null)
            {
                var btn = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(
                    xrCtrl, isPrimary ? "primaryButton" : "secondaryButton");
                if (btn != null && btn.wasPressedThisFrame) return true;
            }

            // 1. Input Action dédiée
            InputAction action = GetAction(button);
            if (action != null && action.enabled && action.WasPressedThisFrame())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renvoie true tant que le bouton est maintenu enfoncé.
        /// </summary>
        public static bool Read(VRButton button)
        {
            try
            {
                return ReadInternal(button);
            }
            catch (Exception e)
            {
                VRInputLog.ExceptionOnce("VRButtonReader.Read(" + button + ")", e);
                return false;
            }
        }

        private static bool ReadInternal(VRButton button)
        {
            bool isLeft = button == VRButton.X || button == VRButton.Y;
            bool isPrimary = button == VRButton.X || button == VRButton.A;
            XRNode node = isLeft ? XRNode.LeftHand : XRNode.RightHand;

            // 0. Direct New Input System XRController
            var xrCtrl = isLeft ? UnityEngine.InputSystem.XR.XRController.leftHand : UnityEngine.InputSystem.XR.XRController.rightHand;
            if (xrCtrl != null)
            {
                var btn = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(
                    xrCtrl, isPrimary ? "primaryButton" : "secondaryButton");
                if (btn != null && btn.isPressed) return true;
            }

            // 1. XRInputDevice (OpenXR / Legacy)
            XRInputDevice device = VRStickReader.GetDevice(node);
            if (device.isValid)
            {
                InputFeatureUsage<bool> usage = isPrimary
                    ? CommonUsages.primaryButton
                    : CommonUsages.secondaryButton;
                if (device.TryGetFeatureValue(usage, out bool pressed) && pressed) return true;
            }

            // 1b. Direct GetDevicesWithCharacteristics (sécurité si le nœud n'est pas mappé)
            CharDeviceBuffer.Clear();
            InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                (isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
            InputDevices.GetDevicesWithCharacteristics(characteristics, CharDeviceBuffer);
            for (int i = 0; i < CharDeviceBuffer.Count; i++)
            {
                InputFeatureUsage<bool> usage = isPrimary
                    ? CommonUsages.primaryButton
                    : CommonUsages.secondaryButton;
                if (CharDeviceBuffer[i].TryGetFeatureValue(usage, out bool pressed) && pressed) return true;
            }

            // 2. Input Action dédiée OpenXR
            InputAction action = GetAction(button);
            if (action != null && action.enabled)
            {
                if (action.IsPressed() || action.ReadValue<float>() > 0.5f) return true;
            }

            // 3. Input System Devices directs (parcours exhaustif)
            //    ⚠️ GetChildControl LÈVE une exception si le contrôle n'existe
            //    pas : on passe par le lecteur sûr (nom OU alias).
            string controlName = button.ToString().ToLowerInvariant() + "Button"; // xbutton / abutton
            string genericName = isPrimary ? "primaryButton" : "secondaryButton";
            string pressedName = isPrimary ? "primaryButtonPressed" : "secondaryButtonPressed";

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice dev = devices[i];
                if (!VRStickReader.DeviceMatchesHand(dev, isLeft)) continue;

                var named = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(dev, controlName);
                if (named != null && named.isPressed) return true;

                var pressedVariant = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(dev, pressedName);
                if (pressedVariant != null && pressedVariant.isPressed) return true;

                var generic = VRInputControls.Find<UnityEngine.InputSystem.Controls.ButtonControl>(dev, genericName);
                if (generic != null && generic.isPressed) return true;
            }

            // 4. Fallback Clavier (Editeur sans casque)
#if UNITY_EDITOR
            if (VRDeviceState.EditorFallbackAllowed && Keyboard.current != null)
            {
                switch (button)
                {
                    case VRButton.X:
                        if (Keyboard.current.xKey.isPressed) return true;
                        break;
                    case VRButton.Y:
                        if (Keyboard.current.yKey.isPressed) return true;
                        break;
                    case VRButton.A:
                        // ESPACE = bouton A (saut) dans l'éditeur.
                        // « A » n'est PAS utilisé ici : il sert déjà de strafe
                        // gauche dans le fallback clavier du stick (WASD), et
                        // l'utiliser pour les deux faisait sauter le joueur
                        // à chaque pas de côté.
                        if (Keyboard.current.spaceKey.isPressed) return true;
                        break;
                    case VRButton.B:
                        if (Keyboard.current.bKey.isPressed) return true;
                        break;
                }
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// LECTEUR DE STICK PARTAGÉ — une seule implémentation fallback
    /// XR, réutilisée par tous les scripts qui consomment un joystick.
    /// </summary>
    public static class VRStickReader
    {
        /// <summary>Liste réutilisée : évite une allocation à chaque frame.</summary>
        private static readonly List<XRInputDevice> DeviceBuffer = new List<XRInputDevice>(4);

        private static InputAction _leftStickAction;
        private static InputAction _rightStickAction;

        public static void EnsureActionsInitialized()
        {
            if (_leftStickAction == null)
            {
                _leftStickAction = new InputAction("VRStick_Left", InputActionType.Value);
                _leftStickAction.AddBinding("<XRController>{LeftHand}/thumbstick");
                _leftStickAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
                _leftStickAction.AddBinding("<OculusTouchController>{LeftHand}/thumbstick");
                _leftStickAction.AddBinding("<QuestTouchPlusController>{LeftHand}/thumbstick");
                _leftStickAction.AddBinding("<TouchController>{LeftHand}/thumbstick");
                _leftStickAction.AddBinding("<XRController>{leftHand}/thumbstick");
                _leftStickAction.AddBinding("<XRController>{leftHand}/primary2DAxis");
                _leftStickAction.AddBinding("<OculusTouchController>{leftHand}/thumbstick");
                _leftStickAction.AddBinding("<QuestTouchPlusController>{leftHand}/thumbstick");
                _leftStickAction.AddBinding("<LeftHandXRController>/thumbstick");
                _leftStickAction.AddBinding("<LeftHandXRController>/primary2DAxis");
#if UNITY_EDITOR
                _leftStickAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                _leftStickAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
#endif
                _leftStickAction.Enable();
            }
            else if (!_leftStickAction.enabled)
            {
                _leftStickAction.Enable();
            }

            if (_rightStickAction == null)
            {
                _rightStickAction = new InputAction("VRStick_Right", InputActionType.Value);
                _rightStickAction.AddBinding("<XRController>{RightHand}/thumbstick");
                _rightStickAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
                _rightStickAction.AddBinding("<OculusTouchController>{RightHand}/thumbstick");
                _rightStickAction.AddBinding("<QuestTouchPlusController>{RightHand}/thumbstick");
                _rightStickAction.AddBinding("<TouchController>{RightHand}/thumbstick");
                _rightStickAction.AddBinding("<XRController>{rightHand}/thumbstick");
                _rightStickAction.AddBinding("<XRController>{rightHand}/primary2DAxis");
                _rightStickAction.AddBinding("<OculusTouchController>{rightHand}/thumbstick");
                _rightStickAction.AddBinding("<QuestTouchPlusController>{rightHand}/thumbstick");
                _rightStickAction.AddBinding("<RightHandXRController>/thumbstick");
                _rightStickAction.AddBinding("<RightHandXRController>/primary2DAxis");
                _rightStickAction.Enable();
            }
            else if (!_rightStickAction.enabled)
            {
                _rightStickAction.Enable();
            }
        }

        /// <summary>
        /// Lit le joystick 2D d'une main. Retourne Vector2.zero si aucun
        /// périphérique n'est disponible.
        /// </summary>
        public static Vector2 Read(XRNode hand)
        {
            try
            {
                return ReadInternal(hand);
            }
            catch (Exception e)
            {
                VRInputLog.ExceptionOnce("VRStickReader.Read(" + hand + ")", e);
                return Vector2.zero;
            }
        }

        private static Vector2 ReadInternal(XRNode hand)
        {
            bool isLeft = hand == XRNode.LeftHand;

            // --- NIVEAU 0 : New Input System XRController (Direct, zero overhead) ---
            var xrCtrl = isLeft ? UnityEngine.InputSystem.XR.XRController.leftHand : UnityEngine.InputSystem.XR.XRController.rightHand;
            if (xrCtrl != null)
            {
                var stick = VRInputControls.FindAny<UnityEngine.InputSystem.Controls.Vector2Control>(
                    xrCtrl, "thumbstick", "primary2DAxis", "stick");
                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (v.sqrMagnitude > 1e-4f) return v;
                }
            }

            // --- NIVEAU 1 : XRInputDevice (Subsystem natif OpenXR, direct et sans latence) ---
            XRInputDevice device = GetDevice(hand);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis) && axis.sqrMagnitude > 1e-4f)
            {
                return axis;
            }

            // 1b. Direct GetDevicesWithCharacteristics (si le nœud n'expose pas l'axe sous OpenXR)
            DeviceBuffer.Clear();
            InputDeviceCharacteristics characteristics =
                InputDeviceCharacteristics.Controller |
                (isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
            InputDevices.GetDevicesWithCharacteristics(characteristics, DeviceBuffer);
            for (int i = 0; i < DeviceBuffer.Count; i++)
            {
                if (DeviceBuffer[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 charAxis) && charAxis.sqrMagnitude > 1e-4f)
                {
                    return charAxis;
                }
            }

            // --- NIVEAU 2 : Input Action dédiée OpenXR ---
            EnsureActionsInitialized();
            InputAction action = isLeft ? _leftStickAction : _rightStickAction;
            if (action != null && action.enabled)
            {
                Vector2 val = action.ReadValue<Vector2>();
                if (val.sqrMagnitude > 1e-4f) return val;
            }

            // --- NIVEAU 3 : Input System (noms d'usage et balayage exhaustif) ---
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice dev = devices[i];
                if (!DeviceMatchesHand(dev, isLeft)) continue;

                var stick = VRInputControls.FindAny<UnityEngine.InputSystem.Controls.Vector2Control>(
                    dev, "thumbstick", "primary2DAxis", "stick");

                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (v.sqrMagnitude > 1e-6f) return v;
                }
            }

#if UNITY_EDITOR
            // --- NIVEAU 4 : Fallback Clavier éditeur (sans casque) -----------
            if (VRDeviceState.EditorFallbackAllowed && Keyboard.current != null)
            {
                if (isLeft)
                {
                    float y = 0f;
                    float x = 0f;
                    if (Keyboard.current.zKey.isPressed || Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                    if (Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                    if (Mathf.Abs(x) > 0.01f || Mathf.Abs(y) > 0.01f) return new Vector2(x, y).normalized;
                }
            }
#endif

            return Vector2.zero;
        }

        /// <summary>
        /// Variante « axe seul » : lecture brute, sans deadzone ni
        /// re-normalisation. Utile pour la téléportation et la rotation par
        /// crans, qui ont leurs propres seuils.
        /// </summary>
        public static float ReadAxis(XRNode hand, int axisIndex)
        {
            Vector2 v = Read(hand);
            return axisIndex == 0 ? v.x : v.y;
        }

        /// <summary>
        /// Applique une deadzone puis re-normalise pour que le mouvement
        /// maximal reste atteint à 1. Sans ça, la zone morte réduit la vitesse
        /// maximale perçue par le joueur.
        /// </summary>
        public static float ApplyDeadzone(float value, float deadzone)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone) return 0f;

            // Re-normalise sur [deadzone, 1] pour que 1 reste la vitesse max.
            float scaled = Mathf.InverseLerp(deadzone, 1f, magnitude);
            return Mathf.Sign(value) * scaled;
        }

        /// <summary>Variante Vector2 de la deadzone (magnitude → direction).</summary>
        public static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
        {
            float magnitude = value.magnitude;
            if (magnitude <= deadzone || magnitude < 1e-6f) return Vector2.zero;

            float scaled = Mathf.InverseLerp(deadzone, 1f, magnitude);
            return value / magnitude * Mathf.Min(scaled, 1f);
        }

        /// <summary>
        /// Récupère le périphérique d'une main, avec repli sur une recherche
        /// par caractéristiques (GetDeviceAtXRNode renvoie souvent un device
        /// invalide sous OpenXR).
        /// </summary>
        public static XRInputDevice GetDevice(XRNode hand)
        {
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid) return device;

            DeviceBuffer.Clear();
            InputDeviceCharacteristics characteristics =
                InputDeviceCharacteristics.Controller |
                (hand == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);

            InputDevices.GetDevicesWithCharacteristics(characteristics, DeviceBuffer);
            if (DeviceBuffer.Count > 0) return DeviceBuffer[0];

            return default;
        }

        /// <summary>
        /// Identifie le périphérique Input System d'une main, par usages déclarés
        /// puis par nom (les noms Quest contiennent « Left »/« Right »).
        /// </summary>
        public static bool DeviceMatchesHand(InputDevice dev, bool isLeft)
        {
            if (dev == null || !dev.added) return false;

            // 1. Direct XRController match
            if (isLeft && dev == UnityEngine.InputSystem.XR.XRController.leftHand) return true;
            if (!isLeft && dev == UnityEngine.InputSystem.XR.XRController.rightHand) return true;

            // 2. Usages InternedString check
            var targetUsage = isLeft ? UnityEngine.InputSystem.CommonUsages.LeftHand : UnityEngine.InputSystem.CommonUsages.RightHand;
            string targetName = isLeft ? "left" : "right";
            var usages = dev.usages;
            for (int i = 0; i < usages.Count; i++)
            {
                if (usages[i] == targetUsage) return true;
                if (usages[i].ToString().IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            if (dev.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (dev.displayName != null && dev.displayName.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }
    }

    /// <summary>
    /// (Le purgeur de verrou « VRInputLockRunner » a été SUPPRIMÉ.)
    ///
    /// Il créait un GameObject via RuntimeInitializeOnLoadMethod pour purger
    /// le verrou d'entrée à la fin de chaque frame. Problème : ce GameObject
    /// vivait dans la scène *Menu* et était détruit au chargement de la scène
    /// de jeu (LoadScene). Le purgeur disparaissait avec lui, donc le verrou
    /// restait armé POUR TOUJOURS : dès qu'un geste d'échelle avait eu lieu,
    /// la saisie, le snap turn et la locomotion étaient définitivement
    /// bloqués (« les manettes ne font plus rien »).
    ///
    /// Le verrou est désormais à durée de vie d'UNE FRAME (voir
    /// <see cref="VRInputLock.IsArmedThisFrame"/>) : il n'a plus besoin d'être
    /// purgé par qui que ce soit, et rien ne peut plus rester bloqué.
    /// </summary>
}