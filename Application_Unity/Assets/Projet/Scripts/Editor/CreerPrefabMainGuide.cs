// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  CreerPrefabMainGuide.cs — LA MAIN GUIDE DEVIENT UN ASSET (outil d'éditeur)
//
//  RÔLE
//    Menu « Retoucher le Reel > Outils > Créer le prefab de la main guide (et l'ajouter au
//    Kit) ». En un clic :
//      • matériaux Mat_Main_Guide (peau) et Mat_Main_Guide_Manchette (URP Lit) ;
//      • prefab Assets/Projet/Resources/Prefabs/Main Guide.prefab (sous Resources : ProgressionEtapes
//        le charge dans les scènes qui n'ont pas de main posée, comme Scene3) : composant MainGuide
//        (réglages dans l'Inspector) + enfant « Modele_Main » = la main droite du sample XRI
//        débarrassée du suivi des mains, avec sa manchette ;
//      • une instance dans le prefab « Kit Scene VR » (si elle n'y est pas déjà).
//    Relançable : le prefab est refait, les matériaux existants sont gardés (retouches
//    conservées), le Kit n'en reçoit jamais deux.
// =====================================================================================
using System.IO;
using UnityEditor;
using UnityEngine;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools
{
    public static class CreerPrefabMainGuide
    {
        private const string CheminMain = "Assets/Samples/XR Interaction Toolkit/3.0.4/Hands Interaction Demo/Prefabs/Right Hand Interaction Visual.prefab";
        private const string DossierPrefab = "Assets/Projet/Resources/Prefabs";
        private const string CheminPrefab = DossierPrefab + "/Main Guide.prefab";
        private const string CheminKit = "Assets/Projet/Prefabs_Objets/VR/Kit Scene VR.prefab";
        private const string DossierMateriaux = "Assets/Projet/Art_Graphismes/Materials";

        [MenuItem("Retoucher le Reel/Outils/Créer le prefab de la main guide (et l'ajouter au Kit)")]
        public static void Creer()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CheminMain);
            if (source == null)
            {
                EditorUtility.DisplayDialog("Main guide", "Main du sample XRI introuvable :\n" + CheminMain, "OK");
                return;
            }

            Material peau = Materiau("Mat_Main_Guide", new Color(0.82f, 0.74f, 0.66f), 0.3f);
            Material manchette = Materiau("Mat_Main_Guide_Manchette", new Color(0.12f, 0.1f, 0.14f), 0.3f);

            // --- La main : copie du visuel, sans suivi des mains ni reflets d'interaction ---
            GameObject racine = new GameObject("Main Guide");
            GameObject modele = Object.Instantiate(source, racine.transform);
            modele.name = "Modele_Main";
            Transform poignet = Trouver(modele.transform, "R_Wrist");
            SkinnedMeshRenderer maillage = modele.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (poignet == null || maillage == null)
            {
                Object.DestroyImmediate(racine);
                EditorUtility.DisplayDialog("Main guide", "La main du sample n'a pas le squelette attendu (R_Wrist).", "OK");
                return;
            }
            for (int i = modele.transform.childCount - 1; i >= 0; i--)
            {
                Transform enfant = modele.transform.GetChild(i);
                if (enfant != poignet && enfant != maillage.transform) Object.DestroyImmediate(enfant.gameObject);
            }
            MonoBehaviour[] scripts = modele.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--) Object.DestroyImmediate(scripts[i]);

            Material[] materiaux = new Material[maillage.sharedMaterials.Length];
            for (int i = 0; i < materiaux.Length; i++) materiaux[i] = peau;
            maillage.sharedMaterials = materiaux;
            maillage.enabled = true;
            maillage.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            maillage.updateWhenOffscreen = true;

            GameObject cylindre = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylindre.name = "Manchette";
            Object.DestroyImmediate(cylindre.GetComponent<Collider>());
            cylindre.transform.SetParent(poignet, false);
            cylindre.transform.localPosition = new Vector3(0f, -0.005f, -0.012f);
            cylindre.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cylindre.transform.localScale = new Vector3(0.068f, 0.018f, 0.05f);
            cylindre.GetComponent<Renderer>().sharedMaterial = manchette;

            MainGuide guide = racine.AddComponent<MainGuide>();
            guide.modele = modele;

            // --- Le prefab ---
            if (!AssetDatabase.IsValidFolder(DossierPrefab)) AssetDatabase.CreateFolder("Assets/Projet/Resources", "Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(racine, CheminPrefab);
            Object.DestroyImmediate(racine);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Main guide", "Échec de l'enregistrement du prefab.", "OK");
                return;
            }

            // --- Dans le Kit Scene VR ---
            string bilanKit = "Kit introuvable : " + CheminKit;
            if (File.Exists(CheminKit))
            {
                GameObject kit = PrefabUtility.LoadPrefabContents(CheminKit);
                if (kit.GetComponentInChildren<MainGuide>(true) == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, kit.transform);
                    instance.name = "Main Guide";
                    PrefabUtility.SaveAsPrefabAsset(kit, CheminKit);
                    bilanKit = "ajoutée au Kit Scene VR";
                }
                else
                {
                    bilanKit = "déjà dans le Kit Scene VR";
                }
                PrefabUtility.UnloadPrefabContents(kit);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("[MainGuide] Prefab créé : " + CheminPrefab + " ; main guide " + bilanKit + ".");
        }

        /// <summary>Matériau URP Lit (gardé s'il existe déjà : ses retouches ne sont pas écrasées).</summary>
        private static Material Materiau(string nom, Color couleur, float emission)
        {
            string chemin = DossierMateriaux + "/" + nom + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (m != null) return m;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = nom, color = couleur };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", couleur * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, chemin);
            return m;
        }

        private static Transform Trouver(Transform t, string nom)
        {
            if (t.name == nom) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = Trouver(t.GetChild(i), nom);
                if (r != null) return r;
            }
            return null;
        }
    }
}
