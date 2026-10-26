// =====================================================================================
//  VRScaleGrab.cs — ÉCHELLE DE L'OBJET TENU
//
//  RÔLE
//    Redimensionne l'objet tenu par l'une des mains, avec deux gestes
//    (combinables, mais un seul à la fois — voir plus bas) :
//      • Thumbstick : stick droit vers le haut/bas (facteur e^(axe × sensibilité × dt),
//        donc la même vitesse à 72 et 120 Hz).
//      • TwoHands   : on tient l'objet d'une main et on MAINTIENT le grip de
//        l'autre main ; l'écartement des deux manettes pilote l'échelle.
//
//  À POSER SUR : UNE seule main (la droite dans la scène de jeu). L'autre main
//  est détectée automatiquement.
//
//  ⚠️ ORDRE D'EXÉCUTION : −50, donc AVANT VRGrabHand (0). C'est ce composant
//     qui arme VRInputLock : la saisie doit voir le verrou dans la MÊME frame,
//     sinon la main libre vole l'objet en cours de redimensionnement.
//     Le verrou expirant au bout d'une frame, il doit être RÉ-ARMÉ à chaque
//     frame du geste (voir ContinuePinch et UpdateThumbstick).
//
//  ⚠️ Un seul geste à la fois : le pincement et le stick écrivent la même
//     échelle (sinon : dérive inexplicable).
//
//  RÉFÉRENCES : voir README.md (sections 3, 4 et 5)
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using InputDevice = UnityEngine.InputSystem.InputDevice;

namespace RemodelerLeReel.VR
{
    public enum GrabScaleMode
    {
        /// <summary>Écartement / rapprochement des deux manettes.</summary>
        TwoHands = 0,
        /// <summary>Push avant/arrière du thumbstick de la main qui tient l'objet.</summary>
        Thumbstick = 1,
        /// <summary>Les deux gestes en même temps (par défaut).</summary>
        Both = 2
    }

    /// <summary>
    /// Agrandit / rétrécit l'objet actuellement saisi par une manette.
    ///
    /// Deux gestes (combinables, mode "Both") :
    ///  - Deux manettes : on tient l'objet avec une main, on maintient l'appui (grip ou
    ///    trigger) avec l'autre main, puis on écarte / rapproche les deux manettes.
    ///  - Thumbstick : on tient l'objet et on pousse le stick vers le haut (grandir)
    ///    ou vers le bas (rétrécir).
    ///
    /// L'objet visé est celui accroché par VRGrabHand : pendant la saisie il devient
    /// enfant de la main, on le retrouve donc sans dépendre de son implémentation.
    /// Ce composant se pose sur UNE seule main, l'autre est détectée automatiquement.
    ///
    /// Déroulé d'une frame (Update) :
    ///   1. ResolveOtherHand      retrouve la main qui porte ce composant vs l'autre main.
    ///   2. TryGetHeldObject      demande aux deux mains quel Rigidbody elles tiennent.
    ///   3. ApplyWorldScale       ne modifie l'échelle que si la valeur a vraiment changé.
    ///   4. UpdateScaleAudio      donne le retour sonore (intro puis boucle).
    ///
    /// Le suivi 6-DOF des manettes est assuré par VRGrabHand sur le même GameObject ;
    /// ce composant ne fait que lire la position de son propre Transform.
    ///
    /// ORDRE D'EXÉCUTION : -50, donc AVANT VRGrabHand (0). C'est indispensable :
    /// c'est ce composant qui arme le verrou d'entrée, et VRGrabHand doit le
    /// voir dans la MÊME frame pour interdire à la main libre de voler l'objet
    /// en cours de redimensionnement.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Remodeler le Reel/VR/VR Scale Grab (Echelle de l'objet saisi)")]
    public class VRScaleGrab : MonoBehaviour
    {
        [Header("--- Configuration ---")]
        [Tooltip("Geste utilisé : deux manettes, stick, ou les deux")]
        public GrabScaleMode mode = GrabScaleMode.Both;

        [Tooltip("Main qui porte ce script (l'autre main est détectée automatiquement)")]
        public XRNode handNode = XRNode.RightHand;

        [Header("--- Référence de taille ---")]
        [Tooltip("Ancrer les limites (×N) et la masse sur la taille INITIALE de l'objet, figée " +
                 "une seule fois : elle ne se réinitialise plus à chaque saisie/re-saisie. " +
                 "Décoché = ancien comportement (référence reprise à chaque saisie).")]
        public bool anchorToInitialSize = true;

        [Header("--- Stick ---")]
        [Tooltip("Sensibilité : facteur d'échelle par seconde quand le stick est poussé à fond")]
        public float stickSensitivity = 0.45f;
        [Tooltip("Inverser le sens du stick")]
        public bool invertStick = false;

