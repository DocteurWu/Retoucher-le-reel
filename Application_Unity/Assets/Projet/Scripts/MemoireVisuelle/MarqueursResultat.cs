// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MarqueursResultat.cs — LES BOÎTES DE L'ÉCRAN DE RÉSULTAT
//
//  RÔLE
//    À la fin d'une partie : une boîte VERTE autour de chaque objet bien placé, ROUGE autour de
//    chaque objet raté, et pour les ratés une boîte BLANCHE « ici » à l'endroit où il devait être.
//    Des arêtes (LineRenderer) plutôt qu'un objet qui brille : un Gaussian Splat n'a pas de
//    matériau à teinter (même raison que Surbrillance).
//
//  CRÉÉ PAR : MemoireVisuelle (rien à poser dans la scène). Effacer() les retire au « Rejouer ».
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Boîtes colorées et étiquettes du résultat.</summary>
    [AddComponentMenu("")]
    public class MarqueursResultat : MonoBehaviour
    {
        // Parcours à travers les 12 arêtes d'un cube (coins numérotés par bits : x = 1, y = 2, z = 4).
        private static readonly int[] Parcours = { 0, 1, 3, 2, 0, 4, 5, 7, 6, 4, 5, 1, 3, 7, 6, 2 };

        private readonly List<GameObject> _marqueurs = new List<GameObject>();
        private readonly List<Material> _materiaux = new List<Material>();
        private readonly List<Transform> _etiquettes = new List<Transform>();

        /// <summary>Ajoute une boîte colorée autour de cette zone.</summary>
        public void AjouterBoite(Bounds zone, Color couleur, string etiquette)
        {
            GameObject go = new GameObject("Marqueur_" + etiquette);
            go.transform.SetParent(transform, false);

            Material materiau = VRUtils.CreateUnlitMaterial(couleur);
            _materiaux.Add(materiau);

            LineRenderer ligne = go.AddComponent<LineRenderer>();
            ligne.useWorldSpace = true;
            ligne.positionCount = Parcours.Length;
            ligne.widthMultiplier = 0.012f;
            ligne.numCapVertices = 2;
            ligne.sharedMaterial = materiau;
            ligne.shadowCastingMode = ShadowCastingMode.Off;
            ligne.receiveShadows = false;

            Vector3 demi = zone.extents + Vector3.one * 0.02f; // petite marge : la boîte ne colle pas à l'objet
            for (int i = 0; i < Parcours.Length; i++)
            {
                int c = Parcours[i];
                ligne.SetPosition(i, zone.center + new Vector3(
                    (c & 1) == 0 ? -demi.x : demi.x,
                    (c & 2) == 0 ? -demi.y : demi.y,
                    (c & 4) == 0 ? -demi.z : demi.z));
            }

            if (!string.IsNullOrEmpty(etiquette))
            {
                GameObject texte = new GameObject("Etiquette");
                texte.transform.SetParent(go.transform, false);
                texte.transform.position = zone.center + Vector3.up * (demi.y + 0.1f);
                TextMeshPro t = texte.AddComponent<TextMeshPro>();
                t.text = etiquette;
                t.fontSize = 1.2f;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.color = couleur;
                t.rectTransform.sizeDelta = new Vector2(0.6f, 0.15f);
                _etiquettes.Add(texte.transform);
            }
            _marqueurs.Add(go);
        }

        /// <summary>Retire tous les marqueurs.</summary>
        public void Effacer()
        {
            foreach (GameObject go in _marqueurs)
            {
                if (go != null) Destroy(go);
            }
            foreach (Material m in _materiaux)
            {
                if (m != null) Destroy(m);
            }
            _marqueurs.Clear();
            _materiaux.Clear();
            _etiquettes.Clear();
        }

        private void LateUpdate()
        {
            // Les étiquettes restent lisibles : tournées vers le joueur (rotation horizontale).
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (Transform e in _etiquettes)
            {
                if (e == null) continue;
                Vector3 regard = e.position - cam.transform.position;
                regard.y = 0f;
                if (regard.sqrMagnitude > 1e-6f) e.rotation = Quaternion.LookRotation(regard, Vector3.up);
            }
        }

        private void OnDestroy()
        {
            foreach (Material m in _materiaux)
            {
                if (m != null) Destroy(m);
            }
        }
    }
}
