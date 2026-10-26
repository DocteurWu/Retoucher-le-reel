// =====================================================================================
//  OutilsScene.cs — OUTILS D'ÉDITEUR (non embarqué dans le jeu)
//
//  RÔLE
//    Petits outils d'automatisation pour installer des composants dans les scènes
//    sans les éditer à la main (le fichier YAML d'une scène est fragile à
//    modifier au clavier).
//
//    ⚠️ Ce fichier est dans un dossier « Editor » : Unity ne le compile PAS dans
//       le jeu (build Android/PC). Il n'existe que dans l'éditeur.
//
//  UTILISATION
//    Menu « Retoucher le Reel / Outils / … » dans l'éditeur,
//    ou en ligne de commande :
//      Unity.exe -batchmode -quit -nographics -projectPath <projet>
//                 -executeMethod RemodelerLeReel.EditorTools.OutilsScene.InstallerNarration
//
//  RÉFÉRENCES : voir Assets/Projet/Scripts/Jeu/README.md
// =====================================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools
{
    public static class OutilsScene
    {
        /// <summary>Nom de l'objet vide qui PORTE les composants de narration.</summary>
        private const string NomObjetNarration = "Narration";

        private static readonly string[] ScenesATraiter =
        {
            "Assets/Projet/Scenes_Niveaux/Menu.unity",
            "Assets/Projet/Scenes_Niveaux/Scene.de.base1.unity"
        };

        [MenuItem("Retoucher le Reel/Outils/Installer la narration")]
        public static void InstallerNarration()
        {
            foreach (string chemin in ScenesATraiter)
            {
                InstallerDansScene(chemin);
            }

            Debug.Log("[OutilsScene] Installation de la narration terminée.");
        }

        private static void InstallerDansScene(string chemin)
        {
            Scene scene = EditorSceneManager.OpenScene(chemin, OpenSceneMode.Single);

            // 1. L'objet hôte (créé s'il n'existe pas encore).
            GameObject hote = null;
            foreach (GameObject racine in scene.GetRootGameObjects())
            {
                if (racine.name == NomObjetNarration)
                {
                    hote = racine;
                    break;
                }
            }

            if (hote == null)
            {
                hote = new GameObject(NomObjetNarration);
            }

            // 2. Une première séquence (introduction au démarrage) si l'objet n'en a
            //    aucune ; la bulle est ajoutée avec (RequireComponent).
            if (hote.GetComponent<NarrationSequence>() == null)
            {
                NarrationSequence intro = hote.AddComponent<NarrationSequence>();
                intro.repliques = new System.Collections.Generic.List<Replique>
                {
                    new Replique { texte = "Le silence est total. Quelqu'un est parti sans éteindre la machine." }
                };
            }
            NarrationBulle bulle = hote.GetComponent<NarrationBulle>();
            if (bulle == null) bulle = hote.AddComponent<NarrationBulle>();

            // 3. La caméra : on la renseigne explicitement pour ne pas dépendre
            //    du tag « MainCamera ».
            Camera camera = TrouverCameraPrincipale(scene);
            if (camera != null)
            {
                bulle.cameraCible = camera.transform;
            }
            else
            {
                Debug.LogWarning("[OutilsScene] Aucune caméra trouvée dans " + chemin);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[OutilsScene] Narration installée dans " + chemin);
        }

        private static Camera TrouverCameraPrincipale(Scene scene)
        {
            Camera meilleure = null;

            foreach (GameObject racine in scene.GetRootGameObjects())
            {
                foreach (Camera cam in racine.GetComponentsInChildren<Camera>(true))
                {
                    if (meilleure == null)
                    {
                        meilleure = cam;
                    }

                    if (cam.name == "Main Camera")
                    {
                        return cam;
                    }
                }
            }

            return meilleure;
        }
    }
}