        [Header("--- Deux manettes ---")]
        [Tooltip("Lissage du geste d'écartement (0 = brut, 20 = très fluide)")]
        [Range(0f, 20f)] public float pinchSmoothing = 12f;
        [Tooltip("Sensibilité du geste à deux mains (1 = direct, 0.6 = doux et progressif)")]
        [Range(0.1f, 2f)] public float pinchSensitivity = 0.6f;
        [Tooltip("Appui minimum de la seconde main pour déclencher le pincement")]
        [Range(0f, 1f)] public float pinchPressThreshold = 0.5f;
        [Tooltip("Écartement minimal entre les manettes (m) pour éviter une division par zéro")]
        public float minPinchDistance = 0.03f;
        [Tooltip("Distance max (m) entre la main libre et la surface de l'objet pour autoriser le scale à deux mains")]
        public float pinchTouchDistance = 0.06f;

        [Header("--- Limites par défaut (sans composant VRScalable) ---")]
        [Tooltip("Taille minimale en % de la taille INITIALE de l'objet")]
        public float defaultMinRatio = 0.25f;
        [Tooltip("Taille maximale en % de la taille INITIALE de l'objet")]
        public float defaultMaxRatio = 4f;

        [Header("--- Haptique (Vague de Redimensionnement) ---")]
        [Tooltip("Activer les vibrations haptiques en vague pendant le scale")]
        public bool enableScaleHaptics = true;

        [Range(0f, 1f), Tooltip("Intensité de base de la vibration lors du scale")]
        public float baseScaleHapticIntensity = 0.25f;

        [Range(0f, 1f), Tooltip("Intensité maximale au pic de la vague haptique")]
        public float maxScaleHapticIntensity = 0.65f;

        [Tooltip("Fréquence de la vague haptique (cycles par seconde)")]
        public float hapticWaveFrequency = 4.0f;

        [Tooltip("Vibrer également la main tenant l'objet en mode Thumbstick (en plus de la main qui scale)")]
        public bool vibrateHoldingHandToo = true;

        [Header("--- Son ---")]
        // Le clip d'origine ("ScaleGrab (one piece).mp3", 12 s) a été découpé en deux
        // segments avec ffmpeg, tous deux au format OGG (pas de padding d'encodeur, donc
        // un point de boucle propre) :
        //   - "ScaleGrab intro.ogg" = 0 -> 4 s  : le bruit d'ouverture, entendu UNE fois.
        //   - "ScaleGrab loop.ogg"  = 4 s -> fin : la partie répétée pendant le scale.
        // L'Unity AudioSource ne sait boucler que de 0 à la fin du clip, on utilise donc
        // deux AudioSource : l'intro se joue une fois, puis la boucle prend le relais.
        [Tooltip("Source d'intro réutilisée si elle est renseignée, sinon créée au runtime")]
        public AudioSource scaleAudio;
        [Tooltip("Segment joué une seule fois au début du scale (ici 0 -> 4 s)")]
        public AudioClip introClip;
        [Tooltip("Segment bouclé tant que le scale dure (ici 4 s -> fin)")]
        public AudioClip loopClip;
        [Tooltip("Utiliser la durée de introClip comme délai avant la boucle")]
        public bool autoLoopDelay = true;
        [Tooltip("Délai avant de démarrer la boucle (sans introClip ou si autoLoopDelay est décoché)")]
        public float loopDelay = 4f;
        [Range(0f, 1f)] [Tooltip("Volume du retour sonore")]
        public float scaleVolume = 1f;
        [Tooltip("Fondu à l'arrêt du scale (0 = arrêt sec)")]
        public float fadeOutDuration = 0.15f;
        [Range(0f, 1f)] [Tooltip("1 = source 3D (spatialisée sur la manette), 0 = son non spatialisé")]
        public float spatialBlend = 1f;
        [Tooltip("Distance à laquelle le son est à volume maximum (mode 3D)")]
        public float minDistance = 1f;
        [Tooltip("Distance au-delà de laquelle le son est inaudible (mode 3D)")]
        public float maxDistance = 20f;

        /// <summary>Vrai tant qu'un objet est saisi et prêt à être redimensionné.</summary>
        public bool IsScaling => _target != null;
        public static bool IsScalingActive { get; private set; }

        /// <summary>
        /// Durée sans changement d'échelle après laquelle on considère que le geste est
        /// terminé. Ce petit tampon évite que le son se coupe quand le facteur d'échelle
        /// arrive en butée (limite min/max atteinte : la valeur ne change plus).
        /// </summary>
        private const float ScaleActiveWindow = 0.1f;

        // --- État de la saisie ---
        private VRGrabHand _otherHand;        // la main qui ne porte PAS ce composant
        private Rigidbody _target;            // objet actuellement redimensionné
        private Transform _targetTransform;
        private VRScalable _marker;           // limits optionnelles de l'objet
        private Transform _holderHand;        // main qui tient réellement l'objet
        private XRNode _holderNode = XRNode.RightHand;

