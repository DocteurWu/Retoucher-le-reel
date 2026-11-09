// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  FonduMemoire.cs — LE FONDU AU NOIR PENDANT LE MÉLANGE
//
//  RÔLE
//    Un voile noir collé devant la caméra, dont on anime l'opacité (0 = rien, 1 = noir). Pendant
//    qu'il est noir, le jeu déplace les objets : le joueur ne voit jamais ce changement.
//
//  CRÉÉ PAR : MemoireVisuelle, au besoin (rien à poser dans la scène).
//
//  VR : le voile est un quad à 30 cm des yeux, dessiné en dernier (file 4000). Un fondu court
//    et doux (~0,8 s) reste confortable ; jamais de flash blanc.
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Voile noir devant la caméra, à opacité animée.</summary>
    [AddComponentMenu("")]
    public class FonduMemoire : MonoBehaviour
    {
        private Renderer _rendu;
        private Material _materiau;
        private float _opacite;

        /// <summary>Opacité actuelle (0 à 1).</summary>
        public float Opacite => _opacite;

        /// <summary>Le fondu de la scène (créé sur la caméra principale s'il n'existe pas encore).</summary>
        public static FonduMemoire Obtenir()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;

            FonduMemoire existant = cam.GetComponentInChildren<FonduMemoire>(true);
            if (existant != null) return existant;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Fondu_Memoire";
            Destroy(go.GetComponent<Collider>()); // purement visuel
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            go.transform.localScale = Vector3.one * 2f; // couvre tout le champ de vision à 30 cm
            return go.AddComponent<FonduMemoire>();
        }

        private void Awake()
        {
            _rendu = GetComponent<Renderer>();
            _materiau = VRUtils.CreateUnlitMaterial(new Color(0f, 0f, 0f, 0f));
            _materiau.renderQueue = 4000; // après tout le reste, splats compris
            _rendu.sharedMaterial = _materiau;
            _rendu.shadowCastingMode = ShadowCastingMode.Off;
            _rendu.receiveShadows = false;
            Appliquer(0f);
        }

        private void OnDestroy()
        {
            if (_materiau != null) Destroy(_materiau);
        }

        /// <summary>Fait passer l'opacité à « cible » en « duree » secondes (à utiliser dans une coroutine).</summary>
        public IEnumerator Vers(float cible, float duree)
        {
            float depart = _opacite;
            float t = 0f;
            while (t < duree)
            {
                t += Time.deltaTime;
                Appliquer(Mathf.Lerp(depart, cible, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, duree))));
                yield return null;
            }
            Appliquer(cible);
        }

        /// <summary>Fixe l'opacité tout de suite.</summary>
        public void Appliquer(float opacite)
        {
            _opacite = Mathf.Clamp01(opacite);
            if (_materiau != null) _materiau.color = new Color(0f, 0f, 0f, _opacite);
            if (_rendu != null) _rendu.enabled = _opacite > 0.001f; // rien à dessiner quand c'est transparent
        }
    }
}
