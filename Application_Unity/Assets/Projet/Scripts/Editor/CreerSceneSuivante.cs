// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  CreerSceneSuivante.cs — CRÉER UNE SCÈNE À PARTIR D'UN NOUVEAU SPLAT (outil d'éditeur)
//
//  RÔLE
//    Fabrique « Scene2 » en copiant « Scene.de.base1 » (rig VR, sol,
//    narration, objets saisissables déjà réglés), puis :
//      • remplace le décor Gaussian par le splat SÉLECTIONNÉ dans le Project ;
//      • retire l'épreuve du récipient et ses narrations (propres à la scène 1) ;
//      • remplace la réplique d'introduction par un texte à réécrire ;
//      • ajoute la scène aux Build Settings (sinon l'épreuve ne peut pas la charger).
//
//  UTILISATION
//    1. Si le .ply vient de SuperSplat (« .compressed.ply »), le décompresser :
//       Traitement_Gaussian_SAGA/Scripts_Entrainement/03_decompresser_ply_supersplat.py
//    2. Tools > Gaussian Splats > Create GaussianSplatAsset sur le .ply décompressé.
//    3. Sélectionner l'asset créé dans le Project, puis
//       Retoucher le Reel > Outils > Créer la scène suivante avec le splat sélectionné.
//    4. Ajuster la position / rotation / échelle de l'objet « Decor Gaussian » et du rig.
// =====================================================================================
using System.Collections.Generic;
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools
{
    public static class CreerSceneSuivante
    {
        private const string SceneModele = "Assets/Projet/Scenes_Niveaux/Scene.de.base1.unity";
        private const string SceneNouvelle = "Assets/Projet/Scenes_Niveaux/Scene2.unity";
        private const string MenuCreer = "Retoucher le Reel/Outils/Créer la scène suivante avec le splat sélectionné";

        [MenuItem(MenuCreer, true)]
        private static bool PeutCreer() => Selection.activeObject is GaussianSplatAsset;

        [MenuItem(MenuCreer)]
        public static void Creer()
        {
            GaussianSplatAsset splat = Selection.activeObject as GaussianSplatAsset;
            if (splat == null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneNouvelle) != null
                && !EditorUtility.DisplayDialog("Scène suivante",
                    SceneNouvelle + " existe déjà. La remplacer par une nouvelle copie de la scène 1 ?",
                    "Remplacer", "Annuler"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(SceneNouvelle);
            if (!AssetDatabase.CopyAsset(SceneModele, SceneNouvelle))
            {
                Debug.LogError("[Scène suivante] Copie impossible de " + SceneModele);
                return;
            }

            var scene = EditorSceneManager.OpenScene(SceneNouvelle, OpenSceneMode.Single);

            // --- Décor : le splat de la scène 1 est le seul GaussianSplatRenderer HORS des
            //     objets saisissables (la fraise en est un, avec un Rigidbody).
            GaussianSplatRenderer decor = Object.FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.GetComponentInParent<Rigidbody>() == null);
            if (decor != null)
            {
                Undo.RecordObject(decor, "Splat de la scène suivante");
                decor.m_Asset = splat;
                decor.gameObject.name = "Decor Gaussian";
                EditorUtility.SetDirty(decor);
            }
            else
            {
                Debug.LogWarning("[Scène suivante] Aucun décor Gaussian trouvé : ajoute un GaussianSplatRenderer à la main.");
            }

            // --- L'épreuve et ses narrations n'appartiennent qu'à la scène 1.
            foreach (EpreuveRecipient epreuve in Object.FindObjectsByType<EpreuveRecipient>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(epreuve.gameObject);
            }

            List<NarrationSequence> sequences = Object.FindObjectsByType<NarrationSequence>(FindObjectsSortMode.None).ToList();
            NarrationSequence intro = sequences.FirstOrDefault(s => s.declencheur == DeclencheurNarration.AuDemarrage);
            foreach (NarrationSequence s in sequences)
            {
                if (s != intro) Object.DestroyImmediate(s);
            }
            if (intro != null)
            {
                intro.repliques = new List<Replique> { new Replique { texte = "(À écrire : introduction de la nouvelle scène.)", duree = 5f } };
                EditorUtility.SetDirty(intro);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AjouterAuxBuildSettings(SceneNouvelle);

            Selection.activeObject = decor != null ? decor.gameObject : null;
            Debug.Log("[Scène suivante] " + SceneNouvelle + " créée avec le splat « " + splat.name
                      + " » et ajoutée aux Build Settings. Ajuste la position du décor (« Decor Gaussian ») et du rig.");
        }

        private static void AjouterAuxBuildSettings(string chemin)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == chemin)) return;
            scenes.Add(new EditorBuildSettingsScene(chemin, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