        /// <summary>
        /// Nœud du stick qui sert au redimensionnement.
        ///
        /// C'est le stick de la MAIN LIBRE, jamais celui de la main qui tient :
        /// le joueur tient l'objet d'une main et pilote l'échelle de l'autre,
        /// ce qui laisse la main qui tient entièrement disponible. Lire le
        /// stick de la main qui tiendrait ferait qu'un simple mouvement du
        /// pouce pendant la préhension redimensionne l'objet par surprise.
        /// <summary>
        /// Nœud du stick qui sert au redimensionnement (toujours le stick droit selon consigne).
        /// </summary>
        private XRNode ScaleStickNode => XRNode.RightHand;
        private float _baseWorldSize = 1f;    // taille de référence « 1× » (base des limites et de la masse)
        private Rigidbody _baseRefBody;       // objet auquel appartient _baseWorldSize (pour le figer par objet)
        private float _baseMass;              // masse du Rigidbody à la saisie
        private bool _scaleMass;             // VRScalable.scaleRigidbodyMass

        // --- État du pincement à deux mains ---
        private bool _pinching;
        private float _pinchStartDistance = 1f; // écartement des manettes au départ du geste
        private Vector3 _pinchStartScale = Vector3.one;
        private float _pinchFactor = 1f;

        // --- État de la vague haptique ---
        private float _hapticWaveTimer = 0f;
        private float _lastScaleDirection = 0f;

        // --- État du son ---
        private AudioSource _introSource;     // joue le segment 0 -> 4 s, une seule fois
        private AudioSource _loopSource;      // joue le segment 4 s -> fin, en boucle
        private float _lastScaleTime = float.NegativeInfinity; // dernier changement d'échelle
        private float _scalingSince = -1f;    // début du geste audio (-1 = inactif)
        private bool _loopStarted;           // la boucle a-t-elle déjà pris le relais ?
        private Coroutine _fadeRoutine;      // fondu de sortie en cours

        /// <summary>
        /// Prépare les deux AudioSource au démarrage.
        /// Si un AudioSource a été câblé dans l'inspector (scaleAudio) il sert de source
        /// d'intro, sinon on en crée un. La source de boucle est toujours créée par le
        /// script : une même source ne peut pas jouer deux clips différents en même temps.
        /// </summary>
        private void Awake()
        {
            if (scaleAudio != null)
            {
                _introSource = scaleAudio;
                ConfigureSource(_introSource);
            }
            else
            {
                _introSource = gameObject.AddComponent<AudioSource>();
                ConfigureSource(_introSource);
            }

            _loopSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(_loopSource);
            _loopSource.clip = loopClip;
            _loopSource.loop = true;
        }

        private void Update()
        {
            ResolveOtherHand();

            // Aucun objet saisi (ou VRScalable désactivé) : on coupe le geste.
            // UpdateScaleAudio est quand même appelée pour arrêter le son si besoin.
            if (!TryGetHeldObject(out Transform holder, out XRNode holderNode, out Rigidbody body) ||
                IsScalingDisabled(body))
            {
                IsScalingActive = false;
                // On relâche explicitement le verrou d'entrée : sans objet en
                // main, le stick est de nouveau disponible pour la locomotion.
                VRInputLock.EndScaleMode();
                ResetGesture();
                UpdateScaleAudio();
                return;
            }

            // Nouvel objet saisi (ou changement de main) : on (re)configure la cible.
            if (_target != body)
            {
                _target = body;
                _targetTransform = body.transform;
                _marker = _targetTransform.GetComponent<VRScalable>();
                _scaleMass = _marker != null && _marker.scaleRigidbodyMass;
                _baseMass = (_marker != null && _marker.body != null) ? _marker.body.mass : body.mass;
                EndPinch();
            }

            // Taille de référence « 1× » (base des limites ×N et de la masse) :
            //  - anchorToInitialSize : ANCRÉE sur la taille INITIALE de l'objet. Elle est
            //    mémorisée une SEULE fois par objet et survit au relâchement / à la
            //    re-saisie : ×3 reste donc ×3 de la taille de départ (et non de la taille
            //    courante), et un « reset » revient bien à l'échelle d'origine.
            //  - sinon : reprise de la taille courante à chaque saisie (ancien comportement).
            if (!anchorToInitialSize)
            {
                _baseWorldSize = MaxAxis(body.transform.lossyScale);
                _baseRefBody = null;
            }
            else if (_baseRefBody != body)
            {
                _baseRefBody = body;
                _baseWorldSize = _marker != null
                    ? _marker.InitialWorldSize
                    : MaxAxis(body.transform.lossyScale);
            }

            _holderHand = holder;
            _holderNode = holderNode;

            // ⚠️ Les deux gestes écrivent dans la même méthode (ApplyWorldScale).
            // Les conditions étaient INVERSÉES (le pincement était actif en mode
            // « Thumbstick » et le stick en mode « TwoHands ») : on les active
            // maintenant selon le mode réellement choisi.
            bool allowPinch = mode == GrabScaleMode.TwoHands || mode == GrabScaleMode.Both;
            bool allowStick = mode == GrabScaleMode.Thumbstick || mode == GrabScaleMode.Both;

            if (allowPinch) UpdateTwoHandsPinch();
            IsScalingActive = true;

            // ⚠️ UN SEUL GESTE À LA FOIS : le pincement à deux mains et le stick
            // écrivaient tous les deux l'échelle en même temps (dérive). Quand le
            // pincement est en cours, le stick est ignoré (et inversement).
            if (allowStick && !_pinching) UpdateThumbstick();

            UpdateScaleAudio();
            UpdateScaleHaptics();
        }

