// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ConvertirEnObjetMemoire.cs — TRANSFORME DES OBJETS EN OBJETS DU JEU (outil d'éditeur)
//
//  RÔLE
//    Menu « Retoucher le Reel > Memoire visuelle > Convertir la selection en objets du jeu ».
//    Pour chaque objet sélectionné dans la scène (typiquement un objet segmenté : un parent qui
//    contient le splat) il s'assure que l'objet a :
//      • un Rigidbody (ajouté au besoin) ;
//      • un collider (alerte s'il n'y en a aucun : un splat n'a pas de physique, il faut un
//        BoxCollider sur le parent, voir GUIDE_NOUVELLE_SCENE.md §4) ;
//      • le calque « Grabbable » ;
//      • le composant ObjetMemoire.
//    Il ne déplace rien : les emplacements du mélange se posent à la main (EmplacementMemoire).
//
//  C'est le pont entre la scène maquette (faux objets) et la vraie scène segmentée : le code du
//  jeu ne change pas, seuls les objets changent.
//
//  Annulable (Ctrl+Z).
// =====================================================================================
using UnityEditor;
using UnityEngine;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.EditorTools
{
    public static class ConvertirEnObjetMemoire
    {
        [MenuItem("Retoucher le Reel/Memoire visuelle/Convertir la selection en objets du jeu")]
        public static void Convertir()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                EditorUtility.DisplayDialog("Mémoire visuelle", "Sélectionne d'abord les objets (dans la Hierarchy) à convertir.", "OK");
                return;
            }

            int calque = LayerMask.NameToLayer("Grabbable");
            int convertis = 0;
            foreach (GameObject go in selection)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Convertir en objet du jeu");

                if (go.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody corps = Undo.AddComponent<Rigidbody>(go);
                    corps.mass = 0.5f;
                    corps.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    corps.interpolation = RigidbodyInterpolation.Interpolate;
                }

                if (go.GetComponentInChildren<Collider>() == null)
                {
                    Debug.LogWarning("[Memoire] « " + go.name + " » n'a aucun collider : il ne pourra pas être saisi. "
                        + "Ajouter un BoxCollider sur le parent (voir GUIDE_NOUVELLE_SCENE.md §4).", go);
                }

                if (calque >= 0) go.layer = calque;
                if (go.GetComponent<ObjetMemoire>() == null) Undo.AddComponent<ObjetMemoire>(go);
                EditorUtility.SetDirty(go);
                convertis++;
            }

            Debug.Log("[Memoire] " + convertis + " objet(s) converti(s). Pense à poser des EmplacementMemoire pour le mélange.");
        }

        [MenuItem("Retoucher le Reel/Memoire visuelle/Convertir la selection en objets du jeu", true)]
        private static bool ConvertirActif() => Selection.gameObjects.Length > 0;
    }
}
