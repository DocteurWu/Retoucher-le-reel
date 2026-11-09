// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRBrulure.cs — LA SCÈNE « BRÛLE » JUSQU'À SES GAUSSIENNES (automatique)
//
//  L'EFFET
//    Une onde part d'un foyer et traverse la pièce : chaque splat s'embrase (éclair
//    orange), se réduit à un petit point incandescent, puis les points dérivent vers
//    le haut comme des braises. Le joueur voit que le monde n'est qu'un nuage de
//    Gaussiennes. La RECONSTRUCTION fait l'inverse : une onde cyan ramène chaque
//    point à sa place et la scène redevient photoréaliste.
//    Inspiré de la démo « splat-walk » de Will Eastcott (PlayCanvas).
//
//  COMMENT
//    Tout se passe dans le compute shader du plugin (SplatUtilities.compute, noyau
//    CSCalcViewData, partie « AJOUT Retoucher le réel ») : le plugin a été intégré au
//    projet (Packages/org.nesnausk.gaussian-splatting) pour pouvoir le modifier. Ce
//    composant ne fait qu'avancer l'horloge de l'effet et la donner au plugin
//    (GaussianSplatBrulure), qui l'applique à TOUS les splats : décor et objets.
//    Les mains, l'interface, la bulle de narration ne sont pas des splats : intactes.
//
//  UTILISATION
//    • VRBrulure.Bruler(foyer)                    : la scène brûle (et reste en braises) ;
//    • VRBrulure.Reconstruire(foyer)              : la scène part des braises et se reconstruit ;
//    • VRBrulure.ReconstruireALaProchaineScene()  : la scène chargée ensuite se reconstruit
//      autour du joueur dès son arrivée.
//    Utilisé par ProgressionEtapes : la porte de sortie ouverte en grand fait brûler la
//    scène, puis la scène suivante se reconstruit (« Bruler A La Sortie »).
//
//  Créé automatiquement : rien à poser dans la scène.
// =====================================================================================
using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemodelerLeReel.VR
{
    /// <summary>Anime l'effet « brûlure » des Gaussian splats (voir l'en-tête).</summary>
    [AddComponentMenu("")]
    public class VRBrulure : MonoBehaviour
    {
        /// <summary>Portée de l'onde (m) : au-delà, les splats ne sont pas touchés. Couvre largement une pièce.</summary>
        public const float PorteeParDefaut = 14f;

        /// <summary>Constante de temps du front de brûlure (s) : à 3 m il arrive en ~0,25 s, à 8 m en ~0,85 s.</summary>
        public const float ConstanteBrulure = 1f;

        /// <summary>Constante de temps du front de reconstruction (s).</summary>
        public const float ConstanteReconstruction = 0.6f;

        /// <summary>Reconstruction : temps passé « en braises » avant que l'onde cyan ne parte (s).</summary>
        public const float AttenteReconstruction = 0.6f;

        private static VRBrulure s_instance;
        private static bool s_reconstruireALArrivee;

        private float _tau = -1f;  // secondes depuis le départ ; < 0 : effet coupé
        private float _fin = -1f;  // _tau auquel l'effet s'arrête ; < 0 : jamais (scène laissée en braises)

        /// <summary>Vrai pendant une brûlure ou une reconstruction.</summary>
        public static bool EnCours => s_instance != null && s_instance._tau >= 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            Couper();
            if (s_instance != null) return;
            GameObject go = new GameObject("VRBrulure_Auto");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<VRBrulure>();
            SceneManager.sceneLoaded += SurSceneChargee;
        }

        /// <summary>
        /// La scène brûle depuis <paramref name="foyer"/> (monde). Sans
        /// <paramref name="reconstruireApres"/> (&lt; 0), elle reste en braises : à utiliser
        /// juste avant de charger une autre scène.
        /// </summary>
        public static void Bruler(Vector3 foyer, float reconstruireApres = -1f, float portee = PorteeParDefaut)
        {
            if (s_instance == null) AutoInitialize();
            float delai = reconstruireApres >= 0f ? reconstruireApres : -1f;
            GaussianSplatBrulure.Forme = new Vector4(portee, ConstanteBrulure, delai, ConstanteReconstruction);
            GaussianSplatBrulure.Foyer = new Vector4(foyer.x, foyer.y, foyer.z, 0f);
            s_instance._tau = 0f;
            s_instance._fin = delai >= 0f ? delai + DureeReconstruction(portee) : -1f;
            VRSons.Jouer(VRSon.Brulure, foyer);
            Debug.Log("[Brulure] La scène brûle depuis " + foyer.ToString("F2")
                      + (delai >= 0f ? ", reconstruction après " + delai.ToString("F1") + " s." : ", sans reconstruction."));
        }

        /// <summary>La scène apparaît en braises puis se reconstruit depuis <paramref name="foyer"/> (monde).</summary>
        public static void Reconstruire(Vector3 foyer, float portee = PorteeParDefaut)
        {
            if (s_instance == null) AutoInitialize();
            // On démarre l'horloge assez tard pour que le front de brûlure ait déjà tout
            // réduit en points (jusqu'à ~10 m) : la scène apparaît directement en braises.
            float depart = 2.6f;
            float delai = depart + AttenteReconstruction;
            GaussianSplatBrulure.Forme = new Vector4(portee, ConstanteBrulure, delai, ConstanteReconstruction);
            GaussianSplatBrulure.Foyer = new Vector4(foyer.x, foyer.y, foyer.z, depart);
            s_instance._tau = depart;
            s_instance._fin = delai + DureeReconstruction(portee);
            s_instance.Invoke(nameof(SonReconstruction), AttenteReconstruction);
            Debug.Log("[Brulure] La scène se reconstruit depuis " + foyer.ToString("F2") + ".");
        }

        /// <summary>La prochaine scène chargée se reconstruira autour du joueur.</summary>
        public static void ReconstruireALaProchaineScene()
        {
            if (s_instance == null) AutoInitialize();
            s_reconstruireALArrivee = true;
        }

        /// <summary>Coupe l'effet : tous les splats redeviennent normaux.</summary>
        public static void Couper()
        {
            GaussianSplatBrulure.Foyer = new Vector4(0f, 0f, 0f, -1f);
            if (s_instance != null)
            {
                s_instance._tau = -1f;
                s_instance._fin = -1f;
                s_instance.CancelInvoke();
            }
        }

        /// <summary>Temps pour que l'onde de reconstruction ait tout rendu photoréaliste (s).</summary>
        private static float DureeReconstruction(float portee)
        {
            // sr = tr + T ln(1 - d / (portee + 4)) doit dépasser 1,05 jusqu'à d = portee.
            return 1.05f - ConstanteReconstruction * Mathf.Log(1f - portee / (portee + 4f)) + 0.2f;
        }

        private void SonReconstruction()
        {
            Vector4 f = GaussianSplatBrulure.Foyer;
            VRSons.Jouer(VRSon.Reconstruction, new Vector3(f.x, f.y, f.z));
        }

        private static void SurSceneChargee(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (!s_reconstruireALArrivee)
            {
                Couper();
                return;
            }
            s_reconstruireALArrivee = false;
            // Les objets de la scène sont réveillés (Awake) : la caméra du rig existe déjà.
            Camera cam = Camera.main;
            Reconstruire(cam != null ? cam.transform.position : Vector3.up * 1.6f);
        }

        private void Update()
        {
            if (_tau < 0f) return;
            // Pas de saut d'horloge pendant un chargement (une image longue ne fait pas « sauter » l'effet).
            _tau += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_fin >= 0f && _tau > _fin)
            {
                Couper();
                return;
            }
            Vector4 f = GaussianSplatBrulure.Foyer;
            f.w = _tau;
            GaussianSplatBrulure.Foyer = f;
        }

        private void OnDestroy()
        {
            // L'état est statique (partagé avec l'éditeur) : ne jamais laisser la scène en braises.
            if (s_instance == this)
            {
                SceneManager.sceneLoaded -= SurSceneChargee;
                GaussianSplatBrulure.Foyer = new Vector4(0f, 0f, 0f, -1f);
                s_instance = null;
            }
        }
    }
}