        private void OnDisable()
        {
            ResetGesture();
            StopScaleAudio(immediate: true);
        }

        /// <summary>
        /// Retrouve la main qui ne porte pas ce composant. Comme les deux manettes ont
        /// chacune un VRGrabHand, il suffit de chercher celui dont le XRNode est
        /// différent du nôtre. Mise en cache : la recherche ne tourne que si l'autre main
        /// est introuvable ou désactivée.
        /// </summary>
        private void ResolveOtherHand()
        {
            if (_otherHand != null && _otherHand.isActiveAndEnabled) return;

            // Registre VRGrabHand.AllHands : pas de parcours de scène.
            _otherHand = VRGrabHand.GetHandForNode(VRUtils.OtherHand(handNode));
        }

        /// <summary>
        /// Demande aux deux mains quel Rigidbody elles tiennent et renvoie celle qui en
        /// tient un (avec son XRNode pour lire le stick). L'ordre n'a pas d'importance :
        /// si les deux mains tiennent des objets différents, c'est la main du composant
        /// qui gagne — c'est de toute façon le cas le plus courant.
        /// </summary>
        private bool TryGetHeldObject(out Transform holder, out XRNode holderNode, out Rigidbody body)
        {
            body = VRGrabHand.GetAnyGrabbedBody(out VRGrabHand h);
            if (body != null && h != null)
            {
                holder = h.transform;
                holderNode = h.handNode;
                return true;
            }

            holder = null;
            holderNode = handNode;
            body = null;
            return false;
        }

        private bool IsScalingDisabled(Rigidbody body)
        {
            VRScalable marker = body.GetComponent<VRScalable>();
            return marker != null && !marker.scalingEnabled;
        }

        /// <summary>
        /// VRGrabHand parenthe l'objet saisi à la main pendant la grab : l'objet tenu
        /// est donc un enfant direct de la main. On le cherche là, et en dernier recours
        /// dans toute la hiérarchie (si un jour la main contient un intermediate).
        /// </summary>
        private static Rigidbody FindHeldBody(Transform hand)
        {
            if (hand == null) return null;

            for (int i = 0; i < hand.childCount; i++)
            {
                Rigidbody body = hand.GetChild(i).GetComponent<Rigidbody>();
                if (body != null) return body;
            }

            return hand.GetComponentInChildren<Rigidbody>();
        }

        /// <summary>
        /// Geste à deux mains : une main tient l'objet, l'autre doit rester enfoncée
        /// (grip ou trigger). Tant que ce bouton est maintenu, l'écartement entre les
        /// deux manettes pilote l'échelle.
        /// </summary>
        private void UpdateTwoHandsPinch()
        {
            // La main "libre" est celle qui ne tient pas l'objet : c'est elle qu'on
            // surveille. Si c'est la main de ce composant qui tient, l'autre est libre
            // (et inversement).
            bool holderIsThisHand = _holderHand == transform;
            Transform freeHand = holderIsThisHand
                ? (_otherHand != null ? _otherHand.transform : null)
                : transform;

            if (freeHand == null)
            {
                EndPinch();
                return;
            }

            XRNode freeNode = holderIsThisHand ? (_otherHand != null ? _otherHand.handNode : handNode) : handNode;
            bool pressed = ReadPressValue(freeNode) >= pinchPressThreshold
                || VRHandGestures.IsGrabGesture(freeNode, _pinching);

            // Front montant / descendant de l'appui = début / fin du pincement.
            // Le pincement ne DÉMARRE que si la main libre touche l'objet : les deux mains
            // doivent être sur l'objet (la main qui tient le touche déjà).
            if (pressed && !_pinching && IsTouchingTarget(freeHand.position)) BeginPinch(freeHand);
            else if (!pressed && _pinching) EndPinch();

            if (_pinching) ContinuePinch(freeHand);
        }

