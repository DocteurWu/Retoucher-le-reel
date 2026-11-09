// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRThumbMicrogestures.cs — MICROGESTES DU POUCE (façon Meta), point d'accès unique
//
//  RÔLE
//    Main en poing lâche, le pouce sert de « pavé tactile » sur le côté de l'index :
//      • TAP        : poser puis relever le pouce, sans glisser → téléportation
//                     (1er tap = arc, 2e tap = départ) — lu par VRTeleportation ;
//      • GLISSEMENT : pouce posé qui glisse à gauche / à droite → rotation par
//                     crans — lu par VRSnapTurn.
//
//  ⚠️ Le TAP est reconnu au RELÂCHEMENT du pouce : un glissement commence lui
//     aussi par un appui, il ne doit pas ouvrir l'arc de téléportation au passage.
//
//  Calcul fait UNE fois par frame (cache) quel que soit le nombre de lecteurs :
//  les événements (tap, glissement) restent vrais pendant toute la frame où ils
//  se produisent, pour tous les scripts qui les lisent.
// =====================================================================================
using UnityEngine;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    /// <summary>Tap et glissements du pouce sur l'index, par main.</summary>
    public static class VRThumbMicrogestures
    {
        // --- Réglages (VRTeleportation y recopie ses valeurs d'Inspector) -----------

        /// <summary>Distance pouce ↔ côté de l'index (m) en dessous de laquelle le pouce est posé.</summary>
        public static float PressDistance = 0.022f;

        /// <summary>Distance (m) au-delà de laquelle le pouce est relevé (hystérésis).</summary>
        public static float ReleaseDistance = 0.035f;

        /// <summary>Déplacement latéral du pouce (m) qui vaut un glissement.</summary>
        public static float SwipeDistance = 0.015f;

        /// <summary>Un appui plus long que ça n'est pas un tap (pouce simplement posé).</summary>
        public static float MaxTapDuration = 0.6f;

        // --- État par main -----------------------------------------------------------

        private struct HandState
        {
            public int Frame;
            public bool Pose;
            public Vector3 AimOrigin;
            public Vector3 AimDirection;
            public bool Pressed;
            public float PressTime;
            public float ReferenceLateral;
            public bool Swiped;
            public bool Tap;       // vrai pendant la frame du tap
            public int Swipe;      // -1 / +1 pendant la frame du glissement, 0 sinon
        }

        private static readonly HandState[] _states = new HandState[2];

        /// <summary>Chaque partie repart d'un pouce relâché (statiques gardées entre deux parties : rechargement du domaine désactivé).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RemettreAZero()
        {
            _states[0] = default;
            _states[1] = default;
        }

        /// <summary>Le poing du microgeste est-il formé (main suivie, poing lâche) ?</summary>
        public static bool IsPoseActive(XRNode node) => Get(node).Pose;

        /// <summary>Un tap vient-il d'être fait CETTE frame ?</summary>
        public static bool TapThisFrame(XRNode node) => Get(node).Tap;

        /// <summary>Glissement de CETTE frame : +1 = vers la droite, -1 = vers la gauche, 0 = aucun.</summary>
        public static int SwipeThisFrame(XRNode node) => Get(node).Swipe;

        /// <summary>Rayon de visée du poing (MONDE). Faux si le poing n'est pas formé.</summary>
        public static bool TryGetAim(XRNode node, out Vector3 origin, out Vector3 direction)
        {
            HandState s = Get(node);
            origin = s.AimOrigin;
            direction = s.AimDirection;
            return s.Pose;
        }

        // --- Calculs purs (testés en EditMode) -----------------------------------------

        /// <summary>Sens d'un glissement : ±1 si le déplacement latéral dépasse le seuil, 0 sinon.</summary>
        public static int ClassifySwipe(float lateralDelta, float threshold)
        {
            if (lateralDelta > threshold) return 1;
            if (lateralDelta < -threshold) return -1;
            return 0;
        }

        /// <summary>Un relâchement du pouce est-il un tap ? (ni glissement, ni appui prolongé)</summary>
        public static bool IsTapRelease(bool swiped, float pressDuration, float maxDuration)
        {
            return !swiped && pressDuration <= maxDuration;
        }

        // --- Mise à jour (une fois par frame et par main) ---------------------------

        private static HandState Get(XRNode node)
        {
            int i = node == XRNode.LeftHand ? 0 : 1;
            if (_states[i].Frame != Time.frameCount)
            {
                Update(node, ref _states[i]);
                _states[i].Frame = Time.frameCount;
            }
            return _states[i];
        }

        private static void Update(XRNode node, ref HandState s)
        {
            s.Tap = false;
            s.Swipe = 0;

            s.Pose = VRHandGestures.TryGetMicrogesture(node, out float thumbToIndex,
                out s.AimOrigin, out s.AimDirection, out Vector3 thumbWorld);
            if (!s.Pose)
            {
                // Poing perdu : l'appui en cours est abandonné (ni tap, ni glissement).
                s.Pressed = false;
                return;
            }

            bool pressed = VRHandGestures.IsThumbTapPressed(thumbToIndex, s.Pressed, PressDistance, ReleaseDistance);
            float lateral = Lateral(thumbWorld - s.AimOrigin);

            if (pressed && !s.Pressed)
            {
                // Pouce posé : début d'un tap ou d'un glissement.
                s.PressTime = Time.time;
                s.ReferenceLateral = lateral;
                s.Swiped = false;
            }
            else if (pressed && !s.Swiped)
            {
                int sens = ClassifySwipe(lateral - s.ReferenceLateral, SwipeDistance);
                if (sens != 0)
                {
                    s.Swipe = sens;
                    s.Swiped = true; // un seul cran par glissement : relever le pouce pour recommencer
                }
            }
            else if (!pressed && s.Pressed)
            {
                s.Tap = IsTapRelease(s.Swiped, Time.time - s.PressTime, MaxTapDuration);
            }

            s.Pressed = pressed;
        }

        /// <summary>
        /// Composante « gauche-droite » d'un vecteur, VUE PAR LE JOUEUR (axe droit de
        /// la caméra, à plat). Mesurée par rapport à la base de l'index : un mouvement
        /// de toute la main ne compte pas comme un glissement du pouce.
        /// </summary>
        private static float Lateral(Vector3 v)
        {
            Camera cam = Camera.main;
            Vector3 droite = cam != null ? cam.transform.right : Vector3.right;
            droite.y = 0f;
            if (droite.sqrMagnitude < 1e-6f) droite = Vector3.right;
            return Vector3.Dot(v, droite.normalized);
        }
    }
}
