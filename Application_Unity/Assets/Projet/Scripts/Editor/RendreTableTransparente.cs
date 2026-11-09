// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  RendreTableTransparente.cs — REND LA TABLE INVISIBLE / TRANSPARENTE DANS L'ÉDITEUR
//
//  RÔLE
//    Menu : désactive le MeshRenderer du collider « table » dans la scène active pour que
//    le décor en Gaussian Splatting soit visible sans boîte opaque.
//
//  PLUS D'EXÉCUTION AUTOMATIQUE (2026-11-08) : elle tournait à chaque recompilation, même
//    en mode Play et pendant les tests en mode batch, et pouvait marquer une scène comme
//    modifiée à l'insu de l'équipe. La table de la scène 1 a déjà son rendu coupé, et la
//    main guide le coupe aussi au lancement (MainGuide.MasquerRenduTable).
// =====================================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RemodelerLeReel.EditorTools
{
    public static class RendreTableTransparente
    {
        [MenuItem("Retoucher le Reel/Outils/Rendre la table transparente")]
        public static void Appliquer()
        {
            // En mode Play, la modification serait perdue (et la main guide s'en charge déjà).
            if (EditorApplication.isPlaying) return;

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.Trim().ToLowerInvariant() == "table")
                {
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (mr != null && mr.enabled)
                    {
                        Undo.RecordObject(mr, "Rendre la table transparente");
                        mr.enabled = false;
                        EditorUtility.SetDirty(mr);
                        EditorSceneManager.MarkSceneDirty(go.scene);
                        Debug.Log("[RendreTableTransparente] MeshRenderer de 'table' désactivé pour la rendre transparente.");
                    }
                }
            }
        }
    }
}