        /// <summary>
        /// Mémorise la position de départ du geste : l'écartement des manettes et la
        /// taille de l'objet à cet instant. Tout le pincement se calcule par rapport à
        /// ces deux valeurs (et non par accumulation frame après frame), ce qui évite
        /// toute dérive.
        /// </summary>
        /// <summary>Vrai si le point est à moins de pinchTouchDistance d'un collider de l'objet.</summary>
        private bool IsTouchingTarget(Vector3 point)
        {
            if (_targetTransform == null) return false;
            var colliders = _targetTransform.GetComponentsInChildren<Collider>();
            float maxSqr = pinchTouchDistance * pinchTouchDistance;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                // ClosestPoint n'est pas supporté par les MeshCollider concaves : repli sur les bounds.
                bool exact = !(c is MeshCollider mc) || mc.convex;
                float sqr = exact
                    ? (c.ClosestPoint(point) - point).sqrMagnitude
                    : c.bounds.SqrDistance(point);
                if (sqr <= maxSqr) return true;
            }
            return false;
        }

        private void BeginPinch(Transform freeHand)
        {
            _pinching = true;
            _pinchStartDistance = Mathf.Max(Vector3.Distance(_holderHand.position, freeHand.position), minPinchDistance);
            _pinchStartScale = _targetTransform.lossyScale;
            _pinchFactor = 1f;

            // Pendant un pincement à deux mains, le joueur est en train de
            // dimensionner : la préhension de la main libre est inhibée et le
            // stick de cette main passe en commande d'échelle.
            VRInputLock.BeginScaleMode(ScaleStickNode);
        }

        /// <summary>
        /// Éloigner les mains = multiplier par distance / distance de départ.
        /// Le facteur est lissé exponentiellement (indépendant du framerate) pour que le
        /// geste ne soit pas saccadé quand les manettes tremblent.
        /// </summary>
        private void ContinuePinch(Transform freeHand)
        {
            float distance = Mathf.Max(Vector3.Distance(_holderHand.position, freeHand.position), minPinchDistance);
            float rawRatio = distance / _pinchStartDistance;
            float wanted = Mathf.Pow(rawRatio, pinchSensitivity);

            _pinchFactor = pinchSmoothing > 0f
                ? Mathf.Lerp(_pinchFactor, wanted, 1f - Mathf.Exp(-pinchSmoothing * Time.deltaTime))
                : wanted;

            // Le verrou d'entrée expire au bout d'une frame : il faut le
            // RÉ-ARMER à chaque frame du pincement, sinon la main libre
            // pourrait reprendre l'objet au milieu du geste.
            VRInputLock.BeginScaleMode(ScaleStickNode);

            ApplyWorldScale(_pinchStartScale * _pinchFactor);
        }

        /// <summary>Fin du pincement : la taille obtenue est conservée (ApplyWorldScale l'écrit déjà).</summary>
        private void EndPinch()
        {
            _pinching = false;
            _pinchFactor = 1f;
        }

        /// <summary>
        /// Geste au thumbstick : on lit l'axe vertical de la main qui tient l'objet.
        /// Le facteur est une exponentielle du temps (e^(axe * sensibilité * dt)) donc la
        /// vitesse est la même à 72 et 120 Hz, et le stick à moitié = demi-vitesse.
        /// </summary>
        private void UpdateThumbstick()
        {
            // On lit le stick de la MAIN LIBRE (voir ScaleStickNode) : le joueur
            // tient l'objet d'une main et règle l'échelle avec l'autre.
            float axis = ReadThumbstickAxis(XRNode.RightHand);
            if (Mathf.Abs(axis) < 0.001f) return;

            // Le stick est pris par l'échelle : on le déclare aux autres scripts
            // (téléportation / locomotion / préhension) pour qu'ils s'effacent
            // le temps du geste. Sans ça, pousser le stick téléporte le joueur
            // ET redimensionne l'objet en même temps.
            VRInputLock.BeginScaleMode(ScaleStickNode);

            if (invertStick) axis = -axis;

            float factor = Mathf.Exp(axis * stickSensitivity * Time.deltaTime);
            ApplyWorldScale(_targetTransform.lossyScale * factor);
        }

