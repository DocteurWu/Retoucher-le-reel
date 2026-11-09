// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VROrdreSplats.cs — ORDRE D'AFFICHAGE DES GAUSSIAN SPLATS (automatique)
//
//  LE PROBLÈME
//    Les splats ne s'occultent pas entre eux : le plugin les compose l'un après
//    l'autre, et il les classe selon la distance caméra → ORIGINE de leur Transform.
//    Or un objet segmenté (SAGA) garde le repère de la scène d'origine : son origine
//    est au centre de la pièce, pas sur l'objet. Dès qu'elle passe derrière celle du
//    décor, le décor recouvre l'objet, qui disparaît, même tenu en main.
//
//  LA SOLUTION
//    m_RenderOrder passe avant la distance dans le tri du plugin (plus grand = devant).
//    À chaque frame :
//      • le décor reste à 0, donc derrière tous les objets ;
//      • les objets sont classés selon la distance au CENTRE RÉEL de leurs splats
//        (le plus proche devant).
//    Conséquence acceptée : un objet posé derrière un élément du décor (scan) reste
//    visible à travers lui, car le décor forme un seul bloc de splats.
//
//  DÉCOR = splat à la racine de la scène, sans Rigidbody. Tout le reste est un objet :
//    un décor rangé sous un objet parent serait donc classé comme un objet.
//
//  Créé automatiquement : rien à poser dans la scène.
// =====================================================================================
using System.Collections.Generic;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>Garde le décor derrière les objets et classe les objets par distance réelle.</summary>
    [AddComponentMenu("")]
    public class VROrdreSplats : MonoBehaviour
    {
        /// <summary>Repère de la version du code : affiché par les commandes de démo et dans le journal.</summary>
        public const string VersionCode = "2026-11-06 revue PC VR + signe V";

        private static VROrdreSplats s_instance;

        /// <summary>Période de recherche des splats (s) : les clones héritent de l'ordre de leur source en attendant.</summary>
        private const float PeriodeRecherche = 0.5f;

        private readonly List<GaussianSplatRenderer> _decors = new List<GaussianSplatRenderer>();
        private readonly List<GaussianSplatRenderer> _objets = new List<GaussianSplatRenderer>();
        private readonly List<KeyValuePair<float, GaussianSplatRenderer>> _classement = new List<KeyValuePair<float, GaussianSplatRenderer>>();
        private readonly System.Comparison<KeyValuePair<float, GaussianSplatRenderer>> _parDistance = (a, b) => a.Key.CompareTo(b.Key);
        private float _prochaineRecherche;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            GameObject go = new GameObject("VROrdreSplats_Auto");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<VROrdreSplats>();
            Debug.Log("[Splats] Version du code : " + VersionCode);
        }

        /// <summary>
        /// DIAGNOSTIC : écrit dans le journal l'état de tous les splats (ordre, position réelle
        /// du centre, actif ou non) et de la caméra. Pour comprendre un objet invisible.
        /// </summary>
        public static void Journaliser(string contexte)
        {
            Camera cam = Camera.main;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("[Splats] ").Append(contexte)
                .Append(" | version ").Append(VersionCode)
                .Append(" | tri actif : ").Append(s_instance != null && s_instance.isActiveAndEnabled)
                .Append(" | camera : ").Append(cam != null ? cam.transform.position.ToString("F2") : "aucune");
            foreach (GaussianSplatRenderer r in FindObjectsByType<GaussianSplatRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.Append("\n  ").Append(r.name)
                  .Append(" | ordre ").Append(r.m_RenderOrder)
                  .Append(" | actif ").Append(r.isActiveAndEnabled)
                  .Append(" | asset ").Append(r.m_Asset != null ? r.m_Asset.name : "AUCUN")
                  .Append(" | valide ").Append(r.HasValidAsset && r.HasValidRenderSetup)
                  .Append(" | echelle ").Append(r.transform.lossyScale.ToString("F2"));
                if (r.m_Asset != null)
                {
                    sb.Append(" | centre ").Append(r.transform.TransformPoint((r.m_Asset.boundsMin + r.m_Asset.boundsMax) * 0.5f).ToString("F2"));
                }
            }
            Debug.Log(sb.ToString());
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _prochaineRecherche)
            {
                _prochaineRecherche = Time.unscaledTime + PeriodeRecherche;
                Rechercher();
            }

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 oeil = cam.transform.position;

            for (int i = 0; i < _decors.Count; i++)
            {
                if (_decors[i] != null) _decors[i].m_RenderOrder = 0;
            }

            _classement.Clear();
            for (int i = 0; i < _objets.Count; i++)
            {
                GaussianSplatRenderer r = _objets[i];
                if (r == null || r.m_Asset == null || !r.isActiveAndEnabled) continue;
                Vector3 centre = r.transform.TransformPoint((r.m_Asset.boundsMin + r.m_Asset.boundsMax) * 0.5f);
                _classement.Add(new KeyValuePair<float, GaussianSplatRenderer>((centre - oeil).sqrMagnitude, r));
            }
            _classement.Sort(_parDistance);

            // Le plus proche reçoit le plus grand ordre ; le plus loin reste devant le décor (≥ 1).
            for (int i = 0; i < _classement.Count; i++)
            {
                _classement[i].Value.m_RenderOrder = _classement.Count - i;
            }
        }

        private void Rechercher()
        {
            _decors.Clear();
            _objets.Clear();
            foreach (GaussianSplatRenderer r in FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None))
            {
                bool decor = r.transform.parent == null && !r.TryGetComponent(out Rigidbody _);
                (decor ? _decors : _objets).Add(r);
                if (decor && r.m_Asset != null && r.m_Asset.splatCount > 500000)
                {
                    if (r.m_SortNthFrame < 3) r.m_SortNthFrame = 3;
                    if (r.m_SHOrder > 1) r.m_SHOrder = 1;
                }
                else if (r.m_SortNthFrame < 2)
                {
                    r.m_SortNthFrame = 2;
                }
            }
        }
    }
}
