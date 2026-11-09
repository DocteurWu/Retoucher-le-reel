// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRSmoothLocomotion.cs — LOCOMOTION CONTINUE + MODULE VERTICAL (saut, gravité)
//
//  RÔLE
//    - Stick gauche : translation 2D fluide (avant/arrière/latéral), à la vitesse
//      du mode « Mouvement Continu » (ou toujours si forceSmooth est vrai).
//    - Bouton A (manette droite) : saut avec impulsion verticale et gravité.
//
//  À POSER SUR : l'XR Origin. Câbler xrOriginTransform et headCamera.
//
//  ⚠️ MODULE VERTICAL — LES DEUX NOTIONS À NE PAS CONFONDRE
//    • « un sol existe sous les pieds »  : le rayon (4 m) touche une surface
//      marchable  →  le joueur doit-il TOMBER ?
//    • « le joueur est posé dessus »     : cette surface est à moins de
//      groundedDistance (0,35 m) de ses pieds  →  le joueur peut-il SAUTER ?
//    Confondre les deux fait « flotter » le joueur au bord d'une ledge.
//
//  ⚠️ AUTRES RÈGLES
//    - La sonde part des PIEDS (le rig), jamais de la tête (le sol de la scène
//      ne fait que 20 × 20 m : la tête peut être en avant du sol).
//    - La gravité ne s'applique QUE s'il existe un sol ou si le joueur a déjà
//      été posé : dans une scène SANS sol (le menu), le joueur ne tombe pas
//      (sinon : boucle de chute infinie).
//    - Le filet de sécurité ramène à la DERNIÈRE position valide (pas à y=0).
//
//  RÉFÉRENCES : voir README.md (sections 4 et 6.2)
// =====================================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// LOCOMOTION CONTINUE ET SAUT
    /// 
    /// - Stick gauche : translation 2D fluide (avant/arriere/lateral) si le mode dans
    ///   le menu est configure sur « Mouvement Continu ».
    /// - Bouton A (manette droite) : saut avec impulsion verticale et gravite.
    /// - Le stick droit n'est JAMAIS utilise ici : il est reserve au SnapTurn (et au scale).
    /// </summary>
    [AddComponentMenu("Remodeler le Reel/VR/VR Smooth Locomotion (Translation continue et Saut)")]
    public class VRSmoothLocomotion : MonoBehaviour
    {
        [Header("--- References ---")]
        public Transform xrOriginTransform;
        public Camera headCamera;
        public LayerMask groundMask = 1 << 6; // Layer Ground

        [Header("--- Deplacement (Stick Gauche) ---")]
        [Range(0.1f, 5f)] public float moveSpeed = 2f;
        [Range(0.1f, 5f)] public float strafeSpeed = 1.5f;
        [Range(0f, 0.5f)] public float deadzone = 0.25f;
        public XRNode moveStickNode = XRNode.LeftHand;
        [Tooltip("Force le mouvement continu dans la scene meme si Teleport etait enregistre")]
        public bool forceSmooth = false;

        [Header("--- Saut (Bouton A) et Gravite ---")]
        [Tooltip("Vitesse verticale initiale appliquee lors du saut (m/s)")]
        [Range(1f, 10f)] public float jumpVelocity = 3.8f;
        [Tooltip("Acceleration de la gravite (m/s²)")]
        public float gravity = -9.81f;
        [Tooltip("Vitesse de chute maximale (m/s, negative)")]
        public float terminalVelocity = -25f;
        [Tooltip("Tolerance de saut apres avoir quitter le sol (secondes)")]
        [Range(0f, 0.4f)] public float coyoteTime = 0.12f;
        [Tooltip("Distance de recherche du sol sous les pieds (m)")]
        [Range(0.5f, 8f)] public float groundProbeDistance = 4f;
        [Tooltip("Distance max au sol pour considers le joueur pose (m)")]
        [Range(0.1f, 1f)] public float groundedDistance = 0.35f;
        [Tooltip("Hauteur de la sonde de sol au-dessus des pieds (m)")]
        [Range(0.05f, 1f)] public float feetProbeHeight = 0.15f;
        [Tooltip("Pente maximale considered comme sol (degres)")]
        [Range(0f, 89f)] public float maxGroundSlope = 50f;
        [Tooltip("Hauteur maximale d'une marche que le joueur peut franchir (m)")]
        [Range(0.05f, 1f)] public float maxStepHeight = 0.35f;
        [Tooltip("Vitesse de collage au sol quand le joueur descend (m/s)")]
        [Range(0.5f, 20f)] public float groundSnapSpeed = 6f;

        // Etat interne du saut
        private float _verticalVelocity = 0f;
        private bool _isGrounded = true;
        private float _lastGroundedTime = -999f;
        private bool _wasJumpPressed = false;

        /// <summary>Dernier instant où des mains étaient utilisées (voir SautAutorise).</summary>
        private float _derniereMainSuivie = -999f;

        /// <summary>Après des mains suivies, le saut reste coupé ce temps (s) : la perte de suivi d'une main ne doit pas le rouvrir.</summary>
        private const float GraceMains = 1f;

        /// <summary>
        /// Vrai dès que le joueur a été posé sur un sol.
        ///
        /// ⚠️ INDISPENSABLE : la gravité ne s'applique que si le joueur a déjà
        /// été au sol. Dans une scène SANS sol (le MENU : tout y est en layer
        /// UI, il n'y a aucun « Sol » sur le layer 6), le joueur ne tombeait
        /// donc pas... jusqu'à la réécriture du module vertical. Depuis, il
        /// tombait à l'infini, était ramené à y=0 par le filet de sécurité, et
        /// recommençait : une boucle de chute qui rendait le menu inutilisable.
        ///
        /// Avec ce drapeau : dans une scène sans sol, le joueur reste à sa
        /// position de départ ; dans le jeu (qui a un sol), il tombe
        /// normalement dès qu'il quitte une ledge.
        /// </summary>
        private bool _hasBeenGrounded = false;

        /// <summary>
        /// Dernière position où le joueur était posé sur un sol. Sert de point
        /// de repli s'il tombe hors du monde : le ramener à y=0 l'aurait fait
        /// retomber (et boucler) s'il était hors de la zone marchable.
        /// </summary>
        private Vector3 _lastSafePosition;

        private void Awake()
        {
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;
            VRStickReader.EnsureActionsInitialized();
            VRButtonReader.EnsureActionsInitialized();
        }

        private void OnEnable()
        {
            VRStickReader.EnsureActionsInitialized();
            VRButtonReader.EnsureActionsInitialized();
        }

                // [LH] Deplacement continu lissé avec prise en compte du centrage HMD
        private void Update()
        {
            HandleJumpAndGravity();
            HandleHorizontalMovement();
        }

        /// <summary>
        /// MODULE VERTICAL — saut, gravité, sol.
        ///
        /// ⚠️ RÉÉCRITURE (correction du bug « le joueur reste dans l'air ») :
        ///
        /// L'ancienne version avait trois défauts qui se combinaient :
        ///   1. la sonde de sol partait de la TÊTE du joueur et non de ses PIEDS :
        ///      le sol était raté dès que la tête dépassait le bord de la zone
        ///      marchable (le sol de la scène ne fait que 20 x 20 m) ;
        ///   2. sans sol détecté, AUCUNE gravité n'était appliquée : le joueur
        ///      restait figé en l'air, exactement le symptôme décrit ;
        ///   3. le joueur était « collé » à la hauteur du point touché par le
        ///      rayon : il pouvait se retrouver planté au-dessus d'un meuble.
        ///
        /// La nouvelle version :
        ///   - sonde le sol sous les PIEDS (le rig), pas sous la tête ;
        ///   - applique la gravité TOUJOURS tant que le joueur n'est pas au sol
        ///      (donc il TOMBE dans le vide au lieu d'y flotter) ;
        ///   - ne descend sur le sol que de quelques centimètres à la fois, et
        ///      n'accepte jamais de monter plus haut qu'une marche (maxStepHeight) :
        ///      impossible d'être projeté au-dessus d'un objet ;
        ///   - exige une normale du sol presque verticale (maxGroundSlope) ;
        ///   - offre un coyote time (on peut sauter juste après avoir quitté
        ///      le sol) ;
        ///   - retombe proprement sur le sol à l'atterrissage.
        /// </summary>
        /// <summary>Le saut n'existe qu'à la manette : faux si des mains sont (ou viennent d'être) utilisées.</summary>
        private bool SautAutorise()
        {
            if (VRModalityManager.IsHandMode
                || VRHandGestures.IsTracked(XRNode.LeftHand)
                || VRHandGestures.IsTracked(XRNode.RightHand))
            {
                _derniereMainSuivie = Time.time;
            }
            return Time.time - _derniereMainSuivie > GraceMains;
        }

        private void HandleJumpAndGravity()
        {
            if (xrOriginTransform == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 originPos = xrOriginTransform.position;

            // --- 1. SOL : sonde sous les PIEDS DU JOUEUR, à l'aplomb de sa tête --
            // ⚠️ PAS sous l'origine du rig : c'est le centre de l'espace de jeu SteamVR,
            // qui peut être à plusieurs mètres du joueur (PC VR : x = -6,3 dans le menu).
            // Dans une petite pièce, l'origine tombait hors du sol : le joueur « tombait »
            // dès le début, sans avoir bougé.
            // On distingue deux notions :
            //   hasGround  : il existe UNE surface marchable sous les pieds
            //                 (à portée de la sonde) ;
            //   onGround   : le joueur est POSÉ dessus (la surface est juste
            //                 sous ses pieds).
            // C'est cette separation qui permet au joueur de quitter une ledge
            // (surface loin en dessous => plus « pose » => il tombe) tout en
            // gardant un sol de reference quand il est en plein saut.
            Vector3 feet = originPos + Vector3.up * feetProbeHeight;
            if (headCamera != null)
            {
                feet.x = headCamera.transform.position.x;
                feet.z = headCamera.transform.position.z;
            }
            bool hasGround = Physics.Raycast(feet, Vector3.down, out RaycastHit groundHit,
                                             groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
            bool flat = hasGround && Vector3.Angle(groundHit.normal, Vector3.up) <= maxGroundSlope;
            float gap = hasGround ? (originPos.y - groundHit.point.y) : float.MaxValue;
            bool onGround = flat && gap <= groundedDistance;

            if (onGround)
            {
                _lastGroundedTime = Time.time;
                _hasBeenGrounded = true;
                _lastSafePosition = originPos;

                // Coller au sol UNIQUEMENT si le joueur est DÉJÀ au sol (descente
                // de pente, sortie de téléportation...). En plein saut on ne
                // touche à rien : sinon le saut serait annulé dès la frame
                // suivante, puisque le sol est encore à portée de la sonde.
                if (_isGrounded)
                {
                    float targetY = groundHit.point.y;
                    float delta = targetY - originPos.y;

                    if (Mathf.Abs(delta) <= maxStepHeight)
                    {
                        // Marche / pente : on suit le sol sans à-coup.
                        originPos.y = Mathf.MoveTowards(originPos.y, targetY, groundSnapSpeed * dt);
                    }
                    else if (delta < 0f)
                    {
                        // Le sol est plus bas que la marche autorisée (le joueur
                        // vient de quitter un rebord) : il doit TOMBER, pas rester
                        // suspendu au-dessus du vide.
                        _isGrounded = false;
                    }
                }
            }
            else
            {
                _isGrounded = false;
            }

            // --- 2. SAUT (bouton A de la manette droite) ------------------------
            // ⚠️ Manettes uniquement : en mode mains, ALVR émule des boutons à partir des gestes
            // (pincement = bouton A) et le joueur sautait à chaque fois qu'il attrapait un objet.
            bool sautAutorise = SautAutorise();
            bool jumpPressed = sautAutorise && VRButtonReader.Read(VRButton.A);
            bool jumpTriggered = sautAutorise && ((jumpPressed && !_wasJumpPressed) || VRButtonReader.WasPressed(VRButton.A));
            _wasJumpPressed = jumpPressed;

            bool canJump = onGround || (Time.time - _lastGroundedTime) <= coyoteTime;
            if (jumpTriggered && canJump)
            {
                _verticalVelocity = jumpVelocity;
                _isGrounded = false;
            }

            // --- 3. INTÉGRATION VERTICALE ---------------------------------------
            if (_isGrounded && onGround)
            {
                _verticalVelocity = 0f;
            }
            else if (_hasBeenGrounded || hasGround)
            {
                // La gravité s'applique : le joueur qui vient de quitter une
                // ledge TOMBE (plus de « flotteur » dans le vide).
                _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * dt, terminalVelocity);
                originPos.y += _verticalVelocity * dt;
                _isGrounded = false;
            }
            else
            {
                // Scène SANS sol (le MENU : tout y est en layer UI, aucun
                // « Sol » sur le layer 6) : le joueur n'a jamais été posé et
                // aucune surface n'existe sous lui -> il NE TOMBE PAS.
                // Sans cette branche, il tombait à l'infini, le filet de
                // sécurité le ramenait à y=0, et il recommençait : une boucle
                // de chute qui rendait le menu inutilisable.
                _verticalVelocity = 0f;
            }

            // --- 4. ATTERRISSAGE -------------------------------------------------
            if (!_isGrounded && flat && _verticalVelocity <= 0f && originPos.y <= groundHit.point.y)
            {
                originPos.y = groundHit.point.y;
                _verticalVelocity = 0f;
                _isGrounded = true;
            }

            // --- 5. FILET DE SÉCURITÉ -------------------------------------------
            // Le joueur est tombé hors du monde : on le ramène à sa DERNIÈRE
            // position valide (et non à y=0, ce qui le ferait retomber en
            // boucle s'il était hors de la zone marchable).
            if (originPos.y < -20f)
            {
                originPos = _hasBeenGrounded ? _lastSafePosition : new Vector3(originPos.x, 0f, originPos.z);
                _verticalVelocity = 0f;
                _isGrounded = false;
            }

            // Physics.SyncTransforms inclus : les colliders des objets tenus suivent le rig.
            if (originPos != xrOriginTransform.position) VRUtils.MoveRig(xrOriginTransform, originPos);
        }

        private void HandleHorizontalMovement()
        {
            // En mode mains (Hand Tracking), le joueur utilise UNIQUEMENT la téléportation
            if (VRModalityManager.IsHandMode)
            {
                return;
            }

            // Ne se deplace pas horizontalement si une teleportation est en cours de visee
            if (VRTeleportation.IsAiming)
            {
                return;
            }

            // Ne se deplace pas au stick si l'utilisateur est explicitement en Teleport ET que forceSmooth est faux
            if (VRLocomotionSettings.Mode == LocomotionMode.Teleport && !forceSmooth)
            {
                return;
            }

            Vector2 stick = VRStickReader.Read(moveStickNode);

            float forward = VRStickReader.ApplyDeadzone(stick.y, deadzone);
            float strafe = VRStickReader.ApplyDeadzone(stick.x, deadzone);

            if (Mathf.Abs(forward) < 1e-4f && Mathf.Abs(strafe) < 1e-4f)
            {
                return;
            }

            Vector3 forwardDir = Vector3.forward;
            Vector3 rightDir = Vector3.right;
            if (headCamera != null)
            {
                Vector3 headForward = headCamera.transform.forward;
                forwardDir = new Vector3(headForward.x, 0f, headForward.z);
                if (forwardDir.sqrMagnitude < 1e-4f) forwardDir = Vector3.forward;
                forwardDir.Normalize();

                Vector3 headRight = headCamera.transform.right;
                rightDir = new Vector3(headRight.x, 0f, headRight.z);
                if (rightDir.sqrMagnitude < 1e-4f) rightDir = Vector3.right;
                rightDir.Normalize();
            }

            Vector3 movement = (forwardDir * forward * moveSpeed) + (rightDir * strafe * strafeSpeed);

            // ⚠️ Le stick servant à l'échelle est réservé : on s'efface pendant
            // le geste (sinon le joueur se téléporte ET redimensionne en même
            // temps). VRLocomotionSettings / VRInputLock gèrent cette exclusion.
            if (VRInputLock.IsStickReserved(moveStickNode))
            {
                return;
            }

            // MoveRig : synchronise les colliders des objets tenus (autoSyncTransforms = 0).
            VRUtils.MoveRig(xrOriginTransform, xrOriginTransform.position + movement * Time.deltaTime);
        }
    }
}