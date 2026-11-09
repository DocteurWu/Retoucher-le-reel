// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  AllegerSplat.cs — VERSION ALLÉGÉE D'UN SPLAT (performances VR)
//
//  Menu : Retoucher le Reel > Outils > Alléger un splat (performances VR)
//
//  POURQUOI
//    Le plugin d'aras-p trie et projette CHAQUE splat pour CHAQUE œil (rendu Multi-pass) :
//    le coût suit le nombre de splats. Le décor de la Grange (Scene3) en a 4,4 millions,
//    bien plus que le casque n'en tient à 72-90 images/s.
//
//  CE QUE FAIT L'OUTIL
//    1. Décode l'asset Gaussian sélectionné sur le CPU, directement depuis ses fichiers
//       .bytes (pas d'export GPU : 1 Go de tampon faisait tomber les petites machines).
//    2. Classe les splats par IMPORTANCE = opacité × surface vue (deux plus grands axes, au
//       moins un pixel chacun) : les splats minuscules et presque transparents ne se voient
//       pas, ce sont eux qu'on retire. (Le volume, essayé d'abord, effaçait les objets fins.)
//    3. Écrit un PLY temporaire (dossier Temp du projet) avec la fraction gardée, SH limités
//       à l'ordre choisi, puis le fait importer par le créateur d'assets du plugin (qualité
//       Medium, comme les assets existants) : « <nom>_leger.asset », à côté de l'original.
//    4. Branche le nouvel asset sur le renderer (Ctrl+Z possible) et marque la scène modifiée.
//  L'asset d'origine n'est jamais modifié : remettre l'ancien asset dans le champ « Asset »
//  du GaussianSplatRenderer suffit pour revenir en arrière.
//
//  FORMATS GÉRÉS : ceux du préréglage Medium (position/échelle Norm11, couleur Norm8x4,
//  SH Norm6), utilisés par tous les splats du projet. Les autres sont refusés.
// =====================================================================================
using System;
using System.IO;
using System.Reflection;
using System.Text;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RemodelerLeReel.EditorTools
{
    public class AllegerSplat : EditorWindow
    {
        [MenuItem("Retoucher le Reel/Outils/Alléger un splat (performances VR)")]
        private static void Ouvrir()
        {
            var w = GetWindow<AllegerSplat>(true, "Alléger un splat");
            if (Selection.activeGameObject != null)
                w._cible = Selection.activeGameObject.GetComponentInChildren<GaussianSplatRenderer>();
            w.minSize = new Vector2(420, 200);
        }

        private GaussianSplatRenderer _cible;
        private float _garder = 0.6f;
        private int _ordreSH = 1;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Crée une copie du splat sans ses splats les moins visibles (petits et transparents), " +
                "puis la branche sur le renderer. L'asset d'origine est conservé.", MessageType.Info);
            _cible = (GaussianSplatRenderer)EditorGUILayout.ObjectField("Splat", _cible, typeof(GaussianSplatRenderer), true);
            _garder = EditorGUILayout.Slider("Fraction gardée", _garder, 0.1f, 1f);
            _ordreSH = EditorGUILayout.IntSlider("Ordre SH gardé", _ordreSH, 0, 3);
            if (_cible != null && _cible.m_Asset != null)
            {
                int n = _cible.m_Asset.splatCount;
                EditorGUILayout.LabelField("Splats", n.ToString("N0") + " → " + Mathf.RoundToInt(n * _garder).ToString("N0"));
            }
            using (new EditorGUI.DisabledScope(_cible == null || _cible.m_Asset == null))
            {
                if (GUILayout.Button("Créer la version allégée et l'utiliser"))
                {
                    try { Alleger(_cible, _garder, _ordreSH); }
                    finally { EditorUtility.ClearProgressBar(); }
                }
            }
        }

        // ------------------------------------------------------------------
        // Traitement
        // ------------------------------------------------------------------
        private const float SH_C0 = 0.2820948f;

        public static void Alleger(GaussianSplatRenderer cible, float garder, int ordreSH)
        {
            GaussianSplatAsset a = cible.m_Asset;
            if (a.posFormat != GaussianSplatAsset.VectorFormat.Norm11 || a.scaleFormat != GaussianSplatAsset.VectorFormat.Norm11 ||
                a.colorFormat != GaussianSplatAsset.ColorFormat.Norm8x4 || a.shFormat != GaussianSplatAsset.SHFormat.Norm6 ||
                a.chunkData == null)
            {
                EditorUtility.DisplayDialog("Alléger un splat", "Format non géré : seul le préréglage Medium (Norm11 / Norm8x4 / Norm6, avec chunks) l'est.", "OK");
                return;
            }

            var d = new Decodeur(a);
            int n = a.splatCount;
            int garde = Mathf.Clamp(Mathf.RoundToInt(n * garder), 1, n);

            // 1. Importance de chaque splat = opacité × surface vue (deux plus grands axes), chaque
            //    axe compté au moins comme un pixel à 2 m (≈ 2 mm) : un splat long et fin (cordon,
            //    tige de lampe) garde le poids d'une ligne visible, ce que le volume ne donnait pas.
            Vector3 echelleMonde = cible.transform.lossyScale;
            float pixel = 0.002f / Mathf.Max(1e-6f, Mathf.Max(Mathf.Abs(echelleMonde.x), Mathf.Abs(echelleMonde.y), Mathf.Abs(echelleMonde.z)));
            var importance = new float[n];
            for (int i = 0; i < n; i++)
            {
                if ((i & 0xFFFFF) == 0) EditorUtility.DisplayProgressBar("Alléger un splat", "Mesure de l'importance des splats", 0.3f * i / n);
                importance[i] = Importance(d.Opacite(i), d.Echelle(i), pixel);
            }
            var tri = (float[])importance.Clone();
            Array.Sort(tri);
            float seuil = tri[n - garde];
            tri = null;

            // 2. PLY temporaire (repère local de l'asset : le Transform du renderer ne change pas).
            string nom = a.name + "_leger";
            string dossierTemp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/AllegerSplat"));
            Directory.CreateDirectory(dossierTemp);
            string ply = Path.Combine(dossierTemp, nom + ".ply");
            int ecrits = EcrirePly(ply, d, importance, seuil, garde, ordreSH);
            importance = null;

            // 3. Import par le créateur du plugin, à côté de l'asset d'origine.
            string dossier = Path.GetDirectoryName(AssetDatabase.GetAssetPath(a)).Replace('\\', '/');
            EditorUtility.DisplayProgressBar("Alléger un splat", "Import par le plugin (peut prendre une minute)", 0.6f);
            string erreur = Importer(ply, dossier);
            try { File.Delete(ply); } catch (IOException) { }
            if (erreur != null)
            {
                EditorUtility.DisplayDialog("Alléger un splat", "Échec de l'import : " + erreur, "OK");
                return;
            }

            var nouvel = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(dossier + "/" + nom + ".asset");
            if (nouvel == null)
            {
                EditorUtility.DisplayDialog("Alléger un splat", "Asset introuvable après import : " + dossier + "/" + nom + ".asset", "OK");
                return;
            }

            // 4. Branchement sur le renderer.
            Undo.RecordObject(cible, "Alléger un splat");
            cible.m_Asset = nouvel;
            if (cible.m_SHOrder > ordreSH) cible.m_SHOrder = ordreSH;
            EditorUtility.SetDirty(cible);
            if (cible.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(cible.gameObject.scene);
            Debug.Log("[AllegerSplat] « " + a.name + " » : " + n.ToString("N0") + " → " + ecrits.ToString("N0") +
                      " splats (" + (100f * ecrits / n).ToString("F0") + " %), SH ordre " + ordreSH + ". Nouvel asset : " + dossier + "/" + nom + ".asset", nouvel);
        }

        /// <summary>
        /// Poids d'un splat à l'écran : opacité × produit des deux plus grands axes, chacun au moins
        /// égal à <paramref name="pixel"/>. (Fonction pure.)
        /// </summary>
        public static float Importance(float opacite, Vector3 echelle, float pixel)
        {
            float a = echelle.x, b = echelle.y, c = echelle.z;
            float grand = Mathf.Max(a, Mathf.Max(b, c));
            float petit = Mathf.Min(a, Mathf.Min(b, c));
            float moyen = a + b + c - grand - petit;
            return opacite * Mathf.Max(grand, pixel) * Mathf.Max(moyen, pixel);
        }

        /// <summary>
        /// PLY binaire au format 3DGS. Les coefficients SH gardés portent les noms qu'attend le
        /// lecteur du plugin (f_rest_j pour le rouge, f_rest_15+j vert, f_rest_30+j bleu), même
        /// quand on n'en garde qu'une partie.
        /// </summary>
        private static int EcrirePly(string chemin, Decodeur d, float[] importance, float seuil, int garde, int ordreSH)
        {
            int nbSH = ordreSH <= 0 ? 0 : ordreSH == 1 ? 3 : ordreSH == 2 ? 8 : 15;
            int n = importance.Length;

            // Nombre exact écrit (les ex aequo au seuil sont départagés dans l'ordre des indices).
            int auDessus = 0;
            for (int i = 0; i < n; i++) if (importance[i] > seuil) auDessus++;
            int egauxPermis = garde - auDessus;

            var h = new StringBuilder("ply\nformat binary_little_endian 1.0\nelement vertex ").Append(garde).Append('\n');
            foreach (string p in new[] { "x", "y", "z", "f_dc_0", "f_dc_1", "f_dc_2" }) h.Append("property float ").Append(p).Append('\n');
            for (int c = 0; c < 3; c++)
                for (int j = 0; j < nbSH; j++) h.Append("property float f_rest_").Append(c * 15 + j).Append('\n');
            foreach (string p in new[] { "opacity", "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3" }) h.Append("property float ").Append(p).Append('\n');
            h.Append("end_header\n");

            int ecrits = 0;
            var sh = new Vector3[15];
            using (var fs = new FileStream(chemin, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Encoding.ASCII.GetBytes(h.ToString()));
                for (int i = 0; i < n && ecrits < garde; i++)
                {
                    float imp = importance[i];
                    if (imp < seuil) continue;
                    if (imp == seuil) { if (egauxPermis <= 0) continue; egauxPermis--; }
                    if ((ecrits & 0x3FFFF) == 0) EditorUtility.DisplayProgressBar("Alléger un splat", "Écriture du PLY", 0.3f + 0.3f * i / n);

                    Vector3 p = d.Position(i);
                    Vector4 col = d.Couleur(i); // rgb = couleur, a = opacité linéaire
                    Vector3 s = d.Echelle(i);
                    Vector4 q = d.Rotation(i);  // (x, y, z, w)
                    d.SH(i, sh, nbSH);

                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write((col.x - 0.5f) / SH_C0); w.Write((col.y - 0.5f) / SH_C0); w.Write((col.z - 0.5f) / SH_C0);
                    for (int j = 0; j < nbSH; j++) w.Write(sh[j].x);
                    for (int j = 0; j < nbSH; j++) w.Write(sh[j].y);
                    for (int j = 0; j < nbSH; j++) w.Write(sh[j].z);
                    float op = Mathf.Clamp(col.w, 1e-5f, 1f - 1e-5f);
                    w.Write(Mathf.Log(op / (1f - op)));
                    w.Write(Mathf.Log(Mathf.Max(s.x, 1e-12f))); w.Write(Mathf.Log(Mathf.Max(s.y, 1e-12f))); w.Write(Mathf.Log(Mathf.Max(s.z, 1e-12f)));
                    w.Write(q.w); w.Write(q.x); w.Write(q.y); w.Write(q.z); // convention PLY : rot_0 = w
                    ecrits++;
                }
            }
            return ecrits;
        }

        /// <summary>Lance le créateur d'assets du plugin (fenêtre « Create Gaussian Splat ») sans l'ouvrir. Renvoie l'erreur ou null.</summary>
        private static string Importer(string ply, string dossier)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Type t = typeof(GaussianSplatting.Editor.GaussianSplatAssetCreator);
            var createur = CreateInstance<GaussianSplatting.Editor.GaussianSplatAssetCreator>();
            try
            {
                FieldInfo qualite = t.GetField("m_Quality", F);
                t.GetField("m_InputFile", F).SetValue(createur, ply);
                t.GetField("m_OutputFolder", F).SetValue(createur, dossier);
                t.GetField("m_ImportCameras", F).SetValue(createur, false);
                qualite.SetValue(createur, Enum.Parse(qualite.FieldType, "Medium"));
                t.GetMethod("ApplyQualityLevel", F).Invoke(createur, null);
                t.GetMethod("CreateAsset", F).Invoke(createur, null);
                return (string)t.GetField("m_ErrorMessage", F).GetValue(createur);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return (e.InnerException ?? e).Message;
            }
            finally { DestroyImmediate(createur); }
        }

        // ------------------------------------------------------------------
        // Décodage (miroir CPU de LoadSplatData dans GaussianSplatting.hlsl)
        // ------------------------------------------------------------------
        private sealed class Decodeur
        {
            private readonly byte[] _chk, _pos, _oth, _col, _sh;
            private const int TailleChunk = 64;  // SplatChunkInfo : 16 uint
            private const int LargeurTexture = 2048;

            public Decodeur(GaussianSplatAsset a)
            {
                _chk = a.chunkData.bytes;
                _pos = a.posData.bytes;
                _oth = a.otherData.bytes;
                _col = a.colorData.bytes;
                _sh = a.shData.bytes;
            }

            private uint U(byte[] b, int o) => BitConverter.ToUInt32(b, o);
            private float F(byte[] b, int o) => BitConverter.ToSingle(b, o);
            private static float H0(uint u) => Mathf.HalfToFloat((ushort)(u & 0xFFFF));
            private static float H1(uint u) => Mathf.HalfToFloat((ushort)(u >> 16));
            private static Vector3 Norm11(uint v) => new Vector3((v & 2047) / 2047f, ((v >> 11) & 1023) / 1023f, ((v >> 21) & 2047) / 2047f);
            private static int Chunk(int i) => (i / 256) * TailleChunk;

            public Vector3 Position(int i)
            {
                int c = Chunk(i);
                Vector3 v = Norm11(U(_pos, i * 4));
                return new Vector3(Mathf.Lerp(F(_chk, c + 16), F(_chk, c + 20), v.x),
                                   Mathf.Lerp(F(_chk, c + 24), F(_chk, c + 28), v.y),
                                   Mathf.Lerp(F(_chk, c + 32), F(_chk, c + 36), v.z));
            }

            public Vector3 Echelle(int i)
            {
                int c = Chunk(i);
                Vector3 v = Norm11(U(_oth, i * 8 + 4));
                uint x = U(_chk, c + 40), y = U(_chk, c + 44), z = U(_chk, c + 48);
                Vector3 s = new Vector3(Mathf.Lerp(H0(x), H1(x), v.x), Mathf.Lerp(H0(y), H1(y), v.y), Mathf.Lerp(H0(z), H1(z), v.z));
                s = Vector3.Scale(s, s); s = Vector3.Scale(s, s); s = Vector3.Scale(s, s); // stockée en racine 8e
                return s;
            }

            public Vector4 Rotation(int i)
            {
                uint e = U(_oth, i * 8);
                Vector4 pq = new Vector4((e & 1023) / 1023f, ((e >> 10) & 1023) / 1023f, ((e >> 20) & 1023) / 1023f, ((e >> 30) & 3) / 3f);
                int idx = Mathf.RoundToInt(pq.w * 3f);
                const float r2 = 1.41421356f;
                Vector3 xyz = new Vector3(pq.x * r2 - 1f / r2, pq.y * r2 - 1f / r2, pq.z * r2 - 1f / r2);
                float w = Mathf.Sqrt(1f - Mathf.Clamp01(Vector3.Dot(xyz, xyz)));
                Vector4 q = new Vector4(xyz.x, xyz.y, xyz.z, w);
                if (idx == 0) q = new Vector4(q.w, q.x, q.y, q.z);
                else if (idx == 1) q = new Vector4(q.x, q.w, q.y, q.z);
                else if (idx == 2) q = new Vector4(q.x, q.y, q.w, q.z);
                return q;
            }

            public Vector4 Couleur(int i)
            {
                // Texture 2048 de large, tuiles de 16×16 en ordre de Morton (SplatIndexToPixelIndex).
                uint t = (uint)(i & 255);
                t = (t & 0xFF) | ((t & 0xFE) << 7);
                t &= 0x5555;
                t = (t ^ (t >> 1)) & 0x3333;
                t = (t ^ (t >> 2)) & 0x0f0f;
                int bloc = i >> 8, largeur = LargeurTexture / 16;
                int px = (bloc % largeur) * 16 + (int)(t & 0xF);
                int py = (bloc / largeur) * 16 + (int)(t >> 8);
                int o = (py * LargeurTexture + px) * 4;
                int c = Chunk(i);
                uint r = U(_chk, c), g = U(_chk, c + 4), b = U(_chk, c + 8), al = U(_chk, c + 12);
                float a = Mathf.Lerp(H0(al), H1(al), _col[o + 3] / 255f);
                a = (a - 0.5f) * 0.5f;
                a = Mathf.Sqrt(Mathf.Abs(a)) * Mathf.Sign(a) + 0.5f; // InvSquareCentered01
                return new Vector4(Mathf.Lerp(H0(r), H1(r), _col[o] / 255f),
                                   Mathf.Lerp(H0(g), H1(g), _col[o + 1] / 255f),
                                   Mathf.Lerp(H0(b), H1(b), _col[o + 2] / 255f), a);
            }

            public float Opacite(int i) => Couleur(i).w;

            /// <summary>Les <paramref name="nb"/> premiers coefficients SH (Norm6 : 5.6.5 bits, relatifs au chunk).</summary>
            public void SH(int i, Vector3[] sortie, int nb)
            {
                if (nb == 0) return;
                int c = Chunk(i);
                uint r = U(_chk, c + 52), g = U(_chk, c + 56), b = U(_chk, c + 60);
                for (int k = 0; k < nb; k++)
                {
                    uint e = BitConverter.ToUInt16(_sh, i * 32 + k * 2);
                    sortie[k] = new Vector3(Mathf.Lerp(H0(r), H1(r), (e & 31) / 31f),
                                            Mathf.Lerp(H0(g), H1(g), ((e >> 5) & 63) / 63f),
                                            Mathf.Lerp(H0(b), H1(b), ((e >> 11) & 31) / 31f));
                }
            }
        }
    }
}
