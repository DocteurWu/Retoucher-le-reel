// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VerificationsAuto.cs — VÉRIFICATIONS AUTOMATIQUES (outil d'éditeur)
//
//  RÔLE
//    Vérifie en mode Play (via le mode batch d'Unity) que les briques du jeu
//    font bien ce qu'on attend. Garde-fou : on ne livre pas un script qui
//    compile mais qui ne fait rien.
//
//  CE QUI EST VÉRIFIÉ (narration)
//    La première NarrationSequence « Au démarrage » de la scène :
//      1. sa première réplique s'écrit EN ENTIER ;
//      2. la bulle est en espace-monde, devant la caméra, dans le champ de vision,
//         lisible (pas en miroir) et dessinée par-dessus le décor ;
//      3. la bulle disparaît à la fin de la séquence.
//
//  UTILISATION
//      Unity.exe -batchmode -nographics -projectPath <projet>
//                 -executeMethod RemodelerLeReel.EditorTools.VerificationsAuto.VerifierNarration
//
//  ⚠️ Ne pas passer -quit : le script appelle lui-même EditorApplication.Exit() (en batch seulement ;
//  depuis le menu, il quitte juste le mode Play).
//
//  RÉFÉRENCES : voir Assets/Projet/Scripts/Jeu/README.md
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools
{
    public static class VerificationsAuto
    {
        private const string SceneTest = "Assets/Projet/Scenes_Niveaux/Scene.de.base1.unity";

        private static double _depart;
        private static double _debutDisparition;
        private static double _delaiMax;
        private static string _texteAttendu = string.Empty;
        private static TextMeshProUGUI _panneau;
        private static bool _controle;
        private static bool _termine;
        private static readonly List<string> Erreurs = new List<string>();

        [MenuItem("Retoucher le Reel/Outils/Vérifier la narration (mode Play)")]
        public static void VerifierNarration()
        {
            // Lancé depuis le menu (pas en batch) : ne pas perdre la scène en cours sans demander.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Erreurs.Clear();
            _termine = false;
            _controle = false;
            _panneau = null;

            EditorSceneManager.OpenScene(SceneTest, OpenSceneMode.Single);

            NarrationSequence intro = null;
            foreach (NarrationSequence s in Object.FindObjectsByType<NarrationSequence>(FindObjectsSortMode.None))
            {
                if (s.declencheur == DeclencheurNarration.AuDemarrage && s.repliques != null && s.repliques.Count > 0)
                {
                    intro = s;
                    break;
                }
            }
            if (intro == null)
            {
                Debug.LogError("[VERIF] ECHEC — aucune NarrationSequence « Au démarrage » avec une réplique dans "
                               + SceneTest + " (lance « Installer la narration » d'abord).");
                Quitter(3);
                return;
            }

            _texteAttendu = intro.repliques[0].texte ?? string.Empty;
            // Délai + écriture lente + lecture : large marge.
            _delaiMax = intro.delai + 45d;

            _depart = EditorApplication.timeSinceStartup;
            EditorApplication.update += BoucleDeVerification;
            EditorApplication.EnterPlaymode();
        }

        private static void BoucleDeVerification()
        {
            if (_termine || !EditorApplication.isPlaying) return;

            double ecoule = EditorApplication.timeSinceStartup - _depart;

            // ---- Phase 1 : la réplique doit s'écrire EN ENTIER ----
            // La bulle pose tout le texte puis révèle les lettres avec maxVisibleCharacters.
            if (!_controle)
            {
                foreach (TextMeshProUGUI t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                {
                    if (t.text == _texteAttendu && t.maxVisibleCharacters >= t.textInfo.characterCount)
                    {
                        _panneau = t;
                        break;
                    }
                }

                if (_panneau != null)
                {
                    ControlerPanneau(_panneau);
                    _controle = true;
                    _debutDisparition = EditorApplication.timeSinceStartup;
                    return;
                }

                if (ecoule > _delaiMax)
                {
                    Erreurs.Add("Délai dépassé : la réplique n'a pas été écrite en entier (\"" + _texteAttendu + "\").");
                    Terminer(2);
                }
                return;
            }

            // ---- Phase 2 : la bulle doit DISPARAÎTRE ----
            if (!PanneauCache())
            {
                if (EditorApplication.timeSinceStartup - _debutDisparition > 60d)
                {
                    Erreurs.Add("La bulle ne disparaît pas (60 s après la fin de l'écriture).");
                    Terminer(2);
                }
                return;
            }

            Terminer(0);
        }

        /// <summary>Vrai si la bulle n'est plus visible (détruite, désactivée ou transparente).</summary>
        private static bool PanneauCache()
        {
            if (_panneau == null) return true;

            Canvas canvas = _panneau.GetComponentInParent<Canvas>(true);
            if (canvas == null || !canvas.gameObject.activeInHierarchy) return true;

            CanvasGroup groupe = canvas.GetComponent<CanvasGroup>();
            return groupe != null && groupe.alpha <= 0.02f;
        }

        /// <summary>Contrôle : la bulle est-elle réellement VISIBLE par la caméra ?</summary>
        private static void ControlerPanneau(TextMeshProUGUI panneau)
        {
            Canvas canvas = panneau.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Erreurs.Add("Le texte n'est dans aucun canvas.");
                return;
            }
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Erreurs.Add("Le canvas n'est pas en espace-monde (invisible dans le casque).");
            }
            if (panneau.font == null) Erreurs.Add("Le texte n'a aucune police (invisible).");

            // Dessinée par-dessus le décor : c'est ce qui l'empêche d'être cachée.
            Material m = panneau.fontMaterial;
            if (m != null && m.HasProperty("unity_GUIZTestMode")
                && (int)m.GetFloat("unity_GUIZTestMode") != (int)CompareFunction.Always)
            {
                Erreurs.Add("La bulle n'est pas en ZTest Always : un objet du décor peut la cacher.");
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Erreurs.Add("Pas de Camera.main : impossible de vérifier la visibilité.");
                return;
            }

            Canvas.ForceUpdateCanvases();
            RectTransform canvasRt = canvas.GetComponent<RectTransform>();

            // --- 1. DEVANT la caméra (et pas dans le near clip) ---
            float distance = Vector3.Dot(canvasRt.position - cam.transform.position, cam.transform.forward);
            if (distance < 0.3f)
            {
                Erreurs.Add("La bulle est collée à la caméra ou derrière (distance = " + distance.ToString("F2") + " m).");
            }
            else if (distance > 5f)
            {
                Erreurs.Add("La bulle est trop loin (distance = " + distance.ToString("F2") + " m).");
            }

            // --- 2. DANS LE CHAMP DE VISION ---
            Vector3[] coins = new Vector3[4];
            panneau.rectTransform.GetWorldCorners(coins);
            Bounds boite = new Bounds(coins[0], Vector3.zero);
            for (int i = 1; i < 4; i++) boite.Encapsulate(coins[i]);
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), boite))
            {
                Erreurs.Add("Le texte est hors du champ de vision de la caméra.");
            }

            // --- 3. PAS EN MIROIR ---
            if (Vector3.Dot(canvasRt.right, cam.transform.right) < 0f)
            {
                Erreurs.Add("Le canvas est retourné : le texte s'afficherait en miroir.");
            }
        }

        private static void Terminer(int code)
        {
            _termine = true;
            EditorApplication.update -= BoucleDeVerification;

            if (code == 0 && Erreurs.Count == 0)
            {
                Debug.Log("[VERIF] OK — narration : réplique écrite en entier (\"" + _texteAttendu
                          + "\"), bulle devant la caméra, lisible, par-dessus le décor, puis effacée.");
            }
            else
            {
                foreach (string e in Erreurs) Debug.LogError("[VERIF] ECHEC — " + e);
            }

            Quitter(code);
        }

        /// <summary>
        /// En mode batch : quitte Unity avec le code de retour. Depuis le menu : quitte seulement le
        /// mode Play (avant, cliquer sur le menu fermait l'éditeur à la fin de la vérification).
        /// </summary>
        private static void Quitter(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
    }
}
