// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRPerfLogs.cs — LOGS DE PERFORMANCE LIVE ET DIAGNOSTIC VR (automatique)
//
//  RÔLE
//    Mesure et affiche en direct les métriques clés pour diagnostiquer les saccades :
//      • FPS et temps de frame (ms) avec code couleur (Vert > 68, Jaune 50-68, Rouge < 50) ;
//      • Nombre de splats Gaussiennes actifs et mode de tri (m_SortNthFrame) ;
//      • Résolution de rendu de l'œil XR (OpenXR) ;
//      • État du suivi de la tête et des mains (XR Hands).
//    Écrit dans la console Unity ([PerfVR]) toutes les 2 secondes et affiche un bandeau
//    dans la vue Game (PC) pour suivre les performances pendant le jeu au casque.
//
//  Créé automatiquement à chaque scène : rien à poser dans la scène.
//  Touche F3 : masquer / afficher le panneau de diagnostic à l'écran.
// =====================================================================================
using System.Text;
using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace RemodelerLeReel.VR
{
    /// <summary>Surveillance live des FPS, du temps de rendu et des ressources VR.</summary>
    [AddComponentMenu("")]
    public class VRPerfLogs : MonoBehaviour
    {
        private static VRPerfLogs s_instance;

        private float _tempsDernierLog;
        private float _fpsLisse = 72f;
        private float _msLisse = 13.8f;
        private int _totalSplats = 0;
        private int _nbDecors = 0;
        private int _nbObjets = 0;
        private bool _afficherGUI = true;

        private GUIStyle _styleCadre;
        private GUIStyle _styleTexte;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (s_instance != null) return;
            GameObject go = new GameObject("VRPerfLogs_Auto");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<VRPerfLogs>();
            Debug.Log("[PerfVR] Surveillance des performances active (F3 pour masquer l'overlay)");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
            {
                float fpsInstant = 1f / dt;
                _fpsLisse = Mathf.Lerp(_fpsLisse, fpsInstant, 0.1f);
                _msLisse = Mathf.Lerp(_msLisse, dt * 1000f, 0.1f);
            }

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.F3].wasPressedThisFrame)
            {
                _afficherGUI = !_afficherGUI;
            }

            if (Time.unscaledTime - _tempsDernierLog >= 2.0f)
            {
                _tempsDernierLog = Time.unscaledTime;
                MettreAJourComptesSplats();
                JournaliserConsole();
            }
        }

        private void MettreAJourComptesSplats()
        {
            _totalSplats = 0;
            _nbDecors = 0;
            _nbObjets = 0;

            foreach (GaussianSplatRenderer r in FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None))
            {
                if (!r.isActiveAndEnabled || r.m_Asset == null) continue;
                _totalSplats += r.m_Asset.splatCount;
                if (r.transform.parent == null && !r.TryGetComponent(out Rigidbody _))
                    _nbDecors++;
                else
                    _nbObjets++;
            }
        }

        private void JournaliserConsole()
        {
            string suiviG = VRHandGestures.IsTracked(XRNode.LeftHand) ? "OK" : "--";
            string suiviD = VRHandGestures.IsTracked(XRNode.RightHand) ? "OK" : "--";
            int resW = XRSettings.eyeTextureWidth;
            int resH = XRSettings.eyeTextureHeight;

            // Sans pile d'appels : un Debug.Log ordinaire capture la pile (coûteux) et faisait
            // une petite saccade toutes les 2 s, justement ce que ce script doit mesurer.
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[PerfVR] FPS: {0:F1} ({1:F1} ms) | Splats: {2:N0} ({3} décors, {4} objets) | Res XR: {5}x{6} | Mains: G={7} D={8}",
                _fpsLisse, _msLisse, _totalSplats, _nbDecors, _nbObjets, resW, resH, suiviG, suiviD);
        }

        // Textes du bandeau, recalculés 4 fois par seconde (OnGUI est appelé plusieurs fois
        // par image : reformater les chaînes à chaque appel allouait de la mémoire en continu).
        private float _prochainTexte;
        private string _texteFps = "", _texteSplats = "", _texteXR = "";
        private Color _couleurFps = Color.white;

        private void MettreAJourTextes()
        {
            if (Time.unscaledTime < _prochainTexte) return;
            _prochainTexte = Time.unscaledTime + 0.25f;
            _couleurFps = _fpsLisse >= 68f ? new Color(0.1f, 0.9f, 0.2f) :
                          _fpsLisse >= 50f ? new Color(1f, 0.75f, 0.1f) :
                                             new Color(1f, 0.25f, 0.25f);
            _texteFps = string.Format("FPS: {0:F1}  ({1:F1} ms / cible 13.9 ms)", _fpsLisse, _msLisse);
            _texteSplats = string.Format("Splats: {0:N0} ({1} décors, {2} objets)", _totalSplats, _nbDecors, _nbObjets);
            _texteXR = string.Format("XR: {0}x{1} | Mains: G={2} D={3}",
                XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight,
                VRHandGestures.IsTracked(XRNode.LeftHand) ? "✓" : "✗",
                VRHandGestures.IsTracked(XRNode.RightHand) ? "✓" : "✗");
        }

        private void OnGUI()
        {
            if (!_afficherGUI) return;
            if (Event.current.type != EventType.Repaint) return; // rien d'interactif : on ne dessine qu'au Repaint
            MettreAJourTextes();

            if (_styleCadre == null)
            {
                _styleCadre = new GUIStyle(GUI.skin.box);
                _styleCadre.normal.background = Texture2D.whiteTexture;

                _styleTexte = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }

            Color fondOriginal = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            GUI.Box(new Rect(Screen.width - 320, 10, 310, 75), GUIContent.none, _styleCadre);
            GUI.color = fondOriginal;

            // GUI.Label direct (pas GUILayout : sa passe de mise en page coûte à chaque appel).
            float x = Screen.width - 315;
            _styleTexte.normal.textColor = _couleurFps;
            GUI.Label(new Rect(x, 14, 300, 20), _texteFps, _styleTexte);
            _styleTexte.normal.textColor = Color.white;
            GUI.Label(new Rect(x, 36, 300, 20), _texteSplats, _styleTexte);
            GUI.Label(new Rect(x, 58, 300, 20), _texteXR, _styleTexte);
        }
    }
}