        /// <summary>
        /// Unique point d'écriture de l'échelle, appelé par les deux gestes.
        ///   1. bornage (VRScalable ou valeurs par défaut du composant) ;
        ///   2. conversion échelle monde -> échelle locale (l'objet est enfant de la main) ;
        ///   3. écriture seulement si ça change vraiment, et horodatage du changement
        ///      (c'est cet horodatage qui pilote le son) ;
        ///   4. masse du Rigidbody proportionnelle au volume si demandé.
        /// </summary>
        private void ApplyWorldScale(Vector3 desired)
        {
            desired = ClampWorldScale(desired);

            Vector3 parentScale = _targetTransform.parent != null ? _targetTransform.parent.lossyScale : Vector3.one;
            Vector3 local = DivideSafe(desired, parentScale);

            Vector3 oldLocal = _targetTransform.localScale;
            if ((local - oldLocal).sqrMagnitude < 1e-12f) return;

            float scaleRatio = MaxAxis(local) / Mathf.Max(MaxAxis(oldLocal), 1e-6f);

            _targetTransform.localScale = local;

            if (_holderHand != null)
            {
                if (_targetTransform.parent == _holderHand)
                {
                    _targetTransform.localPosition *= scaleRatio;
                }
                else
                {
                    Vector3 handLocal = _holderHand.InverseTransformPoint(_targetTransform.position);
                    _targetTransform.position = _holderHand.TransformPoint(handLocal * scaleRatio);
                }
            }

            // autoSyncTransforms = 0 : sans cet appel le collider garde l'ancienne
            // taille jusqu'au pas de physique suivant (test de contact du pincement et
            // détection de la bulle de rangement faussés).
            Physics.SyncTransforms();

            _lastScaleTime = Time.unscaledTime;
            _lastScaleDirection = scaleRatio - 1f;

            if (_scaleMass && _baseMass > 0f && _baseWorldSize > 1e-6f && _target != null)
            {
                float ratio = MaxAxis(desired) / _baseWorldSize;
                _target.mass = _baseMass * ratio * ratio * ratio;
            }
        }

        /// <summary>
        /// Retour sonore : le segment d'intro est joué une fois quand le scale commence,
        /// puis le segment de boucle prend le relais à partir de autoLoopDelay / loopDelay
        /// et tourne jusqu'à la fin du geste (fondu de sortie à la fin).
        /// </summary>
        private void UpdateScaleAudio()
        {
            bool scaling = Time.unscaledTime - _lastScaleTime < ScaleActiveWindow;

            // Front montant : le scale commence -> on joue le segment d'intro.
            if (scaling && _scalingSince < 0f)
            {
                StartScaleAudio();
            }
            // Front descendant : plus aucun changement d'échelle depuis 0,1 s -> on coupe.
            else if (!scaling && _scalingSince >= 0f)
            {
                StopScaleAudio(immediate: false);
            }

            // Une fois l'intro terminé (4 s), la boucle démarre et tourne jusqu'à la fin
            // du geste. Le seuil est mesuré depuis le DÉBUT du scale, pas depuis la fin
            // de l'intro, donc les deux ne se chevauchent jamais.
            if (scaling && _scalingSince >= 0f && !_loopStarted &&
                Time.unscaledTime - _scalingSince >= CurrentLoopDelay)
            {
                if (_loopSource.clip != loopClip) _loopSource.clip = loopClip;
                if (_loopSource.clip != null) _loopSource.Play();
                _loopStarted = true;
            }
        }

        /// <summary>
        /// Début du geste audio : on note l'heure de départ (sert de référence pour le
        /// déclenchement de la boucle) et on joue l'intro une seule fois, sans boucle.
        /// Un fondu en cours est annulé : le son repart immédiatement à volume normal.
        /// </summary>
        private void StartScaleAudio()
        {
            _scalingSince = Time.unscaledTime;
            _loopStarted = false;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (introClip == null) return;

            _introSource.clip = introClip;
            _introSource.loop = false;
            _introSource.volume = scaleVolume;
            _introSource.Play();
        }

        /// <summary>
        /// Fin du geste audio. immediate = true coupe tout au sec (utilisé à la
        /// désactivation du composant), sinon on passe par un fondu pour éviter le clic
        /// de l'arrêt.
        /// </summary>
        private void StopScaleAudio(bool immediate)
        {
            _scalingSince = -1f;
            _loopStarted = false;

            if (_introSource == null || _loopSource == null) return;

            if (immediate || fadeOutDuration <= 0f)
            {
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                    _fadeRoutine = null;
                }

                _introSource.Stop();
                _loopSource.Stop();
                _introSource.volume = scaleVolume;
                _loopSource.volume = scaleVolume;
                return;
            }

