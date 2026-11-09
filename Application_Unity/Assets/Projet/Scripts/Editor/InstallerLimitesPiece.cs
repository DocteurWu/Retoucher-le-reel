// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  InstallerLimitesPiece.cs — INSTALLE LE SOL, LES MURS ET LE PLAFOND DANS TOUTES LES SCÈNES
//
//  RÔLE
//    Crée et installe dans la hiérarchie de chaque scène un groupe « Limites_Piece » :
//      - Sol (layer Ground = 6, BoxCollider actif, MeshRenderer DÉSACTIVÉ / invisible)
//      - 4 murs : Mur_Nord, Mur_Sud, Mur_Est, Mur_Ouest (layer Environment = 8,
//        BoxCollider actif, MeshRenderer DÉSACTIVÉ / invisible)
//      - Plafond : (layer Environment = 8, BoxCollider actif, MeshRenderer VISIBLE
//        avec Mat_Plafond_VR double-face)
//
//    Dans l'éditeur Unity, les objets invisibles (murs et sol) restent sélectionnables
//    dans la hiérarchie pour voir leurs gizmos / BoxCollider et les déplacer à volonté.
//    En jeu / VR, seuls les Gaussian Splats et le plafond sont visibles.
//
//    Crée également le Prefab Asset « Assets/Projet/Prefabs_Objets/VR/Limites_Piece.prefab ».
// =====================================================================================
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemodelerLeReel.EditorTools
{
    public static class InstallerLimitesPiece
    {
        public const string CheminMateriauMur = "Assets/Projet/Art_Graphismes/Materials/Mat_Mur_VR.mat";
        public const string CheminMateriauSol = "Assets/Projet/Art_Graphismes/Materials/Mat_Sol_VR.mat";
        public const string CheminMateriauPlafond = "Assets/Projet/Art_Graphismes/Materials/Mat_Plafond_VR.mat";
        public const string CheminPrefabLimites = "Assets/Projet/Prefabs_Objets/VR/Limites_Piece.prefab";
        public const string NomGroupeLimites = "Limites_Piece";

        private struct ConfigScene
        {
            public string cheminScene;
            public Vector2 centre;
            public float largeur;
            public float profondeur;
            public float hauteurMurs;
        }

        private static readonly ConfigScene[] Configurations = new[]
        {
            // Menu (Forge Haute) : 24 m × 24 m
            new ConfigScene
            {
                cheminScene = "Assets/Projet/Scenes_Niveaux/Menu.unity",
                centre = new Vector2(0f, 0f),
                largeur = 24f,
                profondeur = 24f,
                hauteurMurs = 6f
            },
            // Scène 1 (Céramique) : 10.5 m × 2.65 m (x: -5.1 à 5.4, z: -1.55 à 1.1)
            new ConfigScene
            {
                cheminScene = "Assets/Projet/Scenes_Niveaux/Scene.de.base1.unity",
                centre = new Vector2(0.15f, -0.225f),
                largeur = 10.5f,
                profondeur = 2.65f,
                hauteurMurs = 5f
            },
            // Scène 2 (Central2) : 32 m × 17.9 m (x: -13.2 à 18.8, z: -9.3 à 8.6)
            new ConfigScene
            {
                cheminScene = "Assets/Projet/Scenes_Niveaux/Scene2.unity",
                centre = new Vector2(2.8f, -0.35f),
                largeur = 32f,
                profondeur = 17.9f,
                hauteurMurs = 6f
            }
        };

        [MenuItem("Retoucher le Reel/Outils/Installer murs et sol dans toutes les scenes")]
        public static void InstallerToutesLesScenes()
        {
            // Ces menus ouvrent d'autres scènes (OpenSceneMode.Single) : sans cette question, les
            // modifications non enregistrées de la scène ouverte étaient perdues sans avertissement.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Material matMur = AssurerMateriauMur();
            Material matSol = AssurerMateriauSol();
            Material matPlafond = AssurerMateriauPlafond();

            string sceneInitiale = EditorSceneManager.GetActiveScene().path;

            foreach (ConfigScene cfg in Configurations)
            {
                if (!File.Exists(cfg.cheminScene)) continue;

                Scene scene = EditorSceneManager.OpenScene(cfg.cheminScene, OpenSceneMode.Single);
                InstallerDansScene(scene, cfg, matSol, matMur, matPlafond);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[InstallerLimitesPiece] Limites (sol + 4 murs invisibles + plafond visible) installées dans « " + scene.name + " ».");
            }

            // Créer le Prefab Asset modèle
            CreerPrefabAsset(Configurations[0], matSol, matMur, matPlafond);

            // Restaurer la scène d'origine
            if (!string.IsNullOrEmpty(sceneInitiale) && File.Exists(sceneInitiale))
            {
                EditorSceneManager.OpenScene(sceneInitiale, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[InstallerLimitesPiece] Installation terminée avec succès dans toutes les scènes.");
        }

        public static Material AssurerMateriauMur()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CheminMateriauMur);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            mat.name = "Mat_Mur_VR";

            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Cull", 0f); // Double-sided

            Color couleurMur = new Color(0.18f, 0.65f, 0.95f, 0.35f);
            mat.SetColor("_BaseColor", couleurMur);
            mat.SetColor("_Color", couleurMur);
            mat.SetFloat("_Smoothness", 0.3f);

            string dir = Path.GetDirectoryName(CheminMateriauMur);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, CheminMateriauMur);
            return mat;
        }

        public static Material AssurerMateriauSol()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CheminMateriauSol);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            mat.name = "Mat_Sol_VR";
            mat.color = new Color(0.22f, 0.27f, 0.28f, 1f);

            string dir = Path.GetDirectoryName(CheminMateriauSol);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, CheminMateriauSol);
            return mat;
        }

        public static Material AssurerMateriauPlafond()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CheminMateriauPlafond);
            if (mat != null)
            {
                ConfigurerMateriauPlafond(mat);
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            mat.name = "Mat_Plafond_VR";
            ConfigurerMateriauPlafond(mat);

            string dir = Path.GetDirectoryName(CheminMateriauPlafond);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, CheminMateriauPlafond);
            return mat;
        }

        public static void ConfigurerMateriauPlafond(Material mat)
        {
            // Opaque, file d'attente Geometry (2000) : rendu AVANT les Gaussian Splats.
            // Ainsi les Gaussian Splats sont dessinés au premier plan par-dessus le plafond.
            mat.SetFloat("_Surface", 0f); // 0 = Opaque
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1); // Écrit la profondeur pour occulter le skybox en arrière-plan
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry; // 2000
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // Double face pour voir le plafond depuis l'intérieur en regardant en l'air
            mat.SetFloat("_Cull", 0f); // 0 = Off (Double face)

            Color couleurPlafond = new Color(0.26f, 0.28f, 0.32f, 1f);
            mat.SetColor("_BaseColor", couleurPlafond);
            mat.SetColor("_Color", couleurPlafond);
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
        }

        private static void InstallerDansScene(Scene scene, ConfigScene cfg, Material matSol, Material matMur, Material matPlafond)
        {
            // 1. Supprimer l'ancien objet Sol orphelin à la racine si présent
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Sol")
                {
                    Undo.DestroyObjectImmediate(root);
                }
            }

            // 2. Trouver ou créer le groupe Limites_Piece
            GameObject groupe = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == NomGroupeLimites)
                {
                    groupe = root;
                    break;
                }
            }

            if (groupe == null)
            {
                groupe = new GameObject(NomGroupeLimites);
                SceneManager.MoveGameObjectToScene(groupe, scene);
                Undo.RegisterCreatedObjectUndo(groupe, "Créer Limites_Piece");
            }

            // Calques
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 6;
            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer < 0) envLayer = 8;

            groupe.layer = envLayer;
            groupe.transform.position = Vector3.zero;
            groupe.transform.rotation = Quaternion.identity;
            groupe.transform.localScale = Vector3.one;

            float e = 0.3f; // Épaisseur des murs et du plafond
            float halfW = cfg.largeur * 0.5f;
            float halfD = cfg.profondeur * 0.5f;
            float halfH = cfg.hauteurMurs * 0.5f;
            Vector2 c = cfg.centre;

            // Sol (INVISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Sol", groundLayer, matSol,
                new Vector3(c.x, -0.05f, c.y),
                new Vector3(cfg.largeur, 0.1f, cfg.profondeur),
                visible: false);

            // Mur Nord (+Z) (INVISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Mur_Nord", envLayer, matMur,
                new Vector3(c.x, halfH, c.y + halfD),
                new Vector3(cfg.largeur + e * 2f, cfg.hauteurMurs, e),
                visible: false);

            // Mur Sud (-Z) (INVISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Mur_Sud", envLayer, matMur,
                new Vector3(c.x, halfH, c.y - halfD),
                new Vector3(cfg.largeur + e * 2f, cfg.hauteurMurs, e),
                visible: false);

            // Mur Est (+X) (INVISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Mur_Est", envLayer, matMur,
                new Vector3(c.x + halfW, halfH, c.y),
                new Vector3(e, cfg.hauteurMurs, cfg.profondeur + e * 2f),
                visible: false);

            // Mur Ouest (-X) (INVISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Mur_Ouest", envLayer, matMur,
                new Vector3(c.x - halfW, halfH, c.y),
                new Vector3(e, cfg.hauteurMurs, cfg.profondeur + e * 2f),
                visible: false);

            // Plafond (+Y) (VISIBLE en jeu/VR)
            AssurerElement(groupe.transform, "Plafond", envLayer, matPlafond,
                new Vector3(c.x, cfg.hauteurMurs + e * 0.5f, c.y),
                new Vector3(cfg.largeur + e * 2f, e, cfg.profondeur + e * 2f),
                visible: true);
        }

        private static GameObject AssurerElement(Transform parent, string nom, int layer, Material material, Vector3 defPos, Vector3 defScale, bool visible)
        {
            Transform existant = parent.Find(nom);
            GameObject go;
            if (existant != null)
            {
                go = existant.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = nom;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = defPos;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = defScale;
                Undo.RegisterCreatedObjectUndo(go, "Créer " + nom);
            }

            go.layer = layer;

            MeshRenderer rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                if (material != null) rend.sharedMaterial = material;
                rend.enabled = visible;
            }

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            box.center = Vector3.zero;
            box.enabled = true;

            return go;
        }

        private static void CreerPrefabAsset(ConfigScene cfg, Material matSol, Material matMur, Material matPlafond)
        {
            string dir = Path.GetDirectoryName(CheminPrefabLimites);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            GameObject temp = new GameObject(NomGroupeLimites);
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 6;
            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer < 0) envLayer = 8;
            temp.layer = envLayer;

            float e = 0.3f;
            float halfW = cfg.largeur * 0.5f;
            float halfD = cfg.profondeur * 0.5f;
            float halfH = cfg.hauteurMurs * 0.5f;
            Vector2 c = cfg.centre;

            AssurerElement(temp.transform, "Sol", groundLayer, matSol,
                new Vector3(c.x, -0.05f, c.y),
                new Vector3(cfg.largeur, 0.1f, cfg.profondeur),
                visible: false);
            AssurerElement(temp.transform, "Mur_Nord", envLayer, matMur,
                new Vector3(c.x, halfH, c.y + halfD),
                new Vector3(cfg.largeur + e * 2f, cfg.hauteurMurs, e),
                visible: false);
            AssurerElement(temp.transform, "Mur_Sud", envLayer, matMur,
                new Vector3(c.x, halfH, c.y - halfD),
                new Vector3(cfg.largeur + e * 2f, cfg.hauteurMurs, e),
                visible: false);
            AssurerElement(temp.transform, "Mur_Est", envLayer, matMur,
                new Vector3(c.x + halfW, halfH, c.y),
                new Vector3(e, cfg.hauteurMurs, cfg.profondeur + e * 2f),
                visible: false);
            AssurerElement(temp.transform, "Mur_Ouest", envLayer, matMur,
                new Vector3(c.x - halfW, halfH, c.y),
                new Vector3(e, cfg.hauteurMurs, cfg.profondeur + e * 2f),
                visible: false);
            AssurerElement(temp.transform, "Plafond", envLayer, matPlafond,
                new Vector3(c.x, cfg.hauteurMurs + e * 0.5f, c.y),
                new Vector3(cfg.largeur + e * 2f, e, cfg.profondeur + e * 2f),
                visible: true);

            PrefabUtility.SaveAsPrefabAsset(temp, CheminPrefabLimites);
            UnityEngine.Object.DestroyImmediate(temp);
            Debug.Log("[InstallerLimitesPiece] Prefab modèle créé : « " + CheminPrefabLimites + " ».");
        }
    }
}
