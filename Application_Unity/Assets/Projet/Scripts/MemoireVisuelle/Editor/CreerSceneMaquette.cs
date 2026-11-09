// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  CreerSceneMaquette.cs — MONTE LA SCÈNE DE TEST DU JEU « MÉMOIRE VISUELLE » (outil d'éditeur)
//
//  RÔLE
//    Menu « Retoucher le Reel > Memoire visuelle > Creer la scene maquette ». En un clic :
//      • crée la scène Scenes_Niveaux/MemoireVisuelle/Memoire_Maquette.unity à partir du
//        « Kit Scene VR » (rig VR complet, sol invisible, narration, menu poignet) ;
//      • construit une pièce simple (sol, quatre murs, une table, une étagère, un tabouret) ;
//      • pose 8 FAUX OBJETS (balles, boîte, livre, vase, pot, bougie, petit cube) : ce sont des
//        formes de base, pas des splats, mais le jeu les traite comme des objets segmentés ;
//      • pose 11 EMPLACEMENTS possibles pour le mélange (table, étagères, sol) ;
//      • crée le niveau 1 et le niveau 2 (Donnees_Config/MemoireVisuelle/Niveau1.asset, Niveau2.asset)
//        et branche le jeu : après une réussite du niveau 1, le bouton devient « Niveau suivant » ;
//      • ajoute la scène aux Build Settings.
//
//  POURQUOI : développer et régler le jeu avant que la scène scannée soit segmentée. Quand elle
//    le sera, on remplace les faux objets par les vrais (menu « Convertir la selection »), on
//    repose les emplacements, et le code du jeu ne change pas.
//
//  RELANÇABLE : refait la scène de zéro (la scène existante est remplacée ; le niveau est conservé).
//  ⚠️ Enregistre ou ferme tes modifications en cours : la scène ouverte est remplacée.
// =====================================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RemodelerLeReel.Jeu;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.EditorTools
{
    public static class CreerSceneMaquette
    {
        private const string DossierScene = "Assets/Projet/Scenes_Niveaux/MemoireVisuelle";
        private const string ChemSceneMaquette = DossierScene + "/Memoire_Maquette.unity";
        private const string DossierMateriaux = DossierScene + "/Materiaux";
        private const string DossierConfig = "Assets/Projet/Donnees_Config/MemoireVisuelle";
        private const string CheminNiveau = DossierConfig + "/Niveau1.asset";
        private const string CheminNiveau2 = DossierConfig + "/Niveau2.asset";
        private const string CheminKit = "Assets/Projet/Prefabs_Objets/VR/Kit Scene VR.prefab";

        [MenuItem("Retoucher le Reel/Memoire visuelle/Creer la scene maquette")]
        public static void Creer()
        {
            GameObject kit = AssetDatabase.LoadAssetAtPath<GameObject>(CheminKit);
            if (kit == null)
            {
                EditorUtility.DisplayDialog("Mémoire visuelle", "Prefab introuvable :\n" + CheminKit, "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AssurerDossier("Assets/Projet/Scenes_Niveaux", "MemoireVisuelle");
            AssurerDossier(DossierScene, "Materiaux");
            AssurerDossier("Assets/Projet/Donnees_Config", "MemoireVisuelle");

            NiveauMemoire niveau = ObtenirNiveau();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Le kit VR (rig, sol invisible, narration, menu poignet). Le joueur démarre à l'origine.
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(kit);
            rig.transform.position = Vector3.zero;
            SilencerLIntroDuKit(rig);

            // 2. La pièce.
            Material sol = Materiau("Sol", new Color(0.55f, 0.5f, 0.45f));
            Material mur = Materiau("Mur", new Color(0.85f, 0.82f, 0.75f));
            Material bois = Materiau("Bois", new Color(0.45f, 0.3f, 0.18f));
            GameObject piece = new GameObject("Piece");
            Boite(piece.transform, "Plancher", new Vector3(0f, -0.05f, 2f), new Vector3(8f, 0.1f, 8f), sol, false);
            Boite(piece.transform, "Mur_Fond", new Vector3(0f, 1.5f, 6f), new Vector3(8f, 3f, 0.2f), mur, false);
            Boite(piece.transform, "Mur_Gauche", new Vector3(-4f, 1.5f, 2f), new Vector3(0.2f, 3f, 8f), mur, false);
            Boite(piece.transform, "Mur_Droit", new Vector3(4f, 1.5f, 2f), new Vector3(0.2f, 3f, 8f), mur, false);
            Boite(piece.transform, "Mur_Arriere", new Vector3(0f, 1.5f, -2f), new Vector3(8f, 3f, 0.2f), mur, false);

            // Mobilier : surfaces sur lesquelles les objets reposent (collisions simples).
            Boite(piece.transform, "Table", new Vector3(0f, 0.7f, 2.8f), new Vector3(1.6f, 0.06f, 0.8f), bois, true);   // dessus à y = 0,73
            Boite(piece.transform, "Etagere_Haute", new Vector3(3.5f, 1.3f, 4.5f), new Vector3(0.6f, 0.05f, 2.0f), bois, true); // y = 1,325
            Boite(piece.transform, "Etagere_Basse", new Vector3(3.5f, 0.7f, 4.5f), new Vector3(0.6f, 0.05f, 2.0f), bois, true); // y = 0,725
            Boite(piece.transform, "Tabouret", new Vector3(-2.5f, 0.45f, 4f), new Vector3(0.45f, 0.05f, 0.45f), bois, true);  // y = 0,475

            // 3. Les faux objets (posés sur leurs supports de départ). 8 objets : le niveau 2 en déplace 6.
            //    Surfaces : table (dessus y = 0,73), étagère haute (1,325), étagère basse (0,725), tabouret (0,475).
            GameObject objets = new GameObject("Objets_Memoire");
            const float Table = 0.73f, Haute = 1.325f, Basse = 0.725f, Tab = 0.475f;
            Objet(objets.transform, "Balle rouge", PrimitiveType.Sphere, new Vector3(-0.6f, Table + 0.08f, 2.6f), Vector3.one * 0.16f, Materiau("Rouge", new Color(0.9f, 0.15f, 0.15f)));
            Objet(objets.transform, "Boite bleue", PrimitiveType.Cube, new Vector3(0.0f, Table + 0.1f, 2.6f), new Vector3(0.2f, 0.2f, 0.2f), Materiau("Bleu", new Color(0.15f, 0.35f, 0.9f)));
            Objet(objets.transform, "Livre brun", PrimitiveType.Cube, new Vector3(0.6f, Table + 0.03f, 2.6f), new Vector3(0.22f, 0.05f, 0.16f), Materiau("Brun", new Color(0.5f, 0.3f, 0.15f)));
            Objet(objets.transform, "Vase vert", PrimitiveType.Cylinder, new Vector3(3.5f, Haute + 0.12f, 4.0f), new Vector3(0.14f, 0.12f, 0.14f), Materiau("Vert", new Color(0.15f, 0.7f, 0.3f)));
            Objet(objets.transform, "Pot orange", PrimitiveType.Cylinder, new Vector3(3.5f, Haute + 0.1f, 5.0f), new Vector3(0.18f, 0.1f, 0.18f), Materiau("Orange", new Color(0.95f, 0.5f, 0.1f)));
            Objet(objets.transform, "Bougie jaune", PrimitiveType.Capsule, new Vector3(3.5f, Basse + 0.1f, 5.0f), new Vector3(0.07f, 0.1f, 0.07f), Materiau("Jaune", new Color(0.95f, 0.85f, 0.2f)));
            Objet(objets.transform, "Petit cube blanc", PrimitiveType.Cube, new Vector3(-2.5f, Tab + 0.04f, 4f), new Vector3(0.08f, 0.08f, 0.08f), Materiau("Blanc", new Color(0.95f, 0.95f, 0.95f)));
            Objet(objets.transform, "Grosse balle violette", PrimitiveType.Sphere, new Vector3(-1.5f, 0.12f, 3.4f), Vector3.one * 0.24f, Materiau("Violet", new Color(0.55f, 0.2f, 0.75f)));

            // 4. Les emplacements possibles (centre de l'objet posé sur la surface). 11 emplacements, tous à
            //    plus de 35 cm de la place de départ d'un objet (sinon le mélange les considère occupés).
            GameObject emplacements = new GameObject("Emplacements_Memoire");
            Emplacement(emplacements.transform, "Table_Fond_Gauche", new Vector3(-0.6f, Table + 0.1f, 3.05f), 0.3f);
            Emplacement(emplacements.transform, "Table_Fond_Milieu", new Vector3(0.0f, Table + 0.1f, 3.05f), 0.3f);
            Emplacement(emplacements.transform, "Table_Fond_Droite", new Vector3(0.6f, Table + 0.1f, 3.05f), 0.3f);
            Emplacement(emplacements.transform, "Etagere_Haute_Avant", new Vector3(3.5f, Haute + 0.1f, 3.5f), 0.35f);
            Emplacement(emplacements.transform, "Etagere_Haute_Milieu", new Vector3(3.5f, Haute + 0.1f, 4.5f), 0.35f);
            Emplacement(emplacements.transform, "Etagere_Haute_Fond", new Vector3(3.5f, Haute + 0.1f, 5.5f), 0.35f);
            Emplacement(emplacements.transform, "Etagere_Basse_Avant", new Vector3(3.5f, Basse + 0.1f, 3.8f), 0.35f);
            Emplacement(emplacements.transform, "Etagere_Basse_Milieu", new Vector3(3.5f, Basse + 0.1f, 4.4f), 0.35f);
            Emplacement(emplacements.transform, "Sol_Fond_Gauche", new Vector3(-2.8f, 0.12f, 5.2f), 0f);
            Emplacement(emplacements.transform, "Sol_Fond_Droit", new Vector3(1.2f, 0.12f, 5.4f), 0f);
            Emplacement(emplacements.transform, "Sol_Devant", new Vector3(1.5f, 0.12f, 1.2f), 0f);

            // 5. Le jeu : niveau 1, puis niveau 2 (proposé après une réussite).
            GameObject jeu = new GameObject("MemoireVisuelle");
            MemoireVisuelle memoire = jeu.AddComponent<MemoireVisuelle>();
            memoire.niveau = niveau;
            memoire.niveauxSuivants.Add(ObtenirNiveau2());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ChemSceneMaquette);
            AjouterAuxBuildSettings(ChemSceneMaquette);
            Selection.activeGameObject = jeu;

            Debug.Log("[Memoire] Scène maquette créée : " + ChemSceneMaquette + ". Appuie sur Play : N = phase suivante, R = rejouer, F = solution.");
        }

        // ------------------------------------------------------------------
        // Pièces de la scène
        // ------------------------------------------------------------------
        /// <summary>La narration du kit dit « (À écrire : introduction) » au démarrage : on la rend muette (le jeu parle lui-même).</summary>
        private static void SilencerLIntroDuKit(GameObject rig)
        {
            foreach (NarrationSequence sequence in rig.GetComponentsInChildren<NarrationSequence>(true))
            {
                sequence.declencheur = DeclencheurNarration.Manuel;
                EditorUtility.SetDirty(sequence);
            }
        }

        private static void Boite(Transform parent, string nom, Vector3 position, Vector3 taille, Material materiau, bool surface)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nom;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = taille;
            go.GetComponent<Renderer>().sharedMaterial = materiau;
            go.isStatic = true;
            // Murs : pas de collision nécessaire (le sol invisible du kit limite déjà la téléportation) ;
            // surfaces (table, étagères) : collider conservé pour qu'on puisse y poser la main sans les traverser.
            if (!surface)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }

        private static void Objet(Transform parent, string nom, PrimitiveType forme, Vector3 position, Vector3 echelle, Material materiau)
        {
            GameObject go = GameObject.CreatePrimitive(forme);
            go.name = nom;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = echelle;
            go.GetComponent<Renderer>().sharedMaterial = materiau;

            int calque = LayerMask.NameToLayer("Grabbable");
            if (calque >= 0) go.layer = calque;

            Rigidbody corps = go.AddComponent<Rigidbody>();
            corps.mass = 0.5f;
            corps.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            corps.interpolation = RigidbodyInterpolation.Interpolate;

            go.AddComponent<ObjetMemoire>();
        }

        private static void Emplacement(Transform parent, string nom, Vector3 position, float tailleMax)
        {
            GameObject go = new GameObject("Emplacement_" + nom);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<EmplacementMemoire>().tailleMax = tailleMax;
        }

        // ------------------------------------------------------------------
        // Assets
        // ------------------------------------------------------------------
        private static NiveauMemoire ObtenirNiveau()
        {
            NiveauMemoire niveau = AssetDatabase.LoadAssetAtPath<NiveauMemoire>(CheminNiveau);
            if (niveau != null) return niveau;

            niveau = ScriptableObject.CreateInstance<NiveauMemoire>();
            niveau.titre = "Niveau 1";
            AssetDatabase.CreateAsset(niveau, CheminNiveau);
            AssetDatabase.SaveAssets();
            return niveau;
        }

        /// <summary>
        /// Niveau 2 : plus d'objets déplacés (6 sur 8), observation plus courte, marges plus serrées,
        /// et 2 objets changent de TAILLE (un agrandi, un rétréci) : place ET taille à retrouver.
        /// Créé une seule fois : si tu retouches Niveau2.asset, l'outil ne l'écrase pas.
        /// </summary>
        private static NiveauMemoire ObtenirNiveau2()
        {
            NiveauMemoire niveau = AssetDatabase.LoadAssetAtPath<NiveauMemoire>(CheminNiveau2);
            if (niveau != null) return niveau;

            niveau = ScriptableObject.CreateInstance<NiveauMemoire>();
            niveau.titre = "Niveau 2";
            niveau.introduction = "Niveau 2 : plus d'objets vont changer de place, et certains de taille. Tu as moins de temps. Observe bien.";
            niveau.observationMin = 15f;
            niveau.observationMax = 60f;
            niveau.nombreADeplacer = 6;
            niveau.distanceMinDeplacement = 1.5f;
            niveau.nombreAChangerDeTaille = 2;
            niveau.toleranceRatioTaille = 0.3f;
            niveau.permettreRedimensionnement = true;
            niveau.toleranceMetres = 0.3f;
            niveau.toleranceTaille = 0.6f;
            niveau.seuilReussite = 0.75f; // 6 objets sur 8
            AssetDatabase.CreateAsset(niveau, CheminNiveau2);
            AssetDatabase.SaveAssets();
            return niveau;
        }

        private static Material Materiau(string nom, Color couleur)
        {
            string chemin = DossierMateriaux + "/Mat_" + nom + ".mat";
            Material existant = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (existant != null) return existant;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader) { color = couleur };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", couleur);
            AssetDatabase.CreateAsset(m, chemin);
            return m;
        }

        private static void AssurerDossier(string parent, string nom)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + nom)) AssetDatabase.CreateFolder(parent, nom);
        }

        private static void AjouterAuxBuildSettings(string chemin)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == chemin) return;
            }
            scenes.Add(new EditorBuildSettingsScene(chemin, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
