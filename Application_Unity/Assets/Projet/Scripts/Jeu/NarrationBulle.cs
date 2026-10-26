// =====================================================================================
//  NarrationBulle.cs — LA BULLE DE NARRATION (affichage partagé)
//
//  RÔLE
//    Affiche les répliques des <see cref="NarrationSequence"/> posées sur le même
//    objet : texte qui s'écrit lettre par lettre, voix, petits sons, fondu.
//    Une seule bulle par objet « Narration » : les séquences lui envoient leurs
//    demandes, elle les joue UNE PAR UNE (file d'attente) — deux textes ne se
//    chevauchent jamais.
//
//  POINTS CLÉS
//    • TOUJOURS VISIBLE : la bulle est dessinée par-dessus le décor (ZTest Always).
//      Avant, un mur ou un meuble entre les yeux et la bulle la cachait.
//    • SUIVI DOUX DU REGARD : la bulle reste posée dans l'espace et ne revient
//      devant les yeux que si le joueur tourne franchement la tête. Un texte
//      collé à la tête fatigue vite en VR.
//
//  À POSER SUR : l'objet « Narration ». Ajoutée AUTOMATIQUEMENT avec la première
//    NarrationSequence (RequireComponent) : il suffit de régler son apparence.
//
//  TEST SANS CASQUE : Espace / Entrée = finir la réplique, puis passer à la suivante.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>
    /// Bulle unique qui joue, dans l'ordre, les séquences de narration demandées.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Remodeler le Reel/Jeu/Narration - Bulle (affichage)")]
    public class NarrationBulle : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Caméra du joueur. Vide = Camera.main.")]
        public Transform cameraCible;

        [Range(0.4f, 4f)]
        [Tooltip("Distance de la bulle devant les yeux, en mètres.")]
        public float distance = 1.1f;

        [Range(0.2f, 2f)]
        [Tooltip("Largeur de la bulle, en mètres.")]
        public float largeur = 0.75f;

        [Range(-40f, 40f)]
        [Tooltip("Angle sous le regard, en degrés (positif = plus bas). 0 = au centre de la vue.")]
        public float angleSousRegard = 14f;

        [Tooltip("Dessiner la bulle par-dessus le décor : elle n'est jamais cachée par un objet de la scène.")]
        public bool toujoursVisible = true;

        [Header("Suivi du regard")]
        [Tooltip("Vrai = la bulle reste posée et ne revient devant les yeux que si on tourne la tête. "
                 + "Faux = collée à la vue (sous-titre).")]
        public bool suiviDoux = true;

        [Range(5f, 60f)]
        [Tooltip("Écart (degrés) entre le regard et la bulle au-delà duquel elle se recentre.")]
        public float angleRecentrage = 22f;

        [Range(0.5f, 15f)]
        [Tooltip("Vitesse du recentrage (plus grand = plus nerveux).")]
        public float vitesseSuivi = 4f;

        [Header("Apparence")]
        [Range(0f, 1f)]
        public float opaciteFond = 0.6f;

        public Color couleurTexte = Color.white;

        [Range(16f, 60f)]
        [Tooltip("Taille maximale du texte (il rétrécit tout seul si la réplique est longue).")]
        public float tailleTexte = 34f;

        [Header("Rythme")]
        [Range(0.005f, 0.2f)]
        [Tooltip("Secondes entre deux lettres.")]
        public float vitesseEcriture = 0.035f;

        [Range(0f, 0.3f)]
        [Tooltip("Temps de lecture ajouté par caractère quand une réplique a une durée automatique (0).")]
        public float lectureParCaractere = 0.05f;

        [Tooltip("Temps de lecture minimal d'une réplique à durée automatique.")]
        public float lectureMin = 2.5f;

        [Tooltip("Pause entre deux répliques d'une même séquence.")]
        public float pauseEntreRepliques = 0.35f;

        [Range(0f, 2f)]
        public float dureeFondu = 0.35f;

        [Header("Sons")]
        [Tooltip("Petit carillon quand la bulle apparaît.")]
        public bool sonApparition = true;

        [Tooltip("Tic de machine à écrire pendant l'écriture (coupé automatiquement quand une voix parle).")]
        public bool sonLettres = true;

        [Range(1, 6)]
        [Tooltip("Un tic toutes les N lettres.")]
        public int lettresParTic = 2;

        [Range(0f, 1f)]
        public float volumeVoix = 1f;

        // ------------------------------------------------------------------
        // Interne
        // ------------------------------------------------------------------
        private const float LargeurCanvas = 800f;
        private const float HauteurCanvas = 220f;

        private readonly Queue<NarrationSequence> _file = new Queue<NarrationSequence>();
        private NarrationSequence _enCours;
        private Coroutine _tache;
        private Transform _panneau;
        private CanvasGroup _groupe;
        private TextMeshProUGUI _texte;
        private AudioSource _voix;
        private Camera _cam;
        private bool _recentrer;
        private bool _passer;

        /// <summary>Vrai si une séquence est affichée ou en attente.</summary>
        public bool EstOccupee => _enCours != null || _file.Count > 0;

        /// <summary>Séquence actuellement affichée (null si la bulle est vide).</summary>
        public NarrationSequence EnCours => _enCours;

        /// <summary>
        /// Demande l'affichage d'une séquence. Si la bulle est occupée, la séquence
        /// attend son tour — sauf si elle demande à <see cref="NarrationSequence.interrompre"/>.
        /// </summary>
        public void Demander(NarrationSequence sequence)
        {
            if (sequence == null || sequence == _enCours || _file.Contains(sequence)) return;

            if (_enCours != null && sequence.interrompre)
            {
                Interrompre();
            }

            if (_enCours == null)
            {
                _tache = StartCoroutine(Raconter(sequence));
            }
            else
            {
                _file.Enqueue(sequence);
            }
        }

        /// <summary>Finit l'écriture de la réplique en cours, ou passe à la suivante si elle est déjà écrite.</summary>
        public void Passer() => _passer = true;

        /// <summary>Coupe la séquence en cours (elle est considérée comme terminée) et vide la file.</summary>
        public void ToutArreter()
        {
            _file.Clear();
            Interrompre();
            Cacher();
        }

        private void Interrompre()
        {
            if (_tache != null) StopCoroutine(_tache);
            _tache = null;
            if (_voix != null) _voix.Stop();

            NarrationSequence coupee = _enCours;
            _enCours = null;
            if (coupee != null) coupee.NotifierFin();
        }

        // ------------------------------------------------------------------
        // Boucle d'affichage
        // ------------------------------------------------------------------
        private IEnumerator Raconter(NarrationSequence sequence)
        {
            _enCours = sequence;

            if (PreparerPanneau())
            {
                Placer(immediat: true);
                _panneau.gameObject.SetActive(true);
                _texte.text = string.Empty;
                if (sonApparition) VRSons.Jouer(VRSon.NarrationApparition);
                yield return Fondu(0f, 1f);

                List<Replique> repliques = sequence.repliques;
                for (int i = 0; i < repliques.Count; i++)
                {
                    if (i > 0 && pauseEntreRepliques > 0f) yield return new WaitForSeconds(pauseEntreRepliques);
                    yield return Ecrire(repliques[i]);
                }

                yield return Fondu(1f, 0f);
                Cacher();
            }

            _tache = null;
            _enCours = null;
            // La fin peut déclencher d'autres séquences (enchaînement) : elles
            // s'ajoutent à la file, qu'on dépile juste après.
            sequence.NotifierFin();

            if (_enCours == null && _file.Count > 0)
            {
                _tache = StartCoroutine(Raconter(_file.Dequeue()));
            }
        }

        private IEnumerator Ecrire(Replique replique)
        {
            string contenu = replique.texte ?? string.Empty;
            _texte.text = contenu;
            _texte.maxVisibleCharacters = 0;
            _texte.ForceMeshUpdate();
            int total = _texte.textInfo.characterCount;

            bool avecVoix = replique.voix != null;
            float pas = vitesseEcriture;
            if (avecVoix)
            {
                PreparerVoix();
                _voix.clip = replique.voix;
                _voix.volume = volumeVoix;
                _voix.Play();
                // Écriture calée sur la voix : la dernière lettre tombe avec la dernière syllabe.
                if (total > 0) pas = Mathf.Clamp(replique.voix.length / total, 0.005f, 0.5f);
            }

            // maxVisibleCharacters (plutôt que d'ajouter les lettres une à une) : la mise
            // en page est calculée une fois, les mots ne sautent pas de ligne en cours d'écriture.
            _passer = false;
            for (int n = 1; n <= total && !_passer; n++)
            {
                _texte.maxVisibleCharacters = n;
                char c = _texte.textInfo.characterInfo[n - 1].character;
                if (sonLettres && !avecVoix && !char.IsWhiteSpace(c) && n % Mathf.Max(1, lettresParTic) == 0)
                {
                    VRSons.Jouer(VRSon.NarrationLettre, null, 1f, VRSons.PitchAleatoire(0.1f));
                }
                yield return new WaitForSeconds(pas);
            }
            _texte.maxVisibleCharacters = int.MaxValue;
            _passer = false;

            // --- Temps de lecture (compté une fois le texte écrit) ---
            float lecture = replique.duree > 0f
                ? replique.duree
                : Mathf.Max(lectureMin, contenu.Length * lectureParCaractere);
            float fin = Time.time + lecture;
            while (!_passer && (Time.time < fin || (avecVoix && _voix.isPlaying)))
            {
                yield return null;
            }
            _passer = false;
            if (avecVoix) _voix.Stop();
        }

        private IEnumerator Fondu(float de, float vers)
        {
            if (_groupe == null) yield break;
            float ecoule = 0f;
            while (ecoule < dureeFondu)
            {
                ecoule += Time.deltaTime;
                _groupe.alpha = Mathf.Lerp(de, vers, ecoule / dureeFondu);
                yield return null;
            }
            _groupe.alpha = vers;
        }

        private void Cacher()
        {
            if (_groupe != null) _groupe.alpha = 0f;
            if (_panneau != null) _panneau.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_enCours == null) return;

            UnityEngine.InputSystem.Keyboard clavier = UnityEngine.InputSystem.Keyboard.current;
            if (clavier != null && (clavier.spaceKey.wasPressedThisFrame || clavier.enterKey.wasPressedThisFrame))
            {
                Passer();
            }
        }

        // LateUpdate : la caméra a déjà reçu la pose du casque de cette frame.
        private void LateUpdate()
        {
            if (_enCours != null && _panneau != null && _panneau.gameObject.activeSelf)
            {
                Placer(immediat: false);
            }
        }

        // ------------------------------------------------------------------
        // Placement
        // ------------------------------------------------------------------
        /// <summary>
        /// Pose la bulle devant le regard. En suivi doux, elle ne bouge que si le
        /// regard s'en est trop écarté, puis glisse jusqu'à revenir devant les yeux.
        /// </summary>
        private void Placer(bool immediat)
        {
            if (_cam == null) return;
            Transform tete = _cam.transform;

            // Repère « regard sans roulis » : la bulle reste horizontale même si
            // le joueur penche la tête.
            Vector3 avant = tete.forward;
            Quaternion repere = Mathf.Abs(Vector3.Dot(avant, Vector3.up)) < 0.98f
                ? Quaternion.LookRotation(avant, Vector3.up)
                : tete.rotation;
            Vector3 cible = tete.position + repere * (Quaternion.Euler(angleSousRegard, 0f, 0f) * Vector3.forward) * distance;

            if (immediat || !suiviDoux)
            {
                _panneau.position = cible;
                _recentrer = false;
            }
            else
            {
                Vector3 versBulle = _panneau.position - tete.position;
                float ecart = Vector3.Angle(versBulle, cible - tete.position);
                // Téléportation / locomotion : la bulle est trop loin ou trop près.
                bool mauvaiseDistance = Mathf.Abs(versBulle.magnitude - distance) > distance * 0.35f;
                if (ecart > angleRecentrage || mauvaiseDistance) _recentrer = true;

                if (_recentrer)
                {
                    float t = 1f - Mathf.Exp(-vitesseSuivi * Time.deltaTime);
                    _panneau.position = Vector3.Lerp(_panneau.position, cible, t);
                    if (ecart < 1.5f && !mauvaiseDistance) _recentrer = false;
                }
            }

            // La face lisible d'un canvas regarde vers +Z : on l'oriente donc de l'œil vers la bulle.
            Vector3 regard = _panneau.position - tete.position;
            if (regard.sqrMagnitude > 1e-6f) _panneau.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        // ------------------------------------------------------------------
        // Construction du panneau
        // ------------------------------------------------------------------
        private bool PreparerPanneau()
        {
            _cam = cameraCible != null ? cameraCible.GetComponent<Camera>() : Camera.main;
            if (_cam == null)
            {
                Debug.LogWarning("[NarrationBulle] Aucune caméra trouvée (Camera.main). "
                                 + "Renseigne « Camera Cible » dans l'inspecteur.", this);
                return false;
            }
            if (_panneau != null) return true;

            GameObject canvasGo = new GameObject("Narration_Bulle", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            // Enfant de l'espace de tracking (Camera Offset) : la bulle suit le rig
            // quand il se déplace (locomotion, rotation), mais pas les mouvements de tête.
            canvasGo.transform.SetParent(_cam.transform.parent, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000; // au-dessus des autres canvas monde (menu poignet…)

            RectTransform rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(LargeurCanvas, HauteurCanvas);
            // Échelle uniforme en monde, quelle que soit l'échelle du rig.
            float echelleParent = canvasGo.transform.parent != null ? canvasGo.transform.parent.lossyScale.x : 1f;
            rt.localScale = Vector3.one * (largeur / LargeurCanvas) / Mathf.Max(0.0001f, echelleParent);

            _groupe = canvasGo.GetComponent<CanvasGroup>();
            _groupe.alpha = 0f;
            _groupe.interactable = false;
            _groupe.blocksRaycasts = false;

            if (opaciteFond > 0.001f)
            {
                GameObject fondGo = new GameObject("Fond", typeof(RectTransform), typeof(Image));
                fondGo.transform.SetParent(canvasGo.transform, false);
                Etirer(fondGo.GetComponent<RectTransform>(), 0f);
                Image fond = fondGo.GetComponent<Image>();
                fond.color = new Color(0f, 0f, 0f, opaciteFond);
                fond.raycastTarget = false;
                if (toujoursVisible) RendreToujoursVisible(fond);
            }

            GameObject texteGo = new GameObject("Texte", typeof(RectTransform), typeof(TextMeshProUGUI));
            texteGo.transform.SetParent(canvasGo.transform, false);
            Etirer(texteGo.GetComponent<RectTransform>(), 28f);
            _texte = texteGo.GetComponent<TextMeshProUGUI>();
            _texte.alignment = TextAlignmentOptions.Center;
            _texte.enableAutoSizing = true;
            _texte.fontSizeMin = 16f;
            _texte.fontSizeMax = tailleTexte;
            _texte.color = couleurTexte;
            _texte.textWrappingMode = TextWrappingModes.Normal;
            _texte.raycastTarget = false;
            if (toujoursVisible) RendreToujoursVisible(_texte);

            _panneau = canvasGo.transform;
            _panneau.gameObject.SetActive(false);
            return true;
        }

        private static void Etirer(RectTransform rt, float marge)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(marge, marge * 0.6f);
            rt.offsetMax = new Vector2(-marge, -marge * 0.6f);
            rt.localScale = Vector3.one;
        }

        private void PreparerVoix()
        {
            if (_voix != null) return;
            _voix = gameObject.AddComponent<AudioSource>();
            _voix.playOnAwake = false;
            _voix.loop = false;
            _voix.spatialBlend = 0f; // voix « dans la tête » : audible quelle que soit l'orientation
        }

        /// <summary>
        /// Fait dessiner un élément d'interface PAR-DESSUS le décor : le test de
        /// profondeur passe toujours (ZTest Always). Les shaders UI/Default et
        /// TextMeshPro lisent leur ZTest dans la propriété « unity_GUIZTestMode ».
        /// Le Gaussian Splatting est composité AVANT les transparents : la bulle
        /// passe donc aussi devant le nuage de points.
        /// </summary>
        public static void RendreToujoursVisible(Graphic graphique)
        {
            if (graphique == null) return;

            Material materiau;
            if (graphique is TMP_Text tmp)
            {
                materiau = tmp.fontMaterial; // crée une instance propre à ce texte
            }
            else
            {
                materiau = new Material(Canvas.GetDefaultCanvasMaterial());
                graphique.material = materiau;
            }

            materiau.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            materiau.renderQueue = (int)RenderQueue.Overlay;
        }
    }
}
