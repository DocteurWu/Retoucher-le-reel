// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRInputDiagnostics.cs — RAPPORT DE DIAGNOSTIC (outil, sans coût au runtime)
//
//  RÔLE
//    Produit un rapport texte complet de l'état du système VR, à copier-coller
//    pour signaler un bug :
//      1. runtime XR (loader actif, sous-systèmes, mode d'origine du suivi) ;
//      2. TOUS les périphériques de l'Input System : layout, usages, et la
//         liste de leurs boutons/axes AVEC leur valeur ;
//      3. les périphériques de l'ancienne API XR (validité) ;
//      4. la lecture effective de X/Y/A/B (et le contrôle qui a répondu) ;
//      5. l'état du jeu (verrou, locomotion, objet tenu, XR Origin, mains) ;
//      6. le nombre d'exceptions neutralisées par la couche d'entrée.
//
//  UTILISATION
//    C'est une classe STATIQUE : il n'y a rien à mettre en scène. Dans le
//    débogueur Unity (Console → Expression Evaluator), évaluer :
//        RemodelerLeReel.VR.VRInputDiagnostics.BuildReport()
//    ou, temporairement, appeler dans un Update :
//        Debug.Log(VRInputDiagnostics.BuildReport());
//
//  RÉFÉRENCES : voir README.md (section 7)
// =====================================================================================
using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using InputDevice = UnityEngine.InputSystem.InputDevice;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// RAPPORT DE DIAGNOSTIC COMPLET — à copier-coller pour signaler un bug.
    ///
    /// Contenu :
    ///   1. l'état du runtime XR (loader, mode d'origine du suivi) ;
    ///   2. TOUS les périphériques de l'Input System : layout, nom, usages,
    ///      et la liste des contrôles de type bouton/axe avec leur valeur ;
    ///   3. les périphériques de l'ancienne API XR (état de validité) ;
    ///   4. la lecture effective de chaque bouton et de chaque stick ;
    ///   5. l'état du jeu : verrou d'entrée, locomotion, objet tenu, position
    ///      du joueur, état vertical ;
    ///   6. le nombre d'exceptions déjà neutralisées par la couche d'entrée.
    ///
    /// L'utilisateur déclenche ce rapport avec la touche F1 (éditeur) ou
    /// automatiquement toutes les N secondes si l'option est activée.
    /// </summary>
    public static class VRInputDiagnostics
    {
        /// <summary>Construit le rapport complet (une seule chaîne, à copier-coller).</summary>
        public static string BuildReport()
        {
            var sb = new StringBuilder(4096);

            sb.AppendLine("================ RAPPORT DIAGNOSTIC VR ================");
            sb.AppendLine("Date      : " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Scène     : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.AppendLine("Plateforme: " + Application.platform + "  |  Unity " + Application.unityVersion);
            sb.AppendLine("Exceptions neutralisées : " + VRInputLog.ExceptionCount);

            // --- 1. Runtime XR ---------------------------------------------
            sb.AppendLine();
            sb.AppendLine("---- 1. RUNTIME XR ----");
            try
            {
                var gs = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (gs != null && gs.Manager != null)
                {
                    sb.AppendLine("Loader actif        : " + (gs.Manager.activeLoader != null ? gs.Manager.activeLoader.name : "<AUCUN>"));
                    sb.AppendLine("Initialisé          : " + gs.Manager.isInitializationComplete);
                }
                else
                {
                    sb.AppendLine("XRGeneralSettings   : <null>");
                }

                var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                for (int i = 0; i < subsystems.Count; i++)
                {
                    var sub = subsystems[i];
                    if (sub == null) continue;
                    sb.AppendLine("Sous-système : " + sub.GetType().Name +
                                  " running=" + sub.running +
                                  " originMode=" + sub.GetTrackingOriginMode());
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("ERREUR lecture runtime XR : " + e.Message);
            }

            // --- 2. Périphériques Input System --------------------------------
            sb.AppendLine();
            sb.AppendLine("---- 2. PERIPHERIQUES INPUT SYSTEM ----");
            var devices = InputSystem.devices;
            sb.AppendLine("Nombre de périphériques : " + devices.Count);
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice dev = devices[i];
                sb.AppendLine();
                sb.AppendLine("  [" + i + "] " + dev.name);
                sb.AppendLine("      layout      : " + dev.layout);
                sb.AppendLine("      displayName : " + dev.displayName);
                sb.AppendLine("      added       : " + dev.added + "  enabled=" + dev.enabled);
                sb.AppendLine("      usages      : " + DescribeUsages(dev));
                sb.AppendLine("      boutons/axes : " + DescribeControls(dev));
            }

            // --- 3. Ancienne API XR -----------------------------------------
            sb.AppendLine();
            sb.AppendLine("---- 3. PERIPHERIQUES ANCIENNE API XR ----");
            foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand, XRNode.CenterEye })
            {
                XRInputDevice legacy = InputDevices.GetDeviceAtXRNode(node);
                sb.AppendLine("  " + node + " : valid=" + legacy.isValid +
                              (legacy.isValid ? ("  name=" + legacy.name) : ""));
            }

            // --- 4. Lecture effective des entrées ----------------------------
            sb.AppendLine();
            sb.AppendLine("---- 4. LECTURE EFFECTIVE ----");
            sb.AppendLine("  X (dupliquer, main G) : " + DescribeButton(VRButton.X, XRNode.LeftHand));
            sb.AppendLine("  Y (supprimer, main G) : " + DescribeButton(VRButton.Y, XRNode.LeftHand));
            sb.AppendLine("  A (sauter, main D)    : " + DescribeButton(VRButton.A, XRNode.RightHand));
            sb.AppendLine("  B (main D)            : " + DescribeButton(VRButton.B, XRNode.RightHand));
            sb.AppendLine("  Stick G               : " + VRStickReader.Read(XRNode.LeftHand).ToString("F3"));
            sb.AppendLine("  Stick D               : " + VRStickReader.Read(XRNode.RightHand).ToString("F3"));

            // --- 5. État du jeu ----------------------------------------------
            sb.AppendLine();
            sb.AppendLine("---- 5. ETAT DU JEU ----");
            sb.AppendLine("  Mode locomotion   : " + VRLocomotionSettings.Label());
            sb.AppendLine("  Verrou echelle    : " + (VRInputLock.ScaleModeActive ? "ARME (stick " + VRInputLock.ScaleStickNode + ")" : "libre"));
            sb.AppendLine("  Prehension bloq.  : " + VRInputLock.GrabSuppressed);
            sb.AppendLine("  Scale en cours    : " + VRScaleGrab.IsScalingActive);
            sb.AppendLine("  Teleportation vise: " + VRTeleportation.IsAiming);
            sb.AppendLine("  Casque detecte    : " + VRDeviceState.HmdConnected);

            Rigidbody held = VRGrabHand.GetAnyGrabbedBody(out VRGrabHand holder);
            sb.AppendLine("  Objet tenu        : " + (held != null ? held.name : "<aucun>") +
                          (holder != null ? (" par " + holder.handNode) : ""));

            var locomotion = UnityEngine.Object.FindAnyObjectByType<VRSmoothLocomotion>();
            if (locomotion != null && locomotion.xrOriginTransform != null)
            {
                sb.AppendLine("  XR Origin         : " + locomotion.xrOriginTransform.position.ToString("F3") +
                              "  rot=" + locomotion.xrOriginTransform.eulerAngles.ToString("F1"));
            }

            sb.AppendLine("  Mains             :");
            for (int i = 0; i < VRGrabHand.AllHands.Count; i++)
            {
                VRGrabHand hand = VRGrabHand.AllHands[i];
                if (hand == null) continue;
                sb.AppendLine("      " + hand.handNode + " pos=" + hand.transform.position.ToString("F3") +
                              " parent=" + (hand.transform.parent != null ? hand.transform.parent.name : "<null>") +
                              " tient=" + (hand.GrabbedBody != null ? hand.GrabbedBody.name : "-"));
            }

            sb.AppendLine("================ FIN DU RAPPORT ================");
            return sb.ToString();
        }

        private static string DescribeUsages(InputDevice dev)
        {
            var sb = new StringBuilder();
            var usages = dev.usages;
            for (int i = 0; i < usages.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(usages[i].ToString());
            }
            return sb.Length > 0 ? sb.ToString() : "<aucun>";
        }

        /// <summary>Liste les contrôles de type bouton / axe avec leur valeur.</summary>
        private static string DescribeControls(InputDevice dev)
        {
            var sb = new StringBuilder();
            int count = 0;

            var controls = dev.allControls;
            for (int i = 0; i < controls.Count && count < 40; i++)
            {
                InputControl c = controls[i];

                if (c is ButtonControl btn)
                {
                    sb.Append("[B]").Append(c.name).Append("=").Append(btn.isPressed ? "1" : "0");
                    if (c.aliases.Count > 0) sb.Append("(alias:").Append(c.aliases[0].ToString()).Append(")");
                    sb.Append(' ');
                    count++;
                }
                else if (c is AxisControl axis)
                {
                    sb.Append("[A]").Append(c.name).Append("=").Append(axis.ReadValue().ToString("F2")).Append(' ');
                    count++;
                }
                else if (c is Vector2Control vec2)
                {
                    sb.Append("[V2]").Append(c.name).Append("=").Append(vec2.ReadValue().ToString("F2")).Append(' ');
                    count++;
                }
            }

            return count == 0 ? "<aucun bouton/axe>" : sb.ToString();
        }

        /// <summary>
        /// Détaille la lecture d'un bouton : valeur lue, et contrôle du
        /// périphérique qui a réellement répondu (indispensable pour vérifier
        /// que le BON bouton est lu sur la BONNE main).
        /// </summary>
        private static string DescribeButton(VRButton button, XRNode node)
        {
            string value = VRButtonReader.Read(button) ? "PRESSÉ" : "libre";
            string found = "<non trouvé>";

            bool isLeft = node == XRNode.LeftHand;
            var ctrl = isLeft
                ? UnityEngine.InputSystem.XR.XRController.leftHand
                : UnityEngine.InputSystem.XR.XRController.rightHand;

            if (ctrl != null)
            {
                bool isPrimary = button == VRButton.X || button == VRButton.A;
                var c = VRInputControls.Find<ButtonControl>(ctrl, isPrimary ? "primaryButton" : "secondaryButton");
                if (c != null)
                {
                    found = ctrl.name + " / " + c.name + "=" + (c.isPressed ? "1" : "0") +
                            " (état=" + c.ReadValueAsObject() + ")";
                }
            }

            return value + "  |  " + found;
        }
    }
}
