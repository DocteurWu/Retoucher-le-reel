// =====================================================================================
//  VRHandGestures.cs — LECTURE DU SUIVI DES MAINS (XR HANDS) ET DÉTECTION DE GESTES
//
//  RÔLE
//    Point d'accès UNIQUE au XRHandSubsystem pour tous les scripts du projet :
//      - pose de la paume d'une main (pour que VRGrabHand suive la VRAIE main,
//        et non la manette posée sur la table) ;
//      - flexion (curl) de chaque doigt et force de pincement pouce/doigt ;
//      - gestes composés : saisie (poing ou pincement) et signe du pistolet.
//
//  POURQUOI CE FICHIER
//    - Le symbole XR_HANDS_1_1_OR_NEWER n'est défini QUE dans les assemblies du
//      XR Interaction Toolkit, jamais dans Assembly-CSharp : tout le code mains
//      protégé par ce #if était silencieusement retiré de la compilation.
//    - XRHand ne possède PAS de méthode GetFingerPinchStrength : les gestes sont
//      donc calculés ici à partir des seules positions d'articulations
//      (GetJoint / TryGetPose), API stable depuis XR Hands 1.0.
//
//  ESPACES DE COORDONNÉES
//    Les poses XR Hands sont exprimées dans l'espace de TRACKING (même repère que
//    les poses de manettes), c'est-à-dire en local du « Camera Offset » de
//    l'XR Origin. Utiliser ToWorld() avant tout calcul en coordonnées monde.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace RemodelerLeReel.VR
{
    public static class VRHandGestures
    {
        // --- Seuils de geste (mètres / ratio) --------------------------------------

        /// <summary>Distance pouce–doigt en dessous de laquelle le pincement vaut 1.</summary>
        private const float PinchClosedDistance = 0.015f;
        /// <summary>Distance pouce–doigt au-delà de laquelle le pincement vaut 0.</summary>
        private const float PinchOpenDistance = 0.050f;

        /// <summary>
        /// Ratio (paume→bout) / (paume→phalange proximale) d'un doigt replié dans la paume.
        /// Doigt tendu ≈ 2,6–3,0 ; poing fermé ≈ 0,8–1,1.
        /// </summary>
        private const float CurlRatioClosed = 1.15f;
        private const float CurlRatioOpen = 2.3f;

        // Hystérésis de la saisie : seuil d'appui plus haut que le seuil de relâchement,
        // sinon le moindre tremblement du tracking provoque des lâchers intempestifs.
        private const float FistPressCurl = 0.65f;
        private const float FistReleaseCurl = 0.45f;
        private const float PinchPress = 0.80f;
        private const float PinchRelease = 0.50f;

        // --- Cache du sous-système (une recherche par frame au maximum) -----------

        private static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
        private static XRHandSubsystem s_Subsystem;
        private static int s_CacheFrame = -1;

        /// <summary>Sous-système de mains en cours d'exécution, ou null.</summary>
        public static XRHandSubsystem Subsystem
        {
            get
            {
                if (s_CacheFrame != Time.frameCount)
                {
                    s_CacheFrame = Time.frameCount;
                    s_Subsystem = null;
                    s_Subsystems.Clear();
                    SubsystemManager.GetSubsystems(s_Subsystems);
                    for (int i = 0; i < s_Subsystems.Count; i++)
                    {
                        if (s_Subsystems[i] != null && s_Subsystems[i].running)
                        {
                            s_Subsystem = s_Subsystems[i];
                            break;
                        }
                    }
                }
                return s_Subsystem;
            }
        }

        /// <summary>Vrai si au moins une main est suivie en ce moment.</summary>
        public static bool AnyHandTracked
        {
            get
            {
                var sub = Subsystem;
                return sub != null && (sub.leftHand.isTracked || sub.rightHand.isTracked);
            }
        }

        /// <summary>Renvoie la main suivie correspondant au nœud XR (LeftHand / RightHand).</summary>
        public static bool TryGetHand(XRNode node, out XRHand hand)
        {
            hand = default;
            var sub = Subsystem;
            if (sub == null) return false;
            hand = node == XRNode.LeftHand ? sub.leftHand : sub.rightHand;
            return hand.isTracked;
        }

        public static bool IsTracked(XRNode node) => TryGetHand(node, out _);

        /// <summary>Pose d'une articulation, en espace de TRACKING.</summary>
        public static bool TryGetJointPose(XRHand hand, XRHandJointID id, out Pose pose)
        {
            return hand.GetJoint(id).TryGetPose(out pose);
        }

        /// <summary>
        /// Pose de la paume en espace de TRACKING : c'est la pose qui remplace celle
        /// de la manette pour VRGrabHand quand la main est suivie.
        /// </summary>
        public static bool TryGetPalmPose(XRNode node, out Pose pose)
        {
            pose = default;
            if (!TryGetHand(node, out XRHand hand)) return false;
            return TryGetJointPose(hand, XRHandJointID.Palm, out pose)
                || TryGetJointPose(hand, XRHandJointID.Wrist, out pose);
        }

        /// <summary>
        /// Normale de la PAUME (sortant de l'intérieur de la main) en coordonnées MONDE,
        /// calculée géométriquement (poignet, base de l'index, base de l'auriculaire) :
        /// indépendante des conventions d'axes du joint Palm, qui varient selon le runtime
        /// (SteamVR / ALVR / Meta).
        /// </summary>
        public static bool TryGetPalmNormal(XRNode node, out Vector3 worldNormal)
        {
            worldNormal = Vector3.zero;
            if (!TryGetHand(node, out XRHand hand)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.Wrist, out Pose wrist)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexProximal, out Pose index)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.LittleProximal, out Pose little)) return false;

            Vector3 a = index.position - wrist.position;
            Vector3 b = little.position - wrist.position;
            // Main gauche : a × b ; main droite : b × a (miroir). Sens validé en casque
            // (SteamVR/ALVR) : paume vers le visage → produit scalaire ≈ +0,9.
            Vector3 n = node == XRNode.LeftHand ? Vector3.Cross(a, b) : Vector3.Cross(b, a);
            if (n.sqrMagnitude < 1e-8f) return false;
            worldNormal = DirToWorld(n.normalized);
            return true;
        }

        /// <summary>Position MONDE du bout d'un doigt (pour les raccourcis « toucher un doigt »).</summary>
        public static bool TryGetFingerTipWorld(XRNode node, XRHandFingerID finger, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (!TryGetHand(node, out XRHand hand)) return false;
            if (!TryGetJointPose(hand, TipOf(finger), out Pose tip)) return false;
            worldPos = ToWorld(tip.position);
            return true;
        }

        /// <summary>Position MONDE du bout de l'index (pour appuyer sur des boutons du doigt).</summary>
        public static bool TryGetIndexTipWorld(XRNode node, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (!TryGetHand(node, out XRHand hand)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose tip)) return false;
            worldPos = ToWorld(tip.position);
            return true;
        }

        // --- Conversion tracking → monde ------------------------------------------

        /// <summary>
        /// Repère de tracking = parent de la caméra (Camera Offset de l'XR Origin).
        /// Null si aucune caméra principale : les poses sont alors renvoyées telles quelles.
        /// </summary>
        public static Transform TrackingSpace
        {
            get
            {
                var cam = Camera.main;
                return cam != null ? cam.transform.parent : null;
            }
        }

        public static Vector3 ToWorld(Vector3 trackingPos)
        {
            var space = TrackingSpace;
            return space != null ? space.TransformPoint(trackingPos) : trackingPos;
        }

        public static Vector3 DirToWorld(Vector3 trackingDir)
        {
            var space = TrackingSpace;
            return space != null ? space.TransformDirection(trackingDir) : trackingDir;
        }

        // --- Mesures élémentaires ---------------------------------------------------

        /// <summary>
        /// Force de pincement pouce ↔ bout du doigt, de 0 (écartés ≥ 5 cm) à 1 (collés ≤ 1,5 cm).
        /// </summary>
        public static float GetPinch(XRHand hand, XRHandFingerID finger)
        {
            if (finger == XRHandFingerID.Thumb) return 0f;
            if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose thumb)) return 0f;
            if (!TryGetJointPose(hand, TipOf(finger), out Pose tip)) return 0f;
            return PinchFromDistance(Vector3.Distance(thumb.position, tip.position));
        }

        /// <summary>
        /// Flexion d'un doigt, de 0 (tendu) à 1 (replié dans la paume). Indépendant de
        /// la taille de la main car basé sur un ratio de distances à la paume.
        /// </summary>
        public static float GetCurl(XRHand hand, XRHandFingerID finger)
        {
            if (!TryGetJointPose(hand, XRHandJointID.Palm, out Pose palm)) return 0f;
            if (!TryGetJointPose(hand, TipOf(finger), out Pose tip)) return 0f;
            if (!TryGetJointPose(hand, ProximalOf(finger), out Pose prox)) return 0f;

            return CurlFromDistances(Vector3.Distance(palm.position, tip.position),
                                     Vector3.Distance(palm.position, prox.position));
        }

        // --- Calculs purs (sans XR, testés en EditMode : Editor/Tests) -------------

        /// <summary>Pincement 0..1 à partir de la distance pouce–bout du doigt (m).</summary>
        public static float PinchFromDistance(float thumbToTip)
        {
            return 1f - Mathf.InverseLerp(PinchClosedDistance, PinchOpenDistance, thumbToTip);
        }

        /// <summary>
        /// Flexion 0..1 à partir des distances paume→bout et paume→phalange proximale (m).
        /// Phalange trop proche de la paume (&lt; 5 mm, articulation dégénérée) → 0.
        /// </summary>
        public static float CurlFromDistances(float palmToTip, float palmToProximal)
        {
            if (palmToProximal < 0.005f) return 0f;
            float ratio = palmToTip / palmToProximal;
            return 1f - Mathf.InverseLerp(CurlRatioClosed, CurlRatioOpen, ratio);
        }

        /// <summary>
        /// Décision de saisie avec hystérésis : seuils d'appui plus hauts que ceux de
        /// relâchement. <paramref name="fist"/> = flexion moyenne majeur + annulaire.
        /// </summary>
        public static bool IsGrabFromValues(float fist, float pinch, bool wasActive)
        {
            if (wasActive)
                return fist > FistReleaseCurl || pinch > PinchRelease;
            return fist > FistPressCurl || pinch > PinchPress;
        }

        /// <summary>Posture du pistolet : index tendu, majeur + annulaire repliés.</summary>
        public static bool IsFingerGunPose(float indexCurl, float middleCurl, float ringCurl)
        {
            return indexCurl < 0.35f && middleCurl > 0.55f && ringCurl > 0.45f;
        }

        /// <summary>
        /// Posture du MICROGESTE (téléportation façon Meta) : poing lâche, index plié
        /// (ce n'est pas le pistolet), majeur et annulaire repliés.
        /// </summary>
        public static bool IsMicrogesturePose(float indexCurl, float middleCurl, float ringCurl)
        {
            return indexCurl > 0.3f && middleCurl > 0.5f && ringCurl > 0.45f;
        }

        /// <summary>
        /// « Bouton » du microgeste : le pouce est-il appuyé sur le côté de l'index ?
        /// Hystérésis : appuyé sous <paramref name="pressDistance"/>, relâché seulement
        /// au-delà de <paramref name="releaseDistance"/> (un tremblement ne double-clique pas).
        /// </summary>
        public static bool IsThumbTapPressed(float thumbToIndex, bool wasPressed,
                                             float pressDistance = 0.022f, float releaseDistance = 0.035f)
        {
            return wasPressed ? thumbToIndex < releaseDistance : thumbToIndex < pressDistance;
        }

        /// <summary>Distance d'un point au segment [a, b].</summary>
        public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        // --- Gestes composés ----------------------------------------------------------

        /// <summary>
        /// Geste de SAISIE : poing fermé (majeur + annulaire repliés) OU pincement
        /// pouce-index. <paramref name="wasActive"/> applique l'hystérésis : une saisie
        /// en cours se relâche avec des seuils plus bas que ceux qui la déclenchent.
        /// </summary>
        public static bool IsGrabGesture(XRNode node, bool wasActive)
        {
            if (!TryGetHand(node, out XRHand hand)) return false;

            float fist = 0.5f * (GetCurl(hand, XRHandFingerID.Middle) + GetCurl(hand, XRHandFingerID.Ring));
            float pinch = GetPinch(hand, XRHandFingerID.Index);
            return IsGrabFromValues(fist, pinch, wasActive);
        }

        /// <summary>
        /// SIGNE DU PISTOLET : index tendu, majeur + annulaire repliés.
        /// Renvoie l'origine (bout de l'index) et la direction de l'index en coordonnées MONDE.
        /// <paramref name="isShootPressed"/> = pouce rabattu contre l'index (la « gâchette »).
        /// </summary>
        public static bool TryGetFingerGun(XRNode node, out Vector3 origin, out Vector3 direction, out bool isShootPressed)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            isShootPressed = false;

            if (!TryGetHand(node, out XRHand hand)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose tip)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexProximal, out Pose prox)) return false;

            float indexCurl = GetCurl(hand, XRHandFingerID.Index);
            float middleCurl = GetCurl(hand, XRHandFingerID.Middle);
            float ringCurl = GetCurl(hand, XRHandFingerID.Ring);

            if (!IsFingerGunPose(indexCurl, middleCurl, ringCurl)) return false;

            Vector3 dir = tip.position - prox.position;
            if (dir.sqrMagnitude < 1e-6f) dir = tip.forward;

            origin = ToWorld(tip.position);
            direction = DirToWorld(dir.normalized);

            // Gâchette : bout du pouce ramené contre la phalange intermédiaire de l'index
            // ou du majeur (pouce « abaissé »). Pouce levé ≈ 6–8 cm, abaissé < 3,5 cm.
            if (TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose thumb))
            {
                float dIndex = TryGetJointPose(hand, XRHandJointID.IndexIntermediate, out Pose ii)
                    ? Vector3.Distance(thumb.position, ii.position) : float.MaxValue;
                float dMiddle = TryGetJointPose(hand, XRHandJointID.MiddleIntermediate, out Pose mi)
                    ? Vector3.Distance(thumb.position, mi.position) : float.MaxValue;
                isShootPressed = Mathf.Min(dIndex, dMiddle) < 0.035f;
            }
            return true;
        }

        /// <summary>
        /// MICROGESTE (comme la téléportation des mains de Meta) : poing lâche, le pouce
        /// sert de bouton en tapant sur le côté de l'index.
        /// Renvoie la distance pouce ↔ côté de l'index (phalanges proximale à distale) et
        /// le rayon de visée en coordonnées MONDE : départ à la base de l'index, direction
        /// poignet → jointure du majeur (l'axe du poing). Tap et glissements : voir
        /// <see cref="VRThumbMicrogestures"/>.
        /// </summary>
        public static bool TryGetMicrogesture(XRNode node, out float thumbToIndex, out Vector3 origin, out Vector3 direction,
                                              out Vector3 thumbTipWorld)
        {
            thumbToIndex = float.MaxValue;
            origin = Vector3.zero;
            direction = Vector3.forward;
            thumbTipWorld = Vector3.zero;

            if (!TryGetHand(node, out XRHand hand)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.Wrist, out Pose wrist)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.MiddleProximal, out Pose knuckle)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexProximal, out Pose indexProx)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.IndexIntermediate, out Pose indexInter)) return false;
            if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose thumb)) return false;

            if (!IsMicrogesturePose(GetCurl(hand, XRHandFingerID.Index),
                                    GetCurl(hand, XRHandFingerID.Middle),
                                    GetCurl(hand, XRHandFingerID.Ring))) return false;

            Vector3 indexEnd = TryGetJointPose(hand, XRHandJointID.IndexDistal, out Pose indexDist)
                ? indexDist.position : indexInter.position;
            thumbToIndex = Mathf.Min(
                DistanceToSegment(thumb.position, indexProx.position, indexInter.position),
                DistanceToSegment(thumb.position, indexInter.position, indexEnd));

            Vector3 dir = knuckle.position - wrist.position;
            if (dir.sqrMagnitude < 1e-6f) return false;
            origin = ToWorld(indexProx.position);
            direction = DirToWorld(dir.normalized);
            thumbTipWorld = ToWorld(thumb.position);
            return true;
        }

        // --- Tables d'articulations ---------------------------------------------------

        private static XRHandJointID TipOf(XRHandFingerID finger)
        {
            switch (finger)
            {
                case XRHandFingerID.Thumb: return XRHandJointID.ThumbTip;
                case XRHandFingerID.Index: return XRHandJointID.IndexTip;
                case XRHandFingerID.Middle: return XRHandJointID.MiddleTip;
                case XRHandFingerID.Ring: return XRHandJointID.RingTip;
                default: return XRHandJointID.LittleTip;
            }
        }

        private static XRHandJointID ProximalOf(XRHandFingerID finger)
        {
            switch (finger)
            {
                case XRHandFingerID.Thumb: return XRHandJointID.ThumbProximal;
                case XRHandFingerID.Index: return XRHandJointID.IndexProximal;
                case XRHandFingerID.Middle: return XRHandJointID.MiddleProximal;
                case XRHandFingerID.Ring: return XRHandJointID.RingProximal;
                default: return XRHandJointID.LittleProximal;
            }
        }
    }
}