            if (_fadeRoutine == null) _fadeRoutine = StartCoroutine(FadeOutScaleAudio());
        }

        /// <summary>
        /// Descend le volume des deux sources jusqu'à 0 puis les arrête. On utilise
        /// unscaledDeltaTime pour que le fondu se termine même si timeScale vaut 0.
        /// Le volume est restauré à scaleVolume à la fin pour être prêt au geste suivant.
        /// </summary>
        private IEnumerator FadeOutScaleAudio()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float factor = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                _introSource.volume = scaleVolume * factor;
                _loopSource.volume = scaleVolume * factor;
                yield return null;
            }

            _introSource.Stop();
            _loopSource.Stop();
            _introSource.volume = scaleVolume;
            _loopSource.volume = scaleVolume;
            _fadeRoutine = null;
        }

        /// <summary>
        /// Réglages communs aux deux sources. Le son est spatialisé (spatialBlend = 1
        /// par défaut) pour qu'il vienne de la manette qui scale ; dopplerLevel = 0
        /// évite les variations de hauteur parasites quand on déplace la main.
        /// </summary>
        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.volume = scaleVolume;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
        }

        /// <summary>
        /// Délai avant la boucle : par défaut la durée réelle de l'intro (4 s), donc il
        /// suffit de remplacer les clips sans retoucher ce réglage. Décoché (ou sans
        /// introClip), on retombe sur la valeur manuelle loopDelay.
        /// </summary>
        private float CurrentLoopDelay =>
            autoLoopDelay && introClip != null ? introClip.length : loopDelay;

        /// <summary>
        /// Bornage de la taille. Deux familles de limites, combinées :
        ///   - relatives à la taille de référence « 1× » (initiale) : minSizeRatio /
        ///     maxSizeRatio (VRScalable de l'objet, sinon defaultMinRatio / defaultMaxRatio) ;
        ///   - absolues en mètres : minWorldSize / maxWorldSize, qui protègent des
        ///     objets minuscules ou géants.
        /// Le bornage se fait sur la plus grande dimension, puis on met les trois axes
        /// à l'échelle du même coefficient (l'agrandissement reste uniforme).
        /// </summary>
        private Vector3 ClampWorldScale(Vector3 desired)
        {
            float minRatio = _marker != null ? _marker.minSizeRatio : defaultMinRatio;
            float maxRatio = _marker != null ? _marker.maxSizeRatio : defaultMaxRatio;
            float minWorld = _marker != null ? _marker.minWorldSize : 0.02f;
            float maxWorld = _marker != null ? _marker.maxWorldSize : 20f;

            float minSize = Mathf.Max(_baseWorldSize * Mathf.Clamp(minRatio, 0.001f, 1f), minWorld);
            float maxSize = Mathf.Min(_baseWorldSize * Mathf.Max(maxRatio, 1f), Mathf.Max(maxWorld, minSize + 0.0001f));

            float size = MaxAxis(desired);
            if (size <= 1e-6f) return desired;

            float clamped = Mathf.Clamp(size, minSize, maxSize);
            if (!Mathf.Approximately(clamped, size)) desired *= clamped / size;
            return desired;
        }

        /// <summary>Remet à zéro l'état de saisie (appelé quand plus rien n'est tenu).</summary>
        private void ResetGesture()
        {
            IsScalingActive = false;
            _target = null;
            _targetTransform = null;
            _marker = null;
            _holderHand = null;
            _lastScaleTime = float.NegativeInfinity;
            _hapticWaveTimer = 0f;
            _lastScaleDirection = 0f;
            EndPinch();
        }

        /// <summary>
        /// Valeur d'appui d'une manette (0 = relâché, 1 = enfoncé). Trois niveaux de
        /// lecture par ordre de fiabilité décroissante, comme le reste du projet :
        ///   1. XRInputDevice (API XR historique) ;
        ///   2. périphériques Input System nommés "grip" / "trigger" ;
        ///   3. fallback éditeur sans casque : bouton gauche de la souris pour la main
        ///      gauche, bouton droit pour la main droite (même convention que VRGrabHand).
        /// Le grip est prioritaire sur le trigger pour que les deux ne se marchent pas dessus.
        /// </summary>
        private static float ReadPressValue(XRNode node)
        {
            XRInputDevice device = GetControllerDevice(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.grip, out float grip) && grip > 0f) return grip;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigger) && trigger > 0f) return trigger;
            }

            bool isLeft = node == XRNode.LeftHand;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice dev = devices[i];
                if (!DeviceMatchesHand(dev, isLeft)) continue;

                var gripControl = VRInputControls.Find<AxisControl>(dev, "grip");
                if (gripControl != null) return gripControl.ReadValue();

                var triggerControl = VRInputControls.Find<AxisControl>(dev, "trigger");
                if (triggerControl != null) return triggerControl.ReadValue();
            }

#if UNITY_EDITOR
            if (VRDeviceState.EditorFallbackAllowed && Mouse.current != null)
            {
                ButtonControl button = isLeft ? Mouse.current.leftButton : Mouse.current.rightButton;
                if (button.isPressed) return 1f;
            }
