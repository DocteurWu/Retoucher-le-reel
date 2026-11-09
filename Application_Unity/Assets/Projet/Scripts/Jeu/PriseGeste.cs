// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  PriseGeste.cs — UN GESTE ENREGISTRÉ, PRÊT À ÊTRE REJOUÉ PAR LES MAINS FANTÔMES
//
//  RÔLE
//    Charge une prise de EnregistreurGestes (JSON) et donne, pour n'importe quel instant,
//    la pose des 26 articulations de chaque main (interpolée entre deux images). Les mains
//    fantômes du tutoriel la rejouent en boucle à la place des poses calculées.
//
//  OÙ : Assets/Projet/Resources/Gestes/<nom>.json (un TextAsset par geste, inclus dans le
//    build). Les prises brutes sont dans Application_Unity/Enregistrements_Gestes/ ; les
//    retenues y sont copiées, recadrées sur le mouvement et débarrassées des sauts du rig
//    (téléportation pendant la prise).
//
//  REPÈRE : celui de la tête au début de la prise (x droite, y haut, z devant).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Geste enregistré : poses des articulations des deux mains au fil du temps.</summary>
    public class PriseGeste
    {
        private const string DossierResources = "Gestes/";

        private static readonly Dictionary<string, PriseGeste> s_cache = new Dictionary<string, PriseGeste>();

        private readonly EnregistreurGestes.Image[] _images;
        private readonly float _frequence;
        private readonly int _articulations;

        /// <summary>Durée de la prise (s).</summary>
        public float Duree { get; }

        private PriseGeste(EnregistreurGestes.Prise prise)
        {
            _images = prise.images.ToArray();
            _frequence = Mathf.Max(1f, prise.frequence);
            _articulations = prise.articulations != null ? prise.articulations.Length : 0;
            Duree = (_images.Length - 1) / _frequence;
        }

        /// <summary>La prise de ce nom (Resources/Gestes/<nom>), null s'il n'y en a pas.</summary>
        public static PriseGeste Charger(string nom)
        {
            if (string.IsNullOrEmpty(nom)) return null;
            if (s_cache.TryGetValue(nom, out PriseGeste dejaLue)) return dejaLue;

            PriseGeste prise = null;
            TextAsset texte = Resources.Load<TextAsset>(DossierResources + nom);
            if (texte != null)
            {
                EnregistreurGestes.Prise donnees = JsonUtility.FromJson<EnregistreurGestes.Prise>(texte.text);
                if (donnees != null && donnees.images != null && donnees.images.Count > 1) prise = new PriseGeste(donnees);
                else Debug.LogWarning("[Tutoriel] Prise « " + nom + " » vide ou illisible.");
            }
            s_cache[nom] = prise;
            return prise;
        }

        /// <summary>
        /// Pose de chaque articulation d'une main à l'instant <paramref name="t"/> (dans le repère
        /// de la prise). Faux si la main n'était pas suivie à cet instant.
        /// </summary>
        public bool Echantillonner(float t, bool gauche, Pose[] sortie)
        {
            float position = Mathf.Clamp(t, 0f, Duree) * _frequence;
            int i0 = Mathf.Clamp(Mathf.FloorToInt(position), 0, _images.Length - 1);
            int i1 = Mathf.Min(i0 + 1, _images.Length - 1);
            float k = position - i0;

            float[] a = gauche ? _images[i0].gauche : _images[i0].droite;
            float[] b = gauche ? _images[i1].gauche : _images[i1].droite;
            bool aValide = a != null && a.Length >= _articulations * 7 && _articulations > 0;
            bool bValide = b != null && b.Length >= _articulations * 7 && _articulations > 0;
            if (!aValide && !bValide) return false;
            if (!aValide) { a = b; k = 0f; }
            if (!bValide) { b = a; k = 0f; }

            int n = Mathf.Min(_articulations, sortie.Length);
            for (int j = 0; j < n; j++)
            {
                int o = j * 7;
                Vector3 pa = new Vector3(a[o], a[o + 1], a[o + 2]);
                Vector3 pb = new Vector3(b[o], b[o + 1], b[o + 2]);
                Quaternion ra = Normaliser(a[o + 3], a[o + 4], a[o + 5], a[o + 6]);
                Quaternion rb = Normaliser(b[o + 3], b[o + 4], b[o + 5], b[o + 6]);
                sortie[j] = new Pose(Vector3.Lerp(pa, pb, k), Quaternion.Slerp(ra, rb, k));
            }
            return true;
        }

        /// <summary>Quaternion lu dans le fichier (une articulation absente vaut 0, 0, 0, 0 : identité).</summary>
        private static Quaternion Normaliser(float x, float y, float z, float w)
        {
            float n = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            return n < 1e-6f ? Quaternion.identity : new Quaternion(x / n, y / n, z / n, w / n);
        }
    }
}
