// =====================================================================================
//  VRDuplicateDelete.cs — DUPLIQUER / SUPPRIMER L'OBJET TENU
//
//  RÔLE
//    Sur appui d'un bouton (X = dupliquer, Y = supprimer par défaut, réglables
//    dans l'Inspector), agit sur l'objet actuellement tenu :
//      • Dupliquer : on instancie un clone, on le replace dans la hiérarchie
//        d'origine de l'original, on lui rend une physique normale et on le
//        décale pour qu'il ne soit pas superposé à l'original.
//      • Supprimer : on relâche proprement l'objet (sans vitesse de lancer)
//        puis on détruit le GameObject.
//
//  À POSER SUR : UNE seule main (la gauche dans la scène de jeu). Le bouton est
//  lu sur la main configurée, QUELE QUE SOIT la main qui tient l'objet.
//
//  ⚠️ ORDRE D'EXÉCUTION : +10, donc APRÈS VRGrabHand (0). Il doit connaître
//     l'objet tenu À L'INSTANT : avant (en −40) une duplication demandée dans
//     la même frame que la saisie était ignorée.
//
//  ⚠️ X et Y sont sur la manette GAUCHE : il faut donc tenir l'objet (souvent
//     de la main droite) et appuyer sur X/Y de la main gauche.
//
//  RÉFÉRENCES : voir README.md (sections 3, 4 et 6.5)
// =====================================================================================
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
    /// <summary>
    /// DUPLIQUER / SUPPRIMER l'objet tenu, en VR, sans quitter l'application.
    ///
    /// Même logique que <see cref="VRScaleGrab"/> : ce composant se pose sur UNE main,
    /// il retrouve tout seul l'objet saisi (grâce au parentage fait par
    /// <see cref="VRGrabHand"/>) et il agit sur lui quand le joueur presse un bouton.
    ///
    /// GESTES — choisis directement dans l'Inspector (champs duplicateButton / deleteButton) :
    ///  - Dupliquer : bouton X par défaut.
    ///  - Supprimer : bouton Y par défaut.
    /// X et Y sont physiquement sur la manette GAUCHE, A et B sur la DROITE : l'action est
    /// lue sur la manette correspondante, quelle que soit la main qui tient l'objet.
    /// IMPORTANT : il faut D'ABORD tenir un objet (gâchette/main), sinon l'appui ne fait rien.
    /// Chaque geste se déclenche sur le FRONT MONTANT du bouton (un appui = une action),
    /// jamais en maintien : sinon l'objet se dupliquerait des dizaines de fois par seconde.
    ///
    /// DÉROULÉ D'UNE FRAME (Update) :
    ///   1. ResolveOtherHand   retrouve la main qui ne porte PAS ce composant.
    ///   2. TryGetHeldObject   demande aux deux mains quel Rigidbody elles tiennent.
    ///   3. TryDuplicate       instancie un clone à côté de l'original (décalé).
    ///   4. TryDelete          relâche proprement l'objet puis le détruit.
    ///
    /// POURQUOI LE CLONE EST DÉTACHÉ DE LA MAIN : pendant la saisie, l'objet est
    /// rendu cinématique et parenté à la main (voir VRGrabHand). On ne peut donc pas
    /// l'instancier tel quel. On crée le clone, on le rend à la hiérarchie d'origine
    /// de l'objet, puis on lui réapplique une physique normale : le duplicata est un
    /// objet autonome dès sa naissance.
    ///
    /// À poser sur le MÊME GameObject qu'un VRGrabHand, sur une seule main.
    /// </summary>
    /// <remarks>
    /// ORDRE D'EXÉCUTION : ce composant s'exécute APRÈS VRGrabHand (0) et
    /// APRÈS VRScaleGrab (-50). Il doit en effet connaître l'objet TENU À
    /// L'INSTANT : avant, une duplication demandée dans la même frame que la
    /// saisie était ignorée (« j'appuie sur X et il ne se passe rien »).
    /// </remarks>
    [DefaultExecutionOrder(10)]
    [AddComponentMenu("Remodeler le Reel/VR/VR Duplicate Delete (Dupliquer / Supprimer l'objet saisi)")]
    public class VRDuplicateDelete : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // CONFIGURATION
        // ---------------------------------------------------------------------

        [Header("--- Configuration ---")]

        /// <summary>
        /// Main qui porte ce script. Elle sert à retrouver l'autre manette ; l'objet tenu,
        /// lui, peut être dans cette main comme dans l'autre. Identique à VRScaleGrab :
        /// le composant n'a pas besoin d'être posé sur les deux mains.
        /// </summary>
        [Tooltip("Main qui porte ce script (l'autre main est détectée automatiquement)")]
        public XRNode handNode = XRNode.LeftHand;

        /// <summary>
        /// Bouton d'une manette, sélectionnable dans l'Inspector.
        /// X et Y sont sur la manette GAUCHE, A et B sur la manette DROITE
        /// (convention Oculus Touch).
        /// </summary>
        public enum VRHandButton
        {
            X,
            Y,
            A,
            B
        }

        /// <summary>
        /// Bouton qui déclenche la DUPLICATION de l'objet tenu. Modifiable dans l'Inspector.
        /// Par défaut : X (manette gauche).
        /// </summary>
        [Tooltip("Bouton qui DUPLIQUE l'objet tenu (X et Y = manette gauche)")]
        public VRHandButton duplicateButton = VRHandButton.X;

        /// <summary>
        /// Bouton qui déclenche la SUPPRESSION de l'objet tenu. Modifiable dans l'Inspector.
        /// Par défaut : Y (manette gauche).
        /// </summary>
        [Tooltip("Bouton qui SUPPRIME l'objet tenu (X et Y = manette gauche)")]
        public VRHandButton deleteButton = VRHandButton.Y;

        /// <summary>
        /// Affiche des messages dans la Console (diagnostic) : utile pour comprendre
        /// « pourquoi rien ne se passe » (objet non tenu, bouton non détecté...).
        /// </summary>
        [Tooltip("Afficher des messages de diagnostic dans la Console")]
        public bool logDebug = false;

        // ---------------------------------------------------------------------
        // DUPLICATION
        // ---------------------------------------------------------------------

        [Header("--- Duplication ---")]

        /// <summary>
        /// Décalage appliqué entre l'original et son clone, pour qu'ils n'apparaissent
        /// pas superposés (ce qui provoquerait une explosion de la physique). En mètres.
        /// Par défaut : 15 cm sur le côté droit de l'objet.
        /// </summary>
        [Tooltip("Décalage entre l'original et le duplicata (mètres)")]
        public Vector3 duplicateOffset = new Vector3(0.15f, 0f, 0f);

        /// <summary>
        /// Si true, le décalage ci-dessus est interprété dans le repère de l'objet :
        /// le clone apparaît toujours « sur le côté de l'objet » quelle que soit son
        /// orientation. Si false, le décalage est en coordonnées monde (toujours vers
        /// le même point cardinal).
        /// </summary>
        [Tooltip("Interpréter le décalage dans le repère de l'objet (sinon : repère monde)")]
        public bool offsetRelativeToObject = true;

        /// <summary>
        /// Si true, le décalage est multiplié par la taille de l'objet : un petit objet
        /// reçoit un petit écart, un grand objet un grand écart. Évite qu'un objet
        /// minuscule soit éjecté très loin et qu'un objet géant reste imbriqué.
        /// </summary>
        [Tooltip("Adapter le décalage à la taille de l'objet")]
        public bool scaleOffsetWithObjectSize = false;

        /// <summary>
        /// Remet à zéro la vitesse linéaire et angulaire du clone juste après sa
        /// création. Utile car le clone hérite de la pose de l'objet tenu : sans ce
        /// nettoyage, une petite vitesse résiduelle pourrait l'envoyer valser.
        /// </summary>
        [Tooltip("Mettre à zéro la vitesse du duplicata à sa création")]
        public bool zeroCloneVelocity = true;

        // ---------------------------------------------------------------------
        // LIMITES DE NOMBRE
        // ---------------------------------------------------------------------

        [Header("--- Limites ---")]

        /// <summary>
        /// Nombre maximal d'exemplaires d'un même objet source (original inclus).
        ///
        /// raised AU-DELÀ de ce nombre, la duplication est refusée.
        ///
        /// CE N'EST PAS UNE CONTRAINTE COSMÉTIQUE : le projet contient des objets
        /// « Gaussian Splatting », dont le rendu alloue des GPU buffers propres à
        /// CHAQUE instance et retrie tous les splats à chaque image. Passer de
        /// 1 à 30 exemplaires d'un scan de 43 000 splats, c'est 30 tris par
        /// image et une saturation mémoire qui fait tomber le framerate en
        /// quelques secondes. Le plafond protège le joueur de lui-même.
        ///
        /// Mettre 0 pour désactiver la limite.
        /// </summary>
        [Tooltip("Nombre max d'exemplaires du même objet (0 = illimité)")]
        [Min(0)] public int maxInstancesPerSource = 10;

        /// <summary>
        /// Empêche de supprimer le DERNIER exemplaire d'un objet.
        ///
        /// Sans ce garde-fou, un joueur peut supprimer l'unique cylindre de la
        /// cylindre de la scène : plus rien à saisir, plus de respawn possible,
        /// et la scène est définitivement gâchée. La suppression du dernier
        /// exemplaire est donc refusée (et signalée dans la Console).
        ///
        /// Chaque duplication porte la marque VRCloneOrigin, ce qui permet de
        /// savoir si des clones existent encore.
        /// </summary>
        [Tooltip("Refuser la suppression du dernier exemplaire d'un objet")]
        public bool protectLastInstance = false;

        // ---------------------------------------------------------------------
        // SON
        // ---------------------------------------------------------------------

        [Header("--- Son ---")]

        /// <summary>Source réutilisée si renseignée, sinon créée au runtime sur la main.</summary>
        [Tooltip("Source audio réutilisée si renseignée, sinon créée au runtime")]
        public AudioSource actionAudio;

        /// <summary>Son joué au moment d'une duplication (optionnel).</summary>
        [Tooltip("Son joué lors d'une duplication")]
        public AudioClip duplicateClip;

        /// <summary>Son joué au moment d'une suppression (optionnel).</summary>
        [Tooltip("Son joué lors d'une suppression")]
        public AudioClip deleteClip;

        [Range(0f, 1f)] [Tooltip("Volume des retours sonores")]
        public float volume = 1f;

        // ---------------------------------------------------------------------
        // ÉTAT INTERNE
        // ---------------------------------------------------------------------

        /// <summary>La main qui ne porte PAS ce composant (l'autre manette).</summary>
        private VRGrabHand _otherHand;

        /// <summary>Le VRGrabHand posé sur CE MÊME objet (la main hôte), mis en cache.</summary>
        private VRGrabHand _selfHand;

        /// <summary>État du bouton « dupliquer » à la frame précédente (détection de front).</summary>
        private bool _wasDuplicatePressed;

        /// <summary>État du bouton « supprimer » à la frame précédente (détection de front).</summary>
        private bool _wasDeletePressed;

        /// <summary>Source de lecture des retours sonores (créée si nécessaire).</summary>
        private AudioSource _audio;

        /// <summary>Un clone vient d'être créé (original, clone). Écouté par la narration.</summary>
        public static event System.Action<Rigidbody, GameObject> ObjetDuplique;

        /// <summary>Un objet va être détruit (appelé juste avant Destroy). Écouté par la narration.</summary>
        public static event System.Action<Rigidbody> ObjetSupprime;

        // =====================================================================
        // INITIALISATION
        // =====================================================================

        /// <summary>
        /// Prépare la source audio. Si une source est câblée dans l'inspector elle est
        /// réutilisée, sinon on en crée une sur la main. Le son est spatialisé (il vient
        /// de la manette) et dopplerLevel = 0 pour éviter les variations de hauteur quand
        /// la main bouge.
        /// </summary>
        private void Awake()
        {
            _audio = actionAudio != null ? actionAudio : gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.volume = volume;
            _audio.spatialBlend = 1f;
            _audio.dopplerLevel = 0f;
        }

        // =====================================================================
        // BOUCLE PRINCIPALE
        // =====================================================================

        /// <summary>
        /// Chaque frame : on résout les mains, on lit les deux boutons configurés dans
        /// l'Inspector et on agit sur le FRONT MONTANT de chacun.
        ///
        /// Les boutons sont lus MÊME si aucun objet n'est tenu : ainsi, si le joueur
        /// appuie sans rien tenir, on peut (en mode diagnostic) l'expliquer dans la Console.
        /// </summary>
        private void Update()
        {
            ResolveOtherHand();

            bool duplicatePressed = ReadButton(duplicateButton);
            bool deletePressed = ReadButton(deleteButton);

            bool duplicateEdge = (duplicatePressed && !_wasDuplicatePressed) || VRButtonReader.WasPressed((VRButton)duplicateButton);
            bool deleteEdge = (deletePressed && !_wasDeletePressed) || VRButtonReader.WasPressed((VRButton)deleteButton);

            _wasDuplicatePressed = duplicatePressed;
            _wasDeletePressed = deletePressed;

            // En mode mains, les boutons X/Y « physiques » sont des faux appuis générés par
            // l'émulation de gestes (ALVR / SteamVR) : seul le menu poignet fait foi.
            if (VRModalityManager.IsHandMode) return;

            if (!duplicateEdge && !deleteEdge) return;

            if (!TryGetHeldObject(out VRGrabHand holderHand, out Rigidbody body))
            {
                if (logDebug)
                {
                    Debug.Log(
                        "[VRDuplicateDelete] Bouton appuyé, mais AUCUN objet n'est tenu. " +
                        "Attrape d'abord un objet (gâchette), puis appuie sur " +
                        duplicateButton + " (dupliquer) ou " + deleteButton + " (supprimer).",
                        this);
                }
                return;
            }

            if (duplicateEdge) TryDuplicate(holderHand, body);
            else TryDelete(holderHand, body);
        }

        /// <summary>
        /// Déclenche la duplication de l'objet actuellement tenu (appelable depuis un bouton UI ou Hand Menu).
        /// </summary>
        public void DuplicateHeldObject()
        {
            if (TryGetHeldObject(out VRGrabHand holderHand, out Rigidbody body))
            {
                TryDuplicate(holderHand, body);
            }
            else if (logDebug)
            {
                Debug.Log("[VRDuplicateDelete] Action UI Dupliquer ignorée : aucun objet n'est tenu.", this);
            }
        }

        /// <summary>
        /// Déclenche la suppression de l'objet actuellement tenu (appelable depuis un bouton UI ou Hand Menu).
        /// </summary>
        public void DeleteHeldObject()
        {
            if (TryGetHeldObject(out VRGrabHand holderHand, out Rigidbody body))
            {
                TryDelete(holderHand, body);
            }
            else if (logDebug)
            {
                Debug.Log("[VRDuplicateDelete] Action UI Supprimer ignorée : aucun objet n'est tenu.", this);
            }
        }

        // =====================================================================
        // RÉSOLUTION DE LA MAIN ET DE L'OBJET TENU
        // =====================================================================

        /// <summary>
        /// Retrouve la main qui ne porte pas ce composant. On se fie d'abord au
        /// VRGrabHand posé sur CE MÊME objet (source de vérité : c'est lui qui saisit),
        /// puis au champ handNode en secours. On ignore toujours notre propre transform.
        /// Mise en cache : la recherche ne tourne que si l'autre main est introuvable.
        /// </summary>
        private void ResolveOtherHand()
        {
            if (_selfHand == null || !_selfHand.isActiveAndEnabled) _selfHand = GetComponent<VRGrabHand>();
            if (_otherHand != null && _otherHand.isActiveAndEnabled) return;

            // Registre VRGrabHand.AllHands : pas de parcours de scène.
            var hands = VRGrabHand.AllHands;
            XRNode selfNode = _selfHand != null ? _selfHand.handNode : handNode;

            _otherHand = null;

            // 1) une AUTRE manette portant un handNode différent (cas normal).
            for (int i = 0; i < hands.Count; i++)
            {
                if (hands[i] == null || hands[i].transform == transform) continue;
                if (hands[i].handNode != selfNode)
                {
                    _otherHand = hands[i];
                    return;
                }
            }

            // 2) secours : n'importe quelle autre manette, même si son handNode est mal
            //    renseigné. Mieux vaut détecter la seconde main que ne rien faire.
            for (int i = 0; i < hands.Count; i++)
            {
                if (hands[i] != null && hands[i].transform != transform)
                {
                    _otherHand = hands[i];
                    return;
                }
            }
        }

        /// <summary>
        /// Renvoie la main qui tient un objet (et son Rigidbody), en donnant la priorité
        /// à la main qui porte ce composant. L'ordre est sans importance : si les deux
        /// mains tiennent un objet différent, c'est la main du composant qui gagne.
        /// </summary>
        private bool TryGetHeldObject(out VRGrabHand holderHand, out Rigidbody body)
        {
            body = VRGrabHand.GetAnyGrabbedBody(out holderHand);
            if (body != null && holderHand != null) return true;

            if (_selfHand != null && _selfHand.GrabbedBody != null)
            {
                holderHand = _selfHand;
                body = _selfHand.GrabbedBody;
                return true;
            }
            if (_otherHand != null && _otherHand.GrabbedBody != null)
            {
                holderHand = _otherHand;
                body = _otherHand.GrabbedBody;
                return true;
            }

            return false;
        }

        /// <summary>
        /// VRGrabHand parente l'objet saisi à la main : l'objet tenu est donc un enfant
        /// direct (ou au pire un descendant) de la main. On le cherche là.
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
        /// Renvoie le VRGrabHand correspondant à la main qui tient l'objet, afin de
        /// pouvoir la faire relâcher proprement avant une suppression.
        /// </summary>
        private VRGrabHand GetHandComponent(Transform holder)
        {
            if (holder == transform) return GetComponent<VRGrabHand>();
            return _otherHand;
        }

        // =====================================================================
        // ACTION : DUPLIQUER
        // =====================================================================

        /// <summary>
        /// Instancie un duplicata de l'objet tenu, posé à côté de l'original.
        ///
        /// Étapes :
        ///  1. Vérifier l'autorisation (composant VRDuplicable).
        ///  2. Mémoriser la pose MONDE de l'original (il est dans la main) et le parent
        ///     d'origine de l'objet, avant toute modification.
        ///  3. Instancier (l'objet lui-même, ou le modèle de remplacement du marqueur).
        ///  4. Replacer le clone dans la hiérarchie d'origine et lui donner la pose de
        ///     l'original décalée : il naît donc à côté, pas dans la main.
        ///  5. Restituer une physique normale au clone (le clone ne doit PAS hériter de
        ///     l'état cinématique de la saisie).
        /// </summary>
        private void TryDuplicate(VRGrabHand hand, Rigidbody body)
        {
            VRDuplicable marker = body.GetComponent<VRDuplicable>();
            if (marker != null && !marker.duplicationEnabled)
            {
                if (logDebug) Debug.Log("[VRDuplicateDelete] Duplication bloquée par VRDuplicable sur « " + body.name + " ».", this);
                return;
            }

            // Un clone de clone reste rattaché à l'ORIGINAL de la famille :
            // c'est lui qui porte le plafond. Sans cela, on pourrait contourner
            // la limite en dupliquant toujours le dernier clone.
            GameObject root = ResolveSourceRoot(body.gameObject);

            // PLAFOND : refuse de dépasser le nombre maximal d'exemplaires.
            if (maxInstancesPerSource > 0)
            {
                int existing = VRCloneOrigin.CountInstances(root);
                if (existing >= maxInstancesPerSource)
                {
                    Debug.LogWarning(
                        "[VRDuplicateDelete] Duplication refusée : « " + root.name + " » est déjà à " +
                        "son maximum de " + maxInstancesPerSource + " exemplaires. " +
                        "Supprime d'abord un exemplaire pour en créer un autre.", this);
                    return;
                }
            }

            Transform parent = hand != null ? hand.GrabbedOriginalParent : body.transform.parent;

            // SÉCURITÉ : le clone ne doit JAMAIS rester attaché à une main, sinon il
            // suivrait la manette et serait impossible à saisir séparément. Si le
            // parent calculé est la main elle-même (objet qui n'avait pas de parent
            // d'origine), on le détache à la racine de la scène.
            if (hand != null && parent != null && parent == hand.transform)
            {
                if (logDebug) Debug.LogWarning(
                    "[VRDuplicateDelete] Parent d'origine absent : le clone est placé à la racine " +
                    "pour ne pas rester attaché à la main.", this);
                parent = null;
            }

GameObject source = (marker != null && marker.duplicationPrefab != null)
                ? marker.duplicationPrefab
                : body.gameObject;

            // HERITAGE DE LA PHYSIQUE : on recopie le mode de détection de
            // collision de l'original. Un fruit réglé en « Discrete » (valeur
            // par défaut) ne survivrait pas à un lancer : à 5 m/s et 72 images/s
            // il avance de 7 cm entre deux frames alors qu'il fait 6 cm de large,
            // et traverse le sol. On lui laisse le réglage que l'auteur a choisi.
            CollisionDetectionMode sourceDetection = CollisionDetectionMode.Discrete;
            RigidbodyInterpolation sourceInterpolation = RigidbodyInterpolation.Interpolate;
            if (body != null)
            {
                sourceDetection = body.collisionDetectionMode;
                sourceInterpolation = body.interpolation;
            }

            Vector3 worldPosition = body.transform.position;
            Quaternion worldRotation = body.transform.rotation;

            GameObject clone = Instantiate(source);
            Transform cloneTransform = clone.transform;

            // Retour dans la hiérarchie d'origine (hors de la main), puis pose décalée.
            cloneTransform.SetParent(parent, true);
            cloneTransform.SetPositionAndRotation(
                worldPosition + ComputeOffset(body.transform),
                worldRotation
            );

            // Le clone appartient à la même famille que l'original : c'est ce
            // qui permet de compter les exemplaires et d'appliquer le plafond.
            VRCloneOrigin.Attach(clone, root);

            if (clone.TryGetComponent(out Rigidbody cloneBody))
            {
                cloneBody.isKinematic = false;
                cloneBody.useGravity = true;
                cloneBody.interpolation = sourceInterpolation != RigidbodyInterpolation.None
                    ? sourceInterpolation
                    : RigidbodyInterpolation.Interpolate;
                cloneBody.collisionDetectionMode = sourceDetection;

                if (zeroCloneVelocity)
                {
#if UNITY_6000_0_OR_NEWER
                    cloneBody.linearVelocity = Vector3.zero;
#else
                    cloneBody.velocity = Vector3.zero;
#endif
                    cloneBody.angularVelocity = Vector3.zero;
                }
            }
            else if (logDebug)
            {
                // Sans Rigidbody, VRGrabHand ne pourra jamais le saisir : on le dit.
                Debug.LogWarning(
                    "[VRDuplicateDelete] Le clone de « " + body.name + " » n'a pas de Rigidbody : " +
                    "il ne sera pas saisissable par la main.", clone);
            }

            PlayAudio(duplicateClip, VRSon.Duplication, worldPosition);
            ObjetDuplique?.Invoke(body, clone);
            hand?.TriggerHaptic(0.65f, 0.12f, 3); // Impulsion haptique de duplication (pop)

            // SPAWN DU CLONE : on Capture explicitement le point de rappel
            // maintenant qu'il est à sa position finale. Sans cet appel, le
            // clone garderait le point de rappel de l'original et
            // réapparaîtrait au milieu de la scène après être tombé.
            if (clone.TryGetComponent(out VRThrowableRespawn respawner))
            {
                respawner.CaptureSpawnPoint();
            }

            if (logDebug)
            {
                Debug.Log("[VRDuplicateDelete] Duplication de « " + body.name + " » (bouton " +
                          duplicateButton + ") → clone « " + clone.name + " » en " +
                          cloneTransform.position + ", parent « " + (parent ? parent.name : "(null)") + " ».",
                          this);
            }
        }

        /// <summary>
        /// Remonte jusqu'à l'objet SOURCE d'une famille de clones.
        /// Un clone porte un VRCloneOrigin ; on suit la chaîne jusqu'à l'original.
        /// </summary>
        private static GameObject ResolveSourceRoot(GameObject candidate)
        {
            if (candidate == null) return null;

            VRCloneOrigin marker = candidate.GetComponent<VRCloneOrigin>();
            return (marker != null && marker.source != null) ? marker.source : candidate;
        }

        /// <summary>
        /// Calcule le décalage monde entre l'original et son clone, en tenant compte du
        /// repère (monde ou objet) et, si demandé, de la taille RÉELLE de l'objet.
        ///
        /// POINT IMPORTANT : sans cet ajustement, le clone naît exactement SUR
        /// l'original (15 cm pour un objet d'un mètre). Les deux se superposent
        /// alors visuellement — on a « rien dupliqué » — et la main ne peut pas
        /// saisir l'un sans l'autre. On décale donc d'au moins la moitié de la
        /// taille de l'objet, pour que le clone soit franchement séparé.
        /// </summary>
        private Vector3 ComputeOffset(Transform target)
        {
            Vector3 offset = duplicateOffset;

            if (offsetRelativeToObject) offset = target.rotation * offset;

            if (scaleOffsetWithObjectSize)
            {
                Vector3 size = MeasureWorldSize(target);
                // On prend la plus grande dimension horizontale : c'est celle qui
                // décide de la séparation visuelle.
                float span = Mathf.Max(size.x, size.z);
                float needed = span * 0.75f + 0.05f;
                float current = new Vector2(offset.x, offset.z).magnitude;

                if (needed > current)
                {
                    Vector3 dir = new Vector3(offset.x, 0f, offset.z);
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
                    dir.Normalize();
                    offset = dir * needed;
                }
            }

            return offset;
        }

        /// <summary>
        /// Taille approximative (MONDE) de l'objet, mesurée sur ses colliders, ou à
        /// défaut sur son Renderer. Sert à espacer intelligemment les clones.
        /// Les bounds de Unity sont déjà exprimés en espace monde, donc aucune
        /// conversion n'est nécessaire ici.
        /// </summary>
        private static Vector3 MeasureWorldSize(Transform target)
        {
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Collider col in target.GetComponentsInChildren<Collider>())
            {
                if (col == null || !col.enabled) continue;
                if (!hasBounds) { bounds = col.bounds; hasBounds = true; }
                else bounds.Encapsulate(col.bounds);
            }

            if (!hasBounds)
            {
                Renderer renderer = target.GetComponentInChildren<Renderer>();
                if (renderer != null) return renderer.bounds.size;
                return Vector3.one * 0.2f;
            }

            return bounds.size;
        }

        // =====================================================================
        // ACTION : SUPPRIMER
        // =====================================================================

        /// <summary>
        /// Détruit l'objet tenu.
        ///
        /// Point CRITIQUE : on demande d'abord à la main de RELÂCHER l'objet
        /// (sans vitesse de lancer). Sans cette étape, VRGrabHand garderait une
        /// référence vers un objet détruit (« missing reference ») et lèverait une
        /// erreur à son prochain relâchement. On détruit ensuite le GameObject.
        /// </summary>
        private bool TryDelete(VRGrabHand hand, Rigidbody body)
        {
            VRDuplicable marker = body.GetComponent<VRDuplicable>();
            if (marker != null && !marker.deletionEnabled)
            {
                if (logDebug) Debug.Log("[VRDuplicateDelete] Suppression bloquée par VRDuplicable sur « " + body.name + " ».", this);
                return false;
            }

            // GARDE-FOU DERNIER EXEMPLAIRE : on refuse de supprimer l'unique
            // exemplaire vivant d'un objet si protectLastInstance est activé.
            if (protectLastInstance)
            {
                GameObject root = ResolveSourceRoot(body.gameObject);
                int remaining = VRCloneOrigin.CountInstances(root);
                if (remaining <= 1)
                {
                    Debug.LogWarning(
                        "[VRDuplicateDelete] Suppression refusée : « " + body.name + " » est le " +
                        "dernier exemplaire. La scène deviendrait vide ; duplique-le d'abord " +
                        "ou désactive « Protéger le dernier exemplaire ».", this);
                    return false;
                }
            }

            hand?.ReleaseObject(applyThrowVelocity: false);

            PlayAudio(deleteClip, VRSon.Suppression, body.transform.position);
            ObjetSupprime?.Invoke(body);
            hand?.TriggerHaptic(0.8f, 0.18f, 3); // Impulsion haptique de suppression (choc sec)
            if (logDebug) Debug.Log("[VRDuplicateDelete] Suppression de « " + body.name + " » (bouton " + deleteButton + ").", this);
            Destroy(body.gameObject);
            return true;
        }

        /// <summary>Duplique un objet qui n'est tenu par aucune main (ex. rangé dans la bulle du menu poignet).</summary>
        public void DuplicateBody(Rigidbody body)
        {
            if (body != null) TryDuplicate(null, body);
        }

        /// <summary>Supprime un objet non tenu. Renvoie faux si la suppression est refusée.</summary>
        public bool DeleteBody(Rigidbody body)
        {
            return body != null && TryDelete(null, body);
        }

        // =====================================================================
        // LECTURE DES BOUTONS (3 NIVEAUX)
        // =====================================================================

        /// <summary>
        /// Valeur d'appui du bouton demandé (X, Y, A ou B), configuré dans l'Inspector.
        ///
        /// X et Y sont lus sur la manette GAUCHE, A et B sur la DROITE. Trois niveaux de
        /// lecture par ordre de fiabilité décroissante, comme le reste du projet :
        ///   1. XRInputDevice (API XR historique) — primaryButton (= X / A) ou
        ///      secondaryButton (= Y / B) sur le nœud de la manette concernée ;
        ///   2. périphériques Input System de la même main — contrôles nommés xButton /
        ///      yButton / aButton / bButton, puis primary/secondaryButton en secours ;
        ///   3. fallback éditeur sans casque : la touche clavier PORTANT LE MÊME NOM
        ///      (X, Y, A ou B) simule le bouton correspondant.
        /// </summary>
        private bool ReadButton(VRHandButton button)
        {
            return VRButtonReader.Read((VRButton)button);
        }

        // =====================================================================
        // SON
        // =====================================================================

        /// <summary>Joue le clip renseigné, ou à défaut le son procédural de <see cref="VRSons"/>.</summary>
        private void PlayAudio(AudioClip clip, VRSon repli, Vector3 position)
        {
            if (clip != null && _audio != null)
            {
                _audio.PlayOneShot(clip, volume);
                return;
            }
            VRSons.Jouer(repli, position);
        }
    }
}
