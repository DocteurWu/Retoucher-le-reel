// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRGrabLocomotion.cs — DÉPLACEMENT « ESCALADE » : SE TIRER EN SAISISSANT LE VIDE
//
//  RÔLE
//    Fermer le poing (ou pincer) avec une main VIDE « accroche » le monde à cette main :
//    tirer la main vers soi fait avancer le joueur, la pousser le fait reculer, comme
//    sur une paroi d'escalade. Avec les deux mains, le mouvement est la moyenne des deux.
//
//  PRINCIPE
//    La position de chaque main est lue dans l'espace de TRACKING (repère du casque),
//    qui ne bouge pas quand l'XR Origin se déplace. Le déplacement de l'XR Origin est
//    donc exactement l'opposé du déplacement de la main dans la pièce : pas de dérive
//    ni de rétroaction.
//
//  CONFLITS ÉVITÉS
//    - Main qui tient un objet : ignorée (c'est une saisie, pas une escalade).
//    - Redimensionnement à deux mains en cours : désactivé.
//    - Signe du pistolet (téléportation) : geste différent (index tendu), aucun conflit.
//
//  GARDE-FOUS
//    - Vitesse plafonnée (maxSpeed) : un saut de tracking ne projette pas le joueur.
//    - Murs : un SphereCast depuis la tête arrête le déplacement contre le décor
//      statique (colliders sans Rigidbody, triggers ignorés).
//    - Bord du monde : le déplacement est refusé s'il n'y a plus de sol sous la tête
//      (même règle que la téléportation : il faut une surface sur laquelle marcher).
//
//  Créé automatiquement au lancement (aucune mise en place dans les scènes).
// =====================================================================================
using UnityEngine;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    [DefaultExecutionOrder(20)] // après VRGrabHand (0) : on sait déjà si la main a saisi un objet
    [AddComponentMenu("Remodeler le Reel/VR/VR Grab Locomotion (Escalade)")]
    public class VRGrabLocomotion : MonoBehaviour
    {
        private static VRGrabLocomotion s_instance;
        private static float s_lastClimbTime = -10f;

        /// <summary>
        /// Remise à zéro à chaque lancement : le projet désactive le rechargement du domaine en
        /// mode Play, les statiques survivent d'une partie à l'autre. L'heure de la dernière
        /// escalade (horloge de la partie précédente) laissait IsClimbingActive vrai pendant
        /// autant de secondes au début de la partie suivante, et coupait le redimensionnement.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RemettreAZero()
        {
            s_lastClimbTime = -10f;
            s_instance = null;
        }

        /// <summary>Vrai tant que le joueur grimpe activement (ou vient de le faire il y a moins de 0.25s).</summary>
        public static bool IsClimbingActive => Time.unscaledTime - s_lastClimbTime < 0.25f;

        /// <summary>Vrai si la main spécifiée est actuellement en train de tracter le monde en escalade.</summary>
        public static bool IsHandClimbing(XRNode node)
        {
            if (s_instance == null) return false;
            int idx = node == XRNode.LeftHand ? 0 : 1;
            return s_instance._active[idx];
        }

        private void OnEnable()
        {
            s_instance = this;
        }

        private void OnDisable()
        {
            if (s_instance == this) s_instance = null;
        }
        [Tooltip("Exiger les DEUX mains fermées pour se déplacer (sinon une seule suffit)")]
        public bool requireBothHands = false;

        [Tooltip("Autoriser le déplacement vertical (sinon on reste au sol)")]
        public bool allowVertical = false;

        [Tooltip("Multiplicateur du déplacement (1 = 1:1 avec la main)")]
        [Range(0.5f, 3f)] public float moveMultiplier = 1.5f;

        [Tooltip("Uniquement en mode mains (hand tracking) — évite les déplacements involontaires au grip manette")]
        public bool handModeOnly = true;

        [Tooltip("Vitesse maximale du déplacement (m/s)")]
        public float maxSpeed = 3f;

        [Tooltip("Rayon de collision de la tête contre les murs (m)")]
        public float headRadius = 0.2f;

        [Tooltip("Distance max (m) sous la tête pour considérer qu'il y a encore du sol")]
        public float groundProbeDistance = 4f;

        private readonly bool[] _active = new bool[2];
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private readonly Vector3[] _prevPos = new Vector3[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindFirstObjectByType<VRGrabLocomotion>() != null) return;
            var go = new GameObject("VRGrabLocomotion_Auto");
            go.AddComponent<VRGrabLocomotion>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            Transform space = VRHandGestures.TrackingSpace;
            Transform rig = space != null ? (space.parent != null ? space.parent : space) : null;

            bool allowed = rig != null
                && (!handModeOnly || VRModalityManager.IsHandMode)
                // Mains : escalade OU téléportation, selon le réglage du menu.
                && (!VRModalityManager.IsHandMode || VRLocomotionSettings.HandMode == HandLocomotionMode.Climb)
                && !VRScaleGrab.IsScalingActive
                && !VRTeleportation.IsAiming // poing du microgeste de visée ≠ escalade
                && !VRPorte.UneMainTientUnePorte; // une porte qu'on tire ne doit pas tirer le joueur

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < 2; i++)
            {
                XRNode node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
                var hand = VRGrabHand.GetHandForNode(node);
                // SaisieEnAttente : main qui se ferme sur un objet, ce n'est pas (encore) de l'escalade.
                bool handNearDoor = hand != null && VRPorte.UneMainEstProcheDUnePorte(hand.transform.position);
                bool climbing = allowed && hand != null && hand.IsGrabInputActive && !hand.IsHoldingObject && !hand.SaisieEnAttente && !handNearDoor;

                if (!climbing)
                {
                    _active[i] = false;
                    continue;
                }

                Vector3 pos = space.InverseTransformPoint(hand.transform.position);
                if (_active[i])
                {
                    sum += _prevPos[i] - pos;
                    count++;
                }
                _active[i] = true;
                _prevPos[i] = pos;
            }

            if (count == 0) return;
            s_lastClimbTime = Time.unscaledTime;
            if (requireBothHands && !(_active[0] && _active[1])) return;

            Vector3 worldDelta = space.TransformVector(sum / count) * moveMultiplier;
            if (!allowVertical) worldDelta.y = 0f;

            worldDelta = Vector3.ClampMagnitude(worldDelta, maxSpeed * Time.deltaTime);
            worldDelta = LimitByWorld(worldDelta, rig);
            if (worldDelta.sqrMagnitude < 1e-10f) return;

            VRUtils.MoveRig(rig, rig.position + worldDelta);
        }

        /// <summary>
        /// Raccourcit le déplacement contre un mur et l'annule s'il mène hors du sol.
        /// Seul le décor STATIQUE (collider sans Rigidbody) compte : les objets
        /// saisissables et les objets tenus ne bloquent pas le joueur.
        /// </summary>
        /// <summary>
        /// Collider qui fait partie du rig (mains, manettes, objet tenu) : ni mur ni sol pour la
        /// tête. Sans ce filtre, un collider du rig bloquait l'escalade ou passait pour du sol.
        /// </summary>
        private static bool DuRig(Collider c, Transform rig) => rig != null && c.transform.IsChildOf(rig);

        private Vector3 LimitByWorld(Vector3 delta, Transform rig)
        {
            Camera cam = Camera.main;
            if (cam == null) return delta;

            Vector3 head = cam.transform.position;
            float dist = delta.magnitude;
            if (dist < 1e-5f) return delta;
            Vector3 dir = delta / dist;

            // 1. Murs
            int n = Physics.SphereCastNonAlloc(head, headRadius, dir, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
            float allowed = dist;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider == null || _hits[i].collider.attachedRigidbody != null || DuRig(_hits[i].collider, rig)) continue;
                // distance 0 = déjà au contact au départ : on laisse s'éloigner, pas s'enfoncer.
                if (_hits[i].distance <= 0f)
                {
                    if (Vector3.Dot(_hits[i].normal, dir) < 0f) allowed = 0f;
                    continue;
                }
                allowed = Mathf.Min(allowed, _hits[i].distance);
            }
            delta = dir * allowed;

            // 2. Bord du monde : il doit rester du sol sous la tête après le déplacement.
            Vector3 probe = head + delta;
            n = Physics.RaycastNonAlloc(probe, Vector3.down, _hits, groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider != null && _hits[i].collider.attachedRigidbody == null && !DuRig(_hits[i].collider, rig)) return delta;
            }
            return Vector3.zero;
        }
    }
}
