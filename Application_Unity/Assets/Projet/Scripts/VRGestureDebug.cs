// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRGestureDebug.cs — ATELIER DE GESTES (outil de mise au point, à retirer en jeu)
//
//  RÔLE
//    Affiche EN CASQUE, au-dessus de chaque main suivie, les mesures avec
//    lesquelles on construit un geste :
//      • flexion (curl) des 5 doigts, de 0 (tendu) à 1 (replié) ;
//      • pincement pouce ↔ index / majeur, de 0 (écartés) à 1 (collés) ;
//      • distance pouce ↔ côté de l'index (le « bouton » des microgestes) ;
//      • les gestes du projet reconnus à cet instant (vert = reconnu) ;
//      • les événements tap / glissement (affichés une demi-seconde).
//    On fait le geste voulu, on lit les valeurs, on en déduit les seuils.
//    Mode d'emploi complet : Assets/Projet/Scripts/GESTES.md
//
//  À POSER SUR : n'importe quel objet de la scène (par ex. le XR Origin), le temps
//    de régler un geste. « logToConsole » écrit aussi les valeurs dans Editor.log.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace RemodelerLeReel.VR
{
    /// <summary>Panneau de valeurs des gestes au-dessus de chaque main.</summary>
    [AddComponentMenu("Remodeler le Reel/VR/Atelier de gestes (debug)")]
    public class VRGestureDebug : MonoBehaviour
    {
        [Tooltip("Rafraîchissements par seconde (lisibilité : les valeurs bougent sans arrêt).")]
        [Range(2f, 30f)] public float refreshRate = 10f;

        [Tooltip("Hauteur du panneau au-dessus du poignet (m).")]
        public float heightAboveWrist = 0.13f;

        [Tooltip("Écrire aussi les valeurs dans la console / Editor.log (préfixe [Gestes]).")]
        public bool logToConsole = false;

        [Tooltip("Intervalle entre deux lignes de log (s).")]
        public float logInterval = 0.5f;

        private readonly TextMeshPro[] _panels = new TextMeshPro[2];
        private readonly string[] _event = new string[2];
        private readonly float[] _eventTime = new float[2];
        private float _nextRefresh;
        private float _nextLog;

        private void OnDisable()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_panels[i] != null) Destroy(_panels[i].gameObject);
                _panels[i] = null;
            }
        }

        private void Update()
        {
            // Les événements durent UNE frame : on les capte à chaque frame,
            // puis on les laisse affichés un court instant.
            for (int i = 0; i < 2; i++)
            {
                XRNode node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
                if (VRThumbMicrogestures.TapThisFrame(node)) Flash(i, "TAP !");
                int swipe = VRThumbMicrogestures.SwipeThisFrame(node);
                if (swipe != 0) Flash(i, swipe > 0 ? "GLISSEMENT →" : "← GLISSEMENT");
            }

            bool refresh = Time.unscaledTime >= _nextRefresh;
            if (refresh) _nextRefresh = Time.unscaledTime + 1f / refreshRate;
            bool log = logToConsole && Time.unscaledTime >= _nextLog;
            if (log) _nextLog = Time.unscaledTime + logInterval;

            for (int i = 0; i < 2; i++)
            {
                XRNode node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
                TextMeshPro panel = Panel(i);
                if (!VRHandGestures.TryGetHand(node, out XRHand hand))
                {
                    panel.gameObject.SetActive(false);
                    continue;
                }
                panel.gameObject.SetActive(true);
                Place(panel, hand);

                if (refresh || log)
                {
                    string texte = Describe(node, hand, i);
                    if (refresh) panel.text = texte;
                    if (log) Debug.Log("[Gestes] " + StripTags(texte).Replace("\n", " | "));
                }
            }
        }

        private void Flash(int i, string message)
        {
            _event[i] = message;
            _eventTime[i] = Time.unscaledTime;
            _nextRefresh = 0f; // affichage immédiat
        }

        // ------------------------------------------------------------------
        // Contenu
        // ------------------------------------------------------------------
        private string Describe(XRNode node, XRHand hand, int i)
        {
            float pouce = VRHandGestures.GetCurl(hand, XRHandFingerID.Thumb);
            float index = VRHandGestures.GetCurl(hand, XRHandFingerID.Index);
            float majeur = VRHandGestures.GetCurl(hand, XRHandFingerID.Middle);
            float annulaire = VRHandGestures.GetCurl(hand, XRHandFingerID.Ring);
            float auriculaire = VRHandGestures.GetCurl(hand, XRHandFingerID.Little);

            float pinceIndex = VRHandGestures.GetPinch(hand, XRHandFingerID.Index);
            float pinceMajeur = VRHandGestures.GetPinch(hand, XRHandFingerID.Middle);

            bool pistolet = VRHandGestures.TryGetFingerGun(node, out _, out _, out bool tir);
            bool saisie = VRHandGestures.IsGrabGesture(node, false);
            bool micro = VRThumbMicrogestures.IsPoseActive(node);

            string evenement = Time.unscaledTime - _eventTime[i] < 0.5f ? _event[i] : "";

            return "<b>" + (node == XRNode.LeftHand ? "MAIN GAUCHE" : "MAIN DROITE") + "</b>\n"
                 + "Flexion  P " + F(pouce) + "  I " + F(index) + "  M " + F(majeur)
                 + "  A " + F(annulaire) + "  Au " + F(auriculaire) + "\n"
                 + "Pincement  I " + F(pinceIndex) + "  M " + F(pinceMajeur) + "\n"
                 + "Pouce ↔ côté index  " + Cm(ThumbToIndexSide(hand)) + "\n"
                 + Etat("Pistolet", pistolet) + "  " + Etat("Tir", tir) + "\n"
                 + Etat("Saisie", saisie) + "  " + Etat("Microgeste", micro) + "\n"
                 + "<color=#ffd84a>" + evenement + "</color>";
        }

        /// <summary>Distance pouce ↔ côté de l'index, même calcul que les microgestes, sans exiger le poing.</summary>
        private static float ThumbToIndexSide(XRHand hand)
        {
            if (!VRHandGestures.TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose thumb)
                || !VRHandGestures.TryGetJointPose(hand, XRHandJointID.IndexProximal, out Pose prox)
                || !VRHandGestures.TryGetJointPose(hand, XRHandJointID.IndexIntermediate, out Pose inter)
                || !VRHandGestures.TryGetJointPose(hand, XRHandJointID.IndexDistal, out Pose dist))
                return float.NaN;
            return Mathf.Min(VRHandGestures.DistanceToSegment(thumb.position, prox.position, inter.position),
                             VRHandGestures.DistanceToSegment(thumb.position, inter.position, dist.position));
        }

        private static string F(float v) => v.ToString("0.00");
        private static string Cm(float m) => float.IsNaN(m) ? "?" : (m * 100f).ToString("0.0") + " cm";
        private static string Etat(string nom, bool actif) =>
            (actif ? "<color=#5dff7a>" : "<color=#7a7a7a>") + nom + (actif ? " OUI" : " non") + "</color>";

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        // ------------------------------------------------------------------
        // Panneau
        // ------------------------------------------------------------------
        private TextMeshPro Panel(int i)
        {
            if (_panels[i] != null) return _panels[i];

            GameObject go = new GameObject(i == 0 ? "GesteDebug_Gauche" : "GesteDebug_Droite");
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            // Taille auto dans un cadre de 30 × 14 cm : lisible sans calculer de taille de police.
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.01f;
            t.fontSizeMax = 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(0.3f, 0.14f);
            // Par-dessus tout (mains comprises) : même principe que la bulle de narration.
            Material m = t.fontMaterial;
            m.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            m.renderQueue = (int)RenderQueue.Overlay;
            _panels[i] = t;
            return t;
        }

        private void Place(TextMeshPro panel, XRHand hand)
        {
            if (!VRHandGestures.TryGetJointPose(hand, XRHandJointID.Wrist, out Pose wrist)) return;
            Vector3 pos = VRHandGestures.ToWorld(wrist.position) + Vector3.up * heightAboveWrist;
            panel.transform.position = pos;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 regard = pos - cam.transform.position;
                if (regard.sqrMagnitude > 1e-6f) panel.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
            }
        }
    }
}