#endif

            return 0f;
        }

        /// <summary>
        /// Axe vertical du thumbstick (-1 = vers soi, +1 = vers le haut). Mêmes niveaux de
        /// fallback que ReadPressValue : XRInputDevice, puis périphériques Input System,
        /// puis clavier (W / flèche haut = agrandir, S / flèche bas = rétrécir) quand
        /// aucun casque n'est actif. La petite zone morte sur le carré évite de déclencher
        /// l'échelle avec la dérive naturelle du stick.
        /// </summary>
        private static float ReadThumbstickAxis(XRNode node)
        {
            Vector2 stick = VRStickReader.Read(node);
            return Mathf.Clamp(stick.y, -1f, 1f);
        }

        /// <summary>
        /// Retrouve la manette correspondant à un nœud XR, avec repli par
        /// caractéristiques « Controlleur + main » si <see cref="InputDevices.GetDeviceAtXRNode(XRNode)"/>
        /// renvoie un device invalide (fréquent sous OpenXR). Même pattern que le
        /// suivi de <c>VRGrabHand</c> : sans ce repli, le niveau 1 est simplement
        /// court-circuité et la lecture dépend entièrement du New Input System.
        /// </summary>
        private static XRInputDevice GetControllerDevice(XRNode node)
            => VRStickReader.GetDevice(node);

        /// <summary>
        /// Identifie le périphérique Input System correspondant à une main : d'abord via
        /// ses usages (LeftHand / RightHand), et en secours via son nom ("Oculus Touch
        /// Left", "Index Left"...).
        /// </summary>
        private static bool DeviceMatchesHand(InputDevice dev, bool isLeft)
            => VRStickReader.DeviceMatchesHand(dev, isLeft);

        /// <summary>Plus grande dimension (en valeur absolue) d'un Vector3 d'échelle.</summary>
        private static float MaxAxis(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }

        /// <summary>
        /// Division composante par composante qui ne explose pas sur un parent à échelle
        /// nulle : on remplace les dénominateurs quasi nuls par epsilon.
        /// </summary>
        private static Vector3 DivideSafe(Vector3 value, Vector3 divisor)
        {
            const float epsilon = 1e-6f;
            return new Vector3(
                value.x / (Mathf.Abs(divisor.x) < epsilon ? epsilon : divisor.x),
                value.y / (Mathf.Abs(divisor.y) < epsilon ? epsilon : divisor.y),
                value.z / (Mathf.Abs(divisor.z) < epsilon ? epsilon : divisor.z));
        }
        /// <summary>
        /// Génère une onde / vague haptique continue et modulée dans la ou les manettes qui opèrent le redimensionnement.
        /// L'intensité ondule selon une courbe sinusoïdale ('vague') dont l'amplitude et la pulsation
        /// réagissent dynamiquement au sens et à la vitesse du scale.
        /// </summary>
        private void UpdateScaleHaptics()
        {
            if (!enableScaleHaptics) return;

            bool isScalingNow = Time.unscaledTime - _lastScaleTime < ScaleActiveWindow;
            if (!isScalingNow)
            {
                _hapticWaveTimer = 0f;
                return;
            }

            // Progression temporelle de l'onde
            _hapticWaveTimer += Time.unscaledDeltaTime * hapticWaveFrequency;
            float wave01 = 0.5f + 0.5f * Mathf.Sin(_hapticWaveTimer * Mathf.PI * 2f);

            // Modulation : si on grandit vs rétrécit, légère différence de crête pour une sensation tactile texturée
            float speedBoost = Mathf.Clamp01(Mathf.Abs(_lastScaleDirection) * 50f);
            float currentIntensity = Mathf.Lerp(baseScaleHapticIntensity, maxScaleHapticIntensity, wave01) * (0.6f + 0.4f * speedBoost);
            currentIntensity = Mathf.Clamp01(currentIntensity);

            float impulseDuration = Time.unscaledDeltaTime * 1.8f;

            // Déterminer la manette active qui contrôle le scale :
            // 1. En mode Deux Mains (TwoHands pinch) : les deux mains travaillent ensemble.
            if (_pinching)
            {
                VRGrabHand holder = _holderHand != null ? _holderHand.GetComponent<VRGrabHand>() : null;
                if (holder != null) holder.TriggerHaptic(currentIntensity, impulseDuration, priority: 0);

                if (_otherHand != null)
                {
                    // Déphasage de la vague entre les deux mains pour un effet de transfert tactile stéréo
                    float waveOther = 0.5f + 0.5f * Mathf.Sin((_hapticWaveTimer + 0.25f) * Mathf.PI * 2f);
                    float otherIntensity = Mathf.Lerp(baseScaleHapticIntensity, maxScaleHapticIntensity, waveOther);
                    _otherHand.TriggerHaptic(otherIntensity, impulseDuration, priority: 0);
                }
            }
            // 2. En mode Thumbstick : c'est la manette qui scale (ScaleStickNode = RightHand)
            else
            {
                VRGrabHand scalingHand = VRGrabHand.GetHandForNode(ScaleStickNode);
                if (scalingHand != null)
                {
                    scalingHand.TriggerHaptic(currentIntensity, impulseDuration, priority: 0);
                }

                // Vibration résiduelle / résonance dans la main tenant l'objet si souhaité
                if (vibrateHoldingHandToo && _holderHand != null)
                {
                    VRGrabHand holdingHand = _holderHand.GetComponent<VRGrabHand>();
                    if (holdingHand != null && holdingHand != scalingHand)
                    {
                        holdingHand.TriggerHaptic(currentIntensity * 0.4f, impulseDuration, priority: 0);
                    }
                }
            }
        }
    }
}
