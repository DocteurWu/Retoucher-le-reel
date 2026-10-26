// =====================================================================================
//  VRSons.cs — SOUND DESIGN DU MODULE VR (point d'accès unique)
//
//  RÔLE
//    Joue les retours sonores des interactions : saisie, lâcher, téléportation,
//    rangement dans la bulle, impacts, apparition de la narration…
//    Chaque son existe en version PROCÉDURALE (synthétisée au premier usage) :
//    le jeu « sonne » donc sans aucun fichier audio. Un composant
//    <see cref="VRBanqueSons"/> posé dans la scène permet de remplacer chaque son
//    par un vrai fichier (glisser un AudioClip) et de régler les volumes.
//
//  USAGE
//    VRSons.Jouer(VRSon.Saisie, position);          // son 3D à une position
//    VRSons.Jouer(VRSon.NarrationApparition);       // son 2D (dans la tête)
//
//  ⚠️ Un pool de quelques AudioSource est créé une fois (DontDestroyOnLoad) :
//     pas de GameObject créé par son joué (contrairement à PlayClipAtPoint).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>Catalogue des sons du module VR.</summary>
    public enum VRSon
    {
        Saisie,
        Lacher,
        Teleportation,
        Rangement,
        Recuperation,
        Duplication,
        Suppression,
        Impact,
        NarrationApparition,
        NarrationLettre,
        Reussite
    }

    /// <summary>
    /// Lecture centralisée des sons. Statique : aucun composant à poser.
    /// </summary>
    public static class VRSons
    {
        private const int TailleFile = 10;
        private const int Frequence = 44100;

        private static readonly Dictionary<VRSon, AudioClip> _proceduraux = new Dictionary<VRSon, AudioClip>();
        private static AudioSource[] _sources;
        private static int _prochaine;

        /// <summary>
        /// Joue un son. <paramref name="position"/> null = son 2D (narration, interface) ;
        /// sinon son 3D placé dans le décor.
        /// </summary>
        /// <param name="volume">Multiplicateur (0..1) appliqué au volume de la banque.</param>
        /// <param name="pitch">Hauteur (1 = normale). Une légère variation évite l'effet « mitraillette ».</param>
        public static void Jouer(VRSon son, Vector3? position = null, float volume = 1f, float pitch = 1f)
        {
            VRBanqueSons banque = VRBanqueSons.Instance;
            if (banque != null && !banque.actif) return;

            AudioClip clip = banque != null ? banque.ClipPour(son) : null;
            if (clip == null) clip = Procedural(son);
            if (clip == null) return;

            float volumeFinal = Mathf.Clamp01(volume) * VolumeParDefaut(son)
                                * (banque != null ? banque.volumeGeneral : 1f);
            if (volumeFinal <= 0.001f) return;

            AudioSource source = SourceLibre();
            if (source == null) return;

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.pitch = pitch;
            source.clip = clip;
            source.volume = volumeFinal;
            source.Play();
        }

        /// <summary>Hauteur aléatoire autour de 1 (±<paramref name="ecart"/>).</summary>
        public static float PitchAleatoire(float ecart = 0.06f) => 1f + Random.Range(-ecart, ecart);

        // ------------------------------------------------------------------
        // Pool de sources
        // ------------------------------------------------------------------
        private static AudioSource SourceLibre()
        {
            if (_sources == null || _sources[0] == null)
            {
                GameObject go = new GameObject("VRSons_Auto");
                Object.DontDestroyOnLoad(go);
                _sources = new AudioSource[TailleFile];
                for (int i = 0; i < TailleFile; i++)
                {
                    GameObject enfant = new GameObject("Source_" + i);
                    enfant.transform.SetParent(go.transform, false);
                    AudioSource s = enfant.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Logarithmic;
                    s.minDistance = 0.4f;
                    s.maxDistance = 25f;
                    s.dopplerLevel = 0f;
                    _sources[i] = s;
                }
            }

            // Priorité à une source silencieuse ; sinon on recycle la plus ancienne.
            for (int i = 0; i < TailleFile; i++)
            {
                int index = (_prochaine + i) % TailleFile;
                if (!_sources[index].isPlaying)
                {
                    _prochaine = (index + 1) % TailleFile;
                    return _sources[index];
                }
            }
            AudioSource recyclee = _sources[_prochaine];
            _prochaine = (_prochaine + 1) % TailleFile;
            return recyclee;
        }

        /// <summary>Volume de base de chaque son (équilibrage du mixage).</summary>
        private static float VolumeParDefaut(VRSon son)
        {
            switch (son)
            {
                case VRSon.NarrationLettre: return 0.12f;
                case VRSon.Lacher: return 0.35f;
                case VRSon.Impact: return 0.6f;
                case VRSon.Teleportation: return 0.45f;
                case VRSon.NarrationApparition: return 0.4f;
                default: return 0.5f;
            }
        }

        // ------------------------------------------------------------------
        // Synthèse procédurale
        // ------------------------------------------------------------------
        /// <summary>Quatre notes de 0,12 s qui s'enchaînent, la dernière tenue.</summary>
        private static float Arpege(float t)
        {
            float[] notes = { 523.25f, 659.25f, 784f, 1046.5f };
            int i = Mathf.Min((int)(t / 0.12f), notes.Length - 1);
            float debut = i * 0.12f;
            return Sinus(t, notes[i]) * Decroissance(t - debut, i == notes.Length - 1 ? 3f : 10f);
        }

        private static AudioClip Procedural(VRSon son)
        {
            if (_proceduraux.TryGetValue(son, out AudioClip clip) && clip != null) return clip;

            switch (son)
            {
                // « Tok » feutré : sinus grave qui descend, attaque immédiate.
                case VRSon.Saisie: clip = Synthetiser(son, 0.09f, (t, d) => Sinus(t, Glisse(t, d, 320f, 180f)) * Decroissance(t, 28f)); break;
                // Même famille, plus aigu et plus léger.
                case VRSon.Lacher: clip = Synthetiser(son, 0.07f, (t, d) => Sinus(t, Glisse(t, d, 260f, 420f)) * Decroissance(t, 40f)); break;
                // Souffle : bruit dont l'enveloppe monte puis retombe.
                case VRSon.Teleportation: clip = Synthetiser(son, 0.45f, (t, d) => Bruit() * Cloche(t, d) * 0.6f + Sinus(t, Glisse(t, d, 180f, 520f)) * Cloche(t, d) * 0.25f); break;
                // « Bloup » qui monte (l'objet entre dans la bulle).
                case VRSon.Rangement: clip = Synthetiser(son, 0.22f, (t, d) => Sinus(t, Glisse(t, d, 300f, 900f)) * Decroissance(t, 12f)); break;
                // « Bloup » qui descend (l'objet sort de la bulle).
                case VRSon.Recuperation: clip = Synthetiser(son, 0.22f, (t, d) => Sinus(t, Glisse(t, d, 900f, 300f)) * Decroissance(t, 12f)); break;
                // Deux notes ascendantes (quinte) : « pop » de création.
                case VRSon.Duplication: clip = Synthetiser(son, 0.25f, (t, d) => Sinus(t, t < 0.08f ? 660f : 990f) * Decroissance(t < 0.08f ? t : t - 0.08f, 18f)); break;
                // Choc sec bruité qui s'éteint vite.
                case VRSon.Suppression: clip = Synthetiser(son, 0.2f, (t, d) => (Bruit() * 0.7f + Sinus(t, Glisse(t, d, 220f, 60f))) * Decroissance(t, 20f)); break;
                // Impact : bruit très court + corps grave.
                case VRSon.Impact: clip = Synthetiser(son, 0.12f, (t, d) => (Bruit() * 0.5f + Sinus(t, 140f) * 0.8f) * Decroissance(t, 45f)); break;
                // Carillon doux (deux notes tenues) : la narration commence.
                case VRSon.NarrationApparition: clip = Synthetiser(son, 0.9f, (t, d) => (Sinus(t, 523.25f) * Decroissance(t, 4f) + Sinus(t, 784f, 1) * Decroissance(Mathf.Max(0f, t - 0.12f), 4f) * (t > 0.12f ? 1f : 0f)) * 0.5f); break;
                // Tic de machine à écrire, très bref.
                case VRSon.NarrationLettre: clip = Synthetiser(son, 0.025f, (t, d) => (Bruit() * 0.4f + Sinus(t, 1800f) * 0.6f) * Decroissance(t, 180f)); break;
                // Arpège majeur montant (do-mi-sol-do) : épreuve réussie.
                case VRSon.Reussite: clip = Synthetiser(son, 1.2f, (t, d) => Arpege(t) * 0.5f); break;
            }

            _proceduraux[son] = clip;
            return clip;
        }

        private delegate float Signal(float t, float duree);

        private static AudioClip Synthetiser(VRSon son, float duree, Signal signal)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(duree * Frequence));
            float[] echantillons = new float[n];
            _phases[0] = 0f;
            _phases[1] = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Frequence;
                // Micro-fondu de 3 ms en entrée et en sortie : évite les « clics ».
                float bord = Mathf.Clamp01(Mathf.Min(t, duree - t) / 0.003f);
                echantillons[i] = Mathf.Clamp(signal(t, duree) * bord, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("VRSon_" + son, n, 1, Frequence, false);
            clip.SetData(echantillons, 0);
            return clip;
        }

        // Oscillateur à phase cumulée : une fréquence qui glisse ne « craque » pas.
        // Deux oscillateurs indépendants : un son peut superposer deux notes.
        private static readonly float[] _phases = new float[2];
        private static float Sinus(float t, float frequence, int oscillateur = 0)
        {
            _phases[oscillateur] += 2f * Mathf.PI * frequence / Frequence;
            return Mathf.Sin(_phases[oscillateur]);
        }

        private static float Glisse(float t, float duree, float debut, float fin) => Mathf.Lerp(debut, fin, Mathf.Clamp01(t / duree));
        private static float Decroissance(float t, float vitesse) => Mathf.Exp(-t * vitesse);
        private static float Cloche(float t, float duree) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duree));
        private static float Bruit() => Random.Range(-1f, 1f);
    }
}
