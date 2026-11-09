// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  Surbrillance.cs — MONTRER AU JOUEUR LES OBJETS DE L'ÉTAPE EN COURS
//
//  RÔLE
//    Autour de chaque collider demandé : une boîte lumineuse qui pulse (arêtes + voile
//    transparent) et, au-dessus, un petit losange qui flotte et tourne pour repérer
//    l'objet de loin. La boîte suit l'objet (porte qui tourne, objet qui tombe…).
//
//  POURQUOI UNE BOÎTE, ET PAS L'OBJET QUI BRILLE ?
//    Un Gaussian Splat n'a pas de matériau à éclairer et le plugin ne sait pas le
//    teinter. La boîte marche pour tout objet, splat ou non. Le splat n'écrivant pas
//    de profondeur et étant dessiné avant les transparents, elle s'affiche par-dessus.
//
//  PILOTÉ PAR : ProgressionEtapes (créé au lancement, rien à poser dans la scène).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Boîtes lumineuses autour d'une liste de colliders.</summary>
    [AddComponentMenu("")]
    public class Surbrillance : MonoBehaviour
    {
        /// <summary>Couleur des boîtes.</summary>
        public Color couleur = new Color(0.3f, 0.9f, 1f);

        /// <summary>Marge autour de l'objet (m) : la boîte ne doit pas se confondre avec lui.</summary>
        public float marge = 0.03f;

        /// <summary>Période de la pulsation (s).</summary>
        public float periode = 1.4f;

        /// <summary>Parcours passant par les 12 arêtes d'un cube (indices des coins, 16 points).</summary>
        private static readonly int[] ParcoursAretes = { 0, 1, 3, 2, 0, 4, 5, 7, 6, 4, 5, 1, 3, 7, 6, 2 };

        private class Repere
        {
            public GameObject racine;
            public LineRenderer aretes;
            public Transform voile, losange;
            public Material matVoile, matLosange;
        }

        private readonly List<Repere> _reperes = new List<Repere>();
        private readonly List<Collider> _cibles = new List<Collider>();
        private readonly Vector3[] _coins = new Vector3[8];
        private readonly Vector3[] _chemin = new Vector3[16];
        private Material _matAretes;

        /// <summary>Remplace la liste des objets montrés (liste vide = plus rien).</summary>
        public void Montrer(List<Collider> cibles)
        {
            _cibles.Clear();
            if (cibles != null) _cibles.AddRange(cibles);
        }

        private void LateUpdate()
        {
            // LateUpdate : après le déplacement des objets tenus, de la porte, etc.
            float pulsation = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, periode));
            int n = 0;
            for (int i = 0; i < _cibles.Count; i++)
            {
                Collider c = _cibles[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                Placer(Obtenir(n++), c, pulsation);
            }
            for (int i = n; i < _reperes.Count; i++)
            {
                if (_reperes[i].racine.activeSelf) _reperes[i].racine.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            foreach (Repere r in _reperes)
            {
                Destroy(r.matVoile);
                Destroy(r.matLosange);
            }
            if (_matAretes != null) Destroy(_matAretes);
        }

        // ------------------------------------------------------------------
        // Placement
        // ------------------------------------------------------------------
        private void Placer(Repere r, Collider c, float pulsation)
        {
            if (!r.racine.activeSelf) r.racine.SetActive(true);

            // Boîte exacte (donc tournée) pour un BoxCollider ; boîte droite englobante sinon.
            Vector3 centre, taille;
            Quaternion rotation;
            if (c is BoxCollider b)
            {
                Transform t = b.transform;
                centre = t.TransformPoint(b.center);
                rotation = t.rotation;
                Vector3 e = t.lossyScale;
                taille = new Vector3(Mathf.Abs(b.size.x * e.x), Mathf.Abs(b.size.y * e.y), Mathf.Abs(b.size.z * e.z));
            }
            else
            {
                Bounds bornes = c.bounds;
                centre = bornes.center;
                rotation = Quaternion.identity;
                taille = bornes.size;
            }
            taille += Vector3.one * (marge * 2f);

            Vector3 demi = taille * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = new Vector3((i & 1) == 0 ? -demi.x : demi.x, (i & 2) == 0 ? -demi.y : demi.y, (i & 4) == 0 ? -demi.z : demi.z);
                _coins[i] = centre + rotation * local;
            }
            for (int i = 0; i < _chemin.Length; i++) _chemin[i] = _coins[ParcoursAretes[i]];
            r.aretes.SetPositions(_chemin);

            Color a = couleur;
            a.a = Mathf.Lerp(0.55f, 1f, pulsation);
            r.aretes.startColor = r.aretes.endColor = a;

            r.voile.SetPositionAndRotation(centre, rotation);
            r.voile.localScale = taille;
            Color v = couleur;
            v.a = Mathf.Lerp(0.04f, 0.2f, pulsation);
            r.matVoile.color = v;

            // Losange : au-dessus du point le plus haut de la boîte, flotte et tourne.
            float haut = float.MinValue;
            for (int i = 0; i < 8; i++) haut = Mathf.Max(haut, _coins[i].y);
            r.losange.position = new Vector3(centre.x, haut + 0.13f + 0.025f * Mathf.Sin(Time.time * 2.5f), centre.z);
            r.losange.rotation = Quaternion.Euler(45f, Time.time * 90f, 45f);
            Color l = couleur;
            l.a = 1f;
            r.matLosange.color = l;
        }

        // ------------------------------------------------------------------
        // Pool de repères (créés à la demande, jamais détruits pendant le niveau)
        // ------------------------------------------------------------------
        private Repere Obtenir(int index)
        {
            while (_reperes.Count <= index) _reperes.Add(Creer());
            return _reperes[index];
        }

        private Repere Creer()
        {
            if (_matAretes == null) _matAretes = VRUtils.CreateUnlitMaterial(Color.white);

            Repere r = new Repere { racine = new GameObject("Repere_Surbrillance") };
            r.racine.transform.SetParent(transform, false);

            r.aretes = r.racine.AddComponent<LineRenderer>();
            r.aretes.useWorldSpace = true;
            r.aretes.positionCount = _chemin.Length;
            r.aretes.widthMultiplier = 0.007f;
            r.aretes.numCapVertices = 2;
            r.aretes.sharedMaterial = _matAretes;
            r.aretes.shadowCastingMode = ShadowCastingMode.Off;
            r.aretes.receiveShadows = false;

            r.matVoile = VRUtils.CreateUnlitMaterial(couleur);
            r.voile = Cube(r.racine.transform, "Voile", r.matVoile);

            r.matLosange = VRUtils.CreateUnlitMaterial(couleur);
            r.losange = Cube(r.racine.transform, "Losange", r.matLosange);
            r.losange.localScale = Vector3.one * 0.05f;
            return r;
        }

        private static Transform Cube(Transform parent, string nom, Material materiau)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nom;
            Destroy(go.GetComponent<Collider>()); // purement visuel : ni saisie ni blocage
            go.transform.SetParent(parent, false);
            Renderer rendu = go.GetComponent<Renderer>();
            rendu.sharedMaterial = materiau;
            rendu.shadowCastingMode = ShadowCastingMode.Off;
            rendu.receiveShadows = false;
            return go.transform;
        }
    }
}
